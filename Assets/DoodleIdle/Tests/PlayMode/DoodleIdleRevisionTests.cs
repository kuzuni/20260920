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
        sealed class RevisionRoll : System.Random
        {
            readonly int roll;
            public RevisionRoll(int value){roll=value;}
            public override int Next(int maxValue)=>roll%maxValue;
        }
        sealed class TierWeightRoll : System.Random
        {
            readonly int tierRoll;
            int calls;
            public TierWeightRoll(int value) { tierRoll = value; }
            public override int Next(int maxValue) => calls++ == 0 ? 0 : tierRoll % maxValue;
        }

        [UnityTest]
        public IEnumerator EquipmentArtworkHasSixtyTwoDistinctCompleteIconsAcrossViews()
        {
            game.TogglePause(); var ui=game.Ui;
            foreach(string category in new[]{"Armor","Club"}) {
                var items=ui.Items(category);
                Assert.That(items.Select(x=>x.icon).Distinct().Count(),Is.EqualTo(36));
                var sprites=items.Select(x=>UiKit.Art(x.icon)).ToArray();
                Assert.That(sprites.Select(x=>x.rect).Distinct().Count(),Is.EqualTo(36));
                for(int i=0;i<items.Count;i++) {
                    var sprite=sprites[i];
                    Assert.That(sprite.name,Is.EqualTo(items[i].icon));
                    Assert.That(sprite.texture.name,Is.EqualTo(i < 31 ? "Equipment"+category : "AscensionAtlas"));
                    var rect=sprite.rect;
                    int width=(int)rect.width,height=(int)rect.height;
                    Assert.That(width,Is.GreaterThan(i < 31 ? 120 : 90)); Assert.That(height,Is.GreaterThan(i < 31 ? 120 : 90));
                    var pixels=sprite.texture.GetPixels((int)rect.x,(int)rect.y,width,height);
                    Assert.That(pixels.Count(p=>p.a>.125f),Is.InRange(width*height/5,width*height*95/100));
                    for(int x=0;x<width;x++) {
                        Assert.That(pixels[x].a,Is.LessThan(.13f),items[i].name+" bottom cropped");
                        Assert.That(pixels[(height-1)*width+x].a,Is.LessThan(.13f),items[i].name+" top cropped");
                    }
                    for(int y=0;y<height;y++) {
                        Assert.That(pixels[y*width].a,Is.LessThan(.13f),items[i].name+" left cropped");
                        Assert.That(pixels[y*width+width-1].a,Is.LessThan(.13f),items[i].name+" right cropped");
                    }
                    ui.AddItem(items[i],1);
                }
                typeof(DoodleUi).GetField("equipmentCategory",GrowthPrivate).SetValue(ui,category);
                UiOpen("Equipment"); yield return null;
                var inventory=UiNode("Collection inventory");
                foreach(var item in items) {
                    var card=inventory.GetComponentsInChildren<Button>().Single(b=>b.name=="Slot: "+item.name);
                    Assert.That(card.GetComponentsInChildren<Image>().Any(im=>im.sprite==UiKit.Art(item.icon)),Is.True);
                }
                Object.Destroy(CaptureFrame("equipment-unique-"+category.ToLowerInvariant()+".png",720,1520));
                inventory.GetComponentsInChildren<Button>().Single(b=>b.name=="Slot: "+items.Last().name).onClick.Invoke();
                Assert.That(UiNode("Selected equipment").GetComponentsInChildren<Image>().Any(im=>im.sprite==sprites.Last()),Is.True);
                ui.SkipSummonAnimations=true;
                typeof(DoodleUi).GetMethod("ShowSummonResults",GrowthPrivate).Invoke(ui,new object[]{category,items.Take(5).ToList()});
                yield return null;
                foreach(var sprite in sprites.Take(5)) Assert.That(game.GetComponentsInChildren<Image>().Any(im=>im.gameObject.activeInHierarchy&&im.sprite==sprite),Is.True);
                Object.Destroy(CaptureFrame("equipment-unique-"+category.ToLowerInvariant()+"-summon.png",720,1520));
                ui.CloseFullscreen();
            }
        }

        [UnityTest]
        public IEnumerator SummonTierWeightsMatchTenNineEightSevenSixAndDisplayedPercentages()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Armor", "Club", "Skill", "Companion" }) {
                var choices = ui.Items(category).Where(x => x.rarity == 0).ToList();
                int[] weights = category == "Companion" ? new[] { 10,9,8,7 } : new[] { 10,9,8,7,6 };
                var drawn = new int[weights.Length];
                for (int ticket = 0; ticket < weights.Sum(); ticket++) drawn[choices.IndexOf(ui.GrantItem(category,new TierWeightRoll(ticket)))]++;
                CollectionAssert.AreEqual(weights,drawn,"Every weighted draw interval must match the requested proportions.");
                for (int i = 0; i < choices.Count; i++) {
                    Assert.That(ui.ItemProbability(choices[i]), Is.EqualTo(ui.GradeProbability(category,0)*weights[i]/weights.Sum()).Within(.000001));
                    foreach (int level in new[] { 1, 10, 35 })
                        Assert.That(ui.PreviewItemProbability(choices[i],level), Is.EqualTo(ui.SummonWeights(category,level)[0]/1000d*weights[i]/weights.Sum()).Within(.000001));
                }
                Assert.That(ui.Items(category).Sum(ui.ItemProbability), Is.EqualTo(100).Within(.000001));
            }
            foreach (string category in new[] { "Relic", "DungeonRelic" }) {
                var items = ui.Items(category);
                foreach (var item in items) Assert.That(ui.ItemProbability(item), Is.EqualTo(100d/items.Count));
            }
            UiOpen("Shop"); ui.ShowSummonProbabilities("Skill");
            Assert.That(UiNode("Detail dim: 뽑기 확률").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("10:9:8:7:6")),Is.True);
            yield return null;
        }
        [UnityTest]
        public IEnumerator RevisionSummonLevelsUnlockGradesAndRelicsStayUniformWithoutLevels()
        {
            game.TogglePause();var ui=game.Ui;ui.Diamonds=100000;
            var states=(System.Collections.Generic.Dictionary<string,DoodleUi.SummonState>)typeof(DoodleUi).GetField("summonStates",GrowthPrivate).GetValue(ui);
            foreach(string category in new[]{"Armor","Club","Skill","Companion"})
            {
                double previousLegend=0,previousMyth=0,previousGod=0;
                for(int level=1;level<=DoodleUi.MaxSummonLevel;level++)
                {
                    states[category].level=level;
                    var weights=ui.SummonWeights(category,level);
                    var defaults=(int[])new DoodleUi.CommerceTuning().rates[level-1].basisPoints.Clone();
                    if(level<35 && (category=="Skill"||category=="Companion")) { defaults[5]+=defaults[6];defaults[6]=0; }
                    if(category=="Skill"||category=="Companion") { defaults[7]+=defaults[8];defaults[8]=0; }
                    CollectionAssert.AreEqual(defaults,weights,"Resource tuning and fallback defaults must agree at every level.");
                    Assert.That(weights.Sum(),Is.EqualTo(100000));
                    int boundary=0;
                    for(int grade=0;grade<weights.Length;grade++) {
                        if(weights[grade]>0) {
                            Assert.That(ui.GrantItem(category,new RevisionRoll(boundary)).rarity,Is.EqualTo(grade));
                            Assert.That(ui.GrantItem(category,new RevisionRoll(boundary+weights[grade]-1)).rarity,Is.EqualTo(grade));
                        }
                        boundary+=weights[grade];
                    }
                    Assert.That(ui.Items(category).Sum(ui.ItemProbability),Is.EqualTo(100).Within(.000001));
                    Assert.That(ui.GradeProbability(category,0),Is.GreaterThanOrEqualTo(10));
                    Assert.That(ui.GradeProbability(category,1),Is.GreaterThanOrEqualTo(9));
                    double legend=ui.GradeProbability(category,4),myth=ui.GradeProbability(category,5),god=ui.GradeProbability(category,6);
                    Assert.That(legend,Is.GreaterThanOrEqualTo(previousLegend));if(level<=30)Assert.That(myth,Is.GreaterThanOrEqualTo(previousMyth));Assert.That(god,Is.GreaterThanOrEqualTo(previousGod));
                    if(level<6)Assert.That(legend,Is.Zero);
                    if(level<15)Assert.That(myth,Is.Zero);
                    if(level<25)Assert.That(god,Is.Zero);
                    previousLegend=legend;previousMyth=myth;previousGod=god;
                }
                int[] draws=new int[DoodleUi.GradeNames.Length];
                for(int ticket=0;ticket<100000;ticket++)draws[ui.GrantItem(category,new RevisionRoll(ticket)).rarity]++;
                CollectionAssert.AreEqual(new[]{15000,20000,20000,20000,18000,1900,3000,category=="Skill"||category=="Companion"?2100:2000,category=="Skill"||category=="Companion"?0:100},draws,"All level 50 intervals match the category probabilities.");
                states[category].level=1;
                for(int ticket=0;ticket<100000;ticket++)Assert.That(ui.GrantItem(category,new RevisionRoll(ticket)).rarity,Is.LessThan(4));
                states[category].level=49;states[category].experience=9999;
                Assert.That(ui.TrySummon(category,50,false),Is.True);
                Assert.That(ui.SummonLevel(category),Is.EqualTo(50));Assert.That(ui.SummonExperience(category),Is.Zero);
                Assert.That(ui.TrySummon(category,50,false),Is.True);
                Assert.That(ui.SummonLevel(category),Is.EqualTo(50));Assert.That(ui.SummonExperience(category),Is.Zero);
                ui.CloseFullscreen();
            }
            foreach(int level in new[]{2,10,20,30}) {
                states["Armor"].level=level;
                ui.ShowSummonProbabilities("Armor");
                var panel=UiNode("Detail dim: 뽑기 확률");
                var weights=ui.SummonWeights("Armor",level);
                for(int grade=0;grade<DoodleUi.GradeNames.Length;grade++) {
                    var row=panel.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Probability_grade_"+grade);
                    string displayed=row.GetComponentsInChildren<Text>().Single(t=>t.name=="Grade probability rate").text;
                    Assert.That(displayed,Is.EqualTo((weights[grade]/1000d).ToString("0.###",System.Globalization.CultureInfo.InvariantCulture)+"%"));
                }
                if(level==30) { yield return null; Object.Destroy(CaptureFrame("summon-rates-revised-level30.png",720,1520)); }
                ui.CloseDetail();
            }
            var relics=ui.Items("Relic");Assert.That(relics.Select(x=>x.rarity).Distinct().Count(),Is.EqualTo(1));
            foreach(var item in relics)Assert.That(ui.ItemProbability(item),Is.EqualTo(12.5));
            CollectionAssert.AreEquivalent(relics,Enumerable.Range(0,8).Select(i=>ui.GrantItem("Relic",new RevisionRoll(i))).ToArray());
            Assert.That(ui.TrySummon("Relic",50,false),Is.True);
            Assert.That(ui.SummonLevel("Relic"),Is.Zero);Assert.That(ui.SummonExperience("Relic"),Is.Zero);
            Assert.That(UiNode("Summon progress text").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Lv.")||t.text.Contains("경험치")),Is.False);
            Object.Destroy(CaptureFrame("revision-relic-summon-no-level.png",720,1520));ui.CloseFullscreen();
            UiOpen("Shop");
            Assert.That(UiNode("Summon_Relic").GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Lv.")),Is.False);
            Object.Destroy(CaptureFrame("revision-summon-level-30.png",720,1520));
            ui.ShowSummonProbabilities("Armor");Object.Destroy(CaptureFrame("revision-summon-level-30-probabilities.png",720,1520));ui.CloseDetail();
            ui.ShowSummonProbabilities("Relic");Object.Destroy(CaptureFrame("revision-relic-equal-probabilities.png",720,1520));ui.CloseDetail();
            PlayerPrefs.SetString("DoodleUi.Commerce.Armor","{\"level\":999,\"experience\":1000}");
            PlayerPrefs.SetString("DoodleUi.Commerce.Relic","{\"level\":10,\"experience\":100}");
            typeof(DoodleUi).GetMethod("InitCommerce",GrowthPrivate).Invoke(ui,null);
            Assert.That(ui.SummonLevel("Armor"),Is.EqualTo(50));Assert.That(ui.SummonExperience("Armor"),Is.Zero);
            Assert.That(ui.SummonLevel("Relic"),Is.Zero);Assert.That(ui.SummonExperience("Relic"),Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionEnemyFacingFollowsTravelForBothFramesInAllThemes()
        {
            game.TogglePause();game.basicSkillsEnabled=false;
            var state=typeof(DoodleUi).GetField("services",GrowthPrivate).GetValue(game.Ui);
            var animate=typeof(DoodleIdleGame).GetMethod("AnimateActorFrames",GrowthPrivate);
            var enemyField=typeof(DoodleIdleGame).GetField("enemies",GrowthPrivate);
            string[] nativeLeft={"소라게"};
            string[] atlasNames={"Meadow","Desert","Forest","Swamp","Volcano","Coast","Crystal","Twilight","Ruins","Glacier"};
            var camera=Camera.main;camera.orthographicSize=4.8f;camera.transform.position=new Vector3(0,0,-10);
            for(int theme=0;theme<10;theme++)
            {
                state.GetType().GetField("mainStage").SetValue(state,theme*100);
                state.GetType().GetField("mainStageKillProgress").SetValue(state,0);
                game.RequestCombatWaveReset();game.paused=false;
                typeof(DoodleIdleGame).GetMethod("FixedUpdate",GrowthPrivate).Invoke(game,null);
                game.TogglePause();
                var actors=((IList)enemyField.GetValue(game)).Cast<object>().ToArray();
                foreach(var renderer in game.GetComponentsInChildren<SpriteRenderer>())
                    renderer.enabled=renderer.name=="Generated dirt floor";
                for(int kind=0;kind<3;kind++)
                {
                    var pair=actors.Where(a=>(int)a.GetType().GetField("kind").GetValue(a)==kind).Take(2).ToArray();
                    Assert.That(pair.Length,Is.EqualTo(2));
                    for(int column=0;column<2;column++)
                    {
                        var actor=pair[column];var type=actor.GetType();
                        var body=(Rigidbody2D)type.GetField("body").GetValue(actor);
                        var art=(SpriteRenderer)type.GetField("art").GetValue(actor);
                        Assert.That(art.sprite.texture.name,Is.EqualTo(atlasNames[theme]),"Every species now uses its complete two-frame theme atlas.");
                        if(theme==3&&kind==0)Assert.That(body.name,Is.EqualTo("Enemy - 이끼"));
                        if(theme==4&&kind==2)Assert.That(body.name,Is.EqualTo("Enemy - 불도마뱀"));
                        bool sourceLeft=nativeLeft.Any(name=>body.name=="Enemy - "+name);
                        Assert.That(art.flipX,Is.EqualTo(PlayerBody().position.x<body.position.x),"Spawn faces the player after source normalization.");
                        Assert.That(art.transform.localScale.x<0,Is.EqualTo(sourceLeft),"Mixed source art is normalized to a right-facing baseline.");
                        body.simulated=true;body.position=new Vector2(column==0?-2.4f:2.4f,2.5f-kind*2.5f);
                        body.transform.position=body.position;art.enabled=true;
                        type.GetField("phase").SetValue(actor,0f);
                        foreach(int side in new[]{-1,1})
                        {
                            // The player stays in place: turning must follow velocity, not its location.
                            body.linearVelocity=new Vector2(side,0);
                            type.GetField("walkClock").SetValue(actor,0f);
                            animate.Invoke(game,new[]{actor,(object)0f});
                            Assert.That(art.flipX,Is.EqualTo(side<0),body.name);
                            var first=art.sprite;
                            animate.Invoke(game,new[]{actor,(object).2f});
                            Assert.That(art.sprite,Is.Not.SameAs(first));
                            Assert.That(art.sprite.texture,Is.SameAs(first.texture),"Both walking poses must use the replacement artwork.");
                            Assert.That(art.flipX,Is.EqualTo(side<0),"Pose B must retain facing.");
                            Assert.That(art.transform.localScale.x<0,Is.EqualTo(sourceLeft),"Pose B shares the normalized source direction.");
                            bool previous=art.flipX;
                            foreach(var velocity in new[]{Vector2.zero,Vector2.up,new Vector2(-side*.001f,1)})
                            {
                                body.linearVelocity=velocity;animate.Invoke(game,new[]{actor,(object).2f});
                                Assert.That(art.flipX,Is.EqualTo(previous),"Standing and near-vertical movement retain facing.");
                            }
                            body.simulated=false;body.linearVelocity=new Vector2(-side,0);
                            animate.Invoke(game,new[]{actor,(object).2f});
                            Assert.That(art.flipX,Is.EqualTo(previous),"Paused actors cannot turn.");
                            body.simulated=true;
                        }
                        body.linearVelocity=new Vector2(column==0?-1:1,0);
                        type.GetField("walkClock").SetValue(actor,0f);
                        animate.Invoke(game,new[]{actor,(object)0f});
                    }
                }
                // Left column travels left; right column travels right, one row per species.
                Object.Destroy(CaptureFrame("revision-facing-theme-"+theme+".png",1000,1000,false));
            }
            state.GetType().GetField("mainStageKillProgress").SetValue(state,100);
            game.RequestCombatWaveReset();game.paused=false;
            typeof(DoodleIdleGame).GetMethod("FixedUpdate",GrowthPrivate).Invoke(game,null);
            game.TogglePause();
            var boss=((IList)enemyField.GetValue(game))[0];var bossType=boss.GetType();
            var bossBody=(Rigidbody2D)bossType.GetField("body").GetValue(boss);
            var bossArt=(SpriteRenderer)bossType.GetField("art").GetValue(boss);
            Assert.That(game.BossActive,Is.True);bossBody.simulated=true;
            bossBody.linearVelocity=Vector2.left;animate.Invoke(game,new[]{boss,(object).1f});Assert.That(bossArt.flipX,Is.True);
            bossBody.linearVelocity=Vector2.right;animate.Invoke(game,new[]{boss,(object).1f});Assert.That(bossArt.flipX,Is.False);
            Assert.That(bossBody.transform.localScale,Is.EqualTo(Vector3.one*3));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionEquipmentCapsSynthesisChainAndGodUpgrades()
        {
            game.TogglePause(); var ui = game.Ui;
            foreach (string category in new[] { "Armor", "Club", "Necklace" })
            {
                var items = ui.Items(category);
                Assert.That(items.Count, Is.EqualTo(36));
                for (int grade = 0; grade < 9; grade++) Assert.That(items.Count(x => x.rarity == grade), Is.EqualTo(grade < 6 ? 5 : grade == 6 ? 3 : grade == 7 ? 2 : 1));
                for (int index = 0; index < items.Count - 1; index++)
                {
                    var item = items[index]; var next = items[index + 1];
                    Assert.That(ui.SynthesisTarget(item), Is.SameAs(next));
                    Assert.That(next.equipValue, Is.GreaterThan(item.equipValue));
                    item.discovered = true; item.level = 99; item.count = 20;
                    Assert.That(ui.SynthesizeItem(item), Is.Zero);
                    Assert.That(ui.UpgradeItem(item), Is.True);
                    Assert.That(item.level, Is.EqualTo(100));
                    item.count = ui.CopiesNeeded(item);
                    Assert.That(ui.UpgradeItem(item), Is.False);
                    item.level = 100; item.count = 14; int before = next.count;
                    Assert.That(ui.SynthesizeItem(item, true), Is.EqualTo(2));
                    Assert.That(item.count, Is.EqualTo(4));
                    Assert.That(item.level, Is.EqualTo(100));
                    Assert.That(next.count, Is.EqualTo(before + 2));
                    Assert.That(ui.SynthesizeItem(item), Is.Zero);
                }
                var god = items.Last(); god.level = 1000; god.count = 1000; god.discovered = true;
                Assert.That(ui.UpgradeItem(god), Is.True);
                Assert.That(god.level, Is.EqualTo(1001));
                Assert.That(ui.SynthesisTarget(god), Is.Null);
                Assert.That(ui.SynthesizeItem(god, true), Is.Zero);
            }
            UiOpen("Equipment");
            Assert.That(UiNode("Collection actions").GetChild(0).name, Is.EqualTo("일괄 합성"));
            foreach (var item in ui.Items("Armor")) {
                var slot = UiNode("Slot: " + item.name, UiNode("Collection inventory"));
                Assert.That(slot.Find("Quantity gauge/Fill").GetComponent<Image>().color, Is.EqualTo(item.rarity == 8 ? UiKit.Green : UiKit.Purple));
            }
            Assert.That(UiNode("Collection inventory").GetComponentsInChildren<Text>().Count(x => x.name == "Enhancement level"), Is.EqualTo(36));
            Assert.That(UiNode("Collection inventory").GetComponentsInChildren<Text>().Where(x=>x.name=="Enhancement level").All(x=>x.text.StartsWith("Lv.")),Is.True);
            Object.Destroy(CaptureFrame("revision-equipment-synthesis.png", 720, 1520));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionLegacyOverCapLevelsReturnCopiesOnlyOnce()
        {
            game.TogglePause();
            string saved = PlayerPrefs.GetString("DoodleUi.Collections.v1", "");
            var host = new GameObject("Migration fixture");
            try
            {
                PlayerPrefs.SetString("DoodleUi.Collections.v1", "{\"version\":2,\"items\":[{\"id\":\"armor_0\",\"count\":7,\"level\":102,\"discovered\":true}],\"stats\":[]}");
                var ui = host.AddComponent<DoodleUi>(); ui.InitCollections();
                var item = ui.Items("Armor").First(x => x.id == "armor_0");
                Assert.That(item.level, Is.EqualTo(100));
                Assert.That(item.count, Is.EqualTo(36), "The old level 100 and 101 upgrades cost 14 and 15 copies.");
                ui.SaveCollections();
                var reload = new GameObject("Migration reload fixture");
                try
                {
                    var restored = reload.AddComponent<DoodleUi>(); restored.InitCollections();
                    Assert.That(restored.Items("Armor").First(x => x.id == "armor_0").count, Is.EqualTo(36));
                }
                finally { Object.DestroyImmediate(reload); }
            }
            finally { Object.DestroyImmediate(host); PlayerPrefs.SetString("DoodleUi.Collections.v1", saved); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionCriticalLockRefundTracksPricesAndSkillSlotsStayInOneRow()
        {
            game.TogglePause(); var ui = game.Ui;
            Assert.That(ui.Critical4Unlocked, Is.False);
            long gold = ui.Gold;
            Assert.That(ui.UpgradeStat("crit4Chance", 1), Is.False);
            Assert.That(ui.Gold, Is.EqualTo(gold));
            UiOpen("Stats");
            Assert.That(UiNode("Stat crit4Chance").GetComponentsInChildren<Button>().Single().interactable, Is.False);
            GrowthLevels["crit2Chance"] = 3999;
            ui.Gold = long.MaxValue;
            Assert.That(ui.StatUpgradeQuote("crit2Chance", 100, out int upgrades), Is.EqualTo(ui.StatUpgradeQuote("crit2Chance", 1, out _)));
            Assert.That(upgrades, Is.EqualTo(1));
            Assert.That(ui.UpgradeStat("crit2Chance", 100), Is.True);
            Assert.That(ui.Critical4Unlocked, Is.True);
            ui.Gold = ui.StatUpgradeQuote("crit4Chance", 1, out _);
            Assert.That(ui.UpgradeStat("crit4Chance", 1), Is.True);
            var skill = ui.Items("Skill")[0]; skill.discovered = true; skill.level = 99; skill.count = 100;
            Assert.That(ui.RefundSkill(skill), Is.Zero);
            Assert.That(ui.UpgradeItem(skill), Is.True);
            Assert.That(ui.UpgradeItem(skill), Is.False);
            var tuning = (DoodleUi.CommerceTuning)typeof(DoodleUi).GetField("commerceTuning", GrowthPrivate).GetValue(ui);
            tuning.skillUnitCost = 15;
            skill.count = 7; int diamonds = ui.Diamonds;
            Assert.That(ui.SkillRefundQuote(skill), Is.EqualTo(105));
            Assert.That(ui.RefundSkill(skill), Is.EqualTo(105));
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds + 105));
            Assert.That(skill.count, Is.Zero);
            Assert.That(ui.RefundSkill(skill), Is.Zero);
            tuning.skillUnitCost = 35;
            ui.AddItem(skill, 2);
            Assert.That(ui.SkillRefundQuote(skill), Is.EqualTo(70));
            Assert.That(ui.RefundSkill(skill), Is.EqualTo(70));
            ui.AddItem(skill, 5); ui.Diamonds = int.MaxValue;
            Assert.That(ui.RefundSkill(skill), Is.Zero);
            Assert.That(skill.count, Is.EqualTo(5), "A full wallet cannot destroy unpaid copies.");
            UiOpen("Skills");
            Assert.That(UiNode("Equipped Skill").GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(8));
            Assert.That(UiNode("Collection inventory").GetComponent<GridLayoutGroup>().constraintCount, Is.EqualTo(5));
            Object.Destroy(CaptureFrame("revision-skills-eight-slots.png", 720, 1520));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionPopulationRefillsBelow100AndBossStartsAfterStageKillGoal()
        {
            game.TogglePause(); var ui = game.Ui;
            var refill = typeof(DoodleIdleGame).GetMethod("Refill", GrowthPrivate);
            ui.ToggleBreakthroughMode();
            Assert.That(game.EnemyCount, Is.EqualTo(200));
            DefeatActualServiceEnemies(99);
            Assert.That(game.EnemyCount, Is.EqualTo(101));
            refill.Invoke(game, null);
            Assert.That(game.EnemyCount, Is.EqualTo(101));
            DefeatActualServiceEnemies(1);
            Assert.That(ui.MainStage, Is.Zero);
            Assert.That(ui.MainStageKillProgress, Is.Zero);
            refill.Invoke(game, null);
            Assert.That(game.EnemyCount, Is.EqualTo(100), "Exactly 100 does not replenish yet.");
            DefeatActualServiceEnemies(1);
            refill.Invoke(game, null);
            Assert.That(game.EnemyCount, Is.EqualTo(200));
            Assert.That(ui.MainStageKillProgress, Is.EqualTo(1), "Population refill must not clear credited kills.");
            Assert.That(game.BossActive, Is.False);
            ui.ToggleBreakthroughMode();
            int remaining = ui.MainStageRemaining;
            DefeatActualServiceEnemies(remaining);
            Assert.That(game.EnemyCount, Is.EqualTo(200 - remaining), "Boss eligibility occurs with surviving ordinary enemies.");
            Assert.That(ui.MainStage, Is.Zero);
            int kills = game.Kills;
            refill.Invoke(game, null);
            Assert.That(game.BossActive, Is.True);
            Assert.That(game.EnemyCount, Is.EqualTo(1));
            Assert.That(game.Kills, Is.EqualTo(kills), "Removing the field for a boss must not grant extra kill rewards.");
            DefeatActualServiceEnemies(1);
            Assert.That(ui.MainStage, Is.EqualTo(1));
            Assert.That(ui.MainStageKillProgress, Is.Zero);
            refill.Invoke(game, null);
            Assert.That(game.EnemyCount, Is.EqualTo(200));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RevisionAllTenThemesHaveThreePairedEnemiesAndCycleAfterOneThousand()
        {
            game.TogglePause();
            Assert.That(DoodleIdleGame.ThemeIndexForStage(1), Is.Zero);
            Assert.That(DoodleIdleGame.ThemeIndexForStage(100), Is.Zero);
            Assert.That(DoodleIdleGame.ThemeIndexForStage(101), Is.EqualTo(1));
            Assert.That(DoodleIdleGame.ThemeIndexForStage(1000), Is.EqualTo(9));
            Assert.That(DoodleIdleGame.ThemeIndexForStage(1001), Is.Zero);
            Assert.That(DoodleIdleGame.ThemeIndexForStage(2001), Is.Zero);
            Assert.That(game.GetComponentsInChildren<Transform>().Any(x => x.name == "Doodle boulder / solid collider"), Is.False);
            var state = typeof(DoodleUi).GetField("services", GrowthPrivate).GetValue(game.Ui);
            for (int theme = 0; theme < 10; theme++)
            {
                state.GetType().GetField("mainStage").SetValue(state, theme * 100);
                state.GetType().GetField("mainStageKillProgress").SetValue(state, 0);
                game.RequestCombatWaveReset(); game.paused = false;
                typeof(DoodleIdleGame).GetMethod("FixedUpdate", GrowthPrivate).Invoke(game, null);
                game.TogglePause();
                Assert.That(game.CurrentThemeIndex, Is.EqualTo(theme));
                game.Ui.RefreshHud();
                Assert.That(game.EnemyCount, Is.EqualTo(200));
                var frames = (Sprite[][])typeof(DoodleIdleGame).GetField("enemyWalkFrames", GrowthPrivate).GetValue(game);
                Assert.That(frames.Length, Is.EqualTo(3));
                foreach (var pair in frames)
                {
                    Assert.That(pair.Length, Is.EqualTo(2));
                    Assert.That(pair[0].rect.size, Is.EqualTo(pair[1].rect.size));
                    Assert.That(pair[0].rect, Is.Not.EqualTo(pair[1].rect));
                    Assert.That(pair[0].texture.name, Is.Not.EqualTo("Characters"));
                }
                Object.Destroy(CaptureFrame("revision-theme-" + theme + ".png", 720, 1520));
            }
            yield return null;
        }
    }
}
