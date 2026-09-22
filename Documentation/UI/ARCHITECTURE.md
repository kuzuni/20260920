# Final UI implementation contract

Only `Documentation/UI/FinalDesign/01..25 *.png` is the visual reference. Text rules in REQUEST.md override mockup examples. Do not read Desktop references again. Do not run Unity, a game, or local tests. GitHub hosted Actions is the only execution/validation environment.

## Ownership
- Coordinator: DoodleUi.cs, DoodleUiKit.cs, DoodleIdleGame integration, CI/tests, shared docs, assets, all git operations.
- Collections agent: DoodleUi.Collections.cs, DoodleUi.Catalog.cs, collection JSON tuning only.
- Commerce agent: DoodleUi.Commerce.cs, commerce JSON tuning only.
- Services agent: DoodleUi.Services.cs, services JSON tuning only.
- Never edit another owner's file or scene/prefab. Report API requirements to coordinator. Do not commit or push independently.

## Shared code contract

### 2026-09-23 first-day progression (supersedes older numeric rules below)

- See `DAY_ONE_BALANCE.md` for the eight-hour target, stage income projection, combat coefficients, ticket missions and shop fulfillment limitations.
- `CONTINUOUS_CAMPAIGN.md` specifies the opt-in empty-account run to stage 300. Damage-bearing basic shots/stone shots and combat elapsed time run in `FixedUpdate`, alongside other combat, so long render frames cannot extend projectile range during accelerated validation or slow-device gameplay.
- Non-relic summons have 35 levels with exact 0.001% rarity weights. Within each rarity, lower numbered entries are more common. Relic and dungeon-relic pools remain uniform and unlevelled.
- Equipment equip values are percentages of attack/health stats. Skill and companion damage coefficients and levels feed both actual damage and UI estimates. Death demotes the current field stage while retaining highest-stage unlocks.
- Mission action history persists independently of daily/weekly resets. Cave tutorials follow the stage-45 repeating mission cycle. Free diamonds and mileage use the separate `DoodleUi.CommerceExtras.v1` save, included in game reset.
- Mileage art resolves to the separate smooth blue diamond card (`MileageCoupon.png`), never the unused sixth ticket atlas cell. Summon-row companion art is `CompanionMon_10`; its ticket depicts a winged cloud.

### 2026-09-23 hit and notification presentation

- Player contact immunity keeps its one-second window and quarter-second phases. The flash phase uses `DoodlePlayerHit.shader` to replace RGB with white while preserving source alpha and applying explicit material `_Opacity=0.6`; the alternate phase restores the normal sprite material with 0.8 renderer alpha. A private player material avoids affecting other actors and is disposed with the game. The explicit opacity handles URP sprite batching paths that do not supply renderer alpha as vertex color.
- `UiKit.NotificationDot` provides a cached, slightly irregular ink-outline/coral-fill sprite. All notifications retain their common upper-right position and non-interactive layout. Equipment recommendations consider only the strongest discovered item in each equipment category; upgrade/synthesis notifications remain independent.
- Stat titles include current `Lv.N`, with existing current/next value rows. Equipment cards use `Lv.N` and reserve enough header width beside grade and corner badges.
- Dungeon relic wallet, shop/result cost buttons and clear rewards use the transparent `DungeonRelicTicket` icon. It is imported at 256px with mipmaps for clean small UI rendering. Item/category artwork remains the pottery icon.
Namespace DoodleIdle. `public sealed partial class DoodleUi : MonoBehaviour`. Runtime uGUI, programmatic construction. Native panels, text, images, Buttons, ScrollRects (never screenshot backgrounds). Existing game art reused; common hand ink borders, cream white panels, pastel green/blue/yellow action buttons, red close X.

