using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        void TravelTick(string method,float dt) => typeof(DoodleIdleGame).GetMethod(method,GrowthPrivate).Invoke(game,new object[]{dt});
        [UnityTest]
        public IEnumerator SkillTravelDoublesStraightHomingSpiralAndOrbitMotion()
        {
            var bodies=DurableSkillTargets();Place(bodies[0],new Vector2(6,0));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cucumber);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.GuardianSword);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.FireRing);
            game.CastVariant("Eggplant");game.CastVariant("Shuriken");
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.SoundWave);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.WaveSnakes);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.TetherSnake);
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Worm);
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.BouncyBall);
            Assert.That(CastCatalogSkill("Tornado"),Is.True);Assert.That(CastCatalogSkill("Golem"),Is.True);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.OrbitGun);
            game.TogglePause();
            float bananaStart=(float)typeof(DoodleIdleGame).GetField("orbitAngle",GrowthPrivate).GetValue(game);
            Vector2 guardianStart=NamedArt("GuardianSword moving skill").Single().transform.position;
            var golems=NamedArt("Summoned golem");var starts=golems.Select(x=>(Vector2)x.transform.position).ToArray();
            typeof(DoodleIdleGame).GetMethod("SnapshotSummonTargets",GrowthPrivate).Invoke(game,null);
            TravelTick("TickMovingSkills",.1f);TravelTick("TickVariants",.1f);TravelTick("TickAreaSkills",.1f);
            TravelTick("TickSnakes",.1f);TravelTick("UpdateWorms",.1f);TravelTick("UpdateExtraShots",.1f);
            TravelTick("TickExpansionSkills",.1f);TravelTick("TickOrbitGun",.1f);TravelTick("OrbitBananas",.1f);
            Assert.That(NamedArt("Cucumber moving skill").Single().transform.position.x,Is.EqualTo(1.2f).Within(.001));
            Assert.That(Vector2.Distance(guardianStart,NamedArt("GuardianSword moving skill").Single().transform.position),Is.EqualTo(2.4f).Within(.001));
            foreach(var ring in NamedArt("FireRing moving skill"))Assert.That(((Vector2)ring.transform.position).magnitude,Is.EqualTo(.9f).Within(.001));
            foreach(var shot in NamedArt("Cucumber variant projectile"))Assert.That(((Vector2)shot.transform.position).magnitude,Is.EqualTo(1.2f).Within(.001));
            foreach(var shot in NamedArt("Shuriken variant projectile"))Assert.That(((Vector2)shot.transform.position).magnitude,Is.EqualTo(1.4f).Within(.001));
            Assert.That(NamedArt("Traveling sound wave").First().transform.position.x,Is.EqualTo(1.4f).Within(.001));
            Assert.That(NamedArt("TetherSnake head").Single().transform.position.x,Is.EqualTo(1.8f).Within(.001));
            var forwardSnake=NamedArt("WaveSnakes head").OrderByDescending(x=>x.transform.position.x).First();
            Assert.That(forwardSnake.transform.position.x,Is.EqualTo(.9f).Within(.001));
            Assert.That(((Vector2)NamedArt("Spiral worm head").Single().transform.position).magnitude,Is.EqualTo(.25f).Within(.001));
            Assert.That(NamedArt("Ball skill projectile").Single().transform.position.x,Is.EqualTo(3).Within(.001));
            Assert.That(NamedArt("Homing tornado").Single().transform.position.x,Is.EqualTo(2.4f).Within(.001));
            for(int i=0;i<golems.Length;i++)Assert.That(Vector2.Distance(starts[i],golems[i].transform.position),Is.EqualTo(.9f).Within(.001));
            var gun=NamedArt("Orbiting automatic gun").Single().transform.position;
            Assert.That(Mathf.Atan2(gun.y,gun.x),Is.EqualTo(.33f).Within(.001));
            Assert.That((float)typeof(DoodleIdleGame).GetField("orbitAngle",GrowthPrivate).GetValue(game)-bananaStart,Is.EqualTo(.42f).Within(.001));
            yield return null;
        }
        [UnityTest]
        public IEnumerator ThrownSkillsLandInHalfTimeWithoutAcceleratingVolleysOrFireDuration()
        {
            var bodies=DurableSkillTargets();Place(bodies[0],new Vector2(6,0));
            Assert.That(CastCatalogSkill("Stone"),Is.True);
            game.CastVariant("BrickVolley");game.CastVariant("Dumbbell");
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Molotov);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cannon);
            Assert.That(CastCatalogSkill("Meteor"),Is.True);
            game.TogglePause();
            TravelTick("TickTurrets",.01f);
            TravelTick("UpdateShots",.324f);Assert.That(NamedArt("Parabolic stone").Length,Is.EqualTo(1));
            TravelTick("UpdateShots",.002f);Assert.That(NamedArt("Parabolic stone").Length,Is.Zero);
            TravelTick("TickVariants",.399f);Assert.That(NamedArt("Brick variant projectile").Length,Is.GreaterThan(0));
            TravelTick("TickVariants",.002f);Assert.That(NamedArt("Brick variant projectile").Length,Is.Zero);Assert.That(NamedArt("SkillDumbbell variant projectile").Length,Is.Zero);
            TravelTick("TickAreaSkills",.399f);Assert.That(game.ActiveFireZones,Is.Zero);
            TravelTick("TickAreaSkills",.002f);Assert.That(game.ActiveFireZones,Is.EqualTo(1));
            TravelTick("TickMovingSkills",.399f);Assert.That(game.CannonExplosions,Is.Zero);
            TravelTick("TickMovingSkills",.002f);Assert.That(game.CannonExplosions,Is.EqualTo(1));Assert.That(game.ActiveCannons,Is.EqualTo(1));
            TravelTick("TickExpansionSkills",.424f);Assert.That(game.MeteorsLanded,Is.Zero);
            TravelTick("TickExpansionSkills",.002f);Assert.That(game.MeteorsLanded,Is.EqualTo(1));Assert.That(game.MeteorsLaunched,Is.EqualTo(1));
            TravelTick("TickExpansionSkills",.574f);Assert.That(game.MeteorsLaunched,Is.EqualTo(2),"One-second volley spacing is unchanged.");
            TravelTick("TickAreaSkills",3.9f);Assert.That(game.ActiveFireZones,Is.EqualTo(1));
            TravelTick("TickAreaSkills",.11f);Assert.That(game.ActiveFireZones,Is.Zero);
            yield return null;
        }
    }
}
