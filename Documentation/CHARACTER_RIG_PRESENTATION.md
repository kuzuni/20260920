# Character presentation

- All six rig prefabs have an enabled root `SortingGroup`. Appearance swaps replace
  sprites and skin bindings while retaining each prefab renderer's sorting layer/order.
  Runtime depth offsets the group; it does not rewrite part order.
- Animation clips and Animator controllers are authored by the user. Appearance
  changes and presentation tools must not rewrite their curves, states, masks or
  bone hierarchy. The automatic upper-body animation rewrite from commit `1e2e120`
  was reverted to the pre-change animation assets in `83dffd9`.
- `GroundContact` is a rest-pose foot reference outside the animated skeleton.
  The runtime shadow follows this reference, including facing and visual scale.
- Player and enemy rig visuals use twice their previous scale. Companion scale and
  combat collider/range values are unchanged. Procedural actor bob/tilt is removed;
  Animator clips remain the source of character animation.
- The basic Weapon skin (`Club`) displays the equipped Club equipment item.
  An equipped cosmetic weapon skin takes priority. Returning to the basic skin
  immediately restores the equipment appearance, with its grip and displayed size
  fitted to the prefab weapon.

Use **Doodle Idle > Character Rigs > Apply Sorting And Ground Contacts** to enable
sorting groups and recalculate ground references. It does not modify animations or
bone hierarchies. Do not use Rebuild Prefabs Only on manually authored prefabs.

Validation: `DoodleIdle.Tests.DoodleRigPresentationTests` covers all 142 appearances,
equipment/cosmetic switching, shadow facing, and absence of script-driven bobbing.
Tests do not regenerate or migrate the authored animation assets.
