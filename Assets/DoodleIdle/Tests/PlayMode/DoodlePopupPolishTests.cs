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
  [UnityTest] public IEnumerator UpgradeCountsLockCoversAndCollectionAnimations()
  {
   game.paused=true;var ui=game.Ui;ui.Gold=1000000000000;
   ui.ShowPage("Stats");yield return null;yield return null;
   var row=ui.Canvas.GetComponentsInChildren<RectTransform>().Single(x=>x.name=="Stat attack");
   var buy=row.GetComponentInChildren<Button>();var icon=row.GetComponentsInChildren<Image>().Single(x=>x.name=="Icon: StatAttack");
   foreach(int batch in new[]{1,100,13,-1}) {
    if(batch==13)ui.GoldAmount=ui.StatUpgradeQuoteAmount("attack",13,out _);else if(batch<0)ui.Gold=1000000000000;
    int selected=batch==13?-1:batch;typeof(DoodleUi).GetField("statBatch",GrowthPrivate).SetValue(ui,selected);
    ui.StatUpgradeQuoteAmount("attack",selected,out var actual);Assert.That(actual,Is.GreaterThan(0));
    buy.onClick.Invoke();var effect=icon.GetComponentInChildren<DoodleUiIconBurst>();Assert.That(effect.LastBurstCount,Is.EqualTo(Mathf.Min(100,actual)));
    yield return null;Assert.That(effect.GetComponent<ParticleSystem>().particleCount,Is.GreaterThan(0));
   }
   var extended=UiKit.Icon(ui.Canvas.transform,"StatCrit256",86);DoodleUiIconBurst.Play(extended,13);yield return null;
   Assert.That(extended.GetComponentInChildren<DoodleUiIconBurst>().mainTexture,Is.Not.SameAs(UiKit.Circle.texture));Object.Destroy(extended.gameObject);
   var locked=ui.Canvas.GetComponentsInChildren<RectTransform>().First(x=>x.name.StartsWith("Stat crit") && x.Find("Locked cover").gameObject.activeSelf);
   Assert.That(locked.Find("Locked cover").GetComponent<Image>().color.a,Is.EqualTo(.5f));
   Assert.That(locked.Find("Locked cover").GetComponentInChildren<Text>().alignment,Is.EqualTo(TextAnchor.MiddleCenter));
   ui.ShowPage("Skills");yield return null;yield return null;
   var grid=ui.Canvas.GetComponentsInChildren<GridLayoutGroup>().Single(x=>x.name=="Equipped Skill");Assert.That(grid.constraintCount,Is.EqualTo(4));Assert.That(grid.transform.childCount,Is.EqualTo(8));
   foreach(var cover in ui.Canvas.GetComponentsInChildren<RectTransform>().Where(x=>x.name=="Locked cover"))Assert.That(cover.GetComponent<Image>().color,Is.EqualTo(new Color(0,0,0,.5f)));
   Object.Destroy(CaptureFrame("skills-two-rows-lock-covers.png",720,1520));
   var companions=ui.Items("Companion");companions[0].discovered=true;companions[0].equipped=true;companions[0].slot=0;
   ui.ShowPage("Companions");yield return new WaitForSecondsRealtime(.4f);
   var previews=ui.Canvas.GetComponentsInChildren<DoodleIdlePortrait>().Where(x=>x.view==DoodleIdlePortrait.View.Collection).ToArray();Assert.That(previews.Length,Is.GreaterThan(0));
   Assert.That(previews.Any(x=>x.collectionWalking),Is.True);Assert.That(previews.Any(x=>!x.collectionWalking),Is.True);
   Object.Destroy(CaptureFrame("companions-animated-slots.png",720,1520));
   typeof(DoodleUi).GetField("skinCategory",GrowthPrivate).SetValue(ui,"Appearance");ui.ShowPage("Skins");yield return new WaitForSecondsRealtime(.4f);
   previews=ui.Canvas.GetComponentsInChildren<DoodleIdlePortrait>().Where(x=>x.view==DoodleIdlePortrait.View.Collection).ToArray();Assert.That(previews.Length,Is.GreaterThan(0));Assert.That(previews.All(x=>!x.PreviewRig.weaponRenderer.enabled),Is.True);
   Object.Destroy(CaptureFrame("skins-idle-walk.png",720,1520));
  }
  [UnityTest] public IEnumerator BuffShopSettingsAndPvpCandidatePresentation()
  {
   game.paused=true;var ui=game.Ui;ui.ShowPage("Buffs");yield return null;
   Assert.That(ui.Canvas.GetComponentsInChildren<DoodleBuffGlow>().Length,Is.Zero);
   ui.ExtendBuff(true);yield return null;yield return null;
   Assert.That(ui.Canvas.GetComponentsInChildren<DoodleBuffGlow>().Length,Is.EqualTo(1));
   Object.Destroy(CaptureFrame("active-buff-glow.png",720,1520));
   ui.ShowPage("Settings");yield return null;
   var quit=ui.Canvas.GetComponentsInChildren<Button>().Single(x=>x.name=="게임종료");
   var logout=ui.Canvas.GetComponentsInChildren<Button>().Single(x=>x.name=="로그아웃");
   var leave=ui.Canvas.GetComponentsInChildren<Button>().Single(x=>x.name=="회원탈퇴");
   Assert.That(logout.transform.GetSiblingIndex(),Is.GreaterThan(quit.transform.GetSiblingIndex()));Assert.That(leave.transform.GetSiblingIndex(),Is.GreaterThan(logout.transform.GetSiblingIndex()));
   Assert.That(ui.Canvas.GetComponentsInChildren<Button>().Any(x=>x.name=="랭킹"),Is.False);Assert.That(ui.Canvas.GetComponentsInChildren<Button>().Any(x=>x.name=="닉네임 변경"),Is.True);
   var container=UiKit.Column(ui.Canvas.transform,"Isolated popup QA",8,8);UiKit.Stretch(container,20,120,20,120);
   try {
    typeof(DoodleUi).GetMethod("BuildFreeDiamondCard",GrowthPrivate).Invoke(ui,new object[]{container});
    var free=container.Find("Free diamond card");Assert.That(free.GetComponentsInChildren<Image>().Any(x=>x.sprite==UiKit.Art("Diamond")),Is.True);Assert.That(free.GetComponentsInChildren<Text>().Any(x=>x.text=="Free"),Is.True);
    Object.Destroy(free.gameObject);yield return null;
    var look=new DoodlePlayerLook{appearanceIcon="SkinAppearance_1",weaponIcon="Club"};
    typeof(DoodleUi).GetMethod("PvpCandidateButton",GrowthPrivate).Invoke(ui,new object[]{container,"테스트 상대",10,30,"12345",(System.Action)(()=>{}),look});
    yield return new WaitForSecondsRealtime(.4f);
    Assert.That(container.GetComponentInChildren<DoodleRankingPortrait>().Look,Is.SameAs(look));
    var labels=container.GetComponentsInChildren<Text>();Assert.That(labels.Any(x=>x.text=="테스트 상대"),Is.True);Assert.That(labels.Any(x=>x.text=="승점 30"),Is.True);Assert.That(labels.Any(x=>x.text.Contains("승리 +")&&x.text.Contains("패배 -")),Is.True);
    Object.Destroy(CaptureFrame("pvp-candidate-appearance-layout.png",720,1520));
   }finally{Object.Destroy(container.gameObject);}
  }
  [UnityTest] public IEnumerator ShopRelicAndPodiumUseCurrentPresentation()
  {
   game.paused=true;var ui=game.Ui;
   typeof(DoodleUi).GetField("shopTab",GrowthPrivate).SetValue(ui,1);ui.ShowPage("Shop");yield return null;yield return null;
   var free=ui.Canvas.GetComponentsInChildren<RectTransform>().Single(x=>x.name=="Free diamond card");
   Assert.That(free.GetComponentsInChildren<Image>().Any(x=>x.sprite==UiKit.Art("Diamond")),Is.True);
   Assert.That(ui.Canvas.GetComponentsInChildren<Text>().Any(x=>x.text.StartsWith("마일리지 쿠폰 ")&&x.text.EndsWith("개 추가")),Is.True);
   foreach(var card in ui.Canvas.GetComponentsInChildren<RectTransform>().Where(x=>x.name.StartsWith("CurrencyProduct")))Assert.That(card.GetComponentsInChildren<Image>().Any(x=>x.sprite==UiKit.Art("Diamond")),Is.True);
   Object.Destroy(CaptureFrame("shop-original-free-diamond.png",720,1520));
   typeof(DoodleUi).GetField("shopTab",GrowthPrivate).SetValue(ui,2);ui.RefreshPage();yield return null;yield return null;
   var grid=ui.Canvas.GetComponentsInChildren<GridLayoutGroup>().Single(x=>x.name=="Mileage exchange cards");Assert.That(grid.constraintCount,Is.EqualTo(2));
   Object.Destroy(CaptureFrame("mileage-exchange-cards.png",720,1520));
   var item=ui.Items("Relic")[0];item.discovered=true;item.count=100;item.level=0;
   ui.ShowPage("Relics");yield return null;
   for(int i=0;i<15 && item.level==0;i++)typeof(DoodleUi).GetMethod("UpgradeRelicFromUi",GrowthPrivate).Invoke(ui,new object[]{item,false});
   Assert.That(item.level,Is.GreaterThan(0));yield return null;yield return null;
   Assert.That(ui.Canvas.GetComponentsInChildren<DoodleUiIconBurst>().Any(x=>x.LastBurstCount==1),Is.True);
   yield return new WaitForSecondsRealtime(.15f);
   Object.Destroy(CaptureFrame("relic-upgrade-particles.png",720,1520));
   ui.ClosePage();
   var rankType=typeof(DoodleUi).GetNestedType("LocalRank",System.Reflection.BindingFlags.NonPublic);
   var list=(System.Collections.IList)System.Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(rankType));
   for(int i=0;i<3;i++){
    var row=System.Activator.CreateInstance(rankType);rankType.GetField("name").SetValue(row,"테스트 "+(i+1));rankType.GetField("rank").SetValue(row,i+1);rankType.GetField("art").SetValue(row,"Player");rankType.GetField("look").SetValue(row,new DoodlePlayerLook{appearanceIcon=i==0?"Player":"SkinAppearance_"+i,weaponIcon="Club"});rankType.GetField("power").SetValue(row,(GameNumber)12345);list.Add(row);
   }
   ui.ShowDetail("PVP",panel=>typeof(DoodleUi).GetMethod("DrawPvpRanking",GrowthPrivate).Invoke(ui,new object[]{panel,list}));
   yield return new WaitForSecondsRealtime(.4f);
   foreach(var preview in ui.Canvas.GetComponentsInChildren<DoodleIdlePortrait>().Where(x=>x.view==DoodleIdlePortrait.View.Pvp)) {
    preview.RenderNow();Assert.That(preview.PreviewCamera.transform.position.x,Is.EqualTo(preview.PreviewRig.face.headRenderer.bounds.center.x+DoodlePortraitSettings.Current.pvpCameraOffset.x).Within(.02f));
   }
   Assert.That(ui.Canvas.GetComponentsInChildren<Text>().Any(x=>x.text.Contains("1~100")),Is.False);
   yield return new WaitForSecondsRealtime(2f);
   Object.Destroy(CaptureFrame("pvp-centered-podium.png",720,1520));
  }
  [UnityTest] public IEnumerator ActiveProjectilesKeepMovingAfterPlayerDeath()
  {
   DurableSkillTargets();game.paused=true;
   typeof(DoodleIdleGame).GetMethod("FireSlash",GrowthPrivate).Invoke(game,new object[]{Vector2.right});
   var shot=game.GetComponentsInChildren<Transform>().Single(x=>x.name=="Club slash wave");var before=shot.position;
   var player=typeof(DoodleIdleGame).GetField("player",GrowthPrivate).GetValue(game);SetActorField(player,"hp",(GameNumber)0);
   game.paused=false;typeof(DoodleIdleGame).GetMethod("FixedUpdate",GrowthPrivate).Invoke(game,null);game.paused=true;
   Assert.That(shot.position.x,Is.GreaterThan(before.x));Assert.That(game.PlayerHealthAmount,Is.EqualTo((GameNumber)0));yield return null;
  }
 }
}
