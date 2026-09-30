using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        int CurrentSummonResultCount() => UiNode("SummonResultCards").GetComponentsInChildren<Text>(true)
            .Where(t => t.name == "Draw quantity").Sum(t => int.Parse(t.text.Substring(1), NumberStyles.AllowThousands, CultureInfo.InvariantCulture));

        [UnityTest]
        public IEnumerator BulkRollsPreserveEverySeededResultAcrossLevelBoundaries()
        {
            game.TogglePause(); var ui = game.Ui;
            var states = (Dictionary<string,DoodleUi.SummonState>)typeof(DoodleUi).GetField("summonStates",GrowthPrivate).GetValue(ui);
            var needed = (Func<DoodleUi.SummonState,int>)Delegate.CreateDelegate(typeof(Func<DoodleUi.SummonState,int>),ui,typeof(DoodleUi).GetMethod("CommerceExperienceNeeded",GrowthPrivate));
            var advance = (Action<DoodleUi.SummonState,int>)Delegate.CreateDelegate(typeof(Action<DoodleUi.SummonState,int>),ui,typeof(DoodleUi).GetMethod("AdvanceSummonExperience",GrowthPrivate));
            var roll = typeof(DoodleUi).GetMethod("RollSummonRewards",GrowthPrivate);
            foreach (var scenario in new[] { ("Necklace",1,50000), ("Skill",49,1000), ("Relic",1,1000) }) {
                string category=scenario.Item1; var state=states[category]; state.level=scenario.Item2;
                state.experience=category=="Relic"?0:needed(state)-1;
                int level=state.level, experience=state.experience;
                var cursor=new DoodleUi.SummonState{category=category,level=level,experience=experience};
                var items=ui.Items(category);
                var pools=Enumerable.Range(0,9).Select(g=>items.Where(x=>x.rarity==g).ToList()).ToArray();
                var rng=new System.Random(371); var expected=new List<UiItem>();
                // Reference transaction advances exactly one draw at a time.
                for(int i=0;i<scenario.Item3;i++) {
                    if(category=="Relic")expected.Add(items[rng.Next(items.Count)]);
                    else {
                        var weights=ui.SummonWeights(category,cursor.level); int chance=rng.Next(DoodleUi.SummonWeightTotal),grade=0;
                        while(grade<weights.Length-1&&chance>=weights[grade]){chance-=weights[grade];grade++;}
                        var choices=pools[grade];
                        int Weight(int n)=>choices.Count==1?1:Math.Max(1,10-n);
                        int total=Enumerable.Range(0,choices.Count).Sum(Weight),tierRoll=rng.Next(total),tier=0;
                        while(tier<choices.Count-1&&tierRoll>=Weight(tier)){tierRoll-=Weight(tier);tier++;}
                        expected.Add(choices[tier]);
                    }
                    advance(cursor,1);
                }
                var actualRng=new System.Random(371);
                var actual=(List<UiItem>)roll.Invoke(ui,new object[]{category,scenario.Item3,actualRng});
                CollectionAssert.AreEqual(expected,actual,category);
                Assert.That(actualRng.Next(),Is.EqualTo(rng.Next()),"The next RNG call must also be identical.");
                Assert.That((int)state.level,Is.EqualTo(level)); Assert.That((int)state.experience,Is.EqualTo(experience));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SummonMultiplierResetsOnlyWhenTheResultSessionCloses()
        {
            game.TogglePause(); var ui=game.Ui; ui.SkipSummonAnimations=true; ui.Diamonds=int.MaxValue;
            var show=typeof(DoodleUi).GetMethod("ShowSummonResults",GrowthPrivate);
            var item=ui.Items("Necklace")[0]; var rewards=new List<UiItem>{item};
            int Multiplier()=>(int)typeof(DoodleUi).GetField("summonResultMultiplier",GrowthPrivate).GetValue(ui);
            for(int close=0;close<3;close++) {
                if(ui.ActivePage!="Equipment")UiOpen("Equipment");
                var inventory=UiNode("Collection inventory");
                show.Invoke(ui,new object[]{"Necklace",rewards});
                Assert.That(Multiplier(),Is.EqualTo(1));
                Assert.That(UiNode("PaidSummon50"),Is.Not.Null);
                UiClick("Summon multiplier 1000");
                typeof(DoodleUi).GetMethod("ShowSummonItem",GrowthPrivate).Invoke(ui,new object[]{item});
                ui.CloseDetail();
                Assert.That(Multiplier(),Is.EqualTo(1000),"Closing an item preview keeps the result session selection.");
                Assert.That(ui.TrySummon("Necklace",50000,false),Is.True);
                Assert.That(CurrentSummonResultCount(),Is.EqualTo(50000));
                Assert.That(UiNode("Collection inventory"),Is.SameAs(inventory),"Repeat draws must not rebuild the covered collection page.");
                Assert.That(Multiplier(),Is.EqualTo(1000),"Repeating a draw in the same session keeps its multiplier.");
                if(close==0)UiClick("확인",UiNode("Fullscreen: 뽑기 결과"));
                else if(close==1)ui.CloseDetail(); // Escape closes the top overlay through this path.
                else ui.ClosePage();
                Assert.That(Multiplier(),Is.EqualTo(1));
                if(close<2)Assert.That(UiNode("Collection inventory"),Is.Not.SameAs(inventory),"Closing results refreshes the newly earned inventory.");
                yield return new WaitForSecondsRealtime(.2f);
            }
        }

        [UnityTest]
        public IEnumerator BulkSummonsChangeProbabilitiesAtEachExactLevelBoundary()
        {
            game.TogglePause(); var ui = game.Ui; ui.SkipSummonAnimations = true;
            var tuning = (DoodleUi.CommerceTuning)typeof(DoodleUi).GetField("commerceTuning", GrowthPrivate).GetValue(ui);
            tuning.levelExperience = new[] { 10, 500, 100, 200, 300, 400, 500, 600, 700, 20000 };
            tuning.rates = Enumerable.Range(1, 50).Select(level => {
                var weights = new int[9]; weights[(level-1)%9] = DoodleUi.SummonWeightTotal;
                return new DoodleUi.SummonRateTier { level = level, basisPoints = weights };
            }).ToArray();
            var roll = typeof(DoodleUi).GetMethod("RollSummonRewards", GrowthPrivate);
            var rewards = (List<UiItem>)roll.Invoke(ui, new object[] { "Necklace", 10000, new System.Random(14) });
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(1), "Previewing the transaction cannot mutate progression before payment.");
            int start = 0;
            for (int level = 1; level <= 10; level++) {
                int count = level < 10 ? tuning.levelExperience[level-1] : 10000-start;
                Assert.That(rewards.Skip(start).Take(count).All(x => x.rarity == (level-1)%9), Is.True, "Every draw at level " + level);
                start += count;
            }
            int cost = ui.SummonCost("Necklace", 10000);
            ui.Diamonds = cost - 1;
            Assert.That(ui.TrySummon("Necklace", 10000, false), Is.False);
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(1));
            Assert.That(ui.Items("Necklace").Sum(x => x.count), Is.Zero);
            ui.Diamonds = cost;
            Assert.That(ui.TrySummon("Necklace", 10000, false), Is.True);
            Assert.That(ui.Diamonds, Is.Zero);
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(10));
            Assert.That(ui.SummonExperience("Necklace"), Is.EqualTo(6690));
            Assert.That(ui.Items("Necklace").Sum(x => x.count), Is.EqualTo(10000));
            Assert.That(ui.Items("Necklace").Where(x => x.rarity == 1).Sum(x => x.count), Is.EqualTo(500));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(10000));
            Assert.That(UiNode("SummonResultCards").childCount, Is.LessThanOrEqualTo(36));
            // This fixture uses synthetic XP thresholds; inspect the saved transaction before
            // a fresh loader normalizes it against the production progression thresholds.
            Assert.That(PlayerPrefs.GetString("DoodleUi.Commerce.Necklace"), Does.Contain("\"level\":10"));
            Assert.That(PlayerPrefs.GetString("DoodleUi.Commerce.Necklace"), Does.Contain("6690"));
            yield return null;
            Object.Destroy(CaptureFrame("bulk-summon-10000-level-boundaries.png", 720, 1520));
        }

        [UnityTest]
        public IEnumerator BulkSummonTogglesScalePriceAggregateCountsAndKeepFreeDrawsFixed()
        {
            game.TogglePause(); var ui = game.Ui; ui.SkipSummonAnimations = true;
            var items = ui.Items("Necklace"); ui.AddItem(items[0], 1234);
            typeof(DoodleUi).GetMethod("ShowSummonResults", GrowthPrivate).Invoke(ui, new object[] { "Necklace", new List<UiItem> { items[0], items[0], items[1] } });
            Assert.That(UiNode("SummonResultCards").childCount, Is.EqualTo(2));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(3), "Only this draw's count appears, not the inventory total.");
            Assert.That(UiNode("SummonResultCards").GetComponentsInChildren<Text>().Any(t => t.text == "일반 1"), Is.True);
            Assert.That(UiNode("SummonResultCards").GetComponentsInChildren<Text>().Any(t => t.text == "×2"), Is.True);
            ui.Diamonds = 20000000;
            foreach (int multiplier in new[] { 1, 10, 100, 1000 }) {
                UiClick("Summon multiplier " + multiplier); yield return null;
                var button = UiNode("PaidSummon" + (50*multiplier));
                Assert.That(button.GetComponentInChildren<Button>().name, Is.EqualTo((50*multiplier) + "회 뽑기"));
                Assert.That(UiNode("SummonDiamondCost", button).GetComponent<Text>().text, Is.EqualTo(ui.SummonCost("Necklace", 50*multiplier).ToString("N0")));
                Assert.That(ui.FreeSummonsRemaining("Necklace"), Is.EqualTo(3));
            }
            var skip = (RectTransform)UiNode("Summon animation skip");
            var toggle = (RectTransform)UiNode("Summon multipliers");
            Canvas.ForceUpdateCanvases();
            Assert.That(skip.position.x, Is.LessThan(toggle.position.x));
            int before = items.Sum(x => x.count), diamonds = ui.Diamonds;
            int price = ui.SummonCost("Necklace", 50000);
            UiClick("50000회 뽑기", UiNode("Fullscreen: 뽑기 결과"));
            Assert.That(items.Sum(x => x.count), Is.EqualTo(before + 50000));
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds-price));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(50000));
            Assert.That(UiNode("SummonResultCards").childCount, Is.LessThanOrEqualTo(36));
            Assert.That(ui.SummonLevel("Necklace"), Is.EqualTo(12));
            Assert.That(ui.SummonExperience("Necklace"), Is.EqualTo(7250));
            yield return null;
            Object.Destroy(CaptureFrame("bulk-summon-50000-results.png", 720, 1520));
            before = items.Sum(x => x.count); diamonds = ui.Diamonds;
            UiClick("무료 5회\n뽑기", UiNode("Fullscreen: 뽑기 결과"));
            Assert.That(items.Sum(x => x.count), Is.EqualTo(before + 5));
            Assert.That(ui.Diamonds, Is.EqualTo(diamonds));
            Assert.That(CurrentSummonResultCount(), Is.EqualTo(5));
            Assert.That(ui.TrySummon("Necklace", int.MaxValue, false), Is.False);
        }
    }
}
