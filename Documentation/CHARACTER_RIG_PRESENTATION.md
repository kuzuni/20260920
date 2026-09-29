# Character presentation

- All six rig prefabs have an enabled root `SortingGroup`. Appearance swaps replace
  sprites and skin bindings while retaining each prefab renderer's sorting layer/order.
  Runtime depth offsets the group; it does not rewrite part order.
- `Base Layer` owns locomotion, hit and death. The masked `Upper Body` layer owns
  upper-body locomotion, attack, hit and death. Set `Moving` independently of the
  `Attack` trigger: attacks leave the legs in Idle or Move. Leg roots are siblings
  of the torso, preserving their authored world pose without inheriting torso attacks.
- `GroundContact` is a rest-pose foot reference outside the animated skeleton.
  The runtime shadow follows this reference, including facing and visual scale.
- Player and enemy rig visuals use twice their previous scale. Companion scale and
  combat collider/range values are unchanged. Procedural actor bob/tilt is removed;
  Animator clips remain the source of character animation.
- The basic Weapon skin (`Club`) displays the equipped Club equipment item.
  An equipped cosmetic weapon skin takes priority. Returning to the basic skin
  immediately restores the equipment appearance, with its grip and displayed size
  fitted to the prefab weapon.

After changing a prefab skeleton, use **Doodle Idle > Character Rigs > Apply Sorting
Upper Body Attack And Ground Contacts**. This preserves rest poses and sorting,
rebinds animation paths/angles, rebuilds the upper-body masks and recalculates ground
contacts. Do not use Rebuild Prefabs Only to preserve manually edited poses.

Validation: `DoodleIdle.Tests.DoodleRigPresentationTests` covers all 142 appearances,
both attacking locomotion states on all six prefabs (including world-space legs),
equipment/cosmetic switching, shadow facing, and absence of script-driven bobbing.
The same suite also passes after reapplying the editor migration to existing output.
