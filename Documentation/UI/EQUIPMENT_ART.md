# Distinct equipment icons — 2026-09-24

Armor and Club each have 31 dedicated illustrations: five per rarity from Normal through Mythic, then one God. All 62 catalog entries now reference their own `EquipmentArmor_N` or `EquipmentClub_N` key. Item IDs, rarity, ownership, enhancement, synthesis and balance values are unchanged.

Assets:
- `Assets/DoodleIdle/Resources/DoodleIdle/UI/EquipmentArmor.png`
- `Assets/DoodleIdle/Resources/DoodleIdle/UI/EquipmentClub.png`
- Matching `EquipmentArmorLayout.json` / `EquipmentClubLayout.json` contain full-object crop bounds, in bottom-left texture coordinates. The generated rows are not perfectly uniform; using nominal grid cells would clip shoulder plates or include neighboring tips.

`DoodleCollectionArt` resolves these keys for inventory, selected equipment, item details and summon results. The original GearIcons/Characters sheets remain for unrelated navigation, skins and gameplay uses. Catalog icon substitutions were staged independently of the user's unsaved balance edits in Collections.json.

## Built-in image_gen prompt set

Mode: built-in image generation (no CLI/API fallback). Style references: existing `UI/GearIcons.png` and `Characters.png`. Final source files were copied without pixel edits. Alpha connected-component analysis was used only to determine rectangular sprite bounds.

Shared generation specification: production-ready transparent PNG sprite atlas for the existing doodle idle game; thick imperfect black ink outlines, simple cute doodle shapes, muted flat pastel fills and minimal broad shading. Exactly five columns and seven rows, first six rows containing five unique icons each, final row first cell containing God with the other four cells empty. Every icon complete and centered with transparent gutters. No labels, numbers, grid lines, frames, background, floor shadows, characters, faces or loose effect particles. Different silhouettes and constructions rather than recolors. Genuine alpha transparency.

Armor specification: front-facing wearable torso armor only, no person, pants, helmet, hands or feet. Large readable shapes, two to four colors per icon, not realistic or glossy fantasy rendering.

Club specification: complete short-handled clubs, maces and blunt bats, tilted from lower-left handle to upper-right head. Distinct head silhouettes/materials. No swords or axes.

Exact concepts in row-major order:

| Rarity | Armor 1–5 | Club 1–5 |
|---|---|---|
| Normal | Torn stitched linen vest; crossed leather harness; rope-tied wooden slats; woven straw poncho; quilted green tunic | Wooden bat; knotted branch; cream cloth-wrapped bat; bamboo baton; rolling pin |
| Advanced | Fur-collar vest; red brigandine with three metal strips; blue chainmail; asymmetric one-shoulder plate; overlapping leaves | Leather-tied bone; lashed oval stone; iron-banded wood; four-spike iron mace; closed leaf bud |
| Rare | White ribs; blue shell layers; bronze beetle carapace; purple scales/high collar; ice crystal shoulders | Ice crystal; three-tip coral; red spotted mushroom; ridged beehive/honey drop; flowering cactus |
| Epic | Bat-wing shoulders/red gem; desert scarf armor; samurai lamellar; curled wave shells; ivory/gold central ridge | Flame-shaped charred club; yellow lightning bolt; silver crescent; ivory dragon horn; cracked meteor |
| Legendary | Lava-cracked rock; silver crescent shoulder; golden mane collar; blue starry split coat; horned dragon scales | Sunflower disk; purple crystal star; conch/pearl; rectangular runic pillar; red phoenix feather fan |
| Mythic | White feather shoulders; violet orbital ring; obsidian battlements; rose-gold phoenix feathers; aqua trident collar | Ringed violet planet; three-peaked mountain crystal; turquoise spiral shell; crystal crown; dark vortex with metal prongs |
| God | Cream/gold radiant fan shoulders and cyan diamond gem | Cream/gold radiant sun disk with cyan diamond gem |

The first Club output contained unwanted gray background haze. A built-in edit removed only the haze/background/shadows while preserving all 31 designs, positions, order and colors. Final edit instruction: genuine zero-alpha transparency outside each icon's black outline; preserve internal fills; no redesign, movement, checkerboard, labels or added objects.

Final generated sources:
- Armor: `exec-6645f9fe-66a4-4fae-88cf-efd49e9d70f9.png`
- Club: `exec-4adc7d35-d29b-4b64-bb3a-400fd75ba488.png`

Each PNG is 1060×1484 RGBA. The native doodle artwork is preserved in the repository assets.
