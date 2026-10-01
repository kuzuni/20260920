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


## Live stat controls, quest footer and within-rarity summon ratios

Quest claim-all is fixed to the popup bottom via the shared footer, outside the scrolling quest list. It claims the current daily/repeat/weekly tab and retains its top-right availability dot.

While Stats is open, wallet changes refresh the existing upgrade controls (enabled state, MAX count/cost and projected values). The popup is not rebuilt for incoming gold, preserving scroll and pointer state. Normal purchases still use the shared quote calculation.

Within a five-item rarity the weights are 10:9:8:7:6, normalized to 25%, 22.5%, 20%, 17.5%, 15%. Companion rarities contain four entries and use 10:9:8:7, normalized to 29.4118%, 26.4706%, 23.5294%, 20.5882% (display rounding only). Actual RNG uses integer weight totals 40/34; actual/preview probabilities use the same helpers and multiply by the rarity probability. Relic pools remain uniform, and the single God equipment entry receives its entire rarity probability.

Validation for the quest footer, live stat affordability and 10:9:8:7:6 ratios: hosted run 35829914738 on 109ef38 passed 6/6 tests. Coverage includes live x1/x10/x100/MAX affordability without popup recreation, exact tier lottery intervals and displayed probabilities, claim-all/duplicate protection, wallet limits and notification badges. Hosted quest and stat screenshots were visually inspected; quest action remains fixed at the bottom and stat availability matches the current wallet.


## Stage debug window

**Doodle Idle → 스테이지 디버그** displays the current/highest main stage, current location and destination theme. Enter a one-based target stage and click **스테이지 즉시 이동** during play (including paused combat/timeScale=0). **현재 스테이지 가져오기** copies the current field stage into the input.

`DebugSetMainStage` settles pending real kills using their original reward/dungeon context, exits any active dungeon, clears field/dungeon kill progress, updates the highest reached stage, resets the combat wave synchronously and saves. Existing bosses/projectiles/queued skill attacks are cleared and the destination's terrain, actors and HP are loaded immediately without waiting for physics. Player health, pause state, wallet, ownership and prior unlocks are preserved; real pending rewards are credited but skipping stages awards no extra clear/kill reward. Open pages/HUD refresh. Descending does not revoke higher-stage unlocks. Positive UI stage numbering is clamped at one; boss progression is capped at int.MaxValue-1 internally to avoid overflowing the displayed stage after an extreme debug jump.

## Automatic enemy clearance

The balance window also exposes **플레이어 자동 이동 / 적과 유지할 거리**, default 0.6 world units beyond the sum of both collision radii. The same draft/apply/save controls include this setting. Larger enemies and bosses use their scaled collision radius. Zero restores legacy approach movement; joystick/manual control bypasses avoidance.

Automatic movement approaches distant enemies and retreats from nearby bodies. The complete movement step, including a dash, stops outside the configured clearance. A stopped automatic dash retains a fixed short melee strike reach (collision radii plus 0.75), independent of the configured distance; this prevents distance tuning from turning dashes into unlimited-range damage. Field boundaries constrain movement. Crowding or fast enemy charges can still cause contact; this is movement steering rather than immunity.

Hosted validation: run 35871996478 on 3089a60 passed 7/7 PlayMode tests. It covers live distance changes, serialized tuning copies, normal/boss clearance, full dash sweep limits, backing away from a moving enemy without contact damage, zero/manual bypass, mouse/touch controls, existing contact immunity, 160 seconds of automatic combat, paused stage transitions, unlock preservation and pending reward context. The hosted stage-debug desert screenshot was inspected. No local Unity execution was performed.
# 플레이어 주변 스폰 금지 범위 (2026-10-01)

- `Assets/DoodleIdle/Resources/DoodleIdle/PlayerSpawnExclusion.prefab`의 **Circle Collider 2D → Radius**를 조절합니다. 기본 반경은 `4.5` 월드 단위입니다. `Offset`으로 중심도 옮길 수 있습니다.
- 실행 시 플레이어 루트의 `PlayerSpawnExclusion` 자식으로 붙습니다. 외형 리깅/스킨 크기 변경과 독립적으로 플레이어를 따라갑니다.
- 물리 충돌에 참여하지 않는 범위 판정용 콜라이더라 **Enabled는 꺼져 있는 것이 정상**입니다. 기존 몸통 콜라이더와 공격 판정은 유지됩니다.
- 일반 적, 보충 생성, 던전 적, 보스 모두 적의 몸통 반경까지 더해서 범위 밖에 생성합니다. 위치 탐색에 실패해도 금지 영역 안에는 강제 생성하지 않습니다. 전체 전장을 덮는 크기라면 공간이 생길 때까지 생성을 미룹니다.
- `eye_hurt_left/right.png`는 감은 눈 선을 뜬 눈 윤곽과 유사하게 얇게 수정했습니다. 기존 캔버스(128×160), 스프라이트 참조, 얼굴 앵커 및 애니메이션은 유지합니다. 기본 이미지 편집 도구에 “기존 질끈 감은 눈의 꺾인 모양을 유지하고 뜬 눈 테두리 정도로 선만 얇게, 검정 선과 투명 배경”을 요청한 뒤 기존 캔버스에 맞췄으며, 좌우 굵기를 맞추기 위해 한쪽 결과를 대칭 배치했습니다.

