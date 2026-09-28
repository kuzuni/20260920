import { readFile } from 'node:fs/promises';
import { createSign } from 'node:crypto';
import { RefundStore, packageName, pollVoidedPurchases } from './store.mjs';

async function accessToken() {
  // Use an attached Google service account (Cloud/VPS workload identity) when possible.
  // Local/private server fallback: key path outside the repository, never Unity Resources.
  if (!process.env.GOOGLE_APPLICATION_CREDENTIALS) {
    const response = await fetch('http://metadata.google.internal/computeMetadata/v1/instance/service-accounts/default/token',
      { headers: { 'Metadata-Flavor': 'Google' }, signal: AbortSignal.timeout(10000), redirect: 'error' });
    if (!response.ok) throw new Error(`Service-account token request failed (${response.status})`);
    const result = await response.json();
    if (!result.access_token) throw new Error('Missing service-account access token');
    return result.access_token;
  }
  const key = JSON.parse(await readFile(process.env.GOOGLE_APPLICATION_CREDENTIALS, 'utf8'));
  if (key.type !== 'service_account' || !key.client_email || !key.private_key) throw new Error('Invalid service-account key file');
  const b64 = value => Buffer.from(JSON.stringify(value)).toString('base64url');
  const now = Math.floor(Date.now() / 1000);
  const assertion = `${b64({ alg: 'RS256', typ: 'JWT' })}.${b64({ iss: key.client_email,
    scope: 'https://www.googleapis.com/auth/androidpublisher', aud: 'https://oauth2.googleapis.com/token', iat: now, exp: now + 3600 })}`;
  const signature = createSign('RSA-SHA256').update(assertion).sign(key.private_key, 'base64url');
  const response = await fetch('https://oauth2.googleapis.com/token', { method: 'POST', redirect: 'error',
    body: new URLSearchParams({ grant_type: 'urn:ietf:params:oauth:grant-type:jwt-bearer', assertion: `${assertion}.${signature}` }),
    signal: AbortSignal.timeout(15000) });
  if (!response.ok) throw new Error(`Service-account authentication failed (${response.status})`);
  const result = await response.json();
  if (!result.access_token) throw new Error('Missing service-account access token');
  return result.access_token;
}

let store;
try {
  if (!process.env.DOODLE_REFUND_DB) throw new Error('DOODLE_REFUND_DB must point to private persistent server storage');
  const token = await accessToken();
  store = new RefundStore(process.env.DOODLE_REFUND_DB);
  const result = await pollVoidedPurchases(store, async query => {
    const params = new URLSearchParams(Object.entries(query).filter(([, value]) => value !== undefined));
    const response = await fetch(`https://androidpublisher.googleapis.com/androidpublisher/v3/applications/${packageName}/purchases/voidedpurchases?${params}`,
      { headers: { Authorization: `Bearer ${token}` }, signal: AbortSignal.timeout(30000), redirect: 'error' });
    if (!response.ok) throw new Error(`Google Voided Purchases API failed (${response.status}); cursor unchanged`);
    return response.json();
  });
  console.log(JSON.stringify({ status: 'collected', ...result }));
} catch (error) {
  // Never log tokens, receipt bodies, key material, or raw API error payloads.
  const safeMessages = /^(Google Voided Purchases API failed \(\d+\); cursor unchanged|Service-account (token request|authentication) failed \(\d+\)|DOODLE_REFUND_DB must point to private persistent server storage|Missing service-account access token|Invalid service-account key file|Refund polling history gap or clock regression; operator reconciliation required|Malformed (Google response|voided purchase|voided purchases response)|Unsupported multi-quantity refund; manual reconciliation required|Voided order identity conflict|Invalid or repeated Google pagination token)$/;
  console.error(safeMessages.test(error.message) ? error.message : 'Refund collection failed; check private server credentials, storage, network, and response format. Cursor was not advanced.');
  process.exitCode = 1;
} finally { store?.close(); }
