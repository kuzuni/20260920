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