Shared fields/properties: `DoodleIdleGame game; Font font; long Gold = 0; int Diamonds = 0; string PlayerName = "먼지고양이"; long Power` computed from collection bonuses; `string ActivePage {get;}`. Shared `RefreshPage()` rebuilds current page; `Toast(string)`; `Save()` persists local state; `ShowPage(string)` routes `Stats,Equipment,Skills,Companions,Relics,Dungeons,Pvp,Shop,Attendance,Roulette,Buffs,Quests,Chat,Settings`. `ShowDetail(string title, Action<RectTransform> build)` opens stacked modal; `CloseDetail()` closes only top overlay. `ShowRewards(string title, List<UiReward> rewards)` opens panel-free dim reward overlay; UiReward fields `string name, icon; int amount, rarity`.

Screen builder signatures in partial files: `void BuildStats(RectTransform body)`, `BuildEquipment`, `BuildSkills`, `BuildCompanions`, `BuildRelics`, `BuildShop`, `BuildDungeons`, `BuildPvp`, `BuildAttendance`, `BuildRoulette`, `BuildBuffs`, `BuildQuests`, `BuildChat`, `BuildSettings`. Body is a vertical scroll CONTENT with auto preferred height, fixed popup width ~600 logical units. Chat uses the equipment-sized popup with bottom navigation visible; summon results use width-limited readable content inside a fullscreen canvas. Each builder must use layout elements, no screen-size assumptions. Main popup title/close and bottom navigation are coordinator-owned. Tabs can be placed in body; equipment order remains selected spec, total ownership, collection, actions, tabs.

## UiKit static API (coordinator implementation)
- `RectTransform Box(Transform parent,string name,Color color,float height=0)` framed panel with LayoutElement when height>0.
- `RectTransform Column(Transform parent,string name,float spacing=8,float padding=8)` vertical layout auto height.
- `RectTransform Row(Transform parent,string name,float height=52,float spacing=8)` horizontal layout with fixed height; children flexible width by default.
- `Text Text(Transform parent,string text,int size=24,TextAnchor align=MiddleLeft,float height=36)` uses Korean font; non-raycast; flexible width.
- `Button Button(Transform parent,string text,Action click,Color? color=null,float height=52)` framed button, fixed height, flexible width; text autodownsizing min 16.
- `Image Icon(Transform parent,string resource,float size=64)` preserveAspect, fixed square, non-raycast; supports DoodleIdle resources and UI icon names via shared resolver.
- `RectTransform Gauge(Transform parent,string text,float fraction,float height=24)` framed gauge with in-bar text.
- `RectTransform Grid(Transform parent,string name,int columns=4,float cellHeight=112)` responsive grid fixed column count, auto height (width from parent).
- `Button Slot(Transform parent,string name,string icon,int rarity,int count,int needed,bool equipped,bool locked,Action click,float height=112)` rarity frame, top-left grade, icon, count gauge, equipped check; locked silhouette; usable in grid or horizontal row.
- `void Flexible(Transform child,float weight=1)`; `void Height(Transform child,float height)`.
- colors `Ink,Paper,Blue,Green,Yellow,Red`, `Color Rarity(int)`; `Sprite Art(string)` loads/caches resources or generated vector sprites. No Unicode pictograph icons; use actual sprite/vector art.

## Cross-agent collection APIs
Collections implements `List<UiItem> Items(string category)` (Armor,Club,Skill,Companion,Relic), `UiItem GrantItem(string category, System.Random rng)` using `SummonWeights(category)` from UiCommerce.json; uniform within grade. All non-relic summons have levels 1..30. Armor/Club use seven grades with level-30 percentages 10/10/24/25/20/10/1. Skills contain 30 entries (five per grade); companions contain 24 (four per grade), Normal through Mythic; they exclude God and transfer its probability to Mythic (level 30: 10/10/24/25/20/11). Relics are one grade with uniform item probability and no summon progression. `void AddItem(UiItem item,int count)`; `float OwnedBonus`; `long Power`; `List<UiItem> EquippedSkills`; `InitCollections()`.
UiItem public fields: `string id,name,icon,category; int rarity,count,level; bool equipped;`.
Commerce uses these APIs and reports requirements. Main calls InitCollections in Awake. Arrays/catalog are tunable local data, not server truth.

