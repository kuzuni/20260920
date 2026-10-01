# BACKND / Google Play integration

Package: `com.semobobo.game20260920`. BACKND project: `20260920`. Google Cloud project: `game20260920`.
사용자가 2026-10-01에 확인한 뒤끝 콘솔 대상은 **`20260920`** 입니다. 이 저장소의 로그인·게임 저장·PVP·승점 랭킹 설정은 모두 이 프로젝트에서 작업합니다. 이름이 비슷한 다른 프로젝트나 Unity Cloud 프로젝트와 혼동하지 마세요.
Only the internal testing track is authorized. Default store language is English (US), with Korean localization planned. Intended audience: ages 13 and older.

## Local configuration

Copy `BackendSettings.example.json` to `Assets/DoodleIdle/Resources/DoodleIdle/BackendSettings.json` and supply the console values. The real file and its meta are ignored by Git. Never put Google client secrets, service-account keys, signing passwords, or account passwords in Resources or source control.

`Doodle Idle/Backend/Sync SDK Settings and IAP Catalog` synchronizes that local JSON into the official BACKND, Google Login Android, and Chat Inspector assets. It also runs after Editor compilation and before Android builds. On 2026-09-29, the local App ID, signature, web client ID, and Chat UUID were verified against the runtime configuration. The three generated SDK assets are Git-ignored. Function Auth Key remains unset because no BACKND Function has been deployed; it is not needed for the standard login APIs.

Unity Cloud is linked to project `탕탕탕 방치형 rpg` (`b50cc969-eac9-4072-b54e-76d7f26647ca`) in organization `rudwpwjrwkdb1995`. In-App Purchasing is enabled in Project Settings. This is separate from Google Play product registration and the runtime `paymentsEnabled` gate.

On 2026-09-29, the previously empty **Google license key** field in that Unity Cloud project was populated with the RSA public key from this exact Play app's Monetization setup. The saved project page displays the masked key instead of `Not set`; evidence: `artifacts/service-setup/unity-google-license-key.png`. This public key is not the private service-account JSON, and does not complete the pending BACKND JWT verification or a real purchase test.

The Unity IAP Catalog contains six consumable products: `diamonds_10000`, `diamonds_70000`, `diamonds_150000`, `diamonds_500000`, `diamonds_900000`, and `diamonds_2000000`, with English and Korean descriptions. The last two include one and two mileage coupons respectively. Catalog payouts document the rewards; `DoodleIapService` owns actual fulfillment. Codeless/UGS automatic initialization stays disabled to avoid double initialization. Regional selling prices must be configured in Play Console.

`PlayerProfile` and `StageProgress` are private, schema-free BACKND game-data tables. StageRanking uses `StageProgress.stage`, descending, all users, no reset or rank rewards.

## Editor authentication initialization (2026-10-01)

Domain reload is disabled in this project. BACKND's static `IsInitialized` can remain true after Play Mode stops even though its runtime objects were destroyed. `DoodleBackendSession` therefore requires its own successful `InitializeAsync` callback before allowing authentication; the static SDK flag alone cannot skip initialization. Concurrent initialization callers share one task, failed initialization remains retryable, and Google/guest/editor login all use the same gate.

`LoginScreenInitializesBeforeSignupAndCanRelogin` uses the real `DoodleLogin` account creation/login buttons, with no SDK pre-initialization in the test. Two separate Play Mode runs passed: the first entered with SDK flag false; the second entered with the stale flag true. Both rejected missing configuration before initialization, then successfully created a temporary account, entered the game, logged out, logged back into the same account, and withdrew it. Error/Exception/Assert logs were collected and checked after cleanup (zero in both runs). Reports: `artifacts/character-reports/backend-initialization-{first,repeat}-tests.xml` and `backend-initialization-audit.txt`. Enable only with `Library/BackendInitialization.optin`; remove it afterward.

The PVP live test no longer pre-initializes BACKND separately: it must pass through the production login initialization path as well.

## Automatic token login (2026-10-01)

