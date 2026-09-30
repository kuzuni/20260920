using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        static DoodleIdleGame PvpEngine(DoodleIdleGame source,DoodlePvpLoadout loadout,Vector2 pos) =>
            (DoodleIdleGame)typeof(DoodleIdleGame).GetMethod("CreatePvpEngine",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{source,loadout,pos});
        static void PvpConnect(DoodleIdleGame a,DoodleIdleGame b) => typeof(DoodleIdleGame).GetMethod("SetPvpOpponent",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(a,new object[]{b});
        [UnityTest]
        public IEnumerator PvpSnapshotPreservesLoadoutAndBothFightersUseRealCombat()
        {
            game.TogglePause();game.companionsEnabled=true;game.summonSkillsEnabled=true;
            typeof(DoodleUi).GetField("starterDamageBaseline",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game.Ui,(GameNumber)1);
            ServiceSetSavedField(ServiceStateObject,"highestMainStage",1000);
            foreach(var category in new[]{"Skill","Companion","Armor","Club","Necklace","Relic"}) {
                var items=game.Ui.Items(category);
                foreach(var item in items){item.discovered=true;item.level=3;item.equipped=false;}
                int slots=category=="Skill"?8:category=="Companion"?5:1;
                for(int i=0;i<Math.Min(slots,items.Count);i++){items[i].equipped=true;items[i].slot=i;}
            }
            game.Ui.Skins("Appearance").Single(x=>x.id=="appearance_mint").owned=true;
            game.Ui.Skins("Weapon").Single(x=>x.id=="weapon_vine").owned=true;
            Assert.That(game.Ui.EquipSkin("appearance_mint"),Is.True);
            Assert.That(game.Ui.EquipSkin("weapon_vine"),Is.True);
            var captured=game.Ui.CapturePvpLoadout();
            var copy=DoodlePvpPayload.Pack(captured).Unpack();
            Assert.That(copy.collections,Is.EqualTo(captured.collections));
            Assert.That(copy.skins,Is.EqualTo(captured.skins));
            DoodleIdleGame left=null,right=null;
            int kills=game.Kills;var gold=game.Ui.GoldAmount;
            try {
                left=PvpEngine(game,copy,new Vector2(-2,0));right=PvpEngine(game,copy,new Vector2(2,0));
                Assert.That(left.Ui.EquippedSkills.Count,Is.EqualTo(8));
                Assert.That(left.Ui.EquippedCompanions.Count,Is.EqualTo(5));
                Assert.That(left.Ui.MaxHealthAmount,Is.EqualTo(game.Ui.MaxHealthAmount));
                Assert.That(left.Ui.CombatDamageAmount,Is.EqualTo(game.Ui.CombatDamageAmount));
                Assert.That(left.Ui.EquippedAppearanceIcon,Is.EqualTo(game.Ui.EquippedAppearanceIcon));
                Assert.That(left.Ui.EquippedWeaponIcon,Is.EqualTo(game.Ui.EquippedWeaponIcon));
                Assert.That(left.Ui.EquippedAppearanceTint,Is.EqualTo(game.Ui.EquippedAppearanceTint));
                Assert.That(left.ActiveCompanions,Is.EqualTo(5));
                // Keep both fighters alive long enough to exercise all attack sources,
                // without changing the serialized equipment or its regeneration values.
                foreach(var engine in new[]{left,right}) {
                    var actor=typeof(DoodleIdleGame).GetField("player",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(engine);
                    var maximum=engine.PlayerMaxHealthAmount*100;
                    actor.GetType().GetField("hp").SetValue(actor,maximum);
                    actor.GetType().GetField("maxHp").SetValue(actor,maximum);
                }
                PvpConnect(left,right);PvpConnect(right,left);
                yield return null;yield return null;
                left.TogglePause();right.TogglePause();
                float deadline=Time.time+12;
                while(Time.time<deadline && left.PlayerHealthAmount>0 && right.PlayerHealthAmount>0)yield return null;
                Assert.That(left.PlayerHealthAmount<left.PlayerMaxHealthAmount,Is.True,"Opponent must actually attack.");
                Assert.That(right.PlayerHealthAmount<right.PlayerMaxHealthAmount,Is.True,"Challenger must actually attack.");
                Assert.That(left.CompanionAttacks+right.CompanionAttacks,Is.GreaterThan(0));
                Assert.That(left.Ui.EquippedSkills.Sum(x=>left.SkillActivationCount(x.ability))+right.Ui.EquippedSkills.Sum(x=>right.SkillActivationCount(x.ability)),Is.GreaterThan(0));
                Assert.That(game.Kills,Is.EqualTo(kills));Assert.That(game.Ui.GoldAmount,Is.EqualTo(gold));
                Assert.That(left.Kills+right.Kills,Is.Zero,"PVP cannot award field kill rewards.");
            }finally{if(left)UnityEngine.Object.Destroy(left.gameObject);if(right)UnityEngine.Object.Destroy(right.gameObject);}
            yield return null;
        }
        [UnityTest]
        public IEnumerator PvpBattleRestoresPausedFieldAndCameraWhenCancelled()
        {
            game.TogglePause();
            var camera=Camera.main;float zoom=camera.orthographicSize;var pos=camera.transform.position;
            int kills=game.Kills;var snapshot=game.Ui.CapturePvpLoadout();
            var routine=(IEnumerator)typeof(DoodleIdleGame).GetMethod("RunPvpBattle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{snapshot,snapshot,(Action<DoodlePvpOutcome>)(_=>{})});
            try {
                Assert.That(routine.MoveNext(),Is.True);yield return null;
                Assert.That(routine.MoveNext(),Is.True);yield return null;
                Assert.That(game.PvpSessionActive,Is.True);Assert.That(game.Ui.Canvas.enabled,Is.False);
            }finally{(routine as IDisposable)?.Dispose();}
            Assert.That(camera.transform.position,Is.EqualTo(pos));
            yield return null;
            Assert.That(game.PvpSessionActive,Is.False);Assert.That(game.Ui.Canvas.enabled,Is.True);
            Assert.That(camera.orthographicSize,Is.EqualTo(zoom));
            Assert.That(game.Kills,Is.EqualTo(kills));
            Assert.That(UnityEngine.Object.FindObjectsByType<DoodleIdleGame>(FindObjectsSortMode.None).Count(x=>x.IsPvpEngine),Is.Zero);
        }
        [UnityTest]
        public IEnumerator PvpCountdownProducesBothWinAndLossAndReturnsToMainField()
        {
            game.TogglePause();int kills=game.Kills;var gold=game.Ui.GoldAmount;
            var weak=game.Ui.CapturePvpLoadout();weak.playerName="상대 플레이어";
            var levels=(System.Collections.Generic.Dictionary<string,CodeStage.AntiCheat.ObscuredTypes.ObscuredInt>)typeof(DoodleUi).GetField("statLevels",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game.Ui);
            var previousAttack=levels["attack"];levels["attack"]=1000;
            var strong=game.Ui.CapturePvpLoadout();strong.playerName="도전자";
            levels["attack"]=previousAttack;
            foreach(bool expected in new[]{true,false}){
                bool? won=null;
                var routine=(IEnumerator)typeof(DoodleIdleGame).GetMethod("RunPvpBattle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{expected?strong:weak,expected?weak:strong,(Action<DoodlePvpOutcome>)(value=>won=value==DoodlePvpOutcome.Win)});
                var handle=game.StartCoroutine(routine);
                float deadline=Time.realtimeSinceStartup+25;
                try {
                    while(!UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Any(x=>x.text=="3" && x.GetComponentInParent<UnityEngine.Canvas>().name=="PVP countdown") && Time.realtimeSinceStartup<deadline)yield return null;
                    yield return new WaitForSecondsRealtime(.3f);
                    Assert.That(game.PvpSessionActive,Is.True);
                    var engines=UnityEngine.Object.FindObjectsByType<DoodleIdleGame>(FindObjectsSortMode.None).Where(x=>x.IsPvpEngine).ToArray();
                    Assert.That(engines.Length,Is.EqualTo(2));Assert.That(engines.All(x=>x.Elapsed==0),Is.True,"Countdown cannot advance combat.");
                    if(expected){
                        var frame=CaptureFrame("pvp-two-player-field.png",720,1520,false);UnityEngine.Object.Destroy(frame);game.Ui.Canvas.enabled=false;}
                    while(game.PvpSessionActive&&Time.realtimeSinceStartup<deadline)yield return null;
                    Assert.That(won,Is.EqualTo(expected),"Real combat must produce both victory and defeat.");
                    Assert.That(game.Ui.Canvas.enabled,Is.True);Assert.That(game.Kills,Is.EqualTo(kills));Assert.That(game.Ui.GoldAmount,Is.EqualTo(gold));
                }finally{game.StopCoroutine(handle);(routine as IDisposable)?.Dispose();}
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator PvpThirtySecondTimeoutStopsCombatAndUsesHealthRatio()
        {
            game.TogglePause();
            game.basicSkillsEnabled=game.extraSkillsEnabled=game.summonSkillsEnabled=game.companionsEnabled=false;
            foreach(var item in game.Ui.Items("Skill"))item.equipped=false;
            var snapshot=game.Ui.CapturePvpLoadout();
            object Actor(DoodleIdleGame engine)=>typeof(DoodleIdleGame).GetField("player",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(engine);
            foreach(bool draw in new[]{false,true}){
                DoodlePvpOutcome? outcome=null;float finishedAt=0;DoodleIdleGame[] engines=null;
                float originalScale=Time.timeScale;
                var routine=(IEnumerator)typeof(DoodleIdleGame).GetMethod("RunPvpBattle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{snapshot,snapshot,(Action<DoodlePvpOutcome>)(result=>{outcome=result;finishedAt=engines.Max(x=>x.Elapsed);})});
                var handle=game.StartCoroutine(routine);
                try {
                    float deadline=Time.realtimeSinceStartup+45;
                    while(Time.realtimeSinceStartup<deadline){
                        engines=UnityEngine.Object.FindObjectsByType<DoodleIdleGame>(FindObjectsSortMode.None).Where(x=>x.IsPvpEngine).OrderBy(x=>((Rigidbody2D)Actor(x).GetType().GetField("body").GetValue(Actor(x))).position.x).ToArray();
                        if(engines.Length==2)break;yield return null;
                    }
                    Assert.That(engines.Length,Is.EqualTo(2));
                    for(int i=0;i<2;i++){
                        var actor=Actor(engines[i]);
                        actor.GetType().GetField("hp").SetValue(actor,(GameNumber)(i==0||draw?200:50));
                        actor.GetType().GetField("maxHp").SetValue(actor,(GameNumber)(i==0||draw?1000:100));
                    }
                    // Accelerate only test simulation; countdown still takes three real seconds.
                    Time.timeScale=4;bool timerVisible=false;
                    while(game.PvpSessionActive&&Time.realtimeSinceStartup<deadline){
                        timerVisible|=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Any(x=>x.text.StartsWith("남은 시간 "));
                        yield return null;
                    }
                    Assert.That(game.PvpSessionActive,Is.False);
                    Assert.That(finishedAt,Is.InRange(30f,30f+Time.fixedDeltaTime+.001f));
                    Assert.That(outcome,Is.EqualTo(draw?DoodlePvpOutcome.Draw:DoodlePvpOutcome.Loss));
                    Assert.That(timerVisible,Is.True);Assert.That(game.Ui.Canvas.enabled,Is.True);
                }finally{Time.timeScale=originalScale;game.StopCoroutine(handle);(routine as IDisposable)?.Dispose();}
                yield return null;
            }
        }
        [TestCase(0,0)][TestCase(1000,0)][TestCase(0,1000)][TestCase(int.MaxValue,int.MinValue)]
        public void PvpPointsAlwaysChangeByOneToFive(int own,int opponent)
        {
            Assert.That(DoodlePvpRules.Delta(own,opponent,true),Is.InRange(1,5));
            Assert.That(DoodlePvpRules.Delta(own,opponent,false),Is.InRange(-5,-1));
        }
    }
}
