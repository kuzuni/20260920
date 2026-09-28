import { DatabaseSync } from 'node:sqlite';
import { createHash } from 'node:crypto';

export const packageName = 'com.semobobo.game20260920';
export const tokenHash = token => createHash('sha256').update(token).digest('hex');

// Private server storage. Never expose these methods directly to an untrusted client.
export class RefundStore {
  constructor(path) {
    this.db = new DatabaseSync(path);
    this.db.exec(`PRAGMA journal_mode=WAL; PRAGMA synchronous=FULL;
      CREATE TABLE IF NOT EXISTS receipts (
        orderId TEXT PRIMARY KEY, tokenHash TEXT UNIQUE NOT NULL,
        productId TEXT NOT NULL, accountId TEXT NOT NULL,
        diamonds INTEGER NOT NULL, coupons INTEGER NOT NULL);
      CREATE TABLE IF NOT EXISTS voids (
        orderId TEXT PRIMARY KEY, tokenHash TEXT NOT NULL,
        voidedAt TEXT NOT NULL, reason INTEGER, source INTEGER);
      CREATE TABLE IF NOT EXISTS revocations (
        sequence INTEGER PRIMARY KEY AUTOINCREMENT, orderId TEXT UNIQUE NOT NULL,
        tokenHash TEXT NOT NULL, productId TEXT NOT NULL, accountId TEXT NOT NULL,
        diamondsDelta INTEGER NOT NULL, couponsDelta INTEGER NOT NULL);
      CREATE TABLE IF NOT EXISTS poll_state (id INTEGER PRIMARY KEY CHECK(id=1), completedAt INTEGER NOT NULL);`);
  }

  transaction(action) {
    this.db.exec('BEGIN IMMEDIATE');
    try { const value = action(); this.db.exec('COMMIT'); return value; }
    catch (error) { this.db.exec('ROLLBACK'); throw error; }
  }

  // Invoke only after BACKND receipt validation AND trusted fulfillment confirmation.
  // Amounts are the original grant, never a later catalog's price/reward.
  recordVerifiedGrant(receipt) {
    const { orderId, purchaseToken, productId, accountId, diamonds, coupons } = receipt;
    if (!/^GPA\./.test(orderId ?? '') || !purchaseToken || !accountId ||
        !/^diamonds_(10000|70000|150000|500000|900000|2000000)$/.test(productId ?? '') ||
        !Number.isSafeInteger(diamonds) || diamonds <= 0 ||
        !Number.isSafeInteger(coupons) || coupons < 0) throw new Error('Invalid verified grant');
    const hash = tokenHash(purchaseToken);
    return this.transaction(() => {
      const existing = this.db.prepare('SELECT * FROM receipts WHERE orderId=? OR tokenHash=?').all(orderId, hash);
      if (existing.length) {
        const r = existing[0];
        if (existing.length !== 1 || r.orderId !== orderId || r.tokenHash !== hash ||
            r.productId !== productId || r.accountId !== accountId ||
            r.diamonds !== diamonds || r.coupons !== coupons) throw new Error('Immutable receipt identity conflict');
      } else {
        this.db.prepare('INSERT INTO receipts VALUES (?,?,?,?,?,?)').run(orderId, hash, productId, accountId, diamonds, coupons);
      }
      this.reconcile(); // Handles a refund discovered before fulfillment is recorded.
    });
  }

  ingestPage(records) {
    if (!Array.isArray(records)) throw new Error('Malformed voided purchases response');
    this.transaction(() => {
      for (const r of records) {
        if (!/^GPA\./.test(r.orderId ?? '') || typeof r.purchaseToken !== 'string' || !r.purchaseToken ||
            !/^\d+$/.test(String(r.voidedTimeMillis ?? ''))) throw new Error('Malformed voided purchase');
        // The Unity store explicitly rejects purchases with quantity other than one.
        if (r.voidedQuantity != null && Number(r.voidedQuantity) !== 1)
          throw new Error('Unsupported multi-quantity refund; manual reconciliation required');
        const hash = tokenHash(r.purchaseToken);
        const old = this.db.prepare('SELECT tokenHash FROM voids WHERE orderId=?').get(r.orderId);
        if (old && old.tokenHash !== hash) throw new Error('Voided order identity conflict');
        this.db.prepare('INSERT OR IGNORE INTO voids VALUES (?,?,?,?,?)')
          .run(r.orderId, hash, String(r.voidedTimeMillis), r.voidedReason ?? null, r.voidedSource ?? null);
      }
      this.reconcile();
    });
  }

  reconcile() {
    this.db.exec(`INSERT OR IGNORE INTO revocations
      (orderId,tokenHash,productId,accountId,diamondsDelta,couponsDelta)
      SELECT r.orderId,r.tokenHash,r.productId,r.accountId,-r.diamonds,-r.coupons
      FROM receipts r INNER JOIN voids v ON r.orderId=v.orderId AND r.tokenHash=v.tokenHash;`);
  }

  // Durable outbox. The authenticated wallet adapter must apply sequence/orderId once,
  // atomically with its wallet/debt update. Never clamp these negative deltas to zero.
  forAccount(accountId, afterSequence = 0, limit = 100) {
    if (!accountId || !Number.isSafeInteger(afterSequence) || afterSequence < 0 ||
        !Number.isInteger(limit) || limit < 1 || limit > 1000) throw new Error('Invalid outbox query');
    return this.db.prepare('SELECT * FROM revocations WHERE accountId=? AND sequence>? ORDER BY sequence LIMIT ?')
      .all(accountId, afterSequence, limit);
  }
  completedAt() { return this.db.prepare('SELECT completedAt FROM poll_state WHERE id=1').get()?.completedAt ?? null; }
  complete(at) { this.db.prepare('INSERT INTO poll_state VALUES (1,?) ON CONFLICT(id) DO UPDATE SET completedAt=excluded.completedAt').run(at); }
  close() { this.db.close(); }
}

export async function pollVoidedPurchases(store, fetchPage, now = Date.now()) {
  const day = 86400000;
  const completed = store.completedAt();
  // Surface outages beyond Google's retention; do not silently claim reconciliation.
  if (completed !== null && (now - completed > 30 * day || completed > now))
    throw new Error('Refund polling history gap or clock regression; operator reconciliation required');
  const query = { startTime: String(Math.max(now - 30 * day + 1000, (completed ?? now - 30 * day) - day)),
    endTime: String(now), maxResults: '1000', type: '0', includeQuantityBasedPartialRefund: 'true' };
  const seen = new Set();
  let pages = 0, records = 0;
  do {
    const page = await fetchPage({ ...query });
    if (!page || typeof page !== 'object' || Array.isArray(page)) throw new Error('Malformed Google response');
    store.ingestPage(page.voidedPurchases ?? []);
    records += (page.voidedPurchases ?? []).length;
    pages++;
    const next = page.tokenPagination?.nextPageToken;
    if (next != null && (typeof next !== 'string' || seen.has(next))) throw new Error('Invalid or repeated Google pagination token');
    if (next) seen.add(next);
    query.token = next || undefined;
  } while (query.token);
  // Advance only after every page is durable. A crash safely repeats prior pages.
  store.complete(now);
  return { pages, records };
}
