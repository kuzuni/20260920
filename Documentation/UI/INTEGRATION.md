# UI integration and verification

## Entry and local state

Open the existing SampleScene or DoodleIdle scene normally. Both contain DoodleIdleGame. Its BuildHud automatically installs DoodleUi on the same GameObject and requires no Inspector wiring. All work is in the original C:/Users/user/Documents/GitHub/20260920 workspace on codex/final-game-ui while CI/review are in progress.

Only the DoodleUi.* PlayerPrefs keys belong to this feature. Gold, diamonds, collection ownership/levels/loadouts, draw experience/daily free usage, attendance, roulette, quests, buffs, settings and dungeon progress are saved locally. The test fixture snapshots/restores only those keys. Tunable catalog/stat/relic data is in UI/Collections.json, service data in UI/ServicesTuning.json and draw/shop prices in UiCommerce.json under Resources/DoodleIdle.

## Real existing-game connections

- Main profile and wallet read the local model; combat kills earn gold and update live quests.
- Stats/equipment/skill/companion/relic upgrades change the existing combat damage modifier. Attack speed changes the existing basic-attack interval. Fresh starter data is normalized to the original arena's damage and speed to preserve baseline combat behavior.
- Eight displayed equipped skills use the corresponding real combat cooldown clocks. Equipping changes the loadout bonus and HUD. The original automatic23 skills remain active to preserve existing combat; this is explicitly explained in skill detail.
- Dungeon entry consumes only that dungeon's daily attempt and starts a real existing-field kill challenge. Completion grants the configured local reward. No new independent dungeon maps are claimed.
- Active buffs expire by UTC time; gold and damage bonuses apply to real combat/gains.
- UI button/dim gestures block pointer movement, release the joystick, suppress gameplay keyboard shortcuts while modal UI is open, and consume the dismissing release.

## Explicit external and baseline limitations

- Currency products display price/quantity, but payment provider is unconnected. The purchase button explains that no charge or grant occurs.
- Account linking is unconnected and labelled accordingly.
- Chat is a local, session-only input demonstration and sends nothing to a network.
- PVP ranks and opponents are a labelled local simulation; podium art is taken from the same opponent records shown in the ranking list. No online leaderboard or real matchmaking is claimed.
- The existing game has no player health/damage/defense receiver. Health/defense upgrades currently affect the model and displayed power; adding a player survival system would change combat scope.
- The existing game provides no background music/SFX AudioSources. Volume sliders save/apply to available sources; the missing audio is stated on screen. Power saving and pause use real game settings/control.
- Device-local date/saves are not authoritative server time or anti-cheat protected economy.

## Evidence workflow

Run only .github/workflows/doodle-idle-tests.yml on GitHub-hosted Ubuntu. The current task never starts local Unity, play mode, a game or local tests. Existing combat tests remain, plus native UI interaction tests and actual URP raster captures at720x1520,720x1280,900x900,1440x900. The action uploads NUnit XML, editor logs and artifacts/screenshots. Visual geometry errors do not suppress captures; the final capture test still fails until fixed.

Latest commit/run status and remaining work are recorded in PROGRESS.md. A successful compile is not a completed visual review.
