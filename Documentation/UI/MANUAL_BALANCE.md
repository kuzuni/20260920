# Manual balance controls — 2026-09-23

The user canceled the eight-hour/stage-300 target. Earlier campaign timings are historical and do not describe this balance revision. No automatic player-strength or projected first-day correction drives enemy health/damage.

## Odin window

Open **Doodle Idle → 밸런스 조절**. Each of gold, enemy health and enemy damage has an editable starting value and a per-stage increase displayed as a percentage. Starting defaults are 10 gold per kill, 68 HP and 0 contact damage at stage 1. The early damage target stage (70) and target damage (100) are also editable. The live preview compares existing and draft values at stage 1 and a selected stage; it shares production formulas and includes current relic/buff gold bonuses during play. Previewing does not mutate the game. **실행 중인 게임에 적용** changes the current session. Living enemies retain their remaining-health fraction; kills, stage and ownership do not reset. **기본값으로 저장** writes only the balance controls into `ServicesTuning.json` while preserving unrelated service settings; it also applies them during play. **현재 값 다시 불러오기** reads live values while playing, otherwise saved defaults.

**다이아 디버그 → 지급량 → 다이아 지급** adds the entered amount directly to the saved wallet, clamped at its integer limit. It does not consume any daily reward allowance.

Default formulas, where S is the displayed stage:

- Gold per kill: `starting gold (default 10) × (1 + max(0,S−1) × gold increase)`, then existing relic and buff multipliers. Gold cave rewards use the same formula for 500 kills at difficulty `cave stage × 50`.
- Enemy health: `starting HP (default 68) × (1 + max(0,S−1) × health increase)`. Boss health retains its existing ×20 factor.
- Enemy contact damage: stage 0/1 uses the starting damage (default 0); stages 2–70 interpolate from the starting damage to the configurable early target (default 100). After 70: `100 × (1 + (S−70) × damage increase)`. No contact immunity or damage numbers trigger for zero damage.
- Redundant overall multipliers have been removed from the window, tuning data and combat/reward formulas. Default per-stage increases are 2%. These are editable starting values, not a measured stage-300 completion-time promise.

## Collection/stat rules

- At equal enhancement levels, consecutive tiers within a rarity multiply damage/DPS by 1.1; the last tier to the next rarity's first multiplies by 2.5. Equipment has five tiers per rarity except God; skills have five and companions four per rarity, excluding God.
- Skill/companion damage budgets are distributed over actual cooldown and estimated hit count. UI hit damage and estimated DPS use the same coefficients as combat, including player attack, crits and relics. Full-hit estimates still depend on attacks landing.
- Skill enhancement caps at 100, companions at 1000. Enhancement adds up to 99% of base damage over each category's level range, so a higher rarity at Lv.1 remains stronger than the prior rarity at maximum level. Non-God equipment uses 1% per level through Lv.100; God continues indefinitely.
- Auto-equip and recommendations use rarity, then expected DPS for abilities. Skill and companion cards and equipped slots display `Lv.N`.
- Attack/health/regen stat increments are +5/+40/+1 per level without exponential value growth. Normal and dungeon relics add one percentage point to their option per level.

## Payment and UI

- The normal 10/50 summon buttons consume matching tickets before diamonds, with no bulk discount. Three tickets plus seven draws' diamond cost buy ten draws. Both costs appear on the shared shop/result button. Insufficient total payment consumes nothing. Free summons consume neither currency; dungeon relic summons remain exclusive-ticket-only.
- Buff activation is free, retaining its 15-minute duration and active-buff repurchase guard.
- Available free diamonds mark the claim button, currency tab and Shop navigation. Available free draws mark their button and Shop navigation. Markers use the existing hand-drawn upper-right style.
- Diamond reward flight icons are 108 units (3× the prior 36); gold remains 36.
- Player contact immunity stays one second with quarter-second alpha phases of 0.6/0.8. The fade shader preserves sampled sprite RGB and equipped tint; it changes opacity only. No white or black recoloring.

Validation must run in GitHub-hosted Unity Actions, never local Unity/play mode.

### Validation evidence

- [Editable starting values 35821709650](https://github.com/kuzuni/20260920/actions/runs/35821709650): 4/4 passed on `40c3150`. Covers custom starting gold/HP/damage and early damage endpoint, shared preview/live calculations, retained living-enemy HP fraction, JSON round-trip, cave payouts, default zero-damage first stage and contact immunity. Odin editor code compiled in hosted Unity; the window itself was not visually exercised.
- [Full run 35800146301](https://github.com/kuzuni/20260920/actions/runs/35800146301): 106 passed, 12 failed, 1 optional campaign skipped. Failures exposed old fixed-HP/fixed-reward/button fixtures and overly strict float comparisons; all were investigated.
- [Affected checks 35801993171](https://github.com/kuzuni/20260920/actions/runs/35801993171): 33/34 passed on runtime revision `7182916`. This covers the complete quest rules/migration/action hooks, ticket-priority and mixed payment, manual balance controls, tier ratios, skill levels, free buffs/notifications and opacity-only player feedback. The remaining comparison differed by 0.017 DPS out of 167,533 because the two equivalent formulas use float intermediates.
- [Final damage checks 35802737286](https://github.com/kuzuni/20260920/actions/runs/35802737286): 2/2 passed on `319f93c` after changing only that test's tolerance and CI scope. This closes all 12 failures from the broad run; the 34 distinct affected checks are covered across the follow-ups. The full suite was not redundantly rerun after the test-only tolerance fix.
- Hosted screenshots were inspected for mixed ticket/diamond prices, skill enhancement labels, expanded quest rows and preserved player color during the 0.6-alpha phase. Artifacts are retained under `C:/Users/user/.codex/artifacts/progression-themes/run35801993171` and `run35802737286`.

## Quest revision

- Daily: 11 separate objectives, 1,000 diamonds each. Kill 500 enemies; spin once; claim attendance once; enter each of gold/relic caves once; draw 10 from each of Armor, Club, Skill, Companion, Relic and DungeonRelic.
- Weekly: the same 11 categories, 3,000 diamonds each. Kill 5,000; spin 7 times; attendance 5 times; each cave 5 entries; each summon category 100 draws.
- Repeat: 19 objectives. Kill 500; enhance equipment/skills/companions/relics 10 times each; enhance each of the five stats 10 times; draw each of the six categories 10 times; clear a dungeon once; claim attendance once. These pay 5 diamonds/cycle. Roulette pays 3 diamonds for 5 spins. Gold acquisition is removed.
- Entries and successful clears have separate counters. All payment routes (free, tickets, mixed, diamonds) feed the matching summon objective once. Category/stat counters do not bleed into one another.
- Repeated rewards pay completed cycles in bulk while preserving the next cycle's remainder, including across reloads and daily/weekly resets. Wallet-cap handling retains unpaid cycles. Legacy metric indices and pending repeat progress are retained; old claim flags are mapped only to matching objectives.
