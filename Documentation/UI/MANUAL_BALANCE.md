# Manual balance controls — 2026-09-23

The user canceled the eight-hour/stage-300 target. Earlier campaign timings are historical and do not describe this balance revision. No automatic player-strength or projected first-day correction drives enemy health/damage.

## Latest: numeric controls and separate currency debug window

Graph editing was canceled. **Doodle Idle → 밸런스 조절** now contains only numeric starting values, increase rates, the early-damage target, formulas, numeric comparisons, Apply/Save/Reload. Old serialized correction points are retained for compatibility but are ignored by gold, enemy HP/damage and all stat-cost calculations. The exponential baseline and early linear damage ramp remain. Historical graph details below no longer describe the current editor.

**Doodle Idle → 화폐 지급 디버그** is a separate Odin window. Select Gold or Diamonds, enter an amount and press the currency grant button during play. Grants update the wallet, save immediately and refresh the HUD/current popup. Gold saturates at long.MaxValue, diamonds at int.MaxValue; negative API requests grant nothing. Grants do not change balance settings or consume free reward attempts. There are no currency grant controls in the balance window.

### Numeric growth sections

Each of the six balance groups now supports any number of numeric growth sections: **+ 증가율 변경 구간 추가**, starting stage/level, rate %, and per-row deletion. Each row previews the values immediately before and at its threshold. A threshold N changes the multiplier for N-1 → N; it does not reset the starting value. Example start 10, initial 50%, from stage 3 use 100%, from stage 5 use 0% gives 10, 15, 30, 60, 60. With no sections the single exponential baseline is unchanged. The same section model applies independently to common basic-stat costs, x2 costs and x4 costs.

The damage early ramp stays linear through its target stage; growth sections only affect later stages. Thresholds earlier than that ramp end select the rate already active when exponential growth begins. Last section continues indefinitely. Rates are nonnegative; 0% creates a flat section. Evaluation walks section boundaries rather than every stage and retains the 1e30 cap. Apply/save use deep copies and JSON persistence; cave rewards and real stat quotes use the same section calculation as the preview.

## Odin window (historical graph revision)

Open **Doodle Idle → 밸런스 조절**. Each of gold, enemy health and enemy damage has an editable starting value and a per-stage increase displayed as a percentage. Starting defaults are 10 gold per kill, 68 HP and 0 contact damage at stage 1. The early damage target stage (70) and target damage (100) are also editable. The live preview compares existing and draft values at stage 1 and a selected stage; it shares production formulas; the graph shows base gold before relic/buff bonuses. Previewing does not mutate the game. **실행 중인 게임에 적용** changes the current session. Living enemies retain their remaining-health fraction; kills, stage and ownership do not reset. **기본값으로 저장** writes only the balance controls into `ServicesTuning.json` while preserving unrelated service settings; it also applies them during play. **현재 값 다시 불러오기** reads live values while playing, otherwise saved defaults.

**다이아 디버그 → 지급량 → 다이아 지급** adds the entered amount directly to the saved wallet, clamped at its integer limit. It does not consume any daily reward allowance.

Default formulas, where S is the displayed stage:

- Gold per kill: `starting gold (default 10) × (1 + gold increase)^max(0,S−1) × gold curve correction`, then existing relic and buff multipliers. Gold cave rewards use the same formula for 500 kills at difficulty `cave stage × 50`.
- Enemy health: `starting HP (default 68) × (1 + health increase)^max(0,S−1) × HP curve correction`. Boss health retains its existing ×20 factor.
- Enemy contact damage: stage 0/1 uses the starting damage (default 0); stages 2–70 interpolate from the starting damage to the configurable early target (default 100). After 70: `100 × (1 + damage increase)^(S−70)`, then the damage curve correction. No contact immunity or damage numbers trigger for zero damage.
- Redundant overall multipliers have been removed from the window, tuning data and combat/reward formulas. Default per-stage increases are 2%. These are editable starting values, not a measured stage-300 completion-time promise.

## Stat upgrade cost controls