`DoodleLogin.Start` now attempts `LoginWithTheBackendToken` once per session after
SDK initialization. The SDK persists and refreshes its own tokens; the game does
not store passwords or duplicate token values. Successful restoration uses the
same account-scoped cloud-save loading path as manual login. Missing, invalid,
or expired tokens leave manual login available. Connection failures expose an
automatic-login retry button. Explicit logout/withdrawal suppresses startup
login until a new manual sign-in succeeds.

Guest logins now record an account hash for restoring the guest-link UI after
token login. A guest created before this change needs one normal guest login
to establish that metadata; stale guest credentials alone never classify another
account as a guest. Google conversion clears that metadata.

Live verification: temporary custom account created in one Play Mode run, then
restored automatically in a separate run with the same identity and saved 12,345
diamonds. Explicit logout suppression, invalid-token fallback, manual re-login,
and QA withdrawal passed. The existing initialization/login-screen regression
also passed. Reports: `artifacts/character-reports/token-login-tests.xml`,
`token-login-audit.txt`, and `token-login-initialization-tests.xml`.
Live tests require `Library/TokenLoginQA.optin`; run `PreparePersistedToken` and
`ResumePersistedTokenAndCheckLogout` in separate runs. The temporary credential
journal stays in ignored `Library` until successful cleanup. Android Google
sign-in and a full device process restart were not exercised by these Editor tests.

## Chat

BACKND Chat SDK 1.4.1, Base SDK 5.18.17. Chat opens only after authenticated login and a server nickname is available. `DoodleChatService` lives with the login session, calls SDK Update each frame, and disposes on logout, account deletion, authentication loss, tamper detection, and shutdown.

| UI | Open channel group | Channel name |
| --- | --- | --- |
| Global / 글로벌 | `global` | `server-1` |
| 한국 / Korea | `korea` | `server-1` |

Each channel is configured for 50 users per shard. The current FREE project plan has a total limit of 50 concurrent users; paid CCU auto-expansion is disabled. Global remains the default fallback; Korea is joined after the initial global connection completes. Both histories are isolated and capped at 100 messages per app session. Only server-received messages are displayed. Reconnect history is deduplicated and server hide/delete events remove matching entries. No sample conversations ship with the app.

Console message limit: 420 UTF-8 bytes. Client limit: 140 characters and 420 bytes. Server and client enforce a two-second send interval. Names and messages render with rich text disabled. Remote avatar values are not used as arbitrary resource paths. Report/block/unblock calls use native BACKND APIs; reports and filters depend on the project's BACKND plan. Paid plan upgrades have not been enabled.

Validation: `DoodleChatTests` passes input, channel isolation, deduplication, history cap, moderation removal, and wrong-shard rejection. Opt-in live test creates its own temporary account, joins both configured channels, checks positive channel numbers, disconnects, and requests deletion. It does not broadcast to public channels. Live send/receive between two Android devices and report/block service behavior still require device validation.

## Persistence and security

### In-game account deletion

Settings exposes `회원탈퇴` / `Delete account` for signed-in users. Its confirmation calls `Backend.BMember.WithdrawAccount(0)` directly, then disconnects chat, clears the account's local JSON, and returns to `DoodleLogin`. It does not open a website. The confirmation explains permanent loss and the BACKND deletion window of up to one hour. Failed requests keep the account active and allow retry.

On 2026-09-29, the opt-in PlayMode test `SettingsWithdrawalDeletesTemporaryAccountAndReturnsToLogin` passed against project 20260920 using a newly created disposable custom account. It exercised the actual settings and confirmation button callbacks, verified local-save removal and the login scene, then verified the server rejects the deleted account with status 410. This validates the BACKND withdrawal path; Android Google sign-out still requires device validation. Result: `Documents/ArtArchives/20260928-google-play-iap/validation/account-withdrawal.xml`.

The external deletion URL in the Play Console Data safety **draft** is `https://semobobo.netlify.app/delete-account`. The separately generated BACKND English/Korean withdrawal pages are not wired into the game or store. No paid plan was activated. The public documentation does not specify a separate page-creation charge; account/API and database usage remain subject to BACKND pricing regardless of which public instructions URL is used.