Services owns local daily/reward/account/chat/PVP adapters in its file and `InitServices()`, `TickServices()`. Daily resets use UTC date; data stored under DoodleUi-specific PlayerPrefs keys; never alter existing game saves. Shared wallet changes call Save() (coordinator persists wallet) and Toast/RefreshPage as appropriate. No external payment/auth/chat claimed. Services may add `SaveServices()` and collection `SaveCollections()`, called by coordinator Save().

Main integrates real combat kills into wallet/mission and reads real cooldowns. UI upgrading/equipping must retain existing combat system; explicit new bonuses can use small game adapter API supplied by coordinator. Record unsupported external features.

## 2026-09-21 runtime and layout revision

- Skill/Companion collection detail windows retain the 360×550 reference coordinates. DoodleUiWindow.detailScale=1.8 enlarges the inner layout uniformly and sizes the outer panel to 648×990; safety fitting reduces only the uniform scale. DoodlePopupMotion owns the outer animation transform.
- Summon rows are 264 high (Relic 336), with a 213-square icon. Ten/fifty paid buttons share Yellow on both shop and results. Main navigation window measurements remain unchanged; lists scroll.
- PVP uses the former simple UiKit.Box podium with portrait bottom aligned to the pedestal top, respecting source aspect.
- Terrain is one full rectangular opaque sprite across the arena and camera margins. DoodleTerrain repeats the selected Grounds atlas region in its original orientation with a 3-unit pattern. An 8%-wide edge cross-fade uses translated samples from the same atlas cell to join wraps without mirroring grass upside-down. _MainTex holds sprite geometry compatibility; _AtlasTex holds the actual theme atlas. This avoids tight-mesh tile gaps and reduces motifs from the previous 13-unit size. Each theme has an explicit muted base palette. Following feedback that the first 0.16-strength/0.4-saturation pass was too bland, atlas detail now uses 0.55 strength and 0.85 saturation, restoring recognizable markings and theme color at moderate contrast. The output remains fully opaque, including transparent source pixels.
- DoodleIdleGame.Variants reuses projectile, snake, cloud, worm and Molotov mechanics for equipped higher-grade variants. DoodleIdleGame.Companions tracks the five equipped companions, uses visible world positions for attacks and removes actors on unequip. Original automatic drone/guardian/orbit casts moved to companions; explicit diagnostic casts remain available. Other established automatic combat continues.
- Seven reusable ParticleSystems cover existing effects plus recolored purple arrow fire trails and blue Molotov ground flames. Fixed-step simulation shares gameplay pause/reset behavior.
- Catalog IDs are stable across the skill-to-companion move so saved copies and enhancement levels survive. New catalog entries and artwork mapping are documented in Documentation/skill-companion-catalog.md and Documentation/skill-companion-expansion-prompts.md.

## 2026-09-21 equipped skills and complete actor frames

- `DoodleIdleGame.SkillActivation` owns automatic cooldowns for the 30 catalog skills, only while equipped. Removed unconditional legacy skill loops. Explicit `DebugCastSkill(itemId)` bypasses equipment/ownership; queued automatic arrows, sound and variants stop launching after unequip.
- `DoodleSkillTestWindow` is an Odin Editor window with 30 per-skill test buttons and an independent basic slash/dash toggle. It calls the runtime bypass and does not grant, equip or spend items.
- `DoodleCollectionArt` loads distinct skill thumbnails, companion frames and projectiles. Companion frames use a common A/B crop and scale. No old skill or player artwork is used as the new companion character designs.
- Companion patterns are catalog data: trajectory, volley count/gap, interval, speed and explosion radius. Actors follow the player, animate with two complete frames, fire their own art and disappear on unequip.
- Full loadouts return to the existing equipped row for replacement; downward arrows identify selectable occupied slots. Empty cards/HUD slots use a plus icon.
- Sound ellipse depth is .55 of its width across travel; the collision sweep rotates with the same axes.
- Latest actor anatomy and exact built-in image-generation prompts are in `Documentation/two-frame-actors-and-loadouts.md`. The older head-only and arms-only drafts are superseded by the user's mushroom/sprout/pea walking references.

