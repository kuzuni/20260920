using System.Collections;
using System.Linq;
using DoodleIdle.CharacterRigs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PlayerZonesStopApproachAndRetreatUntilEveryEnemyLeavesC()
        {
            var bodies = DurableSkillTargets(); game.TogglePause(); game.autoPlay = true; game.moveSpeed = 6.2f;
            foreach(var body in bodies)Place(body, new Vector2(14,14));
            Place(PlayerBody(),Vector2.zero);
            var zones = PlayerBody().GetComponentInChildren<CharacterMovementZones>();
            Assert.That(zones,Is.Not.Null);
            foreach(var zone in new[]{zones.stopAndAttack,zones.startRetreat,zones.finishRetreat}){
                Assert.That(zone.isTrigger && zone.enabled,Is.True);
                Assert.That(zone.attachedRigidbody,Is.SameAs(PlayerBody()));
            }
            var actors=(IList)typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate).GetValue(game);
            var target=actors.Cast<object>().Single(a=>(Rigidbody2D)a.GetType().GetField("body").GetValue(a)==bodies[0]);
            var movement=typeof(DoodleIdleGame).GetMethod("AutomaticMoveVelocity",GrowthPrivate);
            Vector2 Move()=> (Vector2)movement.Invoke(game,new[]{target,(object).02f});
            Place(bodies[0],new Vector2(8,0));Assert.That(Move().x,Is.GreaterThan(6));
            Place(bodies[0],new Vector2(3.4f,0));Assert.That(Move(),Is.EqualTo(Vector2.zero));
            Assert.That(zones.Retreating,Is.False,"An enemy in A or C alone does not initiate retreat.");
            Place(bodies[0],new Vector2(1.8f,0));Assert.That(Move().x,Is.LessThan(-5));
            Assert.That(zones.Retreating,Is.True);
            Place(bodies[0],new Vector2(2.7f,0));Assert.That(Move().x,Is.LessThan(-5),"Leaving B alone must not stop retreat.");
            Place(bodies[1],new Vector2(0,-2.7f));Place(bodies[0],new Vector2(4,0));
            Move();Assert.That(zones.Retreating,Is.True,"All enemies, not just the target, must leave C.");
            Place(bodies[1],new Vector2(14,14));Place(bodies[0],new Vector2(3.4f,0));
            Assert.That(Move(),Is.EqualTo(Vector2.zero));Assert.That(zones.Retreating,Is.False);
            zones.stopAndAttack.radius *= 2;
            Place(bodies[0],new Vector2(5,0));Assert.That(Move(),Is.EqualTo(Vector2.zero),"Inspector radius changes must affect behavior immediately.");
            game.endlessWorld = false; // Explicitly exercise the optional finite-arena clamp.
            foreach(var body in bodies)Place(body,new Vector2(-14,-14));
            Place(PlayerBody(),new Vector2(game.arenaHalfSize.x-.76f,0));
            Place(bodies[0],PlayerBody().position-Vector2.right*1.8f);
            var wallEscape=Move();
            Assert.That(Mathf.Abs(wallEscape.y),Is.GreaterThan(1),"A wall must allow sideways escape.");
            Assert.That(wallEscape.x,Is.LessThan(.01f));
            game.autoPlay=false;
            var sweep=typeof(DoodleIdleGame).GetMethod("LimitAutomaticStep",GrowthPrivate);
            Assert.That((Vector2)sweep.Invoke(game,new object[]{Vector2.right*5}),Is.EqualTo(Vector2.right*5),"Manual movement is not locked by A.");
            yield return null;
        }
        [UnityTest]
        public IEnumerator PlayerInsideAUsesIdleAttackWithoutAutomaticDash()
        {
            var bodies=DurableSkillTargets();game.autoPlay=true;game.moveSpeed=6.2f;game.basicSkillsEnabled=true;game.enemyContactDamage=0;
            foreach(var body in bodies)Place(body,new Vector2(14,14));
            Place(PlayerBody(),Vector2.zero);Place(bodies[0],new Vector2(3.2f,0));
            var rig=PlayerBody().GetComponentInChildren<CharacterRig>();
            // Make an automatic dash immediately available while target is inside A.
            typeof(DoodleIdleGame).GetField("dashTimer",GrowthPrivate).SetValue(game,0f);
            int dashes=game.DashCasts, hits=game.SlashHits;
            var start=PlayerBody().position;
            for(int i=0;i<100;i++) {
                yield return new WaitForFixedUpdate();
                Assert.That(PlayerBody().linearVelocity.sqrMagnitude,Is.LessThan(.001));
            }
            Assert.That(game.DashCasts,Is.EqualTo(dashes));
            Assert.That(Vector2.Distance(start,PlayerBody().position),Is.LessThan(.01));
            Assert.That(rig.animator.GetBool("Moving"),Is.False,"Base locomotion stays Idle while attacks play.");
            Assert.That(game.SlashHits,Is.GreaterThan(hits),"Idle must still attack and deal real damage.");
            Place(bodies[0],PlayerBody().position+Vector2.right*5.5f);
            typeof(DoodleIdleGame).GetField("dashTimer",GrowthPrivate).SetValue(game,100f);
            hits=game.SlashHits;
            yield return PhysicsTicks(100);
            Assert.That(PlayerBody().linearVelocity.sqrMagnitude,Is.LessThan(.001));
            Assert.That(Vector2.Distance(PlayerBody().position,bodies[0].position),Is.EqualTo(3.76f).Within(.04f));
            Assert.That(game.SlashHits,Is.GreaterThan(hits),"Approach must enter A, not stop just outside its attack boundary.");
        }
    }
}
