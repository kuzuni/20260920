using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator LoadoutOnlyEquippedSkillsCastAndUnequipCancelsQueuedShots()
        {
            var skills = game.Ui.Items("Skill");
            foreach (var item in skills) item.equipped = false;
            game.ResetGame(); yield return null;
            var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(7, 0));
            game.basicSkillsEnabled = game.extraSkillsEnabled = game.summonSkillsEnabled = true;
            yield return PhysicsTicks(100);
            Assert.That(skills.Sum(x => game.SkillActivationCount(x.ability)), Is.Zero);
            Assert.That(game.StonesLaunched + game.ArrowsLaunched + game.VariantProjectilesLaunched, Is.Zero);
            Assert.That(game.BananasActive, Is.False);
            var arrow = skills.Single(x => x.ability == "Arrows");
            arrow.discovered = true; arrow.equipped = true; arrow.slot = 1;
            yield return PhysicsTicks(30);
            Assert.That(game.SkillActivationCount("Arrows"), Is.Zero, "A skill in a locked slot cannot auto-cast.");
            arrow.slot = 0;
            yield return PhysicsTicks(30);
            Assert.That(game.SkillActivationCount("Arrows"), Is.EqualTo(1));
            Assert.That(skills.Where(x => x != arrow).Sum(x => game.SkillActivationCount(x.ability)), Is.Zero);
            Assert.That(game.ArrowsLaunched, Is.InRange(1, 9));
            arrow.equipped = false; int shots = game.ArrowsLaunched;
            yield return PhysicsTicks(50);
            Assert.That(game.ArrowsLaunched, Is.EqualTo(shots), "An unequipped automatic volley must not launch remaining shots.");
        }

        [UnityTest]
        public IEnumerator SkillDebugButtonsCanCastAllUnownedSkillsWithoutChangingLoadoutOrWallet()
        {
            var skills = game.Ui.Items("Skill");
            foreach (var item in skills) { item.equipped = false; item.discovered = false; item.count = 0; }
            game.ResetGame(); yield return null; IsolateSummonTest();
            int diamonds = game.Ui.Diamonds; long gold = game.Ui.Gold;
            yield return PhysicsTicks(1);
            foreach (var item in skills) {
                Assert.That(game.DebugCastSkill(item.id), Is.True, item.id);
                Assert.That(game.SkillActivationCount(item.ability), Is.EqualTo(1), item.id);
            }
            Assert.That(skills.All(x => !x.equipped && !x.discovered && x.count == 0), Is.True);
            Assert.That(game.Ui.Diamonds, Is.EqualTo(diamonds)); Assert.That(game.Ui.Gold, Is.EqualTo(gold));
            Assert.That(game.DebugCastSkill("missing-skill"), Is.False);
            yield return PhysicsTicks(1);
            Assert.That(game.BananasActive, Is.True, "The explicit test bypass also supports the orbit skill.");
#if UNITY_EDITOR
            var editorType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DoodleIdle.Editor.DoodleSkillTestWindow")).FirstOrDefault(t => t != null);
            Assert.That(editorType, Is.Not.Null);
            var window = ScriptableObject.CreateInstance(editorType);
            editorType.GetMethod("RefreshSkills").Invoke(window, null);
            var rows = (System.Collections.IList)editorType.GetField("skills", GrowthPrivate).GetValue(window);
            Assert.That(rows.Count, Is.EqualTo(40)); Object.DestroyImmediate(window);
#endif
        }

        [UnityTest]
        public IEnumerator FullLoadoutsReplaceThroughTheirExistingSlotsAndEmptySlotsShowPlus()
        {
            game.TogglePause();
            ServiceSetSavedField(ServiceStateObject, "mainStage", 1199);
            game.Ui.AddItem(game.Ui.Items("Armor").Single(x => x.rarity == 6 && x.tier == 1), 1);
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = game.Ui.Items(category); int capacity = category == "Skill" ? 8 : 5;
                foreach (var item in items) { game.Ui.AddItem(item, 1); item.equipped = false; }
                UiOpen(category == "Skill" ? "Skills" : "Companions");
                var empty = UiNode("Equipped " + category).GetComponentsInChildren<Image>().Where(x => x.sprite == UiKit.Art("AddSlot")).ToArray();
                Assert.That(empty.Length, Is.EqualTo(capacity));
                for (int i = 0; i < capacity; i++) { items[i].equipped = true; items[i].slot = i; }
                UiOpen(category == "Skill" ? "Skills" : "Companions");
                var candidate = items[capacity];
                UiClick("Slot: " + candidate.name, UiNode("Collection inventory"));
                UiClick("장착", UiNode("Detail actions"));
                Assert.That(game.Ui.HasOverlay, Is.False, "Replacement must return to the original loadout.");
                Assert.That(UiNode("Equipped " + category).GetComponentsInChildren<Transform>().Count(x => x.name == "Replacement arrow"), Is.EqualTo(capacity));
                yield return new WaitForSecondsRealtime(2.5f); // Let the preceding equip toast finish.
                Object.Destroy(CaptureFrame("loadout-replace-" + category + ".png", 720, 1520));
                UiClick("Slot: " + items[1].name, UiNode("Equipped " + category));
                Assert.That(items[1].equipped, Is.False); Assert.That(candidate.equipped, Is.True);
                Assert.That(candidate.slot, Is.EqualTo(1));
                Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(x => x.name == "Replacement arrow"), Is.False);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompanionAndSkillArtHasDistinctCompleteFramesAndExportsBothPoses()
        {
            game.TogglePause();
            foreach (string category in new[] { "Skill", "Companion" }) {
                var items = game.Ui.Items(category);
                Assert.That(items.Select(x => x.icon).Distinct().Count(), Is.EqualTo(category == "Skill" ? 40 : 32));
                foreach (var item in items) Assert.That(UiKit.Art(item.icon), Is.Not.Null, item.id);
            }
            Assert.That(game.Ui.Items("Companion").Select(x => x.projectile).Distinct().Count(), Is.EqualTo(32));
            foreach (var renderer in game.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var display = new GameObject("Companion motion atlas");
            display.transform.SetParent(game.transform);
            var renderers = new SpriteRenderer[24];
            for (int i = 0; i < 24; i++) {
                var a = DoodleCollectionArt.CompanionFrame(i, 0); var b = DoodleCollectionArt.CompanionFrame(i, 1);
                Assert.That(a, Is.Not.SameAs(b)); Assert.That(a.rect, Is.Not.EqualTo(b.rect));
                Assert.That(Vector3.Distance(a.bounds.size, b.bounds.size), Is.LessThan(.00001f)); Assert.That(a.pixelsPerUnit, Is.EqualTo(b.pixelsPerUnit));
                var node = new GameObject("Companion pose " + i); node.transform.SetParent(display.transform);
                node.transform.position = new Vector3((i % 6 - 2.5f) * 1.8f, (1.5f - i / 6) * 1.8f, 0);
                renderers[i] = node.AddComponent<SpriteRenderer>(); renderers[i].sprite = a;
                renderers[i].sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            }
            Camera.main.transform.position = new Vector3(0, 0, -10); Camera.main.orthographicSize = 4.4f;
            Camera.main.backgroundColor = new Color(.85f, .87f, .82f);
            for (int pose = 0; pose < 2; pose++) {
                for (int i = 0; i < 24; i++) { renderers[i].sprite = DoodleCollectionArt.CompanionFrame(i, pose); renderers[i].sharedMaterial.mainTexture = renderers[i].sprite.texture; }
                Object.Destroy(CaptureFrame("companions-all-pose-" + pose + ".png", 1440, 1000, false));
            }
            foreach (var renderer in renderers) Object.Destroy(renderer.sharedMaterial);
            Object.Destroy(display); yield return null;
            ExportCompanionProjectileSheets();
        }

        [UnityTest]
        public IEnumerator SkillSlotsUnlockAtStageBoundariesAndSavedLoadoutsRespectProgress()
        {
            game.TogglePause();
            var ui = game.Ui;
            var skills = ui.Items("Skill");
            foreach (var item in skills) { ui.AddItem(item, 1); item.equipped = false; }
            ServiceSetSavedField(ServiceStateObject, "mainStage", 0);
            Assert.That(ui.UnlockedSkillSlots, Is.EqualTo(1));
            ui.ClosePage(); ui.RefreshHud();
            var hudLocks = UiNode("Eight equipped cooldowns").GetComponentsInChildren<DoodleUiPadlock>();
            Assert.That(hudLocks.Length, Is.EqualTo(7));
            Assert.That(hudLocks.All(x => x.largeHud && x.rectTransform.rect.width >= 40), Is.True);
            Object.Destroy(CaptureFrame("main-large-skill-padlocks.png", 720, 1520));
            ui.AutoEquip("Skill");
            Assert.That(ui.EquippedSkills.Count, Is.EqualTo(1));
            UiOpen("Skills");
            var candidate = skills.First(x => !x.equipped);
            UiClick("Slot: " + candidate.name, UiNode("Collection inventory"));
            UiClick("장착", UiNode("Detail actions"));
            Assert.That(ui.HasOverlay, Is.False);
            Assert.That(UiNode("Equipped Skill").GetComponentsInChildren<Transform>().Count(x => x.name == "Replacement arrow"), Is.EqualTo(1));
            UiClick("Slot: " + ui.EquippedSkills[0].name, UiNode("Equipped Skill"));
            Assert.That(ui.EquippedSkills.Single(), Is.SameAs(candidate));
            Assert.That(UiNode("Equipped Skill").GetComponentsInChildren<DoodleUiPadlock>().Length, Is.EqualTo(7));
            Object.Destroy(CaptureFrame("skill-slots-stage-1.png", 720, 1520));
            Object.Destroy(CaptureFrame("skill-slots-stage-1-landscape.png", 1440, 900));
            Assert.That(UiNode("Locked skill slot 1").GetComponentInChildren<Text>().text, Is.EqualTo("10\n스테이지"), "Compact layouts must retain unlock requirements.");
            int[] stages = { 10, 30, 60, 120, 180, 300, 800 };
            for (int i = 0; i < stages.Length; i++) {
                ServiceSetSavedField(ServiceStateObject, "mainStage", stages[i] - 2);
                Assert.That(ui.UnlockedSkillSlots, Is.EqualTo(i + 1), "Immediately before " + stages[i]);
                ui.AutoEquip("Skill");
                Assert.That(ui.EquippedSkills.Count, Is.EqualTo(i + 1));
                UiOpen("Skills");
                ServiceSetSavedField(ServiceStateObject, "mainStageKillProgress", 100);
                ui.RecordMainCombatKill(true);
                Assert.That(ui.MainStage + 1, Is.EqualTo(stages[i]));
                Assert.That(ui.UnlockedSkillSlots, Is.EqualTo(i + 2));
                Assert.That(UiNode("Equipped Skill").GetComponentsInChildren<DoodleUiPadlock>().Length, Is.EqualTo(6 - i), "Open UI updates when the boss unlocks a slot.");
                ui.AutoEquip("Skill");
                Assert.That(ui.EquippedSkills.Count, Is.EqualTo(i + 2));
                UiOpen("Skills");
                if (i == 0 || i == 6) Object.Destroy(CaptureFrame("skill-slots-stage-" + stages[i] + ".png", 720, 1520));
            }
            var probes = new System.Collections.Generic.List<GameObject>();
            try {
                ui.Save();
                var restored = GrowthProbe(probes);
                typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(restored, null);
                Assert.That(restored.UnlockedSkillSlots, Is.EqualTo(8));
                Assert.That(restored.EquippedSkills.Count, Is.EqualTo(8), "Saved progress must load before applying the skill slot cap.");
                ServiceSetSavedField(ServiceStateObject, "mainStage", 0);
                ServiceSetSavedField(ServiceStateObject, "highestMainStage", 0);
                ui.Save(); // Simulate a legacy early-stage save with eight equipped skills.
                var legacy = GrowthProbe(probes);
                typeof(DoodleUi).GetMethod("InitServices", ServicePrivate).Invoke(legacy, null);
                Assert.That(legacy.EquippedSkills.Count, Is.EqualTo(1));
                Assert.That(legacy.Items("Skill").Count(x => x.equipped), Is.EqualTo(1));
                Assert.That(legacy.Items("Skill").All(x => x.discovered), Is.True, "Only excess equipment is removed; owned skills remain.");
            }
            finally { foreach (var probe in probes) Object.Destroy(probe); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompanionSlotsUnlockFromArmorDiscoveryAndPersistAfterCopiesAreConsumed()
        {
            game.TogglePause(); var ui=game.Ui;
            var armor=ui.Items("Armor"); var companions=ui.Items("Companion");
            foreach(var item in armor){item.discovered=false;item.equipped=false;item.count=item.level=0;}
            foreach(var item in companions){ui.AddItem(item,1);item.equipped=false;}
            Assert.That(ui.UnlockedCompanionSlots,Is.EqualTo(1));
            ui.AddItem(ui.Items("Club").Single(x=>x.rarity==6 && x.tier==1),1);
            Assert.That(ui.UnlockedCompanionSlots,Is.EqualTo(1),"Weapons cannot unlock companion slots.");
            ui.AutoEquip("Companion");Assert.That(ui.EquippedCompanions.Count,Is.EqualTo(1));
            UiOpen("Companions");
            Assert.That(UiNode("Equipped Companion").GetComponentsInChildren<DoodleUiPadlock>().Length,Is.EqualTo(4));
            Object.Destroy(CaptureFrame("companion-armor-unlocks-first.png",720,1520));
            for(int grade=0;grade<=6;grade++) {
                var item=armor.First(x=>x.rarity==grade);ui.AddItem(item,1);
                int expected=Mathf.Clamp(grade-1,1,5);
                Assert.That(ui.UnlockedCompanionSlots,Is.EqualTo(expected));
                item.count=0; // Discovery survives synthesis or consuming spare copies.
                Assert.That(ui.UnlockedCompanionSlots,Is.EqualTo(expected));
                ui.AutoEquip("Companion");Assert.That(ui.EquippedCompanions.Count,Is.EqualTo(expected));
            }
            foreach(var item in armor.Where(x=>x.rarity<6))item.discovered=false;
            Assert.That(ui.UnlockedCompanionSlots,Is.EqualTo(5),"Directly acquiring God armor unlocks all earlier slots.");
            UiOpen("Companions");Object.Destroy(CaptureFrame("companion-armor-unlocks-god.png",720,1520));
            var probes=new System.Collections.Generic.List<GameObject>();
            try {
                ui.Save();var restored=GrowthProbe(probes);
                Assert.That(restored.UnlockedCompanionSlots,Is.EqualTo(5));Assert.That(restored.EquippedCompanions.Count,Is.EqualTo(5));
                foreach(var item in armor){item.discovered=false;item.level=item.count=0;}
                ui.Save();var legacy=GrowthProbe(probes);
                Assert.That(legacy.EquippedCompanions.Count,Is.EqualTo(1));
                Assert.That(legacy.Items("Companion").Count(x=>x.equipped),Is.EqualTo(1));
                Assert.That(legacy.Items("Companion").All(x=>x.discovered),Is.True);
            } finally {foreach(var probe in probes)Object.Destroy(probe);}
            ui.ClosePage();game.TogglePause();IsolateSummonTest();
            foreach(var item in companions)item.equipped=false;
            companions[0].equipped=true;companions[0].slot=0;companions[1].equipped=true;companions[1].slot=1;
            game.companionsEnabled=true;yield return PhysicsTicks(3);
            Assert.That(game.ActiveCompanions,Is.EqualTo(1),"A legacy equipped item in a locked slot cannot spawn.");
        }

        void ExportCompanionProjectileSheets()
        {
            var items = game.Ui.Items("Companion").Take(24).ToList();
            var previews = new Texture2D[24];
            var camera = Camera.main; camera.transform.position = new Vector3(0,0,-10); camera.orthographicSize = 1.5f;
            camera.backgroundColor = new Color(.96f,.96f,.92f);
            var emit = typeof(DoodleIdleGame).GetMethod("EmitCompanionImpact", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            for (int i = 0; i < items.Count; i++) {
                var art = DoodleCollectionArt.CompanionImpact(i);
                Assert.That(art != null, Is.EqualTo(items[i].explosionRadius > 0), items[i].id);
                if (!art) continue;
                Assert.That(art.texture, Is.Not.SameAs(UiKit.Art(items[i].projectile).texture), "Impact fragments must not reuse the projectile texture.");
                var particles = Particles("Companion impact: " + i);
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.GetComponent<ParticleSystemRenderer>().enabled = true;
                emit.Invoke(game,new object[] { i, Vector2.zero, items[i].explosionRadius });
                int expected = i == 19 ? 5 : 10 + i / 4;
                Assert.That(particles.particleCount, Is.EqualTo(expected), items[i].id);
                var emitted = new ParticleSystem.Particle[expected]; particles.GetParticles(emitted);
                Assert.That(emitted.All(p => p.startSize < .43f && p.velocity.sqrMagnitude > .1f), Is.True, "Each particle is a small independently moving fragment.");
                particles.Simulate(.04f,false,false,false);
                Object.Destroy(CaptureFrame("companion-impact-" + i + "-early.png",256,256,false));
                particles.Simulate(.07f,false,false,false);
                previews[i] = CaptureFrame("companion-impact-" + i + "-spread.png",256,256,false);
                particles.GetParticles(emitted);
                Assert.That(emitted.Take(particles.particleCount).Any(p => p.position.magnitude > .12f), Is.True);
                particles.Simulate(.14f,false,false,false);
                Object.Destroy(CaptureFrame("companion-impact-" + i + "-late.png",256,256,false));
                particles.Simulate(1,false,false,false);
                Assert.That(particles.particleCount, Is.Zero, "All impact fragments expire.");
                emit.Invoke(game,new object[] { i, Vector2.zero, items[i].explosionRadius });
                typeof(DoodleIdleGame).GetMethod("ClearParticles", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(game,null);
                Assert.That(particles.particleCount, Is.Zero, "Game reset clears impact fragments too.");
                particles.GetComponent<ParticleSystemRenderer>().enabled = false;
            }
            for (int page = 0; page < 3; page++) {
                var poster = UiKit.Rect(UiRoot, "Companion projectile reference"); UiKit.Stretch(poster);
                poster.gameObject.AddComponent<Image>().color = new Color(.96f, .96f, .92f);
                void PlaceRect(RectTransform rect, Vector2 low, Vector2 high) {
                    rect.anchorMin = low; rect.anchorMax = high; rect.offsetMin = rect.offsetMax = Vector2.zero;
                }
                var heading = UiKit.Text(poster, "동료 명중 효과 · 이전 / 입자 1개 / 변경 후  " + (page + 1) + "/3", 40, TextAnchor.MiddleCenter);
                PlaceRect(heading.rectTransform, new Vector2(.02f, .91f), new Vector2(.98f, .99f));
                for (int n = 0; n < 8; n++) {
                    var item = items[page * 8 + n];
                    var cell = UiKit.Rect(poster, item.name);
                    float left = .015f + n % 4 * .247f, bottom = .03f + (1 - n / 4) * .43f;
                    PlaceRect(cell, new Vector2(left, bottom), new Vector2(left + .235f, bottom + .41f));
                    var title = UiKit.Text(cell, item.name + " · " + UiKit.GradeName(item.rarity), 38, TextAnchor.MiddleCenter);
                    PlaceRect(title.rectTransform, new Vector2(0, .84f), Vector2.one);
                    var oldLabel = UiKit.Text(cell, "이전", 28, TextAnchor.MiddleCenter);
                    PlaceRect(oldLabel.rectTransform, new Vector2(0, .24f), new Vector2(.32f, .35f));
                    var singleLabel = UiKit.Text(cell, "입자 1개", 28, TextAnchor.MiddleCenter);
                    PlaceRect(singleLabel.rectTransform, new Vector2(.34f, .24f), new Vector2(.66f, .35f));
                    var newLabel = UiKit.Text(cell, "변경 후", 28, TextAnchor.MiddleCenter);
                    PlaceRect(newLabel.rectTransform, new Vector2(.68f, .24f), new Vector2(1, .35f));
                    if (item.explosionRadius > 0) {
                        var oldSprite = UiKit.Art(item.projectile);
                        if (item.icon == "CompanionMon_12")
                            oldSprite = (Sprite)typeof(DoodleCollectionArt).GetMethod("Cell", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                                .Invoke(null, new object[] { "CompanionAttacksB", 4, 4, 2 });
                        for (int k = 0; k < 8; k++) {
                            var fragment = UiKit.Icon(cell, item.projectile, 28); fragment.sprite = oldSprite;
                            fragment.color = new Color(1, 1, 1, .7f);
                            fragment.rectTransform.anchorMin = fragment.rectTransform.anchorMax = new Vector2(.16f, .53f);
                            float angle = k * Mathf.PI / 4;
                            fragment.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 43;
                            fragment.transform.localRotation = Quaternion.Euler(0, 0, k * 45);
                        }
                        var one = UiKit.Icon(cell,item.projectile,90); one.sprite = DoodleCollectionArt.CompanionImpact(page * 8 + n);
                        one.rectTransform.anchorMin = one.rectTransform.anchorMax = new Vector2(.5f,.53f); one.rectTransform.anchoredPosition = Vector2.zero;
                        var view = UiKit.Rect(cell,"Actual ParticleSystem render");
                        PlaceRect(view,new Vector2(.67f,.36f),new Vector2(1,.74f));
                        var fitted = UiKit.Rect(view,"Square preview");
                        var raw = fitted.gameObject.AddComponent<RawImage>(); raw.texture = previews[page * 8 + n]; raw.raycastTarget = false;
                        var aspect = fitted.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 1;
                    } else {
                        var unchanged = UiKit.Text(cell,"폭발 없음 · 유지",30,TextAnchor.MiddleCenter);
                        PlaceRect(unchanged.rectTransform,new Vector2(0,.42f),new Vector2(1,.66f));
                    }
                    string motion = item.trajectory == "Arc" ? "포물선" : item.trajectory == "Lightning" ? "번개 타격" : "직선";
                    var pattern = UiKit.Text(cell, DoodleCollectionArt.CompanionImpactName(page * 8 + n) + "\n" + motion + " · " + item.volleyCount + "발" + (item.volleyCount > 1 ? item.volleyGap > 0 ? " 순차" : " 동시" : ""), 26, TextAnchor.MiddleCenter);
                    PlaceRect(pattern.rectTransform, new Vector2(0, .02f), new Vector2(1, .17f));
                }
                Object.Destroy(CaptureFrame("companion-impact-before-after-" + (page + 1) + ".png", 1800, 1200));
                poster.gameObject.SetActive(false); Object.Destroy(poster.gameObject);
            }
            foreach (var preview in previews) if (preview) Object.Destroy(preview);
        }

        [UnityTest]
        public IEnumerator AllEnemyPairsExportAtEqualScaleFacingRight()
        {
            game.TogglePause();
            foreach (var renderer in game.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            var display = new GameObject("Enemy motion atlas"); display.transform.SetParent(game.transform);
            var renderers = new SpriteRenderer[30]; var pairs = new Sprite[30][];
            var loader = typeof(DoodleIdleGame).GetMethod("LoadThemeFrames", GrowthPrivate);
            var orientation = (System.Collections.Generic.Dictionary<Sprite, bool>)typeof(DoodleIdleGame).GetField("sourceFrameFacesLeft", GrowthPrivate).GetValue(game);
            for (int theme = 0; theme < 10; theme++) {
                var frames = (Sprite[][])loader.Invoke(game, new object[] { theme });
                for (int kind = 0; kind < 3; kind++) {
                    int i = theme * 3 + kind; pairs[i] = frames[kind];
                    Assert.That(pairs[i][0].bounds.size, Is.EqualTo(pairs[i][1].bounds.size));
                    var node = new GameObject("Enemy pose " + i); node.transform.SetParent(display.transform);
                    node.transform.position = new Vector3((i % 6 - 2.5f) * 1.8f, (2f - i / 6) * 1.8f, 0);
                    renderers[i] = node.AddComponent<SpriteRenderer>();
                    renderers[i].sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
                }
            }
            Camera.main.transform.position = new Vector3(0, 0, -10); Camera.main.orthographicSize = 5.1f;
            Camera.main.backgroundColor = new Color(.85f, .87f, .82f);
            for (int pose = 0; pose < 2; pose++) {
                for (int i = 0; i < 30; i++) {
                    renderers[i].sprite = pairs[i][pose]; renderers[i].flipX = orientation[pairs[i][pose]];
                    renderers[i].sharedMaterial.mainTexture = pairs[i][pose].texture;
                }
                Object.Destroy(CaptureFrame("enemies-all-right-pose-" + pose + ".png", 1440, 1200, false));
            }
            foreach (var renderer in renderers) Object.Destroy(renderer.sharedMaterial);
            Object.Destroy(display); yield return null;
        }
    }
}
