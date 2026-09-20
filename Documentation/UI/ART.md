# Runtime artwork

The authoritative final UI references are the 25 PNG files in `FinalDesign`. They are design documentation only and never loaded as runtime backgrounds.

Existing generated player, enemy, weapon and skill sprites remain in use. `Assets/DoodleIdle/Resources/DoodleIdle/UI/Icons.png` is a new transparent 4x4 atlas generated with built-in ImageGen on 2026-09-20, using the existing `Characters.png` as the style reference: rounded cute shapes, black imperfect ink lines and pastel marker fills. Its 16 separate cells are alpha-trimmed at runtime and assigned to native Image components with aspect preservation.

Atlas order: diamond, leather armor, metal armor, amulet; stats, crossed clubs, cave, shop; calendar, roulette, potion, quest; chat, settings, red X, key. The exact same red X sprite is used for all dismiss buttons and the selected navigation button.

Generation prompt requested one transparent atlas with exactly 4 columns/4 rows, generous empty margins, no labels, frames, backgrounds or shadows. The source generated output was `exec-dbeb9996-84e3-4294-9ac0-362836e4b087.png`; the final copy is committed in Resources.

Frames, gauges, roulette sectors and reward rays are native code-defined uGUI geometry, so they scale without stretching an entire reference image. The first CI capture showed that the existing Nanum hand font was too thin/small for UI. UI now uses the rounded Korean Jua font (`UI/DisplayFont.ttf`) from https://github.com/google/fonts/tree/main/ofl/jua, with its SIL Open Font License committed alongside. Existing combat damage text retains its original font.

`UI/GearIcons.png` is a second transparent16-cell atlas generated from the same player reference, source `exec-77e1028c-507f-4636-9cdc-9c175d0b1f8a.png`. It provides actual red health heart, blue shield, wing, clover, seven distinct club designs, four primitive armor variants and sun amulet. Prompt explicitly requested wobbly thin ink and pale flat marker colors matching player and final UI references, without glossy3D or background. Inventory data maps individual variants to these sprites.

## Diamond merchandise revision

`Assets/DoodleIdle/Resources/DoodleIdle/UI/CurrencyIcons.png` was generated with built-in ImageGen on 2026-09-20, using repository reference `FinalDesign/09 상점 · 재화.png` and the existing `Characters.png`. Source output: `exec-c10fe46b-1dfc-4565-92be-97cfcd0a750f.png`. The 1536×1024 transparent PNG uses a 3×2 grid: single diamond, pile, bag, brown chest, purple chest, empty. Each product is alpha-trimmed at runtime and preserves aspect. Result-screen ribbon and confetti are separate native uGUI geometry.

Exact generation prompt:

> Create ONE production transparent PNG sprite atlas for a Unity hand-drawn idle game: a 3-column by 2-row regular grid (landscape, each cell square), with exactly FIVE isolated currency-product illustrations and the bottom-right cell EMPTY transparent. Image 1 is the reference for the five diamond product silhouettes, colors and amounts progression, NOT an edit target or background to copy. Image 2 is the style reference: simple cute slightly wobbly bold black ink outlines, gentle flat fills, same drawing style as the cat player. Atlas contents in reading order: top-left ONE cyan/blue faceted diamond with two small golden sparkles; top-center a small PILE of 5 blue diamonds with small sparkles; top-right a brown drawstring BAG overflowing with blue diamonds, two loose gems; bottom-left an OPEN small warm brown treasure CHEST with gold trim overflowing with blue diamonds; bottom-center a larger purple-and-gold deluxe treasure CHEST overflowing with blue diamonds and a few loose gems. Bottom-right remains entirely empty. Center each drawing in its exact cell and keep generous transparent margins (at least 12% per side), no object overlaps a cell boundary. Draw each product large and recognizable. Match the simple shapes of image1's left-column merchandise, not a photorealistic or 3D rendering. True transparent background with alpha; no checkerboard, no floor, no background, no UI panels, no labels, NO TEXT, no numbers, no borders around cells, no watermark. Atlas only.
