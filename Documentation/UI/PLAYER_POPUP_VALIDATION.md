# Player and popup changes — 2026-10-03

Project: 20260920. Unity 6000.3.8f1, DX12 Windows editor.

## Validation scope

Isolated PlayMode profiles preserve the user's save and account. Tests cover actual animation impact events, player death/respawn timing, cosmetic hit feedback, active projectiles after death, UI particle counts, visible collection rig animation, token-loading presentation, opponent appearance/stakes and PVP damage/loadout arithmetic.

Automatic-login presentation uses controlled success/failure responses; it does not modify the saved account or claim a new live token-server test. Nickname UI and SDK compilation are checked; the user's server nickname, logout and withdrawal are not invoked.

Nickname implementation follows the official BACKND [UpdateNickname API](https://docs.backnd.com/sdk-docs/backend/base/user/nickname/update/).

## Assets

`StatUpgradeEffect.prefab` uses the existing summon-circle art and a small native ParticleSystem. Free, paid and mileage-exchange diamonds use the exact existing `UiKit.Art("Diamond")` asset, with a UI Free label on the free card. A built-in image_gen draft was rejected by the user because its diamond differed; that draft is not referenced or shipped. No new diamond bitmap replaces the game's diamond. Animation clips are unchanged by this task.

## Results

All 23 distinct PlayMode checks passed on their latest runs. Initial failures exposed a missing CanvasRenderer, preview glint precision, companion rigs without weapons and a test-only shop navigation toggle; these were repaired and rerun. Result files are local ignored artifacts under `artifacts/character-reports/`:

- `player-first-pass.xml`, `popup-second-pass.xml`, `popup-third-pass.xml`, `popup-visual-pass.xml`, `popup-shop-pass.xml` retain the chronological runs.
- `player-popup-final-results.json` records the latest result for each of the 23 checks. This is an aggregation of focused runs, not a claim that the whole repository test suite was executed.
- Coverage includes 1/100/MAX purchases (including exactly 13 affordable MAX upgrades and the 100-particle cap), baked extended-critical icon artwork, original diamond sprites, collection Idle/Walk, PVP body centering, 100x HP, real combat damage/ownership arithmetic and the existing 30-second HP-ratio rule.

Visual captures were inspected at 720x1520 under `artifacts/screenshots/`: `stat-upgrade-celebration.png`, `skills-two-rows-lock-covers.png`, `companions-animated-slots.png`, `skins-idle-walk.png`, `active-buff-glow.png`, `pvp-candidate-appearance-layout.png`, `shop-original-free-diamond.png`, `mileage-exchange-cards.png`, `relic-upgrade-particles.png` and `pvp-centered-podium.png`.

## Performance sample

`artifacts/performance/player-popup-20261003.json` contains a 300-frame Windows editor sample with eight skills and five companions: mean 16.72 ms/frame (~59.8 FPS), p95 21.29 ms, mean main-thread 16.62 ms. The sampled enemy count was five; this does not establish stable 60 FPS under maximum enemy load or on Android. Editor allocations averaged 286,903 bytes/frame and batches 1,190/frame, so this is not a zero-allocation or full performance-completion claim. Existing pooled effects are reused; new icon particles use one reusable ParticleSystem/mesh per visible source and visible collection previews render at 160px/15 FPS.

## Build

Windows x64 Development build succeeded with 0 errors at 2026-10-03 00:38 KST. Login and main scenes were included. Output: `Builds/Performance/DoodlePerformance.exe`. This validates player compilation/assets; it is not an Android device or live purchase test. The temporary editor command bridge was removed after verification.