## A/B/C 이동 트리거와 FootDust 공통 설정 (2026-10-01)

`Assets/DoodleIdle/CharacterRigs/Prefabs/Player_Standard.prefab`에서 조절합니다.

- `MovementZones/A_StopAndAttack`: 적이 들어오면 자동 이동과 접근 돌진을 멈추고 Idle에서 기본 공격합니다.
- `MovementZones/B_StartRetreat`: 적이 들어오면 후퇴를 시작합니다.
- `MovementZones/C_FinishRetreat`: 후퇴 중에는 타깃 한 명뿐 아니라 모든 살아 있는 적이 C 밖으로 나가야 후퇴가 끝납니다. B 밖으로 나왔다는 이유만으로 즉시 전진하지 않습니다.
- 세 개 모두 활성화된 CircleCollider2D Trigger이며, `Radius`와 `Offset`을 직접 편집합니다. 적의 몸통 반경도 포함해 판단합니다. 기본 실제 전장 반경은 A 3.2 / B 1.4 / C 2.5입니다. 프리팹 Radius는 리그 원본 좌표 단위이므로 표시 숫자가 더 큽니다. A ≥ C > B 순서를 권장합니다.
- 이 범위는 자동 이동을 제어하며 조이스틱/수동 이동에는 강제로 적용하지 않습니다. 애니메이션 클립이나 컨트롤러는 수정하지 않습니다.

먼지는 플레이어의 `GroundContact/FootDust` Particle System이 공통 원본입니다. `Emission/Rate over Distance`만 방출을 결정하며 Rate over Time과 Burst는 0입니다. 기존 스크립트의 수동 Emit/spacing 방출은 제거했습니다. 정지·제자리 방향 전환·발 본 애니메이션에는 나오지 않으며, 순간이동과 풀 재사용은 궤적을 초기화합니다.

플레이어 **프리팹을 저장**하면 Particle System/Renderer 설정(색상, 크기, 거리 방출량, 수명, 재질 등)이 다른 5개 캐릭터 리그 프리팹에도 자동 복사됩니다. 플레이 중에 프리팹을 저장하면 플레이 종료 후 동기화합니다. 각 캐릭터의 GroundContact 위치는 유지됩니다. 수동 실행 메뉴는 `Doodle Idle → Character Rigs → Sync Foot Dust From Player`입니다. 씬 인스턴스만 변경한 경우 먼저 플레이어 프리팹에 Apply해야 공통 원본이 바뀝니다.

## 골드 착지 / 피격 슬래시 프리팹 (2026-10-02)

- `Assets/DoodleIdle/Resources/DoodleIdle/GoldCoinBurst.prefab`: Particle System의 **Start Size**가 동전 크기입니다. 기본 Random Between Two Constants `0.22 ~ 0.4`이며, 두 값을 `0.44 ~ 0.8`로 바꾸면 두 배가 됩니다.
- **Start Speed**를 올리면 더 높고 멀리 튑니다. `Doodle Gold Coin Burst`의 **Launch Spread**는 좌우 퍼짐, **Gravity**는 낙하 가속도, **Landed Lifetime**은 착지 후 사라지는 시간입니다. 동전 수는 **Emission → Bursts → Count**(첫 번째 Burst, 기본 9)입니다.
- 골드는 죽은 캐릭터의 GroundContact에서 위로 튀고, 각 방출 지점의 높이에 착지한 뒤 멈춰서 사라집니다. 서로 다른 높이에서 동시에 죽어도 개별 바닥 높이를 유지합니다. 골드 보상 계산에는 영향을 주지 않습니다.
- `Assets/DoodleIdle/Resources/DoodleIdle/HitSlash.prefab`: 플레이어/적의 실제 피격 처리에 사용하는 붉은 슬래시입니다. **Start Size X/Y**로 길이/폭, **Start Lifetime**으로 지속시간(기본 0.16초), **Start Rotation**으로 각도를 조절합니다. 무적으로 차단된 공격에는 추가 방출하지 않습니다. PVP도 동일한 피해 처리 경로를 사용합니다.
- 프리팹을 저장한 후 다시 플레이하면 반영됩니다. 두 이펙트 모두 전투당 하나의 Particle System을 재사용하며, 타격마다 GameObject를 생성하지 않습니다. 게임 정지/재시작에 맞춰 시뮬레이션도 정지/초기화됩니다. 애니메이션 파일은 변경하지 않습니다.