## 2026-09-22 skill motion tuning

- Cloud skills follow a living target at 2.2 world units/second (normal) or 3 (red), keeping a 2-unit overhead offset and acquiring another enemy when their target dies. Animation, lightning and lifetime remain active.
- Fire's three projectiles use quadratic homing curves. Purple fire arrows use the same curve with two tapered lateral waves, preserving eight sequential shots, doubled artwork and particle trails. Retargeting starts a new curve at the projectile's current position; each projectile still hits once.
- Shotgun pellet artwork is doubled from .22 to .44. Eggplant (`eggplant`, now named `가지의 분노`) uses the same directional rolling animation as cucumber, retaining three straight piercing paths. Durian fires three shots .2 seconds apart. Shuriken emits eight simultaneous directions spaced 45 degrees apart with short fading afterimages. Ice snake head/body artwork is doubled on spawn and every animation update.
- Catalog IDs, ownership and loadout rules remain unchanged. Current wording is in `skill-companion-catalog.md`; later REQUEST entries override the earlier two-shot durian and sequential shuriken specifications.

## Muted terrain verification

- Upright grass correction: runtime commit `4a77f63`, [terrain Actions run 35613992689](https://github.com/kuzuni/20260920/actions/runs/35613992689), 1/1 test passed. The render check covers all ten themes and compares grass tiles one repeat apart horizontally/vertically and at negative coordinates (mean RGB error below 0.001), preventing the former alternating mirror regression. Inspected the grass close-up, repeated field, gameplay portrait, and ruin boundaries. Grass roots stay below their blades. Artifacts: `C:/Users/user/.codex/artifacts/progression-themes/run35613992689`. No local Unity execution.
- Current moderate pass: commit `bfcebfc`, [terrain-only Actions run 35609594613](https://github.com/kuzuni/20260920/actions/runs/35609594613), 1/1 test passed across all ten themes with a gameplay portrait. Inspected all ten ground renders and `terrain-live-portrait.png`; markings and theme colors are visible again with weaker contrast than actors. Captured ground luminance standard deviation is now 0.010–0.034 (first muted pass: 0.003–0.009; original: 0.028–0.090). Artifacts: `C:/Users/user/.codex/artifacts/progression-themes/run35609594613`. The duplicate full push run was cancelled in favor of this focused shader/render check; no new full-suite pass is claimed for this small visual revision.
- The records below describe the first, subsequently rejected low-contrast pass. The workflow now accepts a manual `terrain` validation scope for the existing ten-theme ground render/seam check, including a gameplay portrait capture; push/PR/default runs still execute the full suite.
- Runtime commit `541cbcb`: [GitHub Actions 35604383505](https://github.com/kuzuni/20260920/actions/runs/35604383505) passed all 77 PlayMode tests (0 failures/skips), including opaque terrain coverage across all ten themes.
- Inspected all ten `expansion-ground-0..9.png` renders and `02-fixed-portrait.png` from the run artifact. Ground detail remains visible but subdued, with characters and attacks retaining their original contrast. Captured terrain luminance standard deviation fell from 0.028–0.090 to 0.003–0.009 across themes. This measures the ground-only screenshots, not gameplay balance.
- Local artifact directory: `C:/Users/user/.codex/artifacts/progression-themes/run35604383505`. No Unity execution or tests ran locally.

## 2026-09-22 expansion and latest UI

- `DoodleIdleGame.ExpansionSkills` owns tornado pursuit, golem pursuit/punches, paired claw strikes, rotating meteor and sequential stones. Golem art has two walk frames plus one punch frame. `DoodleExpansionArt` preserves common pivots and scale across each atlas.
- Field population is 200, replenished to 200 strictly below 100. Stage kill progress is independent; 100 credited ordinary kills in breakthrough mode removes remaining ordinary actors without kill rewards and spawns the boss. Farming mode resets the 100-kill counter without advancing.
- `DoodleAttackPower` converts the existing reference weights into attack percentages against the calibrated 128-attack baseline. All hits use current attack, common gear/relic/buff modifiers, their Basic/Skill/Companion relic multiplier and one critical roll. Detail estimates use the same attack-percent calculation and current cooldown; total DPS assumes the stated hit count, expected crits and no extra area targets.
- Relics: existing five IDs/images plus `relic_basic_attack`, `relic_skill_attack`, `relic_companion_attack`. Eight equally likely entries, no summon level. New bonuses are independent of common attack bonuses.
- Paid summon costs are unit × count, with no bulk discount: equipment 10, relic 100, skills/companions 200 diamonds. Skill refunds read `skillUnitCost` from UiCommerce.json.
- Summon result cards suppress quantity gauges. `DoodleSummonReveal` reveals cards in order using unscaled time. Animated confetti replaces static decoration; a persisted skip button completes/hides animation. Repeated draws replace the result tree with no window entrance animation.
- `PreviewItemProbability(item, level)` reads the same weight interpolation as live draws without changing summon state. Probability popup shows item rates with previous/next level buttons; no grade summary/unlock notice.
- Reward close callbacks invoke cosmetic Gold/Diamond flights to existing wallet labels. Close callbacks are removed before use; flights do not mint currency. UI motion uses unscaled time.

## 2026-09-22 latest overrides

- Companion equip capacity derives from highest discovered Armor rarity: 1 initially, then Epic/Legendary/Mythic/God unlock 2/3/4/5. Discovery survives consuming copies. Slot normalization and real combat share this limit.
- Skill catalog keeps IDs but reorders five entries per grade. Eggplant ID now names 오이 분노 with old Cucumber art and two rolling shots. Durian keeps three seven-hit projectiles; a single surviving target is reacquired after a short rebound. Purple arrows launch in eight sequential radial directions.
- SkillRedSlash contains two shared-bounds generated frames. Runtime RedWave uses its right-facing energy crescents with unchanged five-wave cadence.
- Probability UI shows grade totals and equal-within-grade notice. Paging replaces the old tree immediately with no entrance/exit motion. Popup entrance uses opaque scale only.
- DoodleSlidingSelection uses unscaled DOTween and persists tab progress across content rebuilds. Result footer order: level, experience gauge including current/required text, skip toggle, draw buttons, confirm. Relics omit experience. Item cards still omit copy counts.
- Free summon allowance is three five-item draws per category per UTC day. freeUsedCount persists alongside freeUsedDay; legacy used-day saves migrate to one consumed draw.

## Notification and dungeon revision (2026-09-22)
- DoodleNotificationBadge owns one non-raycast, layout-ignored top-right dot and refreshes its predicate every 0.2 unscaled seconds. Predicates query current claim/upgrade/synthesis/equip/entry state, not ownership alone.
- Dungeon save index 1 is retired; indices 0/2 stay stable. Legacy completed records remain valid for previously-earned skin unlocks. Challenge UI uses completed+1. Active retired dungeon saves return to the field.
- CombatDifficultyStage drives actual health/contact damage for both main and dungeon actors; ServicesTuning sets linear growth (health 2% and damage 1% per displayed stage beyond 1). Gold uses one shared GoldForMainKills function; goldStageGrowth defaults to 0, preserving existing field income.
- Dungeon relic entries retain category Relic for the common passive/upgrade pipeline and add dungeonRelic=true. Items(Relic) is the normal pool; Items(DungeonRelic) is exclusive; AllRelics drives the combined inventory and bulk upgrades. Save IDs are distinct.
- New dungeonRelicTickets live in ServiceState; normal relicTickets remain valid for older earned tickets. Only DungeonRelic ticket draws spend the new currency. The new commerce save key is included in complete reset.