The same Odin window now exposes three independent cost groups: Attack/Health/Health Regen (shared), x2 critical chance, and x4 critical chance. Each has a starting gold cost and a per-level cost increase percentage. Defaults are 20/20/40 gold and 0.4% growth for each group. Health/Regen starting costs therefore change from 18/16 to the shared 20. Stat gains stay linear at +5/+40/+1 and +0.025/+0.05 percentage points.

At current level L, the next upgrade costs `ceil(starting cost × (1 + increase)^L × group curve correction)`; 0% means fixed cost. The level preview, single/bulk/MAX quotes and actual purchases use one shared price function. x4 remains locked until x2 reaches its cap. Applying costs preserves levels, stats and wallet and refreshes an open stat popup. Saving defaults writes only the new cost settings into the freshly loaded `Collections.json` alongside the existing service tuning save, preserving the catalog and ability values. Three basic stats always have equal costs at equal levels. These cost groups replace the old per-stat base costs and global cost-growth field.

## Collection/stat rules

- At equal enhancement levels, consecutive tiers within a rarity multiply damage/DPS by 1.1; the last tier to the next rarity's first multiplies by 2.5. Equipment has five tiers per rarity except God; skills have five and companions four per rarity, excluding God.
- Skill/companion damage budgets are distributed over actual cooldown and estimated hit count. UI hit damage and estimated DPS use the same coefficients as combat, including player attack, crits and relics. Full-hit estimates still depend on attacks landing.
- Skill enhancement caps at 100, companions at 1000. Enhancement adds up to 99% of base damage over each category's level range, so a higher rarity at Lv.1 remains stronger than the prior rarity at maximum level. Non-God equipment uses 1% per level through Lv.100; God continues indefinitely.
- Auto-equip and recommendations use rarity, then expected DPS for abilities. Skill and companion cards and equipped slots display `Lv.N`.
- Attack/health/regen stat increments are +5/+40/+1 per level without exponential value growth. Normal and dungeon relics add one percentage point to their option per level.

## Latest summon prices

Skill and Companion draws cost 20 diamonds per item: 200 for 10 and 1,000 for 50. Relic draws cost 10 per item: 100 for 10 and 500 for 50. Both resource tuning and code fallback use these prices. Equipment pricing is unchanged. Matching tickets still pay before diamonds, dungeon relics remain ticket-only, and max-level skill copy refunds use the same current 20-diamond skill unit price.

## Payment and UI

- The normal 10/50 summon buttons consume matching tickets before diamonds, with no bulk discount. Three tickets plus seven draws' diamond cost buy ten draws. Both costs appear on the shared shop/result button. Insufficient total payment consumes nothing. Free summons consume neither currency; dungeon relic summons remain exclusive-ticket-only.
- Buff activation is free, retaining its 15-minute duration and active-buff repurchase guard.
- Available free diamonds mark the claim button, currency tab and Shop navigation. Available free draws mark their button and Shop navigation. Markers use the existing hand-drawn upper-right style.
- Diamond reward flight icons are 108 units (3× the prior 36); gold remains 36.
- Player contact immunity stays one second with quarter-second alpha phases of 0.6/0.8. The fade shader preserves sampled sprite RGB and equipped tint; it changes opacity only. No white or black recoloring.

Validation must run in GitHub-hosted Unity Actions, never local Unity/play mode.

### Validation evidence

