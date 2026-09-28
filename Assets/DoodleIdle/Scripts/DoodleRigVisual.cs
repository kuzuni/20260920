using DoodleIdle.CharacterRigs;
using UnityEngine;
using UnityEngine.Rendering;

namespace DoodleIdle
{
    // The invisible renderer retains the combat origin, tint and facing API. All visible
    // character parts come from the authored prefab and its SpriteSkin/Animator.
    public sealed class DoodleRigVisual : MonoBehaviour
    {
        public CharacterRig Rig { get; private set; }
        public DoodleCharacterCatalog.Entry Entry { get; private set; }
        public bool Paused;
        SpriteRenderer proxy;
        SortingGroup sorting;
        float scale;
        DoodleIdleGame owner;
        public void Configure(DoodleCharacterCatalog.Entry entry)
        {
            proxy = GetComponent<SpriteRenderer>();
            owner = GetComponentInParent<DoodleIdleGame>();
            if (Entry == entry && Rig) return;
            bool replace = !Rig || Rig.rigType != entry.appearance.rigType;
            if (replace) {
                if (Rig) { Rig.gameObject.SetActive(false); Destroy(Rig.gameObject); }
                Rig = Instantiate(entry.prefab, transform, false);
                sorting = Rig.GetComponent<SortingGroup>() ?? Rig.gameObject.AddComponent<SortingGroup>();
                Rig.animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }
            Rig.SetAppearance(entry.appearance);
            Entry = entry;
            scale = 1f / Mathf.Max(.01f, entry.extent);
            proxy.sprite = DoodleCharacterCatalog.Portrait(entry);
            proxy.enabled = false;
            Sync();
        }
        public void Moving(bool moving) { if (Rig) Rig.SetMoving(moving); }
        public void Attack() { if (Rig && !Paused) Rig.Attack(); }
        public void Hit() { if (Rig && !Paused) Rig.Hit(); }
        public void Sync()
        {
            if (!Rig || !proxy) return;
            if (owner) Paused = owner.paused;
            proxy.enabled = false;
            float side = proxy.flipX ? -1 : 1;
            Rig.transform.localScale = new Vector3(side * scale, scale, scale);
            Rig.transform.localPosition = new Vector3(-Entry.center.x * side * scale, -Entry.center.y * scale, 0);
            sorting.sortingOrder = proxy.sortingOrder;
            Rig.animator.speed = Paused ? 0 : 1;
            foreach (var renderer in Rig.partRenderers) renderer.color = proxy.color;
            if (Rig.weaponRenderer) Rig.weaponRenderer.color = proxy.color;
        }
        void LateUpdate() => Sync();
    }
}
