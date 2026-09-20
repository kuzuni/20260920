# Runtime artwork

The authoritative final UI references are the 25 PNG files in `FinalDesign`. They are design documentation only and never loaded as runtime backgrounds.

Existing generated player, enemy, weapon and skill sprites remain in use. `Assets/DoodleIdle/Resources/DoodleIdle/UI/Icons.png` is a new transparent 4x4 atlas generated with built-in ImageGen on 2026-09-20, using the existing `Characters.png` as the style reference: rounded cute shapes, black imperfect ink lines and pastel marker fills. Its 16 separate cells are alpha-trimmed at runtime and assigned to native Image components with aspect preservation.

Atlas order: diamond, leather armor, metal armor, amulet; stats, crossed clubs, cave, shop; calendar, roulette, potion, quest; chat, settings, red X, key. The exact same red X sprite is used for all dismiss buttons and the selected navigation button.

Generation prompt requested one transparent atlas with exactly 4 columns/4 rows, generous empty margins, no labels, frames, backgrounds or shadows. The source generated output was `exec-dbeb9996-84e3-4294-9ac0-362836e4b087.png`; the final copy is committed in Resources.

Frames, gauges, roulette sectors and reward rays are native code-defined uGUI geometry, so they scale without stretching an entire reference image. The first CI capture showed that the existing Nanum hand font was too thin/small for UI. UI now uses the rounded Korean Jua font (`UI/DisplayFont.ttf`) from https://github.com/google/fonts/tree/main/ofl/jua, with its SIL Open Font License committed alongside. Existing combat damage text retains its original font.

`UI/GearIcons.png` is a second transparent16-cell atlas generated from the same player reference, source `exec-77e1028c-507f-4636-9cdc-9c175d0b1f8a.png`. It provides actual red health heart, blue shield, wing, clover, seven distinct club designs, four primitive armor variants and sun amulet. Prompt explicitly requested wobbly thin ink and pale flat marker colors matching player and final UI references, without glossy3D or background. Inventory data maps individual variants to these sprites.
