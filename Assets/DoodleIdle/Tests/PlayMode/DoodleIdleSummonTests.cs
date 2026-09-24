using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        Rigidbody2D PlayerBody() => game.GetComponentsInChildren<Rigidbody2D>().Single(b => b.name.StartsWith("Player -"));
        static void Place(Rigidbody2D body, Vector2 position)
        {
            // Disabled Rigidbody2D objects do not synchronize their Transform automatically.
            // Keep both representations aligned so pause/resume and CI renders use the same fixture.
            body.transform.position = position;
            body.position = position;
        }
        Rigidbody2D[] IsolateSummonTest()
        {
            game.basicSkillsEnabled = game.extraSkillsEnabled = game.summonSkillsEnabled = game.autoPlay = false;
            game.enemyDashEnabled = false;
            game.moveSpeed = 0;
            PlayerBody().position = Vector2.zero;
            PlayerBody().linearVelocity = Vector2.zero;
            var bodies = EnemyBodies();
            for (int i = 0; i < bodies.Length; i++)
            {
                bodies[i].simulated = false;
                Place(bodies[i], new Vector2(-16 + i % 8 * 1.2f, -10 + i / 8 * 1.2f));
            }
            return bodies;
        }
        IEnumerator PhysicsTicks(int count) { for (int i = 0; i < count; i++) yield return new WaitForFixedUpdate(); }
        void SetTargetHealth(Rigidbody2D body, float health)
        {
            var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            var actor=actors.Cast<object>().Single(x=>(Rigidbody2D)x.GetType().GetField("body").GetValue(x)==body);
            actor.GetType().GetField("hp").SetValue(actor, (GameNumber)(health));
            actor.GetType().GetField("maxHp").SetValue(actor, (GameNumber)(health));
        }
        SpriteRenderer[] NamedArt(string name) => game.GetComponentsInChildren<SpriteRenderer>().Where(r => r.name == name).ToArray();

        [UnityTest]
        public IEnumerator CloudsPursueMovingEnemiesAtTheirNewSpeeds()
        {
            var bodies=IsolateSummonTest();
            foreach(var body in bodies)Place(body,new Vector2(20,20));
            Place(bodies[0],new Vector2(6,0));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.StormCloud);game.CastVariant("RedCloud");
            var normal=NamedArt("Drifting storm cloud").Single();var red=NamedArt("Red storm cloud").Single();
            Vector3 origin=normal.transform.position;
            yield return PhysicsTicks(20);
            Assert.That(Vector3.Distance(normal.transform.position,origin),Is.EqualTo(2.2f*.4f).Within(.03f));
            Assert.That(Vector3.Distance(red.transform.position,origin),Is.EqualTo(3f*.4f).Within(.03f));
            Vector3 previous=normal.transform.position;
            Place(bodies[0],new Vector2(-6,4));
            yield return PhysicsTicks(20);
            Assert.That(normal.transform.position.x,Is.LessThan(previous.x));
            Assert.That(normal.transform.position.y,Is.GreaterThan(previous.y),"Clouds steer toward a moving enemy instead of keeping the cast direction.");
            Object.Destroy(CaptureFrame("revised-seeking-clouds.png",1440,900,false));
            game.TogglePause();previous=normal.transform.position;
            yield return new WaitForSecondsRealtime(.1f);
            Assert.That(normal.transform.position,Is.EqualTo(previous));
        }

        [UnityTest]
        public IEnumerator FireCurvesTowardThreeTargetsAndStillHitsOncePerFlame()
        {
            var bodies=IsolateSummonTest();
            foreach(var body in bodies)Place(body,new Vector2(20,20));
            Place(bodies[0],new Vector2(8,0));Place(bodies[1],new Vector2(10,3));Place(bodies[2],new Vector2(10,-3));
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.Fire);
            var flames=NamedArt("Fire skill projectile");Assert.That(flames.Length,Is.EqualTo(3));
            yield return PhysicsTicks(10);
            Assert.That(flames[0].transform.position.x,Is.GreaterThan(0).And.LessThan(8));
            Assert.That(flames[0].transform.position.y,Is.GreaterThan(.6f),"A stationary target straight ahead must produce a visible curved path.");
            Camera.main.transform.position=new Vector3(3,0,-10);Camera.main.orthographicSize=6;
            Object.Destroy(CaptureFrame("revised-curved-fire.png",1440,900,false));
            Place(bodies[0],new Vector2(8,2));
            yield return PhysicsTicks(70);
            Assert.That(game.FireHits,Is.EqualTo(3));
            Assert.That(NamedArt("Fire skill projectile"),Is.Empty);
        }

        [UnityTest]
        public IEnumerator PurpleFireArrowsPierceEveryTouchedEnemyWithoutHomingAndKeepWeakWaveTrail()
        {
            var bodies=DurableSkillTargets();
            Place(bodies[0],new Vector2(3,0));
            Place(bodies[1],new Vector2(6,0));
            Place(bodies[2],new Vector2(9,0));
            Place(bodies[3],new Vector2(6,3));
            var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            float Hp(int index) => (float)(GameNumber)actors[index].GetType().GetField("hp").GetValue(actors[index]);
            float untouched=Hp(3);
            var times=new List<float>();game.SkillProjectileLaunched+=(skill,time,id)=>{if(skill==DoodleIdleGame.ExtraSkill.Arrows)times.Add(time);};
            game.CastVariant("PurpleFireArrows");
            var first=NamedArt("PurpleFireArrows projectile").Single();
            float previousY=0,lastSign=0;int reversals=0;
            for(int i=0;i<25;i++) {
                yield return new WaitForFixedUpdate();
                float y=first.transform.position.y,delta=y-previousY;previousY=y;
                Assert.That(Mathf.Abs(y),Is.LessThanOrEqualTo(.201f));
                if(Mathf.Abs(delta)<.005f)continue;
                float sign=Mathf.Sign(delta);if(lastSign!=0 && sign!=lastSign)reversals++;lastSign=sign;
            }
            Assert.That(reversals,Is.GreaterThanOrEqualTo(2));
            Assert.That(first.transform.localScale.x,Is.EqualTo(2.1f).Within(.001f));
            Assert.That(Particles("Purple Arrow Fire Trail Particle System").particleCount,Is.GreaterThan(0));
            Camera.main.transform.position=new Vector3(4,0,-10);Camera.main.orthographicSize=5;
            Object.Destroy(CaptureFrame("revised-weaving-purple-fire.png",1440,900,false));
            yield return PhysicsTicks(65);
            Assert.That(first.transform.position.x,Is.GreaterThan(18),"The arrow continues past targets without steering back.");
            Assert.That(game.ArrowsLaunched,Is.EqualTo(8));
            Assert.That(times.Count,Is.EqualTo(8));
            for(int i=1;i<times.Count;i++)Assert.That(times[i]-times[i-1],Is.EqualTo(.1f).Within(.025f));
            var shots=(IList)typeof(DoodleIdleGame).GetField("extraShots",GrowthPrivate).GetValue(game);
            for(int i=0;i<8;i++) {
                var direction=(Vector2)shots[i].GetType().GetField("waveDirection").GetValue(shots[i]);
                Assert.That(Mathf.DeltaAngle(Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg,i*45),Is.EqualTo(0).Within(.01f));
            }
            Assert.That(game.ArrowHits,Is.EqualTo(3),"Only the rightward arrow hits these three collinear enemies; seven other arrows spread outward.");
            for(int i=0;i<3;i++)Assert.That(Hp(i),Is.LessThan(100000));
            Assert.That(Hp(3),Is.EqualTo(untouched),"An off-path enemy is not a homing target.");
            yield return PhysicsTicks(80);yield return null;
            Assert.That(NamedArt("PurpleFireArrows projectile"),Is.Empty);
        }

        [UnityTest]
        public IEnumerator EggplantRollsLikeCucumberDurianFiresThreeAndShurikenSpreadsRadially()
        {
            var bodies=IsolateSummonTest();
            foreach(var body in bodies)Place(body,new Vector2(20,20));
            Place(bodies[0],new Vector2(8,0));
            Assert.That(game.Ui.Items("Skill").Single(x=>x.id=="eggplant").name,Is.EqualTo("오이 분노"));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cucumber);game.CastVariant("Eggplant");
            var cucumber=NamedArt("Cucumber moving skill").Single();
            var cucumbers=NamedArt("Cucumber variant projectile");Assert.That(cucumbers.Length,Is.EqualTo(2));
            Assert.That(cucumbers.All(p=>p.sprite.texture==cucumber.sprite.texture),Is.True,"Both use the original cucumber artwork.");
            var times=new List<float>();game.SkillProjectileLaunched+=(skill,time,id)=>{if(skill==DoodleIdleGame.ExtraSkill.BouncyBall)times.Add(time);};
            game.CastVariant("Durian");game.CastVariant("Shuriken");
            Assert.That(NamedArt("Shuriken variant projectile").Length,Is.EqualTo(8),"All radial shots launch together.");
            yield return PhysicsTicks(10);
            foreach(var plant in cucumbers) {
                Assert.That(plant.transform.position.magnitude,Is.EqualTo(cucumber.transform.position.magnitude).Within(.001f));
                Assert.That(Quaternion.Angle(plant.transform.rotation,cucumber.transform.rotation),Is.EqualTo(15).Within(.01f));
                Assert.That(Vector3.Distance(plant.transform.localScale,cucumber.transform.localScale),Is.LessThan(.001f));
            }
            var angles=NamedArt("Shuriken variant projectile").Select(p=>Mathf.Repeat(Mathf.Atan2(p.transform.position.y,p.transform.position.x)*Mathf.Rad2Deg,360)).OrderBy(a=>a).ToArray();
            for(int i=0;i<8;i++)Assert.That(Mathf.Repeat(angles[(i+1)%8]-angles[i],360),Is.EqualTo(45).Within(.02f));
            Assert.That(NamedArt("Shuriken afterimage").Length,Is.GreaterThan(8));
            Object.Destroy(CaptureFrame("revised-rolling-eggplant-radial-shuriken.png",1440,900,false));
            yield return PhysicsTicks(12);
            Assert.That(times.Count,Is.EqualTo(3));
            Assert.That(times[1]-times[0],Is.EqualTo(.2f).Within(.025f));Assert.That(times[2]-times[1],Is.EqualTo(.2f).Within(.025f));
            game.ResetGame();yield return null;
            Assert.That(NamedArt("Shuriken afterimage"),Is.Empty);
        }

        [UnityTest]
        public IEnumerator ThreeDuriansFinishAllSevenHitsAgainstALoneBoss()
        {
            var bodies=DurableSkillTargets();game.refillBelow=0;
            var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            for(int i=actors.Count-1;i>0;i--) {
                Object.Destroy((GameObject)actors[i].GetType().GetField("root").GetValue(actors[i]));actors.RemoveAt(i);
            }
            Place(bodies[0],new Vector2(5,0));
            var hitTimes=new List<float>();var victims=new List<int>();
            game.BallEnemyHit+=(id,count)=>{victims.Add(id);hitTimes.Add(Time.fixedTime);};
            game.CastVariant("Durian");
            yield return PhysicsTicks(250);yield return null;
            Assert.That(game.EnemyCount,Is.EqualTo(1));
            Assert.That(game.BallsCompleted,Is.EqualTo(3));Assert.That(game.LastCompletedBallHits,Is.EqualTo(7));
            Assert.That(victims.Count,Is.EqualTo(21));Assert.That(victims.Distinct().Count(),Is.EqualTo(1));
            Assert.That(hitTimes.Last()-hitTimes.First(),Is.GreaterThan(1),"Remaining hits have a visible rebound cadence, not one-frame duplicate damage.");
            Assert.That(NamedArt("Durian projectile"),Is.Empty);
        }

        [UnityTest]
        public IEnumerator ShotgunFiresTwentyPelletsAndCucumberPiercesWithoutTurning()
        {
            var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(3, 0));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Shotgun);
            Assert.That(game.ShotgunPellets, Is.EqualTo(20));
            Assert.That(NamedArt("Shotgun moving skill").Length, Is.EqualTo(20));
            Assert.That(NamedArt("Shotgun moving skill").Select(r => r.transform.eulerAngles.z).Distinct().Count(), Is.EqualTo(20));
            Assert.That(NamedArt("Shotgun moving skill").All(r=>Mathf.Abs(r.transform.localScale.x-.44f)<.001f),Is.True);
            yield return PhysicsTicks(5);
            Object.Destroy(CaptureFrame("revised-shotgun-pellets.png",1440,900,false));
            yield return PhysicsTicks(40);
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.Shotgun), Is.GreaterThan(0));
            game.ResetGame(); yield return null;
            bodies = IsolateSummonTest();
            for (int i = 0; i < 3; i++) Place(bodies[i], new Vector2(3 + i * 2, 0));
            var victims = new List<int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.Cucumber) victims.Add(id); };
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cucumber);
            var cucumber = NamedArt("Cucumber moving skill").Single();
            Assert.That(cucumber.transform.localScale.x, Is.EqualTo(4.6f));
            yield return PhysicsTicks(70);
            Assert.That(cucumber.transform.position.y, Is.EqualTo(0).Within(.02f));
            Assert.That(cucumber.transform.position.x, Is.GreaterThan(7));
            Assert.That(victims.Distinct().Count(), Is.GreaterThanOrEqualTo(3));
            Assert.That(victims.Count, Is.EqualTo(victims.Distinct().Count()), "A passing cucumber hits each enemy once.");
        }

        [UnityTest]
        public IEnumerator CannonStaysAtDeploymentForTenSecondsAndExplodesInAnArea()
        {
            Time.timeScale = 4;
            var bodies = IsolateSummonTest();
            Place(bodies[0], new Vector2(3, 0)); Place(bodies[1], new Vector2(3, 1.3f)); Place(bodies[2], new Vector2(4.3f, 0));
            var victims = new HashSet<int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.Cannon) victims.Add(id); };
            Vector2 origin = PlayerBody().position;
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Cannon);
            var cannon = NamedArt("Stationary ten second cannon").Single();
            PlayerBody().position = new Vector2(4, -3);
            yield return PhysicsTicks(60);
            Assert.That((Vector2)cannon.transform.position, Is.EqualTo(origin));
            Assert.That(game.CannonExplosions, Is.GreaterThan(0));
            Assert.That(victims.Count, Is.GreaterThanOrEqualTo(3), "Explosion must damage neighbours as well as its target.");
            yield return PhysicsTicks(60);
            Assert.That(game.ActiveStains, Is.GreaterThan(0));
            foreach (var stain in NamedArt("Fading black death stain"))
            {
                Assert.That(stain.color.r, Is.Zero); Assert.That(stain.color.a, Is.InRange(0f, .28f));
                Assert.That(stain.sortingOrder, Is.LessThan(-900));
            }
            yield return PhysicsTicks(360);
            Assert.That(game.ActiveCannons, Is.EqualTo(1), "Cannon should still exist before ten seconds.");
            yield return PhysicsTicks(25);
            Assert.That(game.ActiveCannons, Is.Zero);
            Assert.That(game.LastCannonLifetime, Is.InRange(10, 10.05f));
            int shots = game.CannonShots;
            yield return PhysicsTicks(80);
            Assert.That(game.CannonShots, Is.EqualTo(shots));
            yield return PhysicsTicks(350);
            Assert.That(game.ActiveStains, Is.Zero, "Death marks must fade away, not accumulate forever.");
        }

        [UnityTest]
        public IEnumerator FiveWaveSnakesEmergeAndAttachedSnakeRetargetsWhileRootFollowsPlayer()
        {
            var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(3, 0)); Place(bodies[1], new Vector2(4.5f, 0));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.WaveSnakes);
            Assert.That(NamedArt("WaveSnakes head").Length, Is.EqualTo(5));
            yield return PhysicsTicks(5);
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("WaveSnakes segment") && r.enabled), Is.LessThan(50));
            yield return PhysicsTicks(35);
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Count(r => r.name.StartsWith("WaveSnakes segment") && r.enabled), Is.EqualTo(50));
            Assert.That(NamedArt("WaveSnakes head").Select(r => r.transform.position).Distinct().Count(), Is.EqualTo(5));
            game.ResetGame(); yield return null;
            bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(2.5f, 0)); Place(bodies[1], new Vector2(4.1f, .5f));
            // Preserve a nine-hit lifetime at the live rarity coefficient so the
            // motion test can observe repeated hits followed by retargeting.
            float snakeHit=game.Ui.ItemHitDamage(game.Ui.Items("Skill").Single(x=>x.ability=="TetherSnake"));
            SetTargetHealth(bodies[0],snakeHit*8.5f);SetTargetHealth(bodies[1],snakeHit*8.5f);
            var impacts = new Dictionary<int, int>();
            game.SummonImpact += (skill, id) => { if (skill == DoodleIdleGame.SummonSkill.TetherSnake) impacts[id] = impacts.TryGetValue(id, out int count) ? count + 1 : 1; };
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.TetherSnake);
            yield return PhysicsTicks(65);
            PlayerBody().position = new Vector2(.3f, -.5f);
            yield return PhysicsTicks(2);
            var root = NamedArt("TetherSnake segment 31").Single();
            Assert.That(Vector2.Distance(root.transform.position, PlayerBody().position), Is.LessThan(.03f));
            yield return PhysicsTicks(130);
            Assert.That(impacts.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(impacts.Values.Max(), Is.GreaterThanOrEqualTo(8), "Attached snake must repeatedly hit its living target.");
            Assert.That(game.TetherRetargets, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator GuardianOnlyAttacksInRangeAndRequiredTrailsAreVisible()
        {
            var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(8, 0));
            foreach(var body in bodies)SetTargetHealth(body,10000000);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.GuardianSword);
            Assert.That(game.SummonCasts(DoodleIdleGame.SummonSkill.GuardianSword), Is.Zero);
            Place(bodies[0], new Vector2(4, 0));
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.GuardianSword);
            var sword = NamedArt("Following guardian sword").Single();
            Quaternion swordStart = sword.transform.rotation;
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.FireRing);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Sand);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.StormCloud);
            game.CastExtraSkill(DoodleIdleGame.ExtraSkill.BouncyBall);
            yield return PhysicsTicks(8);
            Assert.That(Quaternion.Angle(swordStart, sword.transform.rotation), Is.GreaterThan(60), "Guardian sweeps its blade when firing.");
            Camera.main.orthographicSize = 4;
            Object.Destroy(CaptureFrame("19-guardian-swing.png", 1440, 900, false));
            foreach (string name in new[] { "FireRing afterimage", "Lightning afterimage", "Bouncy ball afterimage" })
                Assert.That(NamedArt(name).Length, Is.GreaterThan(0), name);
            Assert.That(game.GetComponentsInChildren<ParticleSystem>().Single(p => p.name == "Sand Spray Particle System").particleCount, Is.GreaterThan(0));
            yield return PhysicsTicks(15);
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.GuardianSword), Is.GreaterThan(0));
            var shadow = NamedArt("Soft ground shadow");
            Assert.That(shadow.Length, Is.GreaterThan(20));
            Assert.That(shadow.All(s => s.color.a >= .3f && s.sortingOrder == -900), Is.True);
            game.TogglePause();
            var cloud = NamedArt("Drifting storm cloud").Single(); Vector3 cloudPosition = cloud.transform.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(cloud.transform.position, Is.EqualTo(cloudPosition));
            game.ResetGame(); yield return null;
            Assert.That(game.ActiveSummonObjects, Is.Zero);
            Assert.That(game.ActiveStains, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DragonFlapsAndRedWaveFiresFiveAnimatedWideWavesAtNormalSpeed()
        {
            var bodies = IsolateSummonTest(); Place(bodies[0], new Vector2(4, 0));
            Place(bodies[1], new Vector2(4, 1.8f)); Place(bodies[2], new Vector2(2, 3.5f));
            var launchTimes = new List<float>();
            game.RedWaveLaunched += time => launchTimes.Add(time);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.Dragon);
            game.CastSummonSkill(DoodleIdleGame.SummonSkill.RedWave);
            var wave = NamedArt("RedWave moving skill").Single();
            Assert.That(game.RedWavesLaunched, Is.EqualTo(1), "The volley must be sequential, not five simultaneous waves.");
            Assert.That(Vector2.Dot(wave.transform.right, Vector2.right), Is.GreaterThan(.99f), "The convex edge must lead, with the open crescent facing back.");
            Assert.That(wave.transform.localScale.x, Is.GreaterThan(4));
            Assert.That(wave.sprite.texture.name,Is.EqualTo("SkillRedSlash"));
            var poseA=DoodleExpansionArt.Get("SkillRedSlash",0);var poseB=DoodleExpansionArt.Get("SkillRedSlash",1);
            Assert.That(poseA.rect,Is.Not.EqualTo(poseB.rect));Assert.That(poseA.bounds.size,Is.EqualTo(poseB.bounds.size));
            var wings = NamedArt("Animated dragon wings").Single();
            var wingFrames = new HashSet<string>(); var slashFrames = new HashSet<string>();
            Vector3 start = wave.transform.position;
            for (int i = 0; i < 50; i++)
            {
                yield return new WaitForFixedUpdate();
                wingFrames.Add(wings.sprite.name); slashFrames.Add(wave.sprite.name);
                Assert.That(wings.sharedMaterial.mainTexture, Is.SameAs(wings.sprite.texture));
                Assert.That(wave.sharedMaterial.mainTexture, Is.SameAs(wave.sprite.texture));
            }
            Assert.That(wingFrames.Count, Is.EqualTo(2)); Assert.That(slashFrames.Count, Is.EqualTo(2));
            Assert.That(Vector3.Distance(start, wave.transform.position), Is.InRange(DoodleIdleGame.SlashSpeed * .98f, DoodleIdleGame.SlashSpeed * 1.04f));
            Assert.That(launchTimes.Count, Is.EqualTo(3), "The first second contains three of the five spaced shots.");
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.RedWave), Is.GreaterThanOrEqualTo(2));
            Assert.That(game.DragonFlames, Is.GreaterThanOrEqualTo(6));
            Assert.That(NamedArt("RedWave afterimage").Length, Is.GreaterThan(0));
            Assert.That(NamedArt("Dragon afterimage").Length, Is.GreaterThan(0));
            yield return PhysicsTicks(200);
            Assert.That(launchTimes.Count, Is.EqualTo(5));
            for (int i = 1; i < launchTimes.Count; i++) Assert.That(launchTimes[i] - launchTimes[i - 1], Is.InRange(.459f, .501f));
            Assert.That(game.SummonHits(DoodleIdleGame.SummonSkill.Dragon), Is.GreaterThan(0));
            Assert.That(game.RedWavesLaunched, Is.EqualTo(5));
            Assert.That(NamedArt("RedWave moving skill").Length, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SummonSkillsExportSeparateReadableCombatFrames()
        {
            IsolateSummonTest();
            var bodies = EnemyBodies();
            for (int i = 0; i < 12; i++) Place(bodies[i], new Vector2(Mathf.Cos(i * Mathf.PI / 6), Mathf.Sin(i * Mathf.PI / 6)) * 4);
            Camera.main.orthographicSize = 6.5f;
            foreach (var skill in new[] { DoodleIdleGame.SummonSkill.Cannon, DoodleIdleGame.SummonSkill.Cucumber, DoodleIdleGame.SummonSkill.TetherSnake, DoodleIdleGame.SummonSkill.StormCloud, DoodleIdleGame.SummonSkill.Dragon, DoodleIdleGame.SummonSkill.RedWave }) game.CastSummonSkill(skill);
            yield return PhysicsTicks(50);
            game.TogglePause(); yield return null;
            Object.Destroy(CaptureFrame("08-summons-landscape.png", 1440, 900));
            Object.Destroy(CaptureFrame("09-summons-portrait.png", 720, 1560));
            game.TogglePause();
            yield return PhysicsTicks(7);
            game.TogglePause(); yield return null;
            Object.Destroy(CaptureFrame("10-dragon-animation-next-frame.png", 1440, 900));
            game.ResetGame(); yield return null; IsolateSummonTest();
            bodies = EnemyBodies();
            for (int i = 0; i < 12; i++) Place(bodies[i], new Vector2(Mathf.Cos(i * Mathf.PI / 6), Mathf.Sin(i * Mathf.PI / 6)) * 4);
            foreach (var skill in new[] { DoodleIdleGame.SummonSkill.WaveSnakes, DoodleIdleGame.SummonSkill.FireRing, DoodleIdleGame.SummonSkill.Sand, DoodleIdleGame.SummonSkill.Shotgun }) game.CastSummonSkill(skill);
            yield return PhysicsTicks(28);
            game.TogglePause(); yield return null;
            Object.Destroy(CaptureFrame("11-area-skills.png", 1440, 900));
        }
    }
}