### 착지 Y 범위와 실제 미리보기 (2026-10-02)

- `GoldCoinBurst.prefab`의 **Doodle Gold Coin Burst → 착지 Y 최소 / 착지 Y 최대**를 조절합니다. 방출 지점(GroundContact)의 월드 Y에 더해지는 범위이며, 매 동전마다 이 범위에서 착지 높이를 뽑습니다. 예: `-0.3 / 0.1`이면 발밑 기준 아래 0.3부터 위 0.1 사이, `-0.5 / -0.5`이면 모두 아래 0.5에 착지합니다. 기본 `0 / 0`은 기존과 같은 높이입니다.
- 실행 시 `Gold Coin Particle System`으로 이름을 바꾸던 코드를 제거했습니다. 이제 Hierarchy에도 **GoldCoinBurst**로 표시됩니다.
- 기본 Particle System의 재생 버튼은 게임에서 적용하는 중력/착지 처리를 실행하지 않습니다. 컴포넌트의 **실제 동작 미리보기** 버튼 또는 `Doodle Idle → Effects → Gold Coin Preview`를 사용합니다. 별도 복제본에서 실제 게임과 동일한 `EmitBurst/Simulate`를 호출합니다. 원본 값을 바꾼 뒤 **다시 재생**을 누릅니다. 미리보기는 원본 및 씬을 변경하지 않습니다.
- 피격 슬래시 이미지는 `HitSlashStraight.png`의 직선 형태로 교체했습니다. 스킬에서 쓰는 기존 곡선 슬래시 이미지는 그대로입니다. 이미지 생성은 내장 imagegen 도구를 사용했습니다.
- 이미지 편집 프롬프트: “Use case: precise-object-edit. Edit target: attached curved red slash game sprite. Replace the crescent with ONE perfectly STRAIGHT horizontal sword-cut streak, tapered sharp ends at left and right, long thin pointed lozenge silhouette with a straight centerline. Keep the existing hand-drawn cartoon game's red/coral fill, pale pink-white inner highlight and dark ink outline. Completely remove curved/crescent/hook shapes and debris. No crossing strokes, no arcs, no sword, no characters, no background, no text. Center a single clean horizontal straight slash on a truly transparent canvas, length about 85% of canvas, thickness about 10%, generous clear alpha around it. This is a small 2D combat hit effect sprite. Output transparent PNG.”

### 골드 Y 정렬 (2026-10-02)

골드는 동전마다 현재 월드 Y를 캐릭터와 같은 `100 - Round(Y × 10)` 규칙으로 정렬합니다. 낮은 동전은 앞, 높은 동전은 뒤이며 착지 후에도 같습니다. Particle System 하나는 동전별로 캐릭터 사이에 끼워 그릴 수 없으므로, 파티클 방출/낙하/크기/색상은 유지하고 화면 표시는 재사용 SpriteRenderer로 처리합니다. 64개를 미리 준비하고 동시에 보이는 동전 수가 늘 때만 확장하며, 사라진 동전의 표시 오브젝트는 비활성화해 재사용합니다. 기본 ParticleSystemRenderer의 고정 Order로는 실제 동전 정렬을 바꾸지 않습니다.

골드의 착지 후 투명도는 `DoodleGoldCoinSprite.shader`에서 Unity 6의 스프라이트 색상/알파를 반영합니다. 파티클용 셰이더를 그대로 쓰면 SpriteRenderer.color의 알파가 화면에 적용되지 않으므로 교체하지 마세요.

### 피격 경직과 공통 피격 파티클 (2026-10-02)

- 플레이어와 적은 피격 후 0.1초 동안 물리 이동, 현재 애니메이션 포즈, 기본 공격과 새 스킬 발동을 멈춥니다. 연속 피격은 남은 경직 시간을 0.1초로 갱신합니다. 이미 생성된 스킬과 동료는 계속 움직입니다. 원본 애니메이션 파일은 변경하지 않습니다.
- 적은 사망 즉시 공격/충돌/타겟에서 제외되지만 0.1초 동안 마지막 포즈를 보여 준 뒤 풀로 돌아갑니다. 플레이어는 같은 시간 동안 사망 위치에 멈춘 뒤 초기 위치에서 복귀하며, PVP에서는 사망 포즈를 보여 준 뒤 결과 처리를 합니다.
- 공통 원본: `Assets/DoodleIdle/Resources/DoodleIdle/HitBlood.prefab`. 6개 리깅 프리팹의 머리 본 아래 `Face/HitBlood`에 중첩 프리팹으로 들어 있습니다. 캐릭터별 위치는 해당 Transform으로 조절합니다.
- **Player_Standard 프리팹의 Face/HitBlood에서 Particle System / Renderer 설정을 바꾸고 저장**하면 `CharacterHitBloodSync`가 그 설정을 공통 원본으로 반영합니다. 다른 캐릭터에도 같은 설정이 적용됩니다. 각 캐릭터의 위치는 유지합니다. 플레이 모드에서 바꾼 값은 저장되지 않습니다.
- 수량: Emission의 첫 Burst Count. 크기/속도/색/수명/퍼짐/페이드: 기본 Particle System 모듈. 파티클은 피격 때만 방출하며 게임 일시정지와 적 풀 반환 시 함께 멈추거나 정리합니다.

