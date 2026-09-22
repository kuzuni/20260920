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
        public IEnumerator ExpansionCatalogsDetailScaleAndSummonRowsKeepTheirLayout()
        {
            game.TogglePause();var ui=game.Ui;
            foreach(string category in new[]{"Skill","Companion"}) {
                var items=ui.Items(category);Assert.That(items.Count,Is.EqualTo(category=="Skill"?30:24));
                for(int grade=0;grade<6;grade++)Assert.That(items.Count(x=>x.rarity==grade),Is.EqualTo(category=="Skill"?5:4));
                Assert.That(items.All(x=>x.rarity<6),Is.True);
                foreach(var item in items)Assert.That(UiKit.Art(item.icon),Is.Not.Null,item.id);
                UiOpen(category=="Skill"?"Skills":"Companions");
                var first=items.First(x=>x.discovered);
                UiClick("Slot: "+first.name,UiNode("Collection inventory"));
                DoodlePopupMotion.CompleteAll(UiRoot);ui.Relayout(true);Canvas.ForceUpdateCanvases();
                var window=UiRoot.GetComponentsInChildren<DoodleUiWindow>().Last();
                Assert.That(window.detailScale,Is.EqualTo(1.8f));
                Assert.That(window.inner.localScale.x,Is.EqualTo(window.inner.localScale.y));
                Assert.That(window.content.rect.width,Is.EqualTo(318).Within(.1f),"Reference layout width is preserved while all its contents scale together.");
                Assert.That(((RectTransform)window.transform).rect.width,Is.EqualTo(360*window.inner.localScale.x).Within(.1f));
                foreach(var size in new[]{new Vector2Int(720,1520),new Vector2Int(720,1280),new Vector2Int(1000,1000),new Vector2Int(1520,720)})
                    Object.Destroy(CaptureFrame("expansion-detail-"+category+"-"+size.x+"x"+size.y+".png",size.x,size.y));
                ui.CloseDetail();
            }
            string[] order={"Lightning","Banana","Stone","Arrows","BouncyBall","Fire","Cannon","Shotgun","Sound","DoubleClaw","GiantWorm","Cloud","Molotov","Dumbbell","Eggplant","TetherSnake","Shuriken","WaveSnakes","BrickVolley","Durian","RedWave","Tornado","FireRing","Dragon","BlueMolotov","IceSnakes","PurpleFireArrows","Meteor","RedCloud","Golem"};
            CollectionAssert.AreEqual(order,ui.Items("Skill").Select(x=>x.ability).ToArray());
            for(int i=0;i<order.Length;i++)Assert.That(ui.Items("Skill")[i].rarity,Is.EqualTo(i/5));
            foreach(string id in new[]{"drone","sword","orbit"})Assert.That(ui.Items("Companion").Any(x=>x.id==id),Is.True);
            UiOpen("Shop");Canvas.ForceUpdateCanvases();
            var scroll=UiTopScroll();Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            foreach(string category in new[]{"Armor","Club","Skill","Companion","Relic"}) {
                var row=UiNode("Summon_"+category);
                Assert.That(row.GetComponent<LayoutElement>().preferredHeight,Is.EqualTo(category=="Relic"?336:264));
                Assert.That(row.Find("Icon: "+category).GetComponent<LayoutElement>().preferredWidth,Is.EqualTo(213));
                foreach(string button in new[]{"10회 뽑기","50회 뽑기"})Assert.That(row.GetComponentsInChildren<Button>().Single(b=>b.name==button).GetComponent<Image>().color,Is.EqualTo(UiKit.Yellow));
            }
            Object.Destroy(CaptureFrame("expansion-summon-top.png",720,1520));
            UiScrollBottom();Object.Destroy(CaptureFrame("expansion-summon-bottom.png",720,1520));
            UiOpen("Pvp");Object.Destroy(CaptureFrame("expansion-pvp-classic.png",720,1520));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExpansionVariantsEmitRequestedCountsSizesDirectionsAndFireParticles()
        {
            var bodies=IsolateSummonTest();Place(bodies[0],new Vector2(9,0));Place(bodies[1],new Vector2(10,3));
            game.CastVariant("Eggplant");
            var plants=NamedArt("Cucumber variant projectile");Assert.That(plants.Length,Is.EqualTo(2));
            Assert.That(plants.Select(p=>p.transform.rotation).Distinct().Count(),Is.EqualTo(2));
            game.CastVariant("IceSnakes");Assert.That(NamedArt("IceSnakes head").Length,Is.EqualTo(3));
            Assert.That(NamedArt("IceSnakes head").All(p=>Mathf.Abs(p.transform.localScale.x-.94f)<.001f),Is.True);
            game.CastVariant("GiantWorm");Assert.That(NamedArt("Spiral worm head").Single().transform.localScale.x,Is.EqualTo(1.24f).Within(.01f));
            int before=game.VariantProjectilesLaunched;game.CastVariant("BrickVolley");game.CastVariant("Durian");game.CastVariant("PurpleFireArrows");game.CastVariant("Shuriken");
            game.CastVariant("BlueMolotov");Assert.That(NamedArt("Blue molotov airborne bottle").Length,Is.EqualTo(2));
            game.CastVariant("RedCloud");var cloud=NamedArt("Red storm cloud").Single();Vector3 origin=cloud.transform.position;
            yield return PhysicsTicks(10);
            Assert.That(NamedArt("IceSnakes head").All(p=>p.transform.localScale.x>1.06f),Is.True,"Ice heads retain their doubled size during animation.");
            Assert.That(NamedArt("IceSnakes segment 1").All(p=>p.transform.localScale.x>.88f),Is.True,"Ice body particles also retain their doubled size.");
            Assert.That(NamedArt("Durian projectile").All(p=>Mathf.Abs(p.transform.localScale.x-1.72f)<.01f),Is.True);
            Assert.That(Particles("Purple Arrow Fire Trail Particle System").particleCount,Is.GreaterThan(0));
            Object.Destroy(CaptureFrame("expansion-skill-variants.png",1440,900,false));
            yield return PhysicsTicks(30);
            Assert.That(game.VariantProjectilesLaunched-before,Is.EqualTo(6+3+8+8));
            Assert.That(Vector3.Distance(cloud.transform.position,origin),Is.GreaterThan(.84f));
            Assert.That(Vector3.Distance(cloud.transform.position,origin),Is.LessThanOrEqualTo(DoodleIdleGame.RedCloudMoveSpeed*.8f+.05f));
            yield return PhysicsTicks(4);
            Assert.That(Particles("Blue Molotov Fire Particle System").particleCount,Is.GreaterThan(0));
            Object.Destroy(CaptureFrame("expansion-blue-fire-red-cloud.png",1440,900,false));
            game.ResetGame();yield return null;
            Assert.That(game.VariantProjectilesLaunched,Is.Zero);Assert.That(NamedArt("IceSnakes head"),Is.Empty);
        }

        [UnityTest]
        public IEnumerator ExpansionEquippedCompanionsFollowAttackAndUnequipRemovesThem()
        {
            var bodies=IsolateSummonTest();for(int i=0;i<8;i++)Place(bodies[i],new Vector2(3+i*.4f,1));
            game.Ui.AddItem(game.Ui.Items("Armor").Single(x => x.rarity == 6), 1);
            var items=game.Ui.Items("Companion");string[] selected={"drone","sword","orbit","companion_frost","companion_bee"};
            foreach(var item in items){item.equipped=selected.Contains(item.id);if(item.equipped){item.discovered=true;item.level=1;item.slot=System.Array.IndexOf(selected,item.id);}}
            game.companionsEnabled=true;
            yield return PhysicsTicks(220); // Allow the new rarity-based volley intervals to repeat.
            Assert.That(game.ActiveCompanions,Is.EqualTo(5));Assert.That(game.CompanionAttacks,Is.GreaterThan(5));
            Assert.That(game.CompanionShotsLaunched,Is.GreaterThan(5));
            Assert.That(game.CompanionHits,Is.GreaterThan(0));
            Assert.That(game.CompanionExplosions,Is.GreaterThan(0));
            foreach(string id in selected) Assert.That(game.CompanionShotCount(id),Is.GreaterThan(0),id);
            var shadows = game.GetComponentsInChildren<SpriteRenderer>().Where(s => s.name.StartsWith("Companion shadow: ")).ToArray();
            Assert.That(shadows.Length, Is.EqualTo(5));
            Assert.That(shadows.All(s => s.sortingOrder == -900 && s.color.a > 0 && s.color.a < .5f), Is.True);
            var shadowPositions = shadows.Select(s => s.transform.position).ToArray();
            yield return PhysicsTicks(20);
            for (int i = 0; i < shadows.Length; i++) Assert.That(Vector3.Distance(shadows[i].transform.position, shadowPositions[i]), Is.LessThan(.001f), "Stationary companions keep a fixed ground shadow through both poses.");
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Any(s => s.name.StartsWith("Companion explosion: ")), Is.False);
            Place(PlayerBody(),new Vector2(-3,-3));yield return PhysicsTicks(30);
            foreach(var renderer in game.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.name.StartsWith("Companion: ")))
                Assert.That(Vector2.Distance(renderer.transform.position,PlayerBody().position),Is.LessThan(3.1f));
            Object.Destroy(CaptureFrame("expansion-five-companions.png",1440,900,false));
            foreach(var item in items)item.equipped=false;
            int attacks=game.CompanionAttacks;yield return PhysicsTicks(30);
            Assert.That(game.ActiveCompanions,Is.Zero);Assert.That(game.CompanionAttacks,Is.EqualTo(attacks));
            Assert.That(game.GetComponentsInChildren<SpriteRenderer>().Any(s => s.name.StartsWith("Companion shadow: ")), Is.False);
        }

        [UnityTest]
        public IEnumerator ExpansionGroundUsesContinuousOpaqueWorldPatternAcrossFormerSeams()
        {
            game.TogglePause();var floor=NamedArt("Generated dirt floor").Single();
            Object.Destroy(CaptureFrame("terrain-live-portrait.png",720,1520));
            Assert.That(floor.sharedMaterial.shader.name,Is.EqualTo("DoodleIdle/Terrain"));
            Assert.That(floor.sharedMaterial.GetFloat("_TileSize"),Is.EqualTo(3));
            Assert.That(floor.bounds.size.x,Is.GreaterThan(80));Assert.That(floor.bounds.size.y,Is.GreaterThan(80));
            var state=typeof(DoodleUi).GetField("services",GrowthPrivate).GetValue(game.Ui);
            foreach(var renderer in game.GetComponentsInChildren<Renderer>())if(renderer!=floor)renderer.enabled=false;
            foreach(var canvas in game.GetComponentsInChildren<Canvas>())
                if(canvas.renderMode==RenderMode.WorldSpace)canvas.enabled=false;
            var camera=Camera.main;camera.backgroundColor=Color.magenta;camera.orthographicSize=6;
            for(int theme=0;theme<10;theme++) {
                state.GetType().GetField("mainStage").SetValue(state,theme*100);
                typeof(DoodleIdleGame).GetMethod("ApplyStageTheme",GrowthPrivate).Invoke(game,null);
                camera.transform.position=new Vector3(13.0f,13.0f,-10);
                var capture=CaptureFrame("expansion-ground-"+theme+".png",720,720,false);
                Assert.That(capture.GetPixels32().Count(p=>p.r>245&&p.b>245&&p.g<10),Is.Zero,"No camera clear color may leak through the ground.");
                Object.Destroy(capture);
            }
            // Adjacent repeats must keep the same orientation, including negative world positions.
            // The former triangle-wave sampler alternated upright/upside-down grass every tile.
            state.GetType().GetField("mainStage").SetValue(state,0);
            typeof(DoodleIdleGame).GetMethod("ApplyStageTheme",GrowthPrivate).Invoke(game,null);
            camera.orthographicSize=1.5f;
            camera.transform.position=new Vector3(13.5f,13.5f,-10);
            var reference=CaptureFrame("terrain-upright-tile.png",256,256,false);
            var referencePixels=reference.GetPixels32();
            var centers=new[]{new Vector2(16.5f,13.5f),new Vector2(13.5f,16.5f),new Vector2(-1.5f,-1.5f)};
            for(int i=0;i<centers.Length;i++) {
                camera.transform.position=new Vector3(centers[i].x,centers[i].y,-10);
                var repeated=CaptureFrame("terrain-upright-repeat-"+i+".png",256,256,false);
                var pixels=repeated.GetPixels32();double error=0;
                for(int p=0;p<pixels.Length;p++)error+=Mathf.Abs(pixels[p].r-referencePixels[p].r)+Mathf.Abs(pixels[p].g-referencePixels[p].g)+Mathf.Abs(pixels[p].b-referencePixels[p].b);
                Assert.That(error/(pixels.Length*3*255),Is.LessThan(.001),"Neighboring grass tiles must repeat without flipping or rotating.");
                Object.Destroy(repeated);
            }
            Object.Destroy(reference);
            yield return null;
        }
    }
}
