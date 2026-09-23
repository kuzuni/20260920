# Manual balance controls — 2026-09-23

The user canceled the eight-hour/stage-300 target. Earlier campaign timings are historical and do not describe this balance revision. No automatic player-strength or projected first-day correction drives enemy health/damage.

## Odin window

Open **Doodle Idle → 밸런스 조절**. Each of gold, enemy health and enemy damage has an overall multiplier and a per-stage increase displayed as a percentage. **실행 중인 게임에 적용** changes the current session. Living enemies retain their remaining-health fraction; kills, stage and ownership do not reset. **기본값으로 저장** writes only these six controls into `ServicesTuning.json` while preserving unrelated service settings; it also applies them during play. **현재 값 다시 불러오기** reads live values while playing, otherwise saved defaults.

**다이아 디버그 → 지급량 → 다이아 지급** adds the entered amount directly to the saved wallet, clamped at its integer limit. It does not consume any daily reward allowance.

Default formulas, where S is the displayed stage:

- Gold per kill: `10 × gold multiplier × (1 + max(0,S−1) × gold increase)`, then existing relic and buff multipliers. Gold cave rewards use the same formula for 500 kills at difficulty `cave stage × 50`.
- Enemy health: `68 × health multiplier × (1 + max(0,S−1) × health increase)`. Boss health retains its existing ×20 factor.
- Enemy contact damage: stage 0/1 is 0; stages 2–70 interpolate from 0 to 100. After 70: `100 × (1 + (S−70) × damage increase)`. The overall damage multiplier applies to the curve; default 1. No contact immunity or damage numbers trigger for zero damage.
- Default overall multipliers are 1; default per-stage increases are 2%. These are editable starting values, not a measured stage-300 completion-time promise.

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

## Quest revision

- Daily: 11 separate objectives, 1,000 diamonds each. Kill 500 enemies; spin once; claim attendance once; enter each of gold/relic caves once; draw 10 from each of Armor, Club, Skill, Companion, Relic and DungeonRelic.
- Weekly: the same 11 categories, 3,000 diamonds each. Kill 5,000; spin 7 times; attendance 5 times; each cave 5 entries; each summon category 100 draws.
- Repeat: 19 objectives. Kill 500; enhance equipment/skills/companions/relics 10 times each; enhance each of the five stats 10 times; draw each of the six categories 10 times; clear a dungeon once; claim attendance once. These pay 5 diamonds/cycle. Roulette pays 3 diamonds for 5 spins. Gold acquisition is removed.
- Entries and successful clears have separate counters. All payment routes (free, tickets, mixed, diamonds) feed the matching summon objective once. Category/stat counters do not bleed into one another.
- Repeated rewards pay completed cycles in bulk while preserving the next cycle's remainder, including across reloads and daily/weekly resets. Wallet-cap handling retains unpaid cycles. Legacy metric indices and pending repeat progress are retained; old claim flags are mapped only to matching objectives.