피격 시 붉은 틴트 대신 몸과 무기를 흰색으로 번쩍이며 원래 색으로 돌아옵니다. `DoodleRigVisual.HitFeedback.cs`의 DOTween은 시각적 배율을 0.016초 동안 1.18배로 키운 뒤 0.084초 동안 1배로 복구합니다. 게임의 경직 타이머로 진행하므로 일시정지/PVP에서도 동일하며, 반복 피격·사망·풀 반환 시 크기와 머티리얼을 복구합니다. 원본 포즈와 스킨 배율, 좌우 반전, 몸통 물리 콜라이더는 유지됩니다.

HitBlood의 Main > Scaling Mode는 **Hierarchy**로 유지합니다. 그래야 프리팹에서 조절한 크기와 속도가 게임의 캐릭터 축소 배율에 함께 맞춰집니다. Local은 부모 축소를 무시해 파티클만 커집니다. 분사할 때는 머리 본 회전과 무관하게 현재 시선 반대쪽 위 54.783도로 Cone 축을 맞추며, Shape의 퍼짐 각도와 나머지 파티클 설정은 유지합니다. 오른쪽을 보면 왼쪽 위, 왼쪽을 보면 오른쪽 위로 분사합니다.

피격 강도/시간 조절: **Player_Standard → Face/HitBlood → Character Hit Blood → 경직 / DOTween 공통 설정 열기** 버튼 또는 **Doodle Idle → Effects → Hit Feedback Settings** 메뉴. 원본은 `Assets/DoodleIdle/Resources/DoodleIdle/HitFeedbackSettings.asset`이며 플레이어·적·PVP에 공통 적용됩니다.
- 경직 시간 기본 0.1초, 확대 배율 1.18, 커지는 시간 0.016초, 돌아오는 시간 0.084초. 곡선(Ease)도 각각 선택합니다.
- 흰색 강도 기본 1, 유지 0.02초, 사라지는 시간 0.05초. 확대+복구 또는 유지+페이드가 경직보다 길면 비례해서 줄여 경직 안에 끝냅니다. 진행 중인 피격은 시작할 때의 시간을 유지하고 다음 피격부터 새 값이 적용됩니다.
- HitBlood의 **뒤통수 위 분사 각도** 기본값은 플레이어 프리팹 Z 회전과 같은 **-54.783**입니다. 이 값을 플레이어에서 바꾸고 저장하면 다른 리깅 프리팹에도 공유됩니다. -값의 절댓값이 클수록 위로 가파르게 올라갑니다. Shape의 Angle은 퍼짐 범위입니다.
- 일반 적 이동은 `DoodleIdleGame` 인스펙터의 **적 이동속도 / 플레이어 속도**(기본 0.9)로 조절합니다. 플레이어 속도 6.2이면 적은 5.58이며 플레이어 속도를 바꾸면 자동으로 따라갑니다. 보스 돌진 속도는 기존 별도 설정을 사용합니다.
- 칼날 피격 파티클은 대상 캐릭터 루트의 크기 배율을 Start Size에 곱합니다. 3배 보스는 3배, 보스 크기를 바꾸면 같은 비율로 바뀌며 공통 Particle System 자체 크기를 바꾸지 않아 다른 대상의 이펙트에 영향을 주지 않습니다.

데미지 텍스트는 1.5배로 나타나 0.1초 동안 제자리에 머문 뒤, 다음 0.1초 동안 DOTween OutQuad로 1배가 되면서 상승합니다. 그 후 기존 상승/페이드를 이어갑니다. 텍스트당 트윈 하나를 생성해 풀에서 재사용하고, 게임 경과 시간으로만 진행해 일시정지 중에는 멈춥니다.

일반 적 소환은 플레이어를 중심으로 각도를 고르게 나눈 여러 원에 배치합니다. 기본 반경은 `DoodleIdleGame`의 **적 포위 소환 기본 반경**(6.5)으로 조절하며, 생성 금지 콜라이더가 더 크면 자동으로 바깥으로 밀어냅니다. 벽 근처에서는 맵 안의 빈 방향으로 탐색하고 적끼리 최소 간격을 지킵니다.
