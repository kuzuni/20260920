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
        DoodleLoadingScreen loadingScreen;
        void BuildLoadingScreen()
        {
            uiFont = Resources.Load<Font>("DoodleIdle/UI/DisplayFont");
            loadingScreen = DoodleLoadingScreen.TakeForGame(transform);
            loadingCanvas = loadingScreen.Canvas;
            SetLoadingProgress(0, "게임 준비 중");
        }
        void SetLoadingProgress(float progress, string message)
        {
            LoadingProgress = Mathf.Clamp01(progress);
            loadingScreen.SetProgress(loadingScreen.FromLogin ? .1f + LoadingProgress * .9f : LoadingProgress, message);
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
