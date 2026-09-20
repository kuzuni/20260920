# Final UI implementation contract

Only `Documentation/UI/FinalDesign/01..25 *.png` is the visual reference. Text rules in REQUEST.md override mockup examples. Do not read Desktop references again. Do not run Unity, a game, or local tests. GitHub hosted Actions is the only execution/validation environment.

## Ownership
- Coordinator: DoodleUi.cs, DoodleUiKit.cs, DoodleIdleGame integration, CI/tests, shared docs, assets, all git operations.
- Collections agent: DoodleUi.Collections.cs, DoodleUi.Catalog.cs, collection JSON tuning only.
- Commerce agent: DoodleUi.Commerce.cs, commerce JSON tuning only.
- Services agent: DoodleUi.Services.cs, services JSON tuning only.
- Never edit another owner's file or scene/prefab. Report API requirements to coordinator. Do not commit or push independently.

## Shared code contract
Namespace DoodleIdle. `public sealed partial class DoodleUi : MonoBehaviour`. Runtime uGUI, programmatic construction. Native panels, text, images, Buttons, ScrollRects (never screenshot backgrounds). Existing game art reused; common hand ink borders, cream white panels, pastel green/blue/yellow action buttons, red close X.

Shared fields/properties: `DoodleIdleGame game; Font font; long Gold = 125480; int Diamonds = 1250; string PlayerName = "먼지고양이"; long Power` computed from collection bonuses; `string ActivePage {get;}`. Shared `RefreshPage()` rebuilds current page; `Toast(string)`; `Save()` persists local state; `ShowPage(string)` routes `Stats,Equipment,Skills,Companions,Relics,Dungeons,Pvp,Shop,Attendance,Roulette,Buffs,Quests,Chat,Settings`. `ShowDetail(string title, Action<RectTransform> build)` opens stacked modal; `CloseDetail()` closes only top overlay. `ShowRewards(string title, List<UiReward> rewards)` opens panel-free dim reward overlay; UiReward fields `string name, icon; int amount, rarity`.

Screen builder signatures in partial files: `void BuildStats(RectTransform body)`, `BuildEquipment`, `BuildSkills`, `BuildCompanions`, `BuildRelics`, `BuildShop`, `BuildDungeons`, `BuildPvp`, `BuildAttendance`, `BuildRoulette`, `BuildBuffs`, `BuildQuests`, `BuildChat`, `BuildSettings`. Body is a vertical scroll CONTENT with auto preferred height, fixed popup width ~600 logical units. Fullscreen chat/results use width-limited readable content inside fullscreen canvas. Each builder must use layout elements, no screen-size assumptions. Main popup title/close and bottom navigation are coordinator-owned. Tabs can be placed in body; equipment order remains selected spec, total ownership, collection, actions, tabs.

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
Collections agent implements `List<UiItem> Items(string category)` (Armor,Club,Skill,Companion,Relic), `UiItem GrantItem(string category, System.Random rng)` using exact distribution `60/25/10/4/1` percent grades 0..4; uniform within grade. `void AddItem(UiItem item,int count)`; `float OwnedBonus`; `long Power`; `List<UiItem> EquippedSkills`; `InitCollections()`.
UiItem public fields: `string id,name,icon,category; int rarity,count,level; bool equipped;`.
Commerce uses these APIs and reports requirements. Main calls InitCollections in Awake. Arrays/catalog are tunable local data, not server truth.

Services owns local daily/reward/account/chat/PVP adapters in its file and `InitServices()`, `TickServices()`. Daily resets use UTC date; data stored under DoodleUi-specific PlayerPrefs keys; never alter existing game saves. Shared wallet changes call Save() (coordinator persists wallet) and Toast/RefreshPage as appropriate. No external payment/auth/chat claimed. Services may add `SaveServices()` and collection `SaveCollections()`, called by coordinator Save().

Main integrates real combat kills into wallet/mission and reads real cooldowns. UI upgrading/equipping must retain existing combat system; explicit new bonuses can use small game adapter API supplied by coordinator. Record unsupported external features.
