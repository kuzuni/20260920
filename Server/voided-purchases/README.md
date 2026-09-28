# Google Voided Purchases collector — not deployed

Payment verification stays with BACKND `Receipt.ValidateReceipt`. This component uses
Google `purchases.voidedpurchases.list` only; no BACKND refund plan or Refund API is used.
It does not issue refunds; it records already-voided Google orders for item recovery.

Run tests with Node 22.18+:

```powershell
node --test Server/voided-purchases/store.test.mjs
```

Private server execution requires `DOODLE_REFUND_DB` pointing to a persistent SQLite
file outside this repository. Use one worker with a durable disk. Cloud Run's temporary
filesystem is NOT suitable: choose persistent database hosting before deployment.
Use an attached service account or `GOOGLE_APPLICATION_CREDENTIALS` pointing to a
private JSON key outside the repo. Enable the Android Publisher API and grant the
service account Google Play financial-information access for this app. Never copy
this key into Unity, Resources, the APK, or a public web frontend.

```powershell
node Server/voided-purchases/poll.mjs
```

The host must schedule this command regularly (for example hourly), retry failed runs,
and alert on errors. No scheduler or hosted endpoint has been enabled by these files.
After every successful run the durable cursor advances only once all pages have been
committed. Polls overlap by one day; duplicate order IDs are idempotent. Google's
30-day retention means an outage exceeding that window requires operator recovery.
Unknown orders are retained so late receipt import can still create a recovery event.

## Integration still required before enabling payments

1. A **trusted** server adapter must call `recordVerifiedGrant` after BACKND receipt
   verification and fulfillment, retaining GPA, product ID, account ID, purchase token,
   and original diamond/coupon quantities. This method is not a public ingestion API:
   do not accept a client's claim that its receipt was verified or trust client amounts.
2. An authenticated wallet adapter must consume `forAccount` with server-verified
   account identity. Apply each order/sequence exactly once, transactionally with the
   wallet debt update; retain a durable applied cursor. No endpoint is exposed here.
   Do not directly overwrite the BACKND client save blob: a stale game save could
   otherwise erase refund debt. Multi-device saves must preserve server-owned debt.
3. Deploy the collector and adapters, connect Google Play permissions, then test real
   license-test purchase, repeated poll, refund, offline return, and reinstall.

The outbox stores negative deltas, never a clamped balance. A grant of 2,000,000 diamonds
and 2 coupons produces `-2000000` and `-2`, even when the user's available balance is zero.
Order ID and hashed purchase token must both match. Product ID and reward quantities
come from the immutable original grant, not the current catalog. Multi-quantity purchases
remain unsupported consistently with the Unity billing adapter.

`paymentsEnabled` must remain false until this complete path is connected and tested.
Existing local Unity revocation tests and these collector tests are not evidence of a
deployed refund service.

References: [Google guide](https://developers.google.com/android-publisher/voided-purchases),
[API reference](https://developers.google.com/android-publisher/api-ref/rest/v3/purchases.voidedpurchases/list).
