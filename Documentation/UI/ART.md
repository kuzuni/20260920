# Runtime artwork

The authoritative final UI references are the 25 PNG files in `FinalDesign`. They are design documentation only and never loaded as runtime backgrounds.

Existing generated player, enemy, weapon and skill sprites remain in use. `Assets/DoodleIdle/Resources/DoodleIdle/UI/Icons.png` is a new transparent 4x4 atlas generated with built-in ImageGen on 2026-09-20, using the existing `Characters.png` as the style reference: rounded cute shapes, black imperfect ink lines and pastel marker fills. Its 16 separate cells are alpha-trimmed at runtime and assigned to native Image components with aspect preservation.

Atlas order: diamond, leather armor, metal armor, amulet; stats, crossed clubs, cave, shop; calendar, roulette, potion, quest; chat, settings, red X, key. The exact same red X sprite is used for all dismiss buttons and the selected navigation button.

Generation prompt requested one transparent atlas with exactly 4 columns/4 rows, generous empty margins, no labels, frames, backgrounds or shadows. The source generated output was `exec-dbeb9996-84e3-4294-9ac0-362836e4b087.png`; the final copy is committed in Resources.

Frames, gauges, roulette sectors and reward rays are native code-defined uGUI geometry, so they scale without stretching an entire reference image. Korean text uses the project's existing licensed InterfaceFont.ttf.
