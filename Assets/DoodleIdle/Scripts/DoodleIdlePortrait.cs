using DoodleIdle.CharacterRigs;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    // Independent Idle preview of the authored player rig and current equipped art.
    [DefaultExecutionOrder(300)]
    [RequireComponent(typeof(Image))]
    public sealed class DoodleIdlePortrait : MonoBehaviour
    {
        public enum View { Stats, Profile, Pvp }
        public View view;
        public DoodlePortraitSettings settings;
        public DoodlePlayerLook playerLook;
        public CharacterRig PreviewRig { get; private set; }
        public Camera PreviewCamera { get; private set; }
        DoodleUi owner;
        DoodleCharacterCatalog.Entry entry;
        GameObject stage;
        RawImage image;
        RenderTexture texture;
        Sprite ownedWeapon;
        string lastWeapon;
        float nextFrame;
        int geometryReadyFrame;
        bool dirty=true;
        Color lastTint;
        float lastZoom,lastProfileSize;
        Vector2 lastOffset;
        static int nextStage;

        public void Configure(DoodleUi ui, View kind, DoodlePlayerLook look = null)
        {
            owner = ui; view = kind; playerLook = look; settings = DoodlePortraitSettings.Current;
            GetComponent<Image>().enabled = false;
            var rect = UiKit.Rect(transform, "Live player portrait"); UiKit.Stretch(rect);
            image = rect.gameObject.AddComponent<RawImage>(); image.raycastTarget = false;
            stage = new GameObject("UI player preview " + kind) { hideFlags = HideFlags.DontSave };
            stage.transform.position = new Vector3(10000 + (nextStage++ % 1000) * 100, 10000, 0);
            var cameraObject = new GameObject("Portrait camera", typeof(Camera)); cameraObject.transform.SetParent(stage.transform, false);
            PreviewCamera = cameraObject.GetComponent<Camera>(); PreviewCamera.enabled = false;
            PreviewCamera.orthographic = true; PreviewCamera.clearFlags = CameraClearFlags.SolidColor;
            PreviewCamera.backgroundColor = Color.clear; PreviewCamera.nearClipPlane = .1f; PreviewCamera.farClipPlane = 60;
            PreviewCamera.allowHDR = false; PreviewCamera.allowMSAA = false; PreviewCamera.cullingMask = 1 << 31;
            texture = new RenderTexture(kind == View.Profile ? 192 : 512, kind == View.Profile ? 192 : 512, 24, RenderTextureFormat.ARGB32);
            texture.name = "Live portrait " + kind; texture.Create(); image.texture = texture; PreviewCamera.targetTexture = texture;
            RefreshLook();
        }
        void RefreshLook()
        {
            var look = playerLook;
            string appearance = look != null ? look.appearanceIcon : owner.EquippedAppearanceIcon;
            var next = DoodleCharacterCatalog.Current.Player(DoodleCharacterCatalog.Costume(appearance));
            if (!PreviewRig)
            {
                PreviewRig = Instantiate(next.prefab, stage.transform, false);
                foreach (var child in PreviewRig.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                foreach (var collider in PreviewRig.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
                if (PreviewRig.footDust) { PreviewRig.footDust.enabled = false; PreviewRig.footDust.particles.gameObject.SetActive(false); }
                PreviewRig.animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                PreviewRig.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                PreviewRig.SetMoving(false); PreviewRig.face.horizontalGazeOnly = true;
                if(view==View.Profile){PreviewRig.animator.speed=0;PreviewRig.face.blinking=false;}
            }
            if (entry != next) {
                entry = next; PreviewRig.SetAppearance(next.appearance); lastWeapon = null;dirty=true;
                // These cameras render manually, so visibility culling cannot decide
                // when their mesh is ready. Let SpriteSkin update before caching a photo.
                PreviewRig.UpdateOffscreenMeshes(true);
                PreviewRig.animator.Update(0);
                geometryReadyFrame=Time.frameCount+2;
            }
            Color tint = look != null ? look.appearanceTint : owner.EquippedAppearanceTint;
            if(dirty||lastTint!=tint){foreach(var renderer in PreviewRig.partRenderers)renderer.color=tint;PreviewRig.face.SetTint(tint);lastTint=tint;dirty=true;}
            var source = playerLook == null ? owner.PortraitSourceRig : null;
            if (source && source.weaponRenderer)
            {
                var weapon = PreviewRig.weaponRenderer; weapon.sprite = source.weaponRenderer.sprite;
                weapon.enabled = source.weaponRenderer.enabled; weapon.color = owner.EquippedWeaponTint;
                weapon.transform.localScale = source.weaponRenderer.transform.localScale;
            }
            else ApplyWeapon(look != null ? look.weaponIcon : owner.EquippedWeaponIcon, look != null ? look.weaponTint : owner.EquippedWeaponTint);
        }
        void ApplyWeapon(string key, Color tint)
        {
            if (lastWeapon != key)
            {
                if (ownedWeapon) Destroy(ownedWeapon); ownedWeapon = null;
                Sprite sprite = entry.appearance.weapon;
                if (!string.IsNullOrEmpty(key) && key != "Club")
                {
                    if (key.StartsWith("SkinWeapon_", System.StringComparison.Ordinal)) sprite = DoodleCollectionArt.Get(key);
                    else
                    {
                        var art = UiKit.Art(key);
                        if (art) { ownedWeapon = Sprite.Create(art.texture, art.rect, new Vector2(.15f,.12f), Mathf.Max(art.rect.width,art.rect.height)); sprite = ownedWeapon; }
                    }
                }
                var weapon = PreviewRig.weaponRenderer; weapon.sprite = sprite; weapon.enabled = sprite;
                float reference = entry.appearance.weapon ? Mathf.Max(entry.appearance.weapon.bounds.size.x,entry.appearance.weapon.bounds.size.y) : 1;
                float length = sprite ? Mathf.Max(sprite.bounds.size.x,sprite.bounds.size.y) : 1;
                weapon.transform.localScale = Vector3.one * (entry.appearance.weaponScale * reference / Mathf.Max(.01f,length));
                lastWeapon = key;
            }
            PreviewRig.weaponRenderer.color = tint;
        }
        void LateUpdate()
        {
            if (!stage || !settings) return;
            RefreshLook();
            if(view==View.Profile){
                if(lastZoom!=settings.profileZoom||lastOffset!=settings.profileCameraOffset||lastProfileSize!=settings.profileSize)dirty=true;
                if(!dirty)return;
                lastZoom=settings.profileZoom;lastOffset=settings.profileCameraOffset;lastProfileSize=settings.profileSize;
            }
            if (Time.unscaledTime < nextFrame) return;
            RenderNow(); nextFrame = Time.unscaledTime + 1f / 30;
        }
        public void RenderNow()
        {
            if (!PreviewRig || !settings || Time.frameCount<geometryReadyFrame) return;
            Vector3 center = stage.transform.position + entry.center;
            float radius = entry.extent * .55f;
            float zoom = settings.statsZoom; Vector2 offset = settings.statsCameraOffset;
            if (view == View.Profile)
            {
                var head = PreviewRig.face.headRenderer.bounds;
                center = (PreviewRig.face.leftEye.white.transform.position+PreviewRig.face.rightEye.white.transform.position)*.5f+Vector3.up*(head.size.y*.1f);
                radius = Mathf.Max(head.size.x,head.size.y) * .55f;
                zoom = settings.profileZoom; offset = settings.profileCameraOffset;
                UiKit.Height(transform,settings.profileSize);
                var layout = GetComponent<LayoutElement>(); layout.minWidth = layout.preferredWidth = settings.profileSize;
            }
            else if (view == View.Pvp)
            {
                zoom = settings.pvpZoom; offset = settings.pvpCameraOffset;
                radius /= Mathf.Max(.1f,zoom); zoom = 1;
                center.y = PreviewRig.groundContact.position.y + radius;
            }
            PreviewRig.face.SyncSorting(); PreviewRig.face.RefreshHighlights();
            PreviewCamera.orthographicSize = radius / Mathf.Max(.1f,zoom);
            PreviewCamera.transform.position = center + new Vector3(offset.x,offset.y,-30);
            PreviewCamera.Render();dirty=false;
        }
        void OnEnable() { if (stage) stage.SetActive(true); }
        void OnDisable() { if (stage) stage.SetActive(false); }
        void OnDestroy()
        {
            if (PreviewCamera) PreviewCamera.targetTexture = null;
            if (texture) { texture.Release(); Destroy(texture); }
            if (ownedWeapon) Destroy(ownedWeapon);
            if (stage) Destroy(stage);
        }
    }
}
