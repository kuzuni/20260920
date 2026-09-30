# Embedded BACKND Database 0.0.18

Source: official com.backnd.database 0.0.18 from registry.npmjs.org, cached fingerprint ecd14e70f42541fef55b91092af0478197fa356b.

Client.cs: ProcessQueue captures its cancellation token before disposal instead of repeatedly accessing CancellationTokenSource.Token. The original Dispose cancels and disposes the source, causing the next queue tick and its error handler to throw ObjectDisposedException. Dispose is also idempotent for session teardown. No protocol or permission behavior is changed. Re-evaluate these changes when upgrading the SDK.
