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
                Place(bodies[i], new Vector2(3 + i % 8 * .4f, -1.4f + i / 8 * .4f));
                SetTargetHealth(bodies[i], 1e15f);
            }
        }

        [UnityTest]
        public IEnumerator AscensionSkillsRenderSeparatelyBesideTheExistingPlayer()
        {
            foreach (var ability in new[] { "BladeRing", "FireGolem", "CactusRage", "FireTornado", "RazorShuriken", "MightyDragon", "GodHand", "MissileRage", "ChimeraBrothers", "SolarVolley" }) {
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
        public IEnumerator AscensionRevisedCompanionsUsePoisonSprayBoatGoatAndSingleEarth()
        {
            AscensionTargets(); var ui = game.Ui;
            CollectionAssert.AreEqual(new[] { "황금톱니바퀴", "빗살무늬토기", "청동검", "전갈", "불나방", "boat", "키메라", "GOAT", "태양", "지구" },
                ui.Items("Companion").Where(x => x.rarity >= 6).Select(x => x.name));
            foreach (var item in ui.Items("Companion")) item.equipped = false;
            var scorpion = ui.Items("Companion").Single(x => x.id == "companion_scorpion");
            ui.AddItem(scorpion, 1); scorpion.equipped = true; scorpion.slot = 0; game.companionsEnabled = true;
            yield return PhysicsTicks(27);
            var drops = NamedArt("Companion shot: companion_scorpion");
            Assert.That(game.CompanionShotCount(scorpion.id), Is.EqualTo(5));
            Assert.That(drops.Length, Is.EqualTo(5));
            Assert.That(drops.All(x => x.sprite.name == "AscensionRevision_4"), Is.True);
            var angles = drops.Select(x => x.transform.eulerAngles.z).OrderBy(x => x).ToArray();
            Assert.That(angles.Distinct().Count(), Is.EqualTo(5), "Poison fans out instead of throwing a tail.");
            Assert.That(NamedArt("Scorpion poison spray"), Is.Not.Empty);
            Object.Destroy(CaptureFrame("ascension-scorpion-poison-spray.png", 1000, 1000, false));
            Assert.That(UiKit.Art("AscensionShot_9").name, Is.EqualTo("AscensionRevision_5"));
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
            yield return PhysicsTicks(70);
            Assert.That(times.Count, Is.EqualTo(12));
            for (int i = 1; i < times.Count; i++) {
                Assert.That(times[i] - times[i - 1], Is.InRange(.079f, .121f));
                Assert.That(Vector2.SignedAngle(directions[i - 1], directions[i]), Is.EqualTo(30).Within(.01));
            }
            game.AscensionProjectileLaunched -= launched;
            foreach (var pair in new[] { ("CactusRage", 4), ("RazorShuriken", 8), ("MissileRage", 13), ("SolarVolley", 3) }) {
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
        public IEnumerator AscensionSummonsHaveTenGolemsThreeHeadsLargerDragonAndFallingPalms()
        {
            AscensionTargets();
            CastCatalogSkill("FireGolem");
            Assert.That(NamedArt("FireGolem").Length, Is.EqualTo(10));
            CastCatalogSkill("FireTornado");
            CastCatalogSkill("ChimeraBrothers");
            var heads = NamedArt("ChimeraBrothers head");
            Assert.That(heads.Length, Is.EqualTo(3));
            CollectionAssert.AreEquivalent(new[] { "Ascension_8", "Ascension_9", "Ascension_10" }, heads.Select(x => x.sprite.name));
            CastCatalogSkill("Dragon"); CastCatalogSkill("MightyDragon");
            yield return PhysicsTicks(3);
            Assert.That(NamedArt("MightyDragon head").Single().transform.localScale.x /
                NamedArt("Dragon head").Single().transform.localScale.x, Is.EqualTo(1.7f).Within(.01));
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
            Assert.That(NamedArt("ChimeraBrothers head"), Is.Empty);
        }

        [UnityTest]
        public IEnumerator AscensionAllNewCompanionsAttackAndAllNewArtRendersInCatalogs()
        {
            AscensionTargets(); var ui = game.Ui;
            ui.AddItem(ui.Items("Armor").Single(x => x.id == "armor_grade6_1"), 1);
            game.companionsEnabled = true;
            var added = ui.Items("Companion").Where(x => x.rarity >= 6).ToArray();
            Assert.That(added.Length, Is.EqualTo(10));
            for (int start = 0; start < 10; start += 5) {
                foreach (var item in ui.Items("Companion")) item.equipped = false;
                for (int i = 0; i < 5; i++) { var item = added[start + i]; ui.AddItem(item, 1); item.equipped = true; item.slot = i; }
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
                cell.anchorMin = new Vector2(col * .5f + .02f, .02f + (4 - row) * .194f);
                cell.anchorMax = new Vector2((col + 1) * .5f - .02f, .02f + (5 - row) * .194f);
                cell.offsetMin = cell.offsetMax = Vector2.zero;
                var name = UiKit.Text(cell, added[i].name, 25, TextAnchor.UpperCenter, 35);
                name.rectTransform.anchorMin = new Vector2(0, .83f); name.rectTransform.anchorMax = Vector2.one;
                name.rectTransform.offsetMin = name.rectTransform.offsetMax = Vector2.zero;
                for (int pose = 0; pose < 2; pose++) {
                    var sprite = UiKit.Icon(cell, added[i].icon, 90);
                    sprite.sprite = DoodleCollectionArt.CompanionFrame(24 + i, pose);
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
