using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        void AscensionTargets()
        {
            var bodies = DurableSkillTargets();
            for (int i = 0; i < bodies.Length; i++) {
                if (i < 12) Place(bodies[i], new Vector2(3 + i % 4 * .8f, -1.2f + i / 4 * .8f));
                SetTargetHealth(bodies[i], 1e15f);
            }
        }

        void AscensionSpawnBoss(int displayedStage)
        {
            game.Ui.DebugSetMainStage(displayedStage);
            if (!game.Ui.BreakthroughMode) game.Ui.ToggleBreakthroughMode();
            DefeatActualServiceEnemies(game.Ui.MainStageRemaining);
            typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate).Invoke(game, null);
            Assert.That(game.BossActive, Is.True);
            Assert.That(game.BossTimeRemaining, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator AscensionBreakthroughButtonPulsesOnlyWhenEnabled()
        {
            game.TogglePause(); var ui=game.Ui;
            if(!ui.BreakthroughMode)ui.ToggleBreakthroughMode();
            var pulse=game.GetComponentInChildren<DoodleBreakthroughPulse>(true);
            var surface=pulse.GetComponent<Image>(); var samples=new List<float>();
            for(int i=0;i<6;i++) { yield return new WaitForSecondsRealtime(.2f);samples.Add(surface.color.r); }
            Assert.That(samples.Max()-samples.Min(),Is.GreaterThan(.04f));
            ui.ToggleBreakthroughMode();yield return null;
            Color off=surface.color;
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(surface.color,Is.EqualTo(off));Assert.That(off,Is.EqualTo(Color.gray));
            ui.ToggleBreakthroughMode();yield return new WaitForSecondsRealtime(.25f);
            Assert.That(surface.color.g,Is.GreaterThan(off.g));
        }

        [UnityTest]
        public IEnumerator AscensionRewardCelebrationUsesParticlesAndClosesWithoutDuplicateRewards()
        {
            game.TogglePause(); var ui=game.Ui;long gold=ui.Gold;int diamonds=ui.Diamonds;
            ui.ShowRewards("보상 획득!",new List<UiReward>{new UiReward{icon="Gold",amount=500},new UiReward{icon="Diamond",amount=50}});
            yield return new WaitForSecondsRealtime(.3f);
            var effect=UiNode("Reward celebration particles").GetComponent<DoodleRewardCelebration>();
            Assert.That(effect.LiveParticleCount,Is.GreaterThan(100));
            Assert.That(effect.raycastTarget,Is.False);
            Assert.That(effect.GetComponent<ParticleSystem>().main.useUnscaledTime,Is.True);
            var particles = new ParticleSystem.Particle[160];
            int particleCount=effect.GetComponent<ParticleSystem>().GetParticles(particles);
            Assert.That(particles.Take(particleCount).All(x=>x.startSize>=18),Is.True);
            var rays=UiNode("Rotating reward sunburst").GetComponent<DoodleRewardRays>();
            Assert.That(rays.color.a,Is.InRange(.15f,.35f));
            float angle=rays.transform.eulerAngles.z;
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(Mathf.DeltaAngle(angle,rays.transform.eulerAngles.z),Is.InRange(-10f,-3f),"The translucent rays turn clockwise while gameplay is paused.");
            Object.Destroy(CaptureFrame("ascension-reward-celebration.png",720,1520));
            yield return new WaitForSecondsRealtime(3.2f);
            Assert.That(effect.LiveParticleCount,Is.Zero);
            Object.Destroy(CaptureFrame("ascension-reward-clean-halo.png",720,1520));
            ui.CloseDetail(); yield return new WaitForSecondsRealtime(.35f);
            Assert.That(!effect,Is.True);
            Assert.That(ui.Gold,Is.EqualTo(gold));Assert.That(ui.Diamonds,Is.EqualTo(diamonds),"Presentation never grants a reward twice.");
        }

        [UnityTest]
        public IEnumerator AscensionSkillThumbnailsUseDistinctCompleteArtAndCatalogsEndAtTranscendent()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Skill", "Companion" }) {
                for (int grade = 6; grade <= 7; grade++)
                    Assert.That(ui.Items(category).Count(x=>x.rarity==grade),Is.EqualTo(category=="Skill"?5:4));
                Assert.That(ui.Items(category).Any(x=>x.rarity==8),Is.False);
                for (int level=1;level<=50;level++) Assert.That(ui.SummonWeights(category,level)[8],Is.Zero);
                ui.ShowSummonProbabilities(category);
                Assert.That(UiNode("Detail dim: 뽑기 확률").GetComponentsInChildren<Transform>().Any(x=>x.name=="Probability_grade_8"),Is.False);
                ui.CloseDetail();
            }
            Assert.That(ui.Items("Companion").Any(x=>x.id=="companion_earth"||x.id=="companion_chimera"),Is.False);
            Assert.That(ui.Items("Skill").Single(x=>x.id=="chimera_brothers").ability,Is.EqualTo("SawSnakes"));
            var added=ui.Items("Skill").Where(x=>x.rarity>=6).ToArray();
            var sheet=UiKit.Rect(UiRoot,"Ascension skill thumbnail reference"); UiKit.Stretch(sheet);
            sheet.gameObject.AddComponent<Image>().color=new Color(.96f,.95f,.9f);
            for (int i=0;i<added.Length;i++) {
                var art=UiKit.Art(added[i].icon);
                string expectedTexture=added[i].ability=="CactusRage"?"SkillThumbFoamRoller":added[i].ability=="RazorShuriken"?"SkillThumbCherryShuriken":added[i].ability=="GodHand"?"SkillThumbSkyPalm":added[i].ability=="MissileRage"?"SkillThumbsCactusMissile":"SkillThumbsAscension";
                Assert.That(art.texture.name,Is.EqualTo(expectedTexture));
                var rect=art.rect;
                var pixels=art.texture.GetPixels((int)rect.x,(int)rect.y,(int)rect.width,(int)rect.height);
                Assert.That(pixels.Count(x=>x.a>.125f),Is.GreaterThan(pixels.Length/10));
                for(int x=0;x<(int)rect.width;x++) {
                    Assert.That(pixels[x].a,Is.LessThan(.13f),added[i].name+" bottom edge");
                    Assert.That(pixels[((int)rect.height-1)*(int)rect.width+x].a,Is.LessThan(.13f),added[i].name+" top edge");
                }
                var cell=UiKit.Rect(sheet,added[i].name);int col=i%2,row=i/2;
                cell.anchorMin=new Vector2(col*.5f+.03f,.02f+(4-row)*.192f);
                cell.anchorMax=new Vector2((col+1)*.5f-.03f,.02f+(5-row)*.192f);cell.offsetMin=cell.offsetMax=Vector2.zero;
                var label=UiKit.Text(cell,added[i].name,25,TextAnchor.UpperCenter,35);
                label.rectTransform.anchorMin=new Vector2(0,.82f);label.rectTransform.anchorMax=Vector2.one;
                label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;
                var icon=UiKit.Icon(cell,added[i].icon,160);
                icon.rectTransform.anchorMin=new Vector2(.2f,.03f);icon.rectTransform.anchorMax=new Vector2(.8f,.79f);
                icon.rectTransform.offsetMin=icon.rectTransform.offsetMax=Vector2.zero;
            }
            Object.Destroy(CaptureFrame("ascension-skill-thumbnails.png",900,1520));Object.Destroy(sheet.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AscensionBossTimerHudFailureDeathAndVictoryPreserveStageRules()
        {
            game.TogglePause(); var ui = game.Ui;
            game.basicSkillsEnabled = game.extraSkillsEnabled = game.summonSkillsEnabled = game.companionsEnabled = false;
            game.autoPlay = false; game.enemyContactDamage = 0; game.enemyDashEnabled = false;
            AscensionSpawnBoss(100);
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var boss = actors[0]; var type = boss.GetType();
            type.GetField("hp").SetValue(boss,(float)type.GetField("maxHp").GetValue(boss)*.4f);
            ui.RefreshHud();
            Assert.That(UiNode("Boss challenge HUD").gameObject.activeInHierarchy, Is.True);
            Assert.That(((RectTransform)UiNode("Boss health bar fill")).anchorMax.x, Is.EqualTo(.4f).Within(.001));
            Assert.That(((RectTransform)UiNode("Boss timer bar fill")).anchorMax.x, Is.EqualTo(1));
            var timerColor=UiNode("Boss timer bar fill").GetComponent<Image>().color;
            Assert.That(timerColor.b,Is.GreaterThan(timerColor.r));
            Assert.That(UiNode("Boss challenge HUD").parent.name,Is.EqualTo("Stage progress"));
            Assert.That(UiNode("Boss health bar").GetComponent<Image>().sprite.texture.name,Is.EqualTo("HealthBarFrame"));
            Assert.That(UiNode("Boss timer bar fill").GetComponent<Image>().sprite.texture.name,Is.EqualTo("BossGaugeFill"));
            Object.Destroy(CaptureFrame("ascension-boss-health-and-timer.png",720,1520));
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(game.BossTimeRemaining, Is.EqualTo(10), "Pausing also pauses the challenge timer.");
            game.TogglePause();
            yield return PhysicsTicks(250);
            Assert.That(game.BossTimeRemaining,Is.InRange(4.95f,5.05f));
            ui.RefreshHud(); Assert.That(((RectTransform)UiNode("Boss timer bar fill")).anchorMax.x,Is.InRange(.49f,.51f));
            yield return PhysicsTicks(251);
            Assert.That(ui.BreakthroughMode,Is.True); Assert.That(ui.MainStage,Is.EqualTo(99));
            Assert.That(game.BossActive,Is.False);
            Assert.That(game.GetComponentsInChildren<RectTransform>(true).Single(x=>x.name=="Boss challenge HUD").gameObject.activeSelf,Is.False);
            ReloadPersistedServices(); Assert.That(ui.BreakthroughMode,Is.True); Assert.That(ui.MainStage,Is.EqualTo(99));
            game.TogglePause(); AscensionSpawnBoss(100);
            typeof(DoodleIdleGame).GetField("bossTimeRemaining",GrowthPrivate).SetValue(game,.02f);
            DefeatActualServiceEnemies(1); ui.RefreshHud();
            Assert.That(ui.MainStage,Is.EqualTo(100)); Assert.That(ui.BreakthroughMode,Is.True);
            Assert.That(game.BossTimeRemaining,Is.Zero);
            AscensionSpawnBoss(100);
            boss=actors[0]; type=boss.GetType();
            Place((Rigidbody2D)type.GetField("body").GetValue(boss), PlayerBody().position);
            var player=typeof(DoodleIdleGame).GetField("player",GrowthPrivate).GetValue(game);
            player.GetType().GetField("hp").SetValue(player,1f);
            typeof(DoodleIdleGame).GetField("contactInvulnerability",GrowthPrivate).SetValue(game,0f);
            game.enemyContactDamage=1e10f;
            typeof(DoodleIdleGame).GetMethod("TickPlayerContactDamage",GrowthPrivate).Invoke(game,new object[]{.02f});
            Assert.That(ui.MainStage,Is.EqualTo(99)); Assert.That(ui.BreakthroughMode,Is.True);
            Assert.That(game.PlayerHealth,Is.EqualTo(game.PlayerMaxHealth));
            ui.DebugSetMainStage(100);
            DefeatActualServiceEnemies(7); ui.HandlePlayerDefeat();
            Assert.That(ui.MainStage,Is.EqualTo(99)); Assert.That(ui.MainStageKillProgress,Is.EqualTo(7));
            Assert.That(ui.BreakthroughMode,Is.True,"Ordinary defeat preserves the current mode and stage.");
        }

        [UnityTest]
        public IEnumerator AscensionBalanceEditsAutoSaveAndReloadFromResources()
        {
#if UNITY_EDITOR
            game.TogglePause();
            const string tuningPath = "Assets/DoodleIdle/Resources/DoodleIdle/UI/ServicesTuning.json";
            const string collectionPath = "Assets/DoodleIdle/Resources/DoodleIdle/UI/Collections.json";
            string tuningBefore = System.IO.File.ReadAllText(tuningPath), collectionBefore = System.IO.File.ReadAllText(collectionPath);
            var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("DoodleIdle.Editor.DoodleBalanceWindow")).First(t => t != null);
            var window = ScriptableObject.CreateInstance(type);
            var draftField = type.GetField("draft", GrowthPrivate);
            var statField = type.GetField("statDraft", GrowthPrivate);
            try {
                var draft = (DoodleUi.ServiceTuning)draftField.GetValue(window);
                draft.goldPerEnemy = 37; draft.playerKeepDistance = 1.25f;
                draft.enemyHealthGrowthSteps = new[] { new DoodleGrowthStep { from = 100, growth = .07f } };
                var stats = (UiStatCostTuning)statField.GetValue(window); stats.commonBaseCost = 123;
                stats.critical4GrowthSteps = new[] { new DoodleGrowthStep { from = 42, growth = .12f } };
                type.GetMethod("AutoSaveChanges").Invoke(window, null);
                Assert.That(game.Ui.ReadBalanceTuning().goldPerEnemy, Is.EqualTo(37));
                Assert.That(game.Ui.PlayerKeepDistance, Is.EqualTo(1.25f));
                Assert.That(game.Ui.ReadStatCostTuning().commonBaseCost, Is.EqualTo(123));
                var saved = JsonUtility.FromJson<DoodleUi.ServiceTuning>(System.IO.File.ReadAllText(tuningPath));
                Assert.That(saved.goldPerEnemy, Is.EqualTo(37));
                Assert.That(saved.enemyHealthGrowthSteps.Single().growth, Is.EqualTo(.07f));
                var catalog = JsonUtility.FromJson<UiCollectionTuning>(System.IO.File.ReadAllText(collectionPath));
                Assert.That(catalog.statCosts.critical4GrowthSteps.Single().from, Is.EqualTo(42));
                Assert.That(catalog.items.Length, Is.EqualTo(JsonUtility.FromJson<UiCollectionTuning>(collectionBefore).items.Length));
                Object.DestroyImmediate(window); window = null;
                var host = new GameObject("Balance restart fixture");
                try {
                    var reloaded = host.AddComponent<DoodleUi>(); reloaded.InitCollections();
                    typeof(DoodleUi).GetMethod("InitServices", GrowthPrivate).Invoke(reloaded, null);
                    Assert.That(reloaded.ReadBalanceTuning().goldPerEnemy, Is.EqualTo(37));
                    Assert.That(reloaded.PlayerKeepDistance, Is.EqualTo(1.25f));
                    Assert.That(reloaded.ReadStatCostTuning().commonBaseCost, Is.EqualTo(123));
                } finally { Object.DestroyImmediate(host); }
                window = ScriptableObject.CreateInstance(type);
                var restored = (DoodleUi.ServiceTuning)draftField.GetValue(window); restored.goldPerEnemy = 17;
                type.GetMethod("AutoSaveChanges").Invoke(window, null);
                Assert.That(JsonUtility.FromJson<DoodleUi.ServiceTuning>(System.IO.File.ReadAllText(tuningPath)).goldPerEnemy, Is.EqualTo(17), "Reopening must not disable autosave.");
                string unchanged = System.IO.File.ReadAllText(tuningPath);
                var stamp = System.IO.File.GetLastWriteTimeUtc(tuningPath);
                type.GetMethod("AutoSaveChanges").Invoke(window, null);
                Assert.That(System.IO.File.GetLastWriteTimeUtc(tuningPath), Is.EqualTo(stamp), "Repaint without edits must not write files.");
                Assert.That(System.IO.File.ReadAllText(tuningPath), Is.EqualTo(unchanged));
            } finally {
                if (window) Object.DestroyImmediate(window);
                System.IO.File.WriteAllText(tuningPath, tuningBefore); System.IO.File.WriteAllText(collectionPath, collectionBefore);
                UnityEditor.AssetDatabase.ImportAsset(tuningPath); UnityEditor.AssetDatabase.ImportAsset(collectionPath);
                game.Ui.ApplyBalanceTuning(JsonUtility.FromJson<DoodleUi.ServiceTuning>(tuningBefore));
                game.Ui.ApplyStatCostTuning(JsonUtility.FromJson<UiCollectionTuning>(collectionBefore).statCosts);
            }
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator AscensionBreakthroughGoalsHonorBoundariesAndModeSwitches()
        {
            game.TogglePause(); var ui = game.Ui;
            var refill = typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate);
            foreach (var pair in new[] { (1,20), (99,20), (100,50), (299,50), (300,50), (301,50) }) {
                ui.DebugSetMainStage(pair.Item1);
                Assert.That(ui.MainStageKillGoal, Is.EqualTo(pair.Item2));
                DefeatActualServiceEnemies(pair.Item2 - 1);
                Assert.That(ui.MainBossPending, Is.False);
                DefeatActualServiceEnemies(1);
                Assert.That(ui.MainBossPending, Is.True);
                ReloadPersistedServices();
                Assert.That(ui.MainStageKillProgress, Is.EqualTo(pair.Item2));
                refill.Invoke(game, null); Assert.That(game.BossActive, Is.True);
                DefeatActualServiceEnemies(1);
                Assert.That(ui.MainStage, Is.EqualTo(pair.Item1));
                Assert.That(ui.MainStageKillProgress, Is.Zero);
                refill.Invoke(game, null);
            }
            ui.DebugSetMainStage(1); ui.ToggleBreakthroughMode();
            Assert.That(ui.MainStageKillGoal, Is.EqualTo(50));
            DefeatActualServiceEnemies(49);
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(49));
            Assert.That(ui.MainBossPending, Is.False);
            DefeatActualServiceEnemies(1);
            Assert.That(ui.MainStageKillProgress, Is.Zero);
            Assert.That(ui.MainStage, Is.Zero);
            DefeatActualServiceEnemies(40); ui.ToggleBreakthroughMode();
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(20));
            Assert.That(ui.MainBossPending, Is.True);
            refill.Invoke(game, null); Assert.That(game.BossActive, Is.True);
            ui.ToggleBreakthroughMode();
            Assert.That(ui.MainStageKillProgress, Is.Zero);
            Assert.That(ui.MainStage, Is.Zero);
            Assert.That(ui.MainBossPending, Is.False);
            ReloadPersistedServices();
            Assert.That(ui.BreakthroughMode, Is.False);
            Assert.That(ui.MainStageKillGoal, Is.EqualTo(50));
            yield return null;
        }

        [UnityTest]
        public IEnumerator AscensionFireTornadoAndPalmUseFramesAndPalmImprintsWithoutFireParticles()
        {
            AscensionTargets(); CastCatalogSkill("FireTornado");
            var tornado = NamedArt("FireTornado").Single(); var first = tornado.sprite;
            yield return PhysicsTicks(6);
            Assert.That(tornado.sprite, Is.Not.SameAs(first));
            Assert.That(tornado.sprite.pixelsPerUnit, Is.EqualTo(first.pixelsPerUnit));
            Assert.That(Particles("Meteor Fire Trail Particle System").particleCount, Is.Zero);
            CastCatalogSkill("GodHand"); yield return PhysicsTicks(15);
            Assert.That(NamedArt("Divine palm afterimage"), Is.Not.Empty);
            Assert.That(NamedArt("Divine palm afterimage").All(x=>x.sprite.texture.name=="SkillSkyPalm"),Is.True);
            Object.Destroy(CaptureFrame("ascension-sky-palm-afterimages.png",1200,1000,false));
            Assert.That(Particles("Meteor Fire Trail Particle System").particleCount, Is.Zero);
            yield return PhysicsTicks(30);
            var imprints = NamedArt("Divine palm ground imprint");
            Assert.That(imprints, Is.Not.Empty);
            Assert.That(imprints.All(x => x.sprite == DoodleExpansionArt.Get("SkillPalmCrater")), Is.True);
            Assert.That(imprints.All(x => x.color.r == 1 && x.color.g == 1 && x.color.b == 1 && x.sortingOrder < 0), Is.True,
                "The ground uses the recessed earth artwork in its original colors below actors.");
            Assert.That(NamedArt("Meteor impact crater"), Is.Empty);
            Assert.That(Particles("Meteor Explosion Particle System").particleCount, Is.Zero);
            Object.Destroy(CaptureFrame("ascension-palm-imprint-and-fire-tornado.png", 1200, 1000, false));
            yield return PhysicsTicks(115);
            var ground = NamedArt("Divine palm ground imprint");
            Assert.That(ground.Length, Is.EqualTo(3));
            var positions = ground.Select(x => x.transform.position).ToArray();
            foreach (var body in EnemyBodies()) Place(body,new Vector2(20,20));
            yield return PhysicsTicks(15);
            for (int i=0;i<ground.Length;i++) Assert.That(ground[i].transform.position,Is.EqualTo(positions[i]));
            Object.Destroy(CaptureFrame("ascension-palm-shaped-crater.png",1200,1000,false));
        }

        [UnityTest]
        public IEnumerator AscensionFoamRollersRollBroadsideWithConstantLength()
        {
            var bodies = DurableSkillTargets();
            foreach (var body in bodies) Place(body,new Vector2(20,20));
            Place(bodies[0],new Vector2(8,0));
            CastCatalogSkill("CactusRage");
            Assert.That(game.Ui.Items("Skill").Single(x=>x.ability=="CactusRage").name,Is.EqualTo("폼롤러의 분노"));
            var plants = NamedArt("Ascension_2 variant projectile");
            Assert.That(plants.Length,Is.EqualTo(4));
            var initial = plants.Select(x=>x.transform.position).ToArray();
            foreach (var plant in plants) {
                Assert.That(plant.sprite.texture.name,Is.EqualTo("SkillFoamRoller"));
                Assert.That(plant.sprite.rect.height,Is.GreaterThan(plant.sprite.rect.width*2));
            }
            yield return PhysicsTicks(8);
            for (int frame=0;frame<2;frame++) {
                for (int i=0;i<plants.Length;i++) {
                    Vector2 travel = plants[i].transform.position-initial[i];
                    Assert.That(Mathf.Abs(Vector2.Dot(travel.normalized,plants[i].transform.up)),Is.LessThan(.22f),
                        "The long body stays broadside to travel instead of flying tip-first.");
                    Assert.That(plants[i].transform.localScale.y,Is.EqualTo(4.6f).Within(.001));
                    Assert.That(plants[i].transform.localScale.x,Is.LessThan(4.5f),"Only the short axis compresses while rolling.");
                }
                Object.Destroy(CaptureFrame("ascension-foam-roller-broadside-"+frame+".png",1200,1000,false));
                yield return PhysicsTicks(20);
            }
        }

        [UnityTest]
        public IEnumerator AscensionRazorShurikensRotateOncePerSecond()
        {
            DurableSkillTargets(); CastCatalogSkill("RazorShuriken");
            Assert.That(game.Ui.Items("Skill").Single(x=>x.ability=="RazorShuriken").name,Is.EqualTo("벚꽃 표창"));
            var blades=NamedArt("Ascension_4 variant projectile");
            Assert.That(blades.Length,Is.EqualTo(18));
            Assert.That(blades.All(x=>x.sprite.texture.name=="SkillCherryShuriken"),Is.True);
            var rotations=blades.Select(x=>x.transform.rotation).ToArray();
            yield return PhysicsTicks(10);
            for(int i=0;i<blades.Length;i++)
                Assert.That(Quaternion.Angle(rotations[i],blades[i].transform.rotation),Is.EqualTo(72).Within(.5f));
        }

        [UnityTest]
        public IEnumerator AscensionSkillsRenderSeparatelyBesideTheExistingPlayer()
        {
            foreach (var ability in new[] { "BladeRing", "FireGolem", "CactusRage", "FireTornado", "RazorShuriken", "MightyDragon", "GodHand", "MissileRage", "SawSnakes", "SolarVolley" }) {
                game.ResetGame(); yield return null;
                var bodies = DurableSkillTargets();
                for (int i = 0; i < 4; i++) {
                    Place(bodies[i], new Vector2(3.5f + i % 2, -.75f + i / 2 * 1.5f));
                    SetTargetHealth(bodies[i], 1e15f);
                }
                Assert.That(CastCatalogSkill(ability), Is.True);
                yield return PhysicsTicks(25);
                var camera = Camera.main; float size = camera.orthographicSize; Vector3 position = camera.transform.position;
                try {
                    Object.Destroy(CaptureFrame("ascension-skill-" + ability + ".png", 1200, 1000, false,
                        () => { camera.orthographicSize = 6; camera.transform.position = new Vector3(1.5f, 0, position.z); }));
                } finally { camera.orthographicSize = size; camera.transform.position = position; }
            }
        }

        [UnityTest]
        public IEnumerator AscensionRevisedCompanionsUsePoisonSprayBoatGoat()
        {
            AscensionTargets(); var ui = game.Ui;
            CollectionAssert.AreEqual(new[] { "황금톱니바퀴", "빗살무늬토기", "청동검", "전갈", "불나방", "boat", "GOAT", "태양" },
                ui.Items("Companion").Where(x => x.rarity >= 6).Select(x => x.name));
            foreach (var item in ui.Items("Companion")) item.equipped = false;
            var scorpion = ui.Items("Companion").Single(x => x.id == "companion_scorpion");
            ui.AddItem(scorpion, 1); scorpion.equipped = true; scorpion.slot = 0; game.companionsEnabled = true;
            yield return PhysicsTicks(27);
            var drops = NamedArt("Companion shot: companion_scorpion");
            Assert.That(game.CompanionShotCount(scorpion.id), Is.EqualTo(5));
            Assert.That(drops.Length, Is.EqualTo(5));
            Assert.That(drops.All(x => x.sprite.texture.name == "AscensionRevisions" && x.sprite.rect == UiKit.Art(scorpion.projectile).rect), Is.True);
            var angles = drops.Select(x => x.transform.eulerAngles.z).OrderBy(x => x).ToArray();
            Assert.That(angles.Distinct().Count(), Is.EqualTo(5), "Poison fans out instead of throwing a tail.");
            Assert.That(NamedArt("Scorpion poison spray"), Is.Not.Empty);
            Object.Destroy(CaptureFrame("ascension-scorpion-poison-spray.png", 1000, 1000, false));
            Assert.That(UiKit.Art("AscensionShot_5").name, Is.EqualTo("AscensionRevision_6"));
            Assert.That(UiKit.Art("AscensionShot_7").name, Is.EqualTo("AscensionRevision_7"));
            for (int pose = 0; pose < 2; pose++) {
                Assert.That(DoodleCollectionArt.CompanionFrame(29, pose).name, Is.EqualTo("AscensionRevision_" + pose));
                Assert.That(DoodleCollectionArt.CompanionFrame(31, pose).name, Is.EqualTo("AscensionRevision_" + (2 + pose)));
            }
        }

        [UnityTest]
        public IEnumerator AscensionOriginSaveKeepsOldGodOwnershipLevelAndEquipment()
        {
            game.TogglePause();
            string saved = PlayerPrefs.GetString("DoodleUi.Collections.v1", "");
            var host = new GameObject("Origin migration fixture");
            try {
                PlayerPrefs.SetString("DoodleUi.Collections.v1", "{\"version\":3,\"items\":[{\"id\":\"armor_grade6_1\",\"count\":27,\"level\":500,\"discovered\":true,\"equipped\":true}],\"stats\":[]}");
                var ui = host.AddComponent<DoodleUi>(); ui.InitCollections();
                var origin = ui.Items("Armor").Single(x => x.id == "armor_grade6_1");
                Assert.That(origin.name, Is.EqualTo("근원1 갑옷"));
                Assert.That(origin.rarity, Is.EqualTo(6));
                Assert.That(origin.level, Is.EqualTo(500));
                Assert.That(origin.count, Is.EqualTo(27));
                Assert.That(origin.equipped, Is.True);
                Assert.That(ui.SynthesisTarget(origin).id, Is.EqualTo("armor_grade6_2"));
                CollectionAssert.AreEqual(new[] { "신화", "근원", "초월", "갓" }, DoodleUi.GradeNames.Skip(5));
            }
            finally { Object.DestroyImmediate(host); PlayerPrefs.SetString("DoodleUi.Collections.v1", saved); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AscensionMissilesExplodeOnContactAndSolarVolleyFinishesTwentyOneBounces()
        {
            var bodies = DurableSkillTargets();
            var actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            var hp = actors[0].GetType().GetField("hp");
            var missile = game.Ui.Items("Skill").Single(x => x.ability == "MissileRage");
            float baseline = game.Ui.ItemHitDamage(missile) * 100;
            Place(bodies[0], new Vector2(4, 0)); Place(bodies[1], new Vector2(4, 1.8f));
            SetTargetHealth(bodies[0], baseline); SetTargetHealth(bodies[1], baseline);
            CastCatalogSkill("MissileRage");
            Assert.That(NamedArt("MissileRage projectile").Single().transform.localScale.x, Is.EqualTo(1.95f).Within(.001));
            for (int i = 0; i < 100 && game.MissileRageExplosions == 0; i++) yield return new WaitForFixedUpdate();
            Assert.That(game.MissileRageExplosions, Is.GreaterThan(0));
            Assert.That((float)hp.GetValue(actors[0]), Is.LessThan(baseline));
            Assert.That((float)hp.GetValue(actors[1]), Is.LessThan(baseline), "A real missile explosion damages the nearby enemy too.");
            Object.Destroy(CaptureFrame("ascension-missile-contact-explosion.png", 1000, 1000, false));
            game.ResetGame(); yield return null;
            bodies = DurableSkillTargets(); game.refillBelow = 0;
            actors = (IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game);
            for (int i = actors.Count - 1; i > 0; i--) {
                Object.Destroy((GameObject)actors[i].GetType().GetField("root").GetValue(actors[i])); actors.RemoveAt(i);
            }
            Place(bodies[0], new Vector2(5, 0)); SetTargetHealth(bodies[0], 1e15f);
            CastCatalogSkill("SolarVolley"); yield return PhysicsTicks(250); yield return null;
            Assert.That(game.AscensionLaunchCount("SolarVolley"), Is.EqualTo(3));
            Assert.That(game.BallsCompleted, Is.EqualTo(3));
            Assert.That(game.BallHits, Is.EqualTo(21));
            Assert.That(game.LastCompletedBallHits, Is.EqualTo(7));
            Assert.That(NamedArt("SolarVolley projectile"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AscensionVolleysHaveExactCountsTimingAndCircularDirections()
        {
            AscensionTargets();
            var times = new List<float>(); var directions = new List<Vector2>();
            System.Action<string, float, Vector2> launched = (ability, time, direction) => {
                if (ability == "BladeRing") { times.Add(time); directions.Add(direction); }
            };
            game.AscensionProjectileLaunched += launched;
            Assert.That(CastCatalogSkill("BladeRing"), Is.True);
            Assert.That(game.AscensionLaunchCount("BladeRing"), Is.EqualTo(1));
            yield return PhysicsTicks(100);
            Assert.That(times.Count, Is.EqualTo(44));
            for (int i = 1; i < times.Count; i++) {
                Assert.That(times[i] - times[i - 1], Is.InRange(.019f, .061f));
                Assert.That(Vector2.SignedAngle(directions[i - 1], directions[i]), Is.EqualTo(360f / 22).Within(.01));
            }
            for(int i=0;i<22;i++) {
                Assert.That(Vector2.Distance(directions[i],directions[i+22]),Is.LessThan(.001));
                Assert.That(times[i+22]-times[i],Is.EqualTo(.88f).Within(.025f));
            }
            game.AscensionProjectileLaunched -= launched;
            foreach (var pair in new[] { ("CactusRage", 4), ("RazorShuriken", 18), ("MissileRage", 13), ("SolarVolley", 3) }) {
                Assert.That(CastCatalogSkill(pair.Item1), Is.True);
                yield return PhysicsTicks(80);
                Assert.That(game.AscensionLaunchCount(pair.Item1), Is.EqualTo(pair.Item2), pair.Item1);
            }
            Assert.That(game.MissileRageExplosions, Is.EqualTo(13));
            game.ResetGame(); yield return null;
            Assert.That(game.AscensionLaunchCount("BladeRing"), Is.Zero);
            Assert.That(game.MissileRageExplosions, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AscensionSummonsHaveTenGolemsFiveHeadsLargerDragonAndFallingPalms()
        {
            AscensionTargets();
            CastCatalogSkill("FireGolem");
            Assert.That(NamedArt("FireGolem").Length, Is.EqualTo(10));
            CastCatalogSkill("FireTornado");
            CastCatalogSkill("SawSnakes");
            var heads = NamedArt("SawSnakes head");
            Assert.That(heads.Length, Is.EqualTo(5));
            Assert.That(heads.All(x => x.sprite.name == "AscensionSkillArt_10"), Is.True);
            CastCatalogSkill("Dragon"); CastCatalogSkill("MightyDragon");
            yield return PhysicsTicks(3);
            Assert.That(NamedArt("MightyDragon head").Single().transform.localScale.x /
                NamedArt("Dragon head").Single().transform.localScale.x, Is.EqualTo(1.7f).Within(.01));
            Assert.That(Mathf.DeltaAngle(NamedArt("MightyDragon segment 3").Single().transform.eulerAngles.z,
                NamedArt("MightyDragon wings").Single().transform.eulerAngles.z), Is.EqualTo(-90).Within(.01));
            CastCatalogSkill("GodHand");
            var palm = NamedArt("Falling divine palm").Single();
            Vector3 start = palm.transform.position;
            yield return PhysicsTicks(15);
            Assert.That(palm.transform.position.y, Is.LessThan(start.y));
            Assert.That(palm.transform.position.x, Is.EqualTo(start.x).Within(.001));
            Assert.That(palm.transform.eulerAngles.z, Is.Zero);
            Object.Destroy(CaptureFrame("ascension-summons-doodle-combat.png", 1440, 900, false));
            game.TogglePause(); start = palm.transform.position;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(palm.transform.position, Is.EqualTo(start));
            game.TogglePause();
            yield return PhysicsTicks(150);
            Assert.That(game.MeteorsLanded, Is.EqualTo(3));
            Assert.That(game.GolemHits, Is.GreaterThan(0));
            Assert.That(game.TornadoHits, Is.GreaterThan(0));
            Assert.That(game.DragonFlames, Is.GreaterThan(0));
            yield return PhysicsTicks(400);
            Assert.That(NamedArt("FireGolem"), Is.Empty);
            Assert.That(NamedArt("FireTornado"), Is.Empty);
            Assert.That(NamedArt("SawSnakes head"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AscensionAllNewCompanionsAttackAndAllNewArtRendersInCatalogs()
        {
            AscensionTargets(); var ui = game.Ui;
            ui.AddItem(ui.Items("Armor").Single(x => x.id == "armor_grade6_1"), 1);
            game.companionsEnabled = true;
            var added = ui.Items("Companion").Where(x => x.rarity >= 6).ToArray();
            Assert.That(added.Length, Is.EqualTo(8));
            for (int start = 0; start < added.Length; start += 5) {
                foreach (var item in ui.Items("Companion")) item.equipped = false;
                for (int i = 0; i < System.Math.Min(5, added.Length - start); i++) { var item = added[start + i]; ui.AddItem(item, 1); item.equipped = true; item.slot = i; }
                yield return PhysicsTicks(160);
                foreach (var item in added.Skip(start).Take(5)) Assert.That(game.CompanionShotCount(item.id), Is.GreaterThanOrEqualTo(item.volleyCount), item.name);
                Object.Destroy(CaptureFrame("ascension-companions-combat-" + start + ".png", 1440, 900, false));
            }
            Assert.That(game.CompanionExplosions, Is.GreaterThan(0));
            game.TogglePause();
            foreach (var page in new[] { "Skills", "Companions", "Equipment" }) {
                UiOpen(page); yield return null;
                var scroll = UiNode("Collection inventory").GetComponentInParent<ScrollRect>();
                if (scroll) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0; }
                yield return null;
                Object.Destroy(CaptureFrame("ascension-catalog-" + page + ".png", 720, 1520));
                ui.ClosePage();
            }
            for (int i = 0; i < 36; i++) {
                var art = DoodleAscensionArt.Cell(i);
                Assert.That(art, Is.Not.Null);
                Assert.That(art.rect.width, Is.GreaterThan(60));
                Assert.That(art.rect.height, Is.GreaterThan(60));
            }
            // Render a readable reference sheet through the real uGUI sprite/material path.
            var sheet = UiKit.Rect(UiRoot, "Ascension companion reference"); UiKit.Stretch(sheet);
            sheet.gameObject.AddComponent<Image>().color = new Color(.96f, .95f, .9f);
            for (int i = 0; i < added.Length; i++) {
                var cell = UiKit.Rect(sheet, added[i].name);
                int col = i % 2, row = i / 2;
                cell.anchorMin = new Vector2(col * .5f + .02f, .02f + (3 - row) * .24f);
                cell.anchorMax = new Vector2((col + 1) * .5f - .02f, .02f + (4 - row) * .24f);
                cell.offsetMin = cell.offsetMax = Vector2.zero;
                var name = UiKit.Text(cell, added[i].name, 25, TextAnchor.UpperCenter, 35);
                name.rectTransform.anchorMin = new Vector2(0, .83f); name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
                for (int pose = 0; pose < 2; pose++) {
                    var sprite = UiKit.Icon(cell, added[i].icon, 90);
                    sprite.sprite = DoodleCollectionArt.CompanionFrame(DoodleCollectionArt.CompanionIndex(added[i].icon), pose);
                    sprite.rectTransform.anchorMin = new Vector2(.03f + pose * .32f, .12f);
                    sprite.rectTransform.anchorMax = new Vector2(.32f + pose * .32f, .8f);
                    sprite.rectTransform.offsetMin = sprite.rectTransform.offsetMax = Vector2.zero;
                }
                var shot = UiKit.Icon(cell, added[i].projectile, 70);
                shot.rectTransform.anchorMin = new Vector2(.71f, .18f); shot.rectTransform.anchorMax = new Vector2(.97f, .72f);
                shot.rectTransform.offsetMin = shot.rectTransform.offsetMax = Vector2.zero;
            }
            Object.Destroy(CaptureFrame("ascension-companions-and-projectiles.png", 900, 1520));
            Object.Destroy(sheet.gameObject);
        }

        [UnityTest]
        public IEnumerator AscensionVolleysCancelOnUnequipAndResetWithoutDelayedProjectiles()
        {
            AscensionTargets();
            foreach (var item in game.Ui.Items("Skill")) item.equipped = false;
            var missile = game.Ui.Items("Skill").Single(x => x.ability == "MissileRage");
            game.Ui.AddItem(missile, 1); missile.equipped = true; missile.slot = 0;
            game.summonSkillsEnabled = true;
            yield return PhysicsTicks(30);
            Assert.That(game.AscensionLaunchCount("MissileRage"), Is.InRange(1, 3));
            missile.equipped = false;
            int launched = game.AscensionLaunchCount("MissileRage");
            yield return PhysicsTicks(80);
            Assert.That(game.AscensionLaunchCount("MissileRage"), Is.EqualTo(launched));
            game.ResetGame(); yield return null;
            Assert.That(game.AscensionLaunchCount("MissileRage"), Is.Zero);
        }
    }
}