Local: every second, per-account encrypted/authenticated JSON, atomic replacement. Gameplay values use ACTk obscured backing values, with explicit JSON converters. Server: every 300 seconds and immediate purchase persistence. Failed saves retry after five seconds. A purchase is confirmed to the store only after its reward snapshot has been saved successfully. Logout flushes local data; it does not add an extra server write. A local unsynced snapshot survives process termination and same-device sign-in.

ACTk obscured-memory and speed-hack detectors terminate Android play on detection. These client checks are defense in depth, not server-authoritative validation. Multi-device conflict resolution and a server-authoritative currency ledger remain required before enabling real-money purchases.

## Payments: not release-ready

### BACKND console/document audit — 2026-09-29

Read the actual receipt-verification checklist and its linked
[Google store setup guide](https://docs.backnd.com/guide/console-guide/server-setting/store/googlestore/).
The package is `com.semobobo.game20260920`. Created the `backnd-receipt` service account
in `game20260920` and one JSON key on 2026-09-29. The key is stored outside Git at
`C:/Users/user/Desktop/game20260920-backnd-receipt-jwt.json` and was uploaded to BACKND;
the console confirms an existing JWT file. Never copy its contents into this document.
Google Play Android Developer API and Google Play Games Services Publishing API are
both enabled. Play shows the service account as active. Saved permissions were reopened
and verified: account-level app/report read and financial/order read; app-level order
and subscription management for `com.semobobo.game20260920`. No Cloud Owner or Play
administrator permission was granted.

Copied the actual report bucket from Play financial reports:
`pubsite_prod_6737639609818241561` (without `gs://` or `/earnings/`). BACKND rejected its
save with a JWT permissions error, including after the APIs were enabled. Bucket setup
is therefore **not complete**. New-key propagation is a possible cause, not a verified
diagnosis. Retain the same key and retry after the documented 24–36-hour propagation
window; if it still fails, investigate permissions instead of repeatedly creating keys.
The Google Play RSA public key was copied from this app's monetization setup into BACKND and confirmed present after reloading the console on 2026-09-29. The report bucket still awaits successful permission verification.
Evidence: `artifacts/service-setup/play-receipt-service-account.png` and
`artifacts/service-setup/backnd-jwt-propagation-pending.png`.
Retried on 2026-10-01 using the same desktop JSON in project `20260920`.
After re-uploading, applying `pubsite_prod_6737639609818241561` still returned
“JWT 권한 설정이 올바르지 않습니다. 등록된 권한을 다시 확인해 주세요.”
The elapsed propagation window did not resolve the error. Bucket permission
validation and real purchase receipt validation remain unverified; do not report
the existing-JWT indicator as successful end-to-end receipt validation.
Evidence: `artifacts/service-setup/backnd-jwt-retry-20261001.png`.
No BACKND paid refund plan is required by this implementation.

The guide requires Google API/service-account setup, Play permissions, and JSON upload.
Revenue reporting additionally uses the Play report bucket identifier without `gs://`.
Do not invent a bucket or make a purchase solely to obtain one. The guide warns that
new credential propagation may take 24–36 hours; a saved field alone is not validation.

Compared `DoodleIapService` with the installed-version
[IAP 5 example](https://docs.backnd.com/sdk-docs/backend/base/receipt/unity-iap5-example/)
and [ValidateReceipt reference](https://docs.backnd.com/sdk-docs/backend/base/receipt/validate-receipt/).
It passes `PendingOrder.Info.Receipt` to `Backend.Receipt.ValidateReceipt`, retains
GPA/product/reward details, and confirms only after verified fulfillment and cloud save.
`UsedReceipt` recovery checks original identity rather than granting unconditionally.
Google prices are resolved by BACKND; manual `SetIapPrice` is not necessary in integrated
Google mode. Real license-test verification remains untested and disabled.

Keep `paymentsEnabled` false. Catalog, Unity IAP 5 connection, receipt validation, local receipt ledger, retry, and idempotent negative-balance revocation exist. Tests cover revoking 2,000,000 diamonds plus two mileage coupons from zero, and avoiding duplicate revocation.

Payment receipt verification uses BACKND. Refund detection uses Google Voided Purchases only; do not enable a BACKND refund plan or call BACKND Refund APIs.

`Server/voided-purchases` contains a private Google polling command and SQLite collector with a durable cursor, immutable verified-grant records, GPA/token matching, and an idempotent negative-delta outbox. Eight Node tests passed for negative diamond/coupon deltas, duplicate handling, identity conflicts, late grants, pagination failure/retry, and retention gaps. It is not deployed. Trusted BACKND verification/fulfillment ingestion, authenticated server-wallet application, multi-device debt reconciliation, live Publisher API access verification, hosting/scheduling, and actual purchase/refund tests are still incomplete. Calling a local revocation method or testing the collector is not equivalent to an operating refund reconciliation service. BACKND Function deployment is not an assumed requirement; the execution host has not been selected.

All six Play products now have **active** `standard` purchase options, confirmed in the product list on 2026-09-29. Only Korea/US are available; new countries are excluded. English and Korean product names/descriptions are saved, including coupon counts on the last two. Runtime `paymentsEnabled` remains false until receipt validation is verified.

| Product ID | Korea | US (Play conversion) | Coupons |
| --- | --- | --- | --- |
| diamonds_10000 | KRW 1,100 | USD 0.79 | 0 |
| diamonds_70000 | KRW 5,500 | USD 3.69 | 0 |
| diamonds_150000 | KRW 11,000 | USD 7.49 | 0 |
| diamonds_500000 | KRW 33,000 | USD 21.99 | 0 |
| diamonds_900000 | KRW 55,000 | USD 36.99 | 1 |
| diamonds_2000000 | KRW 110,000 | USD 74.99 | 2 |

Evidence: `artifacts/service-setup/play-iap-six-active.png`. No actual purchase was made and no production release was published.

## Build / release

### Google sign-in without an Android phone

Run `Tools/Start-GoogleLoginEmulator.ps1` in PowerShell. It starts the existing Google Play AVD, installs the local development APK, and opens the actual Android login scene. Sign in manually inside the emulator, then run the script with `-ObserveOnly` to print only non-sensitive `[DoodleAuth]` markers. No credentials or tokens are printed by this diagnostic path.

The current PC uses Android Emulator 37.1.11 with `Medium_Phone_API_36.0` (Google Play API 36), software rendering, and the default Android debug certificate registered in Google Cloud and BACKND. Unity 6000.3 no longer supports the old Android x86-64 target; the ARM64 APK runs through the image's bundled ARM native bridge. The emulator build uses OpenGL ES 3 to avoid the earlier host Vulkan crash path. The normal release login implementation is used, without an authentication bypass. The latest APK destination is `Documents/ArtArchives/20260928-google-play-iap/game20260920-emulator-complete-settings.apk`; override `-ApkPath` on another machine.

Build a fresh APK in the isolated validation project with `DoodleIdle.Editor.DoodleAndroidBuild.EmulatorLoginApk`; optional `DOODLE_EMULATOR_OUTPUT` controls its destination. This build method uses development diagnostics and the default debug keystore rather than the private Play upload keystore.

The latest build completed successfully on 2026-09-29 at 05:09 KST (validation log `emulator-build8.log`, APK 270,546,676 bytes). It includes the synchronized SDK settings, six-product catalog, Unity Cloud/IAP linkage, Activity entry point, and detailed redacted BACKND errors. It has not replaced the running emulator installation or the older Play internal-test AAB.

On 2026-09-29, the initial SDK `ExceptionInInitializerError` was traced to the GameActivity entry point's missing Java Looper during SDK class initialization. Android builds now use `UnityPlayerActivity`. The rebuilt APK was installed, reached BACKND initialization (status 204), and opened the actual Google authentication screen without that exception. The user reported signing in and asked to stop further login testing. The final token/BACKND federation/game-entry sequence was not independently observed. Required success markers remain `google_token_received`, `backnd_google_status_200` or `_201`, and `game_scene_entered`. This emulator test does not validate the separate Play app-signing certificate or an actual store billing purchase. Login errors now display redacted SDK details and BACKND HTTP status/error/message instead of a generic failure.

The local Editor initially lacked the resolved IAP package despite its manifest entry. Installing `com.unity.purchasing@5.4.3` through Package Manager and restarting the Editor cleared the purchasing compilation failures. A fresh Editor compilation succeeded, and its MCP server started successfully on port 222.

`DoodleIdle.Editor.DoodleAndroidBuild.InternalTestBundle` builds signed ARM64 IL2CPP AABs targeting API 36 (minimum API 25). It resolves Android dependencies first. Required environment variables: `DOODLE_ANDROID_KEYSTORE`, `DOODLE_ANDROID_PASSWORD`, `DOODLE_ANDROID_ALIAS`. Optional: `DOODLE_ANDROID_OUTPUT`, `DOODLE_ANDROID_VERSION_CODE`. Signing material remains outside the repo. Use a validation checkout rather than the user's open Editor project.

On 2026-09-29, signed version 0.1.0 (code 1) completed the Android IL2CPP/Gradle build successfully. The bundle is archived outside the repository at `Documents/ArtArchives/20260928-google-play-iap/game20260920-internal.aab`. Play accepted the bundle and published `0.1.0 Internal Test 1` to the internal track only. The owner-selected Google email is the sole member of `game20260920 Internal`; the track is active. Opt-in link: https://play.google.com/apps/internaltest/4701727905013303173 . This does not confirm a successful Android device login or production release.

The opt-in `CaptureInternalTestStoreScreenshots` PlayMode test passed and produced eight actual 1080x1920 Korean game/UI screenshots. Reviewed copies are in the ignored `artifacts/play-store/ko-KR` folder, with a separate `artifacts/play-store/chat-global-korea.png` UI preview. Run with `DOODLE_STORE_CAPTURES=1` and a real graphics device; do not pass `-nographics`.

The web OAuth client, upload-key Android OAuth client, and all three Play signing certificate Android clients are configured. Play enabled quantum-ready hybrid signing, so OAuth must recognize the deployment, hybrid classical, and hybrid PQC certificates. Public SHA-1 fingerprints:

| Certificate | SHA-1 |
| --- | --- |
| Upload | `B55D87917CB5375CE32C6B7728621F3399177C7D` |
| Play deployment | `7C214C24E72300B02C94A559D760B6E9DB56DCAA` |
| Play hybrid classical | `25D339FC4334F93F81CF7340F54A9A1CFA0FD3B6` |
| Play hybrid PQC | `12DA5EF0D45D71A51067516705039335A9E0D952` |
| Local debug (`%USERPROFILE%/.android/debug.keystore`) | `CE8C03FAA1936096064F21A977B6ACDE99C9C35A` |

On 2026-09-29 the local debug Android OAuth client was also registered. The local Resources configuration was compared with the validation build: BACKND App ID, signature, and Google web client ID all match. The local project uses the default debug signing configuration; the release build method injects the separate upload keystore from environment variables.

### BACKND Google Hash Key audit

The installed official SDK's `TheBackendHashKeySettings.FindSha1Key` method (called by the Inspector's Generate button) was executed inside Unity, with each certificate SHA-1 as input. The output is SHA-1 bytes encoded as Base64, not the colon-separated fingerprint used by Google Cloud. The report is archived in `Documents/ArtArchives/20260928-google-play-iap/signing/backnd-official-hash-audit.tsv`.

| BACKND slot | Certificate | Official SDK output |
| --- | --- | --- |
| 1 | Play deployment | `fCFMJOcjALAslKVZ12C26dtW3Ko=` |
| 2 | Play hybrid classical | `JdM5/EM0+T+Bz3NA9UqaHPoP07Y=` |
| 3 | Local upload/release | `tV2HkXy1N1zjLGt3KGIfM5kXfH0=` |
| 4 | Local debug | `zowD+qGTYJYGTyGpd7as3pnJw1o=` |

All four were saved in the BACKND console. The SDK's Android `CommonUtil.getAppHash` reads `PackageInfo.signatures[0]` using legacy `GET_SIGNATURES`. Android documents that this reports the oldest certificate after signing rotation. Android 17 hybrid-signature behavior has not been verified on a device; BACKND exposes only four slots while five certificates exist. The PQC hash (`Etpe8NRdcaUQZ1FnBQOTNang2VI=`) is recorded but is **not** in a BACKND slot. Do not claim full Android 17 coverage or remove the Google OAuth PQC client without verifying the installed APK's `Backend.Utils.GetGoogleHash()` result. No Android device was connected during this audit.

Official instructions: [BACKND Inspector hash generator](https://docs.backnd.com/sdk-docs/backend/base/sdk-utils/get-hash/by-unity-inspector/), [BACKND authentication settings](https://docs.backnd.com/guide/console-guide/server-setting/authenciation/).

OAuth remains in testing mode, with the same owner-approved email registered as its one test user. Reviewers can use the new guest entry without supplied Google credentials. Testing Google linking with another Google account may require adding that account as an OAuth test user while the consent screen remains in testing. Public signing certificates are not secrets. [Google's hybrid signing setup requirements](https://support.google.com/googleplay/android-developer/answer/9842756?hl=en-GB).

The initial app dashboard setup declarations and store listing are saved. Current build has no advertising SDK; the saved ad declaration is No and must be changed when ads are integrated. Store review, device checks and complete English UI translation remain required before production release. Saved declarations do not demonstrate successful Google login or billing on devices.

### Guest sign-in and Google linking (2026-09-29)

The login screen offers Google sign-in and guest sign-in, both gated by the terms checkbox. BACKND `GuestLogin` creates/resumes the device's existing guest credentials. Ordinary sign-out preserves those credentials; successful account deletion clears them. Reinstalling/changing devices without linking loses guest access, and the login screen explains this in Korean and English.

Settings exposes Google linking only for signed-in guests. `CheckUserInBackend` must return 204 before conversion. Existing Google accounts (200), other lookup errors, and conversion conflicts (409) leave the guest identity and progress unchanged. `ChangeCustomToFederation` converts the current guest without signing into or merging another account. A successful cloud save is required before conversion. Guest credentials are removed only after successful conversion with the same BACKND user ID.

On a repeated linking attempt, the official Google SDK signs out its previously selected Google identity before opening account selection again. This does not sign out the BACKND guest. If clearing the Google selection fails or times out, conversion is not attempted. The UI keeps a retry button labelled “Choose another Google account”. A failed initial SDK sign-in does not mark an uninitialized Google client as signed in.

The isolated live BACKND test confirmed guest creation, saving 12,345 diamonds, logout/resume with the same account ID and balance, and deletion of its own temporary guest account. `guest-refund-tests2.xml` reports 7/7 passing (guest/custom login, settings withdrawal, refund idempotency/retry and security checks). A real Android Google account conflict followed by selecting a different Google account has **not** been exercised end to end yet.

The final signed Android IL2CPP build (version code 2) succeeded in `validation/guest-android-build-final.log`. Output: `Documents/ArtArchives/20260928-google-play-iap/game20260920-internal-guest-v2.aab` (205,038,802 bytes, SHA-256 `DB5DABD9D15C272E25E3F1CAFDA4ACA4E384F2A1CF3B2A13A3151B4AE18D31F0`). The four login/session/settings source files match the validation checkout used by this build.

Play published `0.1.0 Internal Test 2 - Guest and Google link` to the existing internal track on 2026-09-29 at 06:42 KST and explicitly shows “Available to internal testers”. English and Korean release notes are saved. No production release was published. Evidence: `artifacts/service-setup/play-internal-guest-v2.png`. Play reported two non-blocking diagnostic-file warnings (R8 mapping and native debug symbols missing); there were no release-blocking errors. Testers use the existing opt-in link: https://play.google.com/apps/internaltest/4701727905013303173 .

Play app access now declares unrestricted guest access. Target audience was saved as 13–15, 16–17, and 18+. Data safety was saved with Google OAuth plus automatic guest account creation. These are console declarations awaiting review, not production publication.

Official references: [Google account selection and sign-out](https://docs.backnd.com/sdk-docs/backend/toolkit/google-login/android/code/), [Guest login](https://docs.backnd.com/en/sdk-docs/backend/base/user/guest/signup-and-login/), [Guest-to-Google conversion](https://docs.backnd.com/en/sdk-docs/backend/base/user/federation/migrate-from-custom/), [Chat SDK](https://docs.backnd.com/en/sdk-docs/chat/intro/), [Google Play Voided Purchases](https://developers.google.com/android-publisher/voided-purchases), [Billing security](https://developer.android.com/google/play/billing/security).

### Store text and live stage leaderboard (2026-09-29)

The English default store description now explains guest entry and Google linking, including refusal to merge an already-registered Google account. A Korean (ko-KR) localization was added with matching title, short description, and full description. Play confirmed the text changes saved; English remains the default. These edits remain pending store review, not a production release.

The stage leaderboard popup (Settings > Stage leaderboard, or the same button in PVP) publishes the current best clear and loads the top 50 records from BACKND. Results use the existing scrollable popup, with the player's row highlighted and personal best shown. A fixed refresh button supports retries, duplicate requests are disabled, and rank requests time out after 30 seconds. The isolated live guest test in `validation/combat-ranking-tests.xml` passed and asserted that real server rows were rendered inside the scrollable popup; the temporary guest was then deleted.

Combat capture stress exposed Unity 2D Animation 13.0.4 transform-cache exceptions while pooled enemy rigs were repeatedly enabled/disabled. The project now pins official 13.0.6 (and its 2D Common 12.0.4 dependency); Unity lists the relevant cached-transform-index fix in 13.0.5 and bone reassignment fixes in 13.0.6. No vendor source patch is used. Reference: https://docs.unity3d.com/Packages/com.unity.2d.animation@13.0/changelog/CHANGELOG.html .

`validation/combat-captures6.xml` passed with official 2D Animation 13.0.6 and generated eight 1080x1920 combat screenshots. Each frame has eight equipped skills and all five companion slots occupied; the test also verifies actual companion attacks. The capture uses a disposable local profile, not the player's account. Files: `artifacts/play-store/combat/combat-01.png` through `combat-08.png`; gallery: `artifacts/play-store/combat-preview.html`. The previous transform-cache exceptions no longer fail this run. Unity still emits null-Transform performance warnings during high enemy churn; that warning is not claimed fixed.

The live stage-popup test passed separately, and the eight Node refund-ledger tests passed. Local and remote obsolete `codex/prefab-characters-ui` branches were removed after confirming their commits are already in main; the local `codex/google-play-iap` pointer was also removed after moving its uncommitted work to main. Local service secrets and signing material remain ignored and outside the commit.

The eight reviewed combat captures were uploaded to Play and saved as the English default phone screenshots (8/8); Korean inherits the same eight images. The listing remains ready to send for review. Evidence: `artifacts/service-setup/play-combat-screenshots-saved.png`.

The signed Android version-code-3 build succeeded in `validation/internal-v3-build.log`. Output: `Documents/ArtArchives/20260928-google-play-iap/game20260920-internal-v3.aab` (205,040,042 bytes, SHA-256 `61627AC94B9EAE9993D9A0D982D2AE3757ACA4DF641BAFF0DAB3A46B74504124`). All 694 tracked C# files and package manifests/locks compared between the main checkout and build checkout matched. The source integration was committed and pushed to main as `eab1222`; both local and remote branch inventories contain only main.

Play published version code 3 as '0.1.0 Internal Test 3 - Rankings and combat fixes' on 2026-09-29 at 07:18 KST. The internal track explicitly shows it available to internal testers. No production release was published. The two non-blocking diagnostic warnings are missing R8 mapping and native debug symbols. Evidence: artifacts/service-setup/play-internal-v3.png.


### PVP Database (2026-10-01)

20260920 프로젝트의 BACKND Database와 PvpRanking 리더보드를 사용한다. 스키마, 권한, 저장 재시도, 클라이언트 전투 판정의 범위는 [PvpDatabase.md](PvpDatabase.md)를 참고한다.
`2026-10-01 07:25 KST`: Windows Development Build `Succeeded 0 errors`. 검증 후 로그인/PVP opt-in 파일과 임시 실행기를 제거했다.
