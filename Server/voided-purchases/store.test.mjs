import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { RefundStore, pollVoidedPurchases } from './store.mjs';

const grant = { orderId: 'GPA.test-1', purchaseToken: 'test-token-1', productId: 'diamonds_2000000',
  accountId: 'account-1', diamonds: 2000000, coupons: 2 };
const refund = { orderId: grant.orderId, purchaseToken: grant.purchaseToken, voidedTimeMillis: '1000' };
const now = 1800000000000;
function fixture(t) {
  const dir = mkdtempSync(join(tmpdir(), 'doodle-refunds-'));
  const path = join(dir, 'refunds.sqlite');
  const store = new RefundStore(path);
  t.after(() => { store.close(); rmSync(dir, { recursive: true }); });
  return { store, path };
}

test('original diamond and mileage grants become durable negative deltas exactly once', t => {
  const { store, path } = fixture(t);
  store.recordVerifiedGrant(grant);
  store.ingestPage([refund, refund]);
  store.recordVerifiedGrant(grant);
  const events = store.forAccount(grant.accountId);
  assert.equal(events.length, 1);
  assert.equal(0 + events[0].diamondsDelta, -2000000);
  assert.equal(0 + events[0].couponsDelta, -2);
  assert.equal(events[0].productId, grant.productId);
  assert.equal(events[0].orderId, grant.orderId);
  assert.deepEqual(store.forAccount('another-user'), []);
  assert.deepEqual(store.forAccount(grant.accountId, events[0].sequence), []);
  const reopened = new RefundStore(path);
  try { assert.deepEqual(reopened.forAccount(grant.accountId), events); } finally { reopened.close(); }
});

test('refund arriving before grant remains pending and matches when grant is recorded', t => {
  const { store } = fixture(t);
  store.ingestPage([refund]);
  assert.deepEqual(store.forAccount(grant.accountId), []);
  store.recordVerifiedGrant(grant);
  assert.equal(store.forAccount(grant.accountId).length, 1);
});

test('same GPA with a different token cannot revoke a purchase', t => {
  const { store } = fixture(t);
  store.recordVerifiedGrant(grant);
  store.ingestPage([{ ...refund, purchaseToken: 'wrong-token' }]);
  assert.deepEqual(store.forAccount(grant.accountId), []);
  assert.throws(() => store.ingestPage([refund]), /identity conflict/);
});

test('immutable receipt identity and original grant cannot be replaced', t => {
  const { store } = fixture(t);
  store.recordVerifiedGrant(grant);
  for (const change of [{ accountId: 'other' }, { diamonds: 10000 }, { coupons: 0 },
    { orderId: 'GPA.other' }, { purchaseToken: 'other' }, { productId: 'diamonds_10000' }])
    assert.throws(() => store.recordVerifiedGrant({ ...grant, ...change }), /conflict/);
});

test('failed second page preserves cursor; retry deduplicates first page', async t => {
  const { store } = fixture(t);
  store.recordVerifiedGrant(grant);
  store.complete(now - 10000);
  await assert.rejects(pollVoidedPurchases(store, async q => {
    if (q.token) throw new Error('temporary outage');
    return { voidedPurchases: [refund], tokenPagination: { nextPageToken: 'page2' } };
  }, now), /outage/);
  assert.equal(store.completedAt(), now - 10000);
  let calls = 0;
  await pollVoidedPurchases(store, async q => {
    calls++;
    assert.equal(q.type, '0');
    assert.equal(q.includeQuantityBasedPartialRefund, 'true');
    return q.token ? {} : { voidedPurchases: [refund], tokenPagination: { nextPageToken: 'page2' } };
  }, now);
  assert.equal(calls, 2);
  assert.equal(store.completedAt(), now);
  assert.equal(store.forAccount(grant.accountId).length, 1);
});

test('malformed page rolls back all rows and cannot advance cursor', async t => {
  const { store } = fixture(t);
  store.recordVerifiedGrant(grant);
  await assert.rejects(pollVoidedPurchases(store, async () => ({ voidedPurchases: [refund, {}] }), now), /Malformed/);
  assert.equal(store.completedAt(), null);
  assert.deepEqual(store.forAccount(grant.accountId), []);
});

test('retention gap and clock regression fail instead of silently losing refunds', async t => {
  const { store } = fixture(t);
  store.complete(now - 31 * 86400000);
  await assert.rejects(pollVoidedPurchases(store, async () => assert.fail('must not fetch'), now), /history gap/);
  store.complete(now + 1);
  await assert.rejects(pollVoidedPurchases(store, async () => assert.fail('must not fetch'), now), /clock regression/);
});

test('repeated pagination token fails and single-quantity partial refund is supported', async t => {
  const { store } = fixture(t);
  store.recordVerifiedGrant(grant);
  await assert.rejects(pollVoidedPurchases(store, async () => ({ voidedPurchases: [{ ...refund, voidedQuantity: 1 }],
    tokenPagination: { nextPageToken: 'loop' } }), now), /pagination/);
  assert.equal(store.completedAt(), null);
  assert.equal(store.forAccount(grant.accountId).length, 1);
  assert.throws(() => store.ingestPage([{ ...refund, voidedQuantity: 2 }]), /multi-quantity/);
});
