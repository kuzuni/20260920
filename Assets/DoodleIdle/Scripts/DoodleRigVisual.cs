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
        public bool HitStopped { get; set; }
        public SpriteRenderer GroundShadow;
        SpriteRenderer proxy;
        SortingGroup sorting;
        int prefabSortingOrder;
        float scale;
        DoodleIdleGame owner;
        bool tintValid;
        Color previousTint;
        bool parked;
        SpriteRenderer[] parkedRenderers;
        bool[] parkedRendererStates;
        SpriteMask[] parkedMasks;
        bool[] parkedMaskStates;
        public void ParkRig()
        {
            if(!Rig||parked)return;
            Rig.CancelAttack();
            HitStopped=false;
            if(Rig.hitBlood)Rig.hitBlood.Clear();
            if(Rig.attackRange)Rig.attackRange.enabled=false;
            if(Rig.face)Rig.face.SuspendForPool();
            if(Rig.footDust)Rig.footDust.enabled=false;
            Rig.animator.speed=0;
            Rig.animator.cullingMode=AnimatorCullingMode.CullCompletely;
            if(parkedRenderers==null){parkedRenderers=Rig.GetComponentsInChildren<SpriteRenderer>(true);parkedRendererStates=new bool[parkedRenderers.Length];parkedMasks=Rig.GetComponentsInChildren<SpriteMask>(true);parkedMaskStates=new bool[parkedMasks.Length];}
            for(int i=0;i<parkedRenderers.Length;i++){
                bool visible=parkedRenderers[i].enabled;parkedRendererStates[i]=visible;
                if(visible)parkedRenderers[i].enabled=false;
            }
            for(int i=0;i<parkedMasks.Length;i++){
                bool active=parkedMasks[i].enabled;parkedMaskStates[i]=active;
                if(active)parkedMasks[i].enabled=false;
            }
            // Disabling the combat root or reparenting this rig invalidates native
            // SpriteSkin/Animator bindings. Park components in place instead.
            parked=true;
        }
        public void RestoreRig()
        {
            if(!parked||!Rig)return;
            for(int i=0;i<parkedRenderers.Length;i++)if(parkedRendererStates[i])parkedRenderers[i].enabled=true;
            // Shader-clipped eyes deliberately keep their native SpriteMask disabled.
            // Don't register stencil ranges again during every pooled respawn.
            for(int i=0;i<parkedMasks.Length;i++)if(parkedMaskStates[i])parkedMasks[i].enabled=true;
            Rig.animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            Rig.animator.speed=Paused?0:1;Rig.ResetPooledAnimation();
            if(Rig.attackRange)Rig.attackRange.enabled=Entry.group=="Enemies";
            if(Rig.face)Rig.face.enabled=true;
            if(Rig.footDust)Rig.footDust.enabled=true; // OnEnable resets the trail at the new position.
            parked=false;tintValid=false;Sync();
        }
        public void Configure(DoodleCharacterCatalog.Entry entry)
        {
            proxy = GetComponent<SpriteRenderer>();
            owner = GetComponentInParent<DoodleIdleGame>();
            if (Entry == entry && Rig) return;
            bool replace = !Rig || Rig.rigType != entry.appearance.rigType;
            if (replace) {
                if (Rig) { Rig.gameObject.SetActive(false); Destroy(Rig.gameObject); }
                Rig = Instantiate(entry.prefab, transform, false);
                if (Rig.face) Rig.face.View = owner ? owner.FaceView : null;
                sorting = Rig.GetComponent<SortingGroup>() ?? Rig.gameObject.AddComponent<SortingGroup>();
                prefabSortingOrder = sorting.sortingOrder;
                // Combat impact events must also run when a character is off camera.
                Rig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            Rig.SetAppearance(entry.appearance);
            tintValid=false;
            Entry = entry;
            // Enemies and companions share rig prefabs; only enemies use this sensor.
            if (Rig.attackRange) Rig.attackRange.enabled = entry.group == "Enemies";
            scale = (entry.group == "Player" || entry.group == "Enemies" ? 2f : 1f) / Mathf.Max(.01f, entry.extent);
            proxy.sprite = DoodleCharacterCatalog.Portrait(entry);
            if (proxy.enabled) proxy.enabled = false;
            Sync();
            if (Rig.footDust) Rig.footDust.Bind(transform);
        }
        public void Moving(bool moving) { if (Rig) Rig.SetMoving(moving); }
        public void Attack() { if (Rig && !Paused && !HitStopped) Rig.Attack(); }
        public bool TryAttack(System.Action impact) => Rig && !Paused && !HitStopped && Rig.TryAttack(impact);
        public void Hit() { if (Rig && !Paused) Rig.Hit(); }
        public void ReactToDamage() { if (Rig && !Paused) Rig.ReactToDamage(); }
        public void LookAt(Transform target) { if (Rig && Rig.face) Rig.face.target = target; }
        public void ShowHitFace() { if (Rig && Rig.face && !Paused) Rig.face.ShowHit(); }
        public void Sync()
        {
            if (parked || !Rig || !proxy) return;
            if (owner) Paused = owner.paused;
            if (proxy.enabled) proxy.enabled = false;
            float side = proxy.flipX ? -1 : 1;
            var nextScale=new Vector3(side*scale,scale,scale);
            if(Rig.transform.localScale!=nextScale)Rig.transform.localScale=nextScale;
            // Portrait bounds include asymmetric hats/weapons. Their horizontal center
            // is not the authored rig pivot: mirroring it moves the whole character.
            var nextPosition=new Vector3(0,-Entry.center.y*scale,0);
            if(Rig.transform.localPosition!=nextPosition)Rig.transform.localPosition=nextPosition;
            // World depth moves the whole character; authored part ordering stays intact.
            int order=prefabSortingOrder+proxy.sortingOrder;
            if(sorting.sortingOrder!=order)sorting.sortingOrder=order;
            float speed=Paused||HitStopped?0:1;if(Rig.animator.speed!=speed)Rig.animator.speed=speed;
            if (Rig.hitBlood) Rig.hitBlood.Paused = Paused;
            if (Rig.footDust) Rig.footDust.Paused = Paused;
            if (Rig.face) Rig.face.Paused = Paused;
            var tint=proxy.color;
            if(!tintValid || tint!=previousTint) {
                tintValid=true;previousTint=tint;
                if(Rig.face)Rig.face.SetTint(tint);
                foreach(var renderer in Rig.partRenderers)renderer.color=tint;
                if(Rig.weaponRenderer)Rig.weaponRenderer.color=tint;
            }
            if (GroundShadow && Rig.groundContact)
            {
                // GroundContact is outside the animated skeleton, so gait does not move it.
                var shadowTransform = GroundShadow.transform;
                var position = Rig.groundContact.position;
                if (!shadowTransform.position.Equals(position)) shadowTransform.position = position;
            }
        }
        void LateUpdate() => Sync();
    }
}
