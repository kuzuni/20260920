using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DoodleIdle
{
    public sealed partial class DoodleIdleGame
    {
        public float LoadingProgress { get; private set; }
        Canvas loadingCanvas;
        Text loadingLabel;
        Image loadingBar;
        DoodleLoadingMotion loadingMotion;

        void BuildLoadingScreen()
        {
            var go = new GameObject("Preparing game", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            loadingCanvas = go.GetComponent<Canvas>();
            loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            loadingCanvas.sortingOrder = 32760;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1520); scaler.matchWidthOrHeight = 1;
            var cover = new GameObject("Loading cover", typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(go.transform, false);
            var rect = (RectTransform)cover.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.sizeDelta = Vector2.zero;
            cover.GetComponent<Image>().color = new Color(.91f, .83f, .68f);
            var illustration=UiKit.Rect(cover.transform,"Loading battle illustration");
            var art=illustration.gameObject.AddComponent<RawImage>();
            art.texture=Resources.Load<Texture2D>("DoodleIdle/UI/LoadingBattle");art.raycastTarget=false;
            uiFont = Resources.Load<Font>("DoodleIdle/UI/DisplayFont");
            var title=Label(cover.transform,"탕탕탕",82,new Vector2(-310,-170),new Vector2(620,110),TextAnchor.MiddleCenter,new Vector2(.5f,1));
            title.color=new Color(.2f,.13f,.08f);
            Label(cover.transform,"방치형 RPG",27,new Vector2(-260,-218),new Vector2(520,48),TextAnchor.MiddleCenter,new Vector2(.5f,1));
            loadingLabel = Label(cover.transform, "게임 준비 중", 26, new Vector2(-290,120), new Vector2(580,54), TextAnchor.MiddleCenter, new Vector2(.5f,0));
            var track = new GameObject("Loading progress", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(cover.transform, false);
            var trackRect = (RectTransform)track.transform;
            trackRect.anchorMin=trackRect.anchorMax=new Vector2(.5f,0);
            trackRect.sizeDelta = new Vector2(520, 62); trackRect.anchoredPosition = new Vector2(0, 93);
            track.GetComponent<Image>().sprite=UiKit.Art("HealthBarFrame");
            track.GetComponent<Image>().raycastTarget=false;
            var inset=UiKit.Rect(track.transform,"Gauge inner area");UiKit.Stretch(inset,9,10,9,10);
            var bar = new GameObject("Fill", typeof(RectTransform), typeof(Image),typeof(Mask)); bar.transform.SetParent(inset, false);
            loadingBar = bar.GetComponent<Image>();loadingBar.sprite=UiKit.Art("HealthBarFill");loadingBar.raycastTarget=false;
            var shine=UiKit.Rect(bar.transform,"Gauge moving highlight");shine.sizeDelta=new Vector2(38,100);
            shine.localRotation=Quaternion.Euler(0,0,-18);
            var highlight=shine.gameObject.AddComponent<Image>();highlight.color=new Color(1,1,1,.24f);highlight.raycastTarget=false;
            loadingMotion=cover.AddComponent<DoodleLoadingMotion>();loadingMotion.art=illustration;
            loadingMotion.shine=shine;loadingMotion.fill=loadingBar;
            SetLoadingProgress(0, "게임 준비 중");
        }

        void SetLoadingProgress(float progress, string message)
        {
            LoadingProgress = Mathf.Clamp01(progress);
            loadingLabel.text = message + "  " + Mathf.RoundToInt(LoadingProgress * 100) + "%";
            var rect = loadingBar.rectTransform;
            rect.anchorMin = Vector2.zero;
            loadingMotion.progress=LoadingProgress;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        // Read the actual art factories, including animation frames and companion impacts.
        // Never cast an ability to warm a pool: that would change combat RNG, damage and progression.
        IEnumerable<Sprite> CombatArt()
        {
            foreach (var sprite in sprites) yield return sprite;
            foreach (var sprite in skillArt) yield return sprite;
            yield return droneFrameB; yield return disc; yield return slash;
            foreach (var sprite in summonArt.Values) yield return sprite;
            foreach (var key in DoodleVariantArt.Skills) yield return DoodleVariantArt.Get(key);
            string[] expansion = { "SkillTornado", "SkillDoubleClaw", "SkillLightning", "SkillRedSlash", "SkillGolem", "SkillDumbbell", "SkillMeteorRock", "SkillMeteorCrater", "SkillPalmCrater", "SkillSpikyCactus", "SkillWhiteMissile", "SkillCherryShuriken", "SkillSkyPalm", "SkillFoamRoller" };
            foreach (var key in expansion) for (int frame = 0; frame < (key == "SkillGolem" ? 4 : 2); frame++) yield return DoodleExpansionArt.Get(key, frame);
            foreach (int cell in new[] { 0, 2, 3, 4, 5, 6, 7, 8, 11, 32, 33, 34 }) yield return DoodleAscensionArt.Cell(cell);
            for (int frame = 0; frame < 4; frame++) yield return DoodleAscensionArt.FireGolem(frame);
            yield return DoodleAscensionArt.FireTornado(1);
            foreach (var item in Ui.Items("Companion")) {
                yield return WorldIcon(item.projectile);
                yield return DoodleCollectionArt.CompanionImpact(DoodleCollectionArt.CompanionIndex(item.icon));
            }
            for (int theme = 0; theme < ThemeNames.Length; theme++) { LoadThemeFrames(theme); yield return ThemeGround(theme); }
        }

        IEnumerator PrewarmCombatVisuals()
        {
            SetLoadingProgress(.6f, "스킬과 동료 효과 준비 중");
            var preview = new GameObject("Loading material preview").AddComponent<SpriteRenderer>();
            preview.transform.SetParent(world, false);
            var textures = new HashSet<Texture>();
            int count = 0;
            foreach (var source in CombatArt()) {
                if (!source) continue;
                var entry = DoodleSkillFaceCatalog.Find(source);
                var sprite = entry != null ? entry.body : source;
                if (!textureMaterials.TryGetValue(sprite.texture, out var material)) {
                    material = new Material(spriteMaterial) { mainTexture = sprite.texture, name = "Doodle / " + sprite.texture.name };
                    textureMaterials.Add(sprite.texture, material);
                }
                preview.sprite = sprite; preview.sharedMaterial = material;
                // A real render uploads each texture while the full-screen loading cover is up.
                if (textures.Add(sprite.texture) || ++count % 8 == 0) yield return null;
            }
            Destroy(preview.gameObject);
            SetLoadingProgress(.8f, "효과 오브젝트 준비 중");
            // Bounded reserves shared by every ability, including newly equipped skills.
            // Labels are assigned at rental; no dummy combat actors or damage are involved.
            for (int i = 0; i < 128; i++) {
                var lease = CreatePreparedVisual(disc);
                lease.gameObject.SetActive(false); preparedVisuals.Push(lease); pooledVisualCount++;
                if (i % 16 == 15) yield return null;
            }
            var catalog = Resources.Load<DoodleSkillFaceCatalog>("DoodleIdle/SkillFaceCatalog");
            foreach (var entry in catalog.entries) {
                if (!entry.facePrefab || preparedFaces.ContainsKey(entry.facePrefab)) continue;
                var reserve = new Stack<DoodleVisualLease>(); preparedFaces.Add(entry.facePrefab, reserve);
                for (int i = 0; i < 4; i++) reserve.Push(CreatePreparedVisual(entry.body));
                // Register and render masks/glints before parking their first instances.
                yield return null;
                foreach (var lease in reserve) { lease.gameObject.SetActive(false); pooledVisualCount++; }
            }
            // The first dense hit used to create Text/Outline objects during combat.
            // Warm the existing bounded damage-number pool without awarding damage or gold.
            uiFont.RequestCharactersInTexture("0123456789.,ABCDEFGHIJKLMNOPQRSTUVWXYZ", 84, FontStyle.Normal);
            for (int i = spareDamageNumbers.Count; i < MaxDamageNumbers; i++) {
                var number = CreateDamageNumber(); number.text.text = "0";
                spareDamageNumbers.Push(number);
                if (i % 16 == 15) yield return null;
            }
            foreach (var system in particleSystems) {
                system.Emit(new ParticleSystem.EmitParams { position = Vector3.zero, startSize = .1f, startLifetime = 1, startColor = Color.white, randomSeed = 1 }, 1);
                system.Simulate(0, false, false, false);
            }
            yield return null;
            yield return null;
            foreach (var number in spareDamageNumbers) number.text.gameObject.SetActive(false);
            ClearParticles();
            SetLoadingProgress(.95f, "전투 시작 준비 중");
        }

        DoodleVisualLease CreatePreparedVisual(Sprite sprite)
        {
            var go = new GameObject("Prepared combat visual"); go.transform.SetParent(world, false);
            var lease = go.AddComponent<DoodleVisualLease>(); lease.art = go.AddComponent<SpriteRenderer>();
            lease.trigger = go.AddComponent<CircleCollider2D>(); lease.trigger.isTrigger = true; lease.trigger.enabled = false;
            lease.returned = true; SetSpriteArt(lease.art, sprite); VisualObjectsCreated++;
            return lease;
        }
    }
}