- [Interactive curves 35824378619](https://github.com/kuzuni/20260920/actions/runs/35824378619): 8/8 passed on `2ffb2f1`. Covers exponential baselines, control-point add/replace/remove, interpolation, independent curve snapshots, JSON restoration, shared actual purchases/payouts, dungeon HP/rewards, early zero damage and numeric caps. Odin graph editor compiled; native mouse dragging/context-menu/Undo interactions were not automated. A subsequent presentation-only change makes axis labels readable in both editor themes.
- [Three stat-cost groups 35823228553](https://github.com/kuzuni/20260920/actions/runs/35823228553): 7/7 passed on `04079dd`, including shared basic costs, independent critical prices, quote/payment/MAX consistency, open-popup refresh and unchanged stat increments.
- [Multiplier removal 35822520920](https://github.com/kuzuni/20260920/actions/runs/35822520920): 4/4 passed on `51738fc`.

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

## Interactive growth graphs

The Odin balance window now uses a two-column workspace: category settings/formula on the left, actual existing/draft plots on the right. Select one of six categories (gold, HP, damage, shared basic-stat cost, x2 cost, x4 cost). There is no universal game-economy formula: this revision chooses exponential baseline growth for extended progression, retaining the requested early damage ramp.

Right-click the graph to add a point at that step/value or delete an existing point. Drag a yellow point to move it; the blue origin changes the starting value. Selected points also have exact numeric inputs. Undo/redo uses Unity Undo; resetting points restores the exponential baseline. X-range and log10(1+value) Y-axis controls affect display only. Zero-valued baseline segments must first be raised with their starting/early-target settings before a correction point can lift them. Curves can be non-monotonic if deliberately edited that way.

Stored control points are per-step correction factors, not a redundant global multiplier. The implicit origin factor is 1; factors use the chosen linear, automatic smooth or manual cubic-Hermite interpolation and remain constant after the last point. Displayed points are actual final values, converted to factors internally. Starting values/growth edits therefore rescale the curve consistently. Points are deep-copied when applying and serialize with the tuning files. All reward/HP/damage/cost previews share gameplay calculations, including currency rounding/caps. Gold plots exclude temporary relic/buff bonuses and show the base reward; field/cave payouts still apply those bonuses. Graph HP/damage show a normal enemy, with the standard contact base 64.

Stage HP and gold have changed from linear to exponential growth; the old stage-300 combat pacing claim remains canceled. No new completion-time guarantee is made.

### Responsive layout and tangent handles

The workspace uses one scrollable content area. Below 960px window width it stacks settings and graph; wider windows use proportional columns. Long labels/comparisons wrap above full-width inputs. Graph size follows its panel width, and tick density/positions adapt to avoid clipping; graph and point controls remain reachable in short/docked windows.

Each curve offers Linear, Automatic Smooth and Manual Handles modes. Automatic uses shape-preserving Hermite slopes. Manual starts from the existing mode's slopes, then allows independent incoming/outgoing slopes through purple handles or exact handle-height inputs. Selecting smooth/manual with no points inserts a neutral endpoint at the displayed range end. Moving a point preserves its slopes. Switching modes, point/handle edits and resets support Undo. Origin has only an outgoing handle; the final point has only an incoming handle because correction remains constant afterward. Handles are vertically draggable at a fixed one-third segment position. Curve/tangent fields are deep-copied on Apply and survive JSON save/load; old curves default to Linear. Negative manual overshoot is clamped to zero before existing reward/HP/cost minimums apply.

Manual handles also expose independent incoming/outgoing angle inputs in degrees. The angle is atan(dC/dstep), where C is the stored correction factor; 0 degrees keeps the correction slope flat while the exponential baseline still grows. It is independent of viewport size/log scaling, so it is not the literal screen angle. Inputs clamp to -89.9..89.9 degrees, ignore non-finite values, and update the same tangents as height edits and drags. Undo, Apply and JSON persistence therefore include angle changes without a duplicate angle field.

Hosted validation for responsive layout/smooth tangents: run 35825716339 passed. Native editor resize/drag interactions were not exercised locally, per repository policy.

Angle validation: GitHub-hosted run 35826473237 on 2c7377d passed 9/9 tests. Coverage includes signed/zero angles, finite limits, endpoint preservation, JSON restoration and live gold payouts after angle edits, plus the existing enemy and stat-cost regressions.

Latest validation: GitHub-hosted run 35828482235 on b99b1c4 passed 12/12 tests. This includes numeric section boundaries/continuity/serialization and live rewards/costs, ignored legacy graph factors, currency grant persistence/saturation, lowered summon prices, ticket-first purchases and dynamic skill refunds. Prior numeric-only and section runs passed 8/8 (35827376387) and 9/9 (35828010258). Native Odin window interaction was not manually exercised; local Unity execution remains prohibited by repository policy.
