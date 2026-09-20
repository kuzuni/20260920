# Unity final UI progress

## Constraints and baseline
- Original workspace C:/Users/user/Documents/GitHub/20260920; branch codex/final-game-ui.
- Preserve pre-existing modified ProjectSettings/McpUnitySettings.json. Never stage/reset this user file.
- No local Unity/editor/play/game/test execution. CI only.
- Final 25 references copied into Documentation/UI/FinalDesign; reviewed all 25. Desktop references are no longer used.
- Existing runtime: DoodleIdleGame partials, 23 automatic combat skills, touch joystick, runtime Korean InterfaceFont, existing GameCI PlayMode suite.
- Heartbeat unity-ui registered ACTIVE, 15 minutes, current task. Created 2026-09-20 19:41:10 KST; initial due approximately 19:56:10 KST (scheduler timing pending).

## Completed
- Read full request, inspected references and existing runtime/CI.
- Created branch and separated module ownership (ARCHITECTURE.md).

## In progress / next
- Coordinator: common adaptive UI, game bridge, reference upload, art, CI visual/interaction coverage.
- Collections: stats/equipment/skills/companions/relics/catalog.
- Commerce: summon/shop/probability/results.
- Services: attendance/roulette/buffs/quests/dungeons/PVP/chat/settings.
- Integrate, push, inspect latest CI XML and actual captures at 9:19, 9:16, square and landscape; fix failures.

## Validation
- No implementation commit tested yet. No local tests run.
- Existing GitHub workflow .github/workflows/doodle-idle-tests.yml; gh not on PATH (credential/CLI access investigation in progress).

## Remaining limitations
- Server, real purchase, account linking, live chat and competitive rankings absent in baseline; implement clearly identified local adapters.
