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
        public IEnumerator PercentageDamageUsesAttackCriticalAndOnlyMatchingRelicBonus()
        {
            game.TogglePause(); var ui = game.Ui;
            var enemy = ((IList)typeof(DoodleIdleGame).GetField("enemies", GrowthPrivate).GetValue(game))[0];
            var health = enemy.GetType().GetField("hp");
            var damage = typeof(DoodleIdleGame).GetMethod("DamageByCategory", GrowthPrivate);
            float Hit(string category) {
                health.SetValue(enemy, 100000f);
                damage.Invoke(game, new object[] { enemy, 128f, Vector2.zero, category });
                return 100000 - (float)health.GetValue(enemy);
            }
            foreach (string category in new[] { "Basic", "Skill", "Companion" })
                Assert.That(Hit(category), Is.EqualTo(ui.CurrentAttackPower).Within(.02f), "A 100% hit scales with current attack.");
            float before = Hit("Skill"); GrowthLevels["attack"] += 10;
            Assert.That(Hit("Skill"), Is.GreaterThan(before));
            foreach (var pair in new[] { new[] { "Basic", "basicAttack" }, new[] { "Skill", "skillAttack" }, new[] { "Companion", "companionAttack" } }) {
                var relic = ui.Items("Relic").Single(x => x.effect == pair[1]);
                float[] values = new[] { Hit("Basic"), Hit("Skill"), Hit("Companion") };
                relic.discovered = true; relic.level = 10;
                int index = 0;
                foreach (string category in new[] { "Basic", "Skill", "Companion" })
                    Assert.That(Hit(category), Is.EqualTo(values[index++] * (category == pair[0] ? 1.2f : 1)).Within(.03f));
                relic.discovered = false; relic.level = 0;
            }
            var general = ui.Items("Relic").Single(x => x.effect == "attack");
            before = Hit("Companion"); general.level += 10;
            Assert.That(Hit("Companion"), Is.GreaterThan(before));
            GrowthLevels["crit2Chance"] = 1000;
            var critical = ui.Items("Relic").Single(x => x.effect == "critDamage"); critical.discovered = true; critical.level = 10;
            foreach (string category in new[] { "Basic", "Skill", "Companion" })
                Assert.That(Hit(category), Is.EqualTo(ui.CurrentAttackPower * 2.4f).Within(.03f));
            GrowthLevels["crit4Chance"] = 2000;
            Assert.That(Hit("Skill"), Is.EqualTo(ui.CurrentAttackPower * 4.8f).Within(.03f));
            foreach (string category in new[] { "Skill", "Companion" }) foreach (var item in ui.Items(category)) {
                Assert.That(ui.ItemHitPercent(item), Is.GreaterThan(0), item.id);
                Assert.That(ui.ItemExpectedDps(item), Is.GreaterThan(0), item.id);
            }
            var meteor = ui.Items("Skill").Single(x => x.ability == "Meteor");
            Assert.That(ui.ItemExpectedDps(meteor), Is.EqualTo(ui.ItemHitDamage(meteor) * 3 * ui.ExpectedCriticalMultiplier / 10).Within(.001));
            UiOpen("Skills"); typeof(DoodleUi).GetMethod("ShowCollectionDetail", GrowthPrivate).Invoke(ui, new object[] { meteor });
            Assert.That(UiNode("Expected DPS").GetComponentsInChildren<Text>().Any(x => x.text == "예상 총 DPS"), Is.True);
            Object.Destroy(CaptureFrame("latest-skill-damage-details.png", 720, 1520));
            ui.CloseDetail(); UiOpen("Relics"); Object.Destroy(CaptureFrame("latest-eight-relics.png", 720, 1520));
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuffBadgesOpenPopupChatMatchesEquipmentAndRewardsFlyToWalletOnce()
        {
            game.TogglePause(); Time.timeScale = 0; var ui = game.Ui;
            UiClick("Gold buff"); Assert.That(ui.ActivePage, Is.EqualTo("Buffs")); ui.ClosePage();
            ui.ExtendBuff(true); ui.ClosePage();
            UiClick("Attack buff"); Assert.That(ui.ActivePage, Is.EqualTo("Buffs"));
            UiOpen("Equipment"); DoodlePopupMotion.CompleteAll(UiRoot);
            var equipment = UiRoot.GetComponentsInChildren<DoodleUiWindow>().Single(x => x.bottomNavigation);
            Vector2 size = ((RectTransform)equipment.transform).rect.size;
            UiOpen("Chat"); DoodlePopupMotion.CompleteAll(UiRoot);
            var chat = UiRoot.GetComponentsInChildren<DoodleUiWindow>().Single(x => x.bottomNavigation);
            Assert.That(chat.full, Is.False); Assert.That(((RectTransform)chat.transform).rect.size, Is.EqualTo(size));
            Assert.That(UiNode("Bottom navigation").gameObject.activeInHierarchy, Is.True);
            Object.Destroy(CaptureFrame("latest-chat-window.png", 720, 1520)); ui.ClosePage();
            long gold = ui.Gold; int diamonds = ui.Diamonds;
            ui.ShowRewards("획득!", new System.Collections.Generic.List<UiReward> { new UiReward { icon="Gold",amount=100 }, new UiReward { icon="Diamond",amount=100 } });
            DoodlePopupMotion.CompleteAll(UiRoot); Canvas.ForceUpdateCanvases();
            UiClick("Reward dim");
            Assert.That(UiNode("Reward flight Gold").childCount, Is.EqualTo(8));
            Assert.That(UiNode("Reward flight Diamond").childCount, Is.EqualTo(8));
            var flight = UiNode("Flying Diamond"); Vector3 start = flight.position;
            ui.CloseDetail();
            // Capture intermediate motion before a slow CI frame can finish the entire flight.
            bool moved = false;
            for (int i = 0; i < 60 && flight; i++) {
                yield return null;
                if (flight && Vector3.Distance(start, flight.position) > 5) { moved = true; break; }
            }
            Assert.That(moved || !flight, Is.True);
            Object.Destroy(CaptureFrame("latest-wallet-reward-flight.png", 720, 1520));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(x => x.name.StartsWith("Reward flight")), Is.False);
            Assert.That(ui.Gold, Is.EqualTo(gold)); Assert.That(ui.Diamonds, Is.EqualTo(diamonds));
        }

        [UnityTest]
        public IEnumerator SummonPricesSequentialRevealSkipRepeatAndAllProbabilityLevels()
        {
            game.TogglePause(); Time.timeScale = 0; var ui = game.Ui;
            bool oldSkip = ui.SkipSummonAnimations; ui.SkipSummonAnimations = false; ui.Diamonds = 100000;
            try {
                foreach (var pair in new[] { new[] { "Armor", "10" }, new[] { "Club", "10" }, new[] { "Skill", "200" }, new[] { "Companion", "200" }, new[] { "Relic", "100" } }) {
                    int unit = int.Parse(pair[1]); Assert.That(ui.SummonCost(pair[0], 10), Is.EqualTo(unit * 10)); Assert.That(ui.SummonCost(pair[0], 50), Is.EqualTo(unit * 50));
                }
                Assert.That(ui.SkillRefundUnitPrice, Is.EqualTo(200));
                Assert.That(ui.TrySummon("Skill", 10, false), Is.True); Assert.That(ui.Diamonds, Is.EqualTo(98000));
                var reveal = UiRoot.GetComponentInChildren<DoodleSummonReveal>();
                Assert.That(reveal.VisibleCards, Is.Zero);
                var cards = UiNode("SummonResultCards").GetComponentsInChildren<CanvasGroup>();
                yield return new WaitForSecondsRealtime(.24f);
                Assert.That(cards[0].alpha, Is.GreaterThan(cards.Last().alpha));
                Assert.That(UiNode("SummonResultCards").GetComponentsInChildren<Transform>().Any(x => x.name == "Quantity gauge"), Is.False);
                UiClick("Summon animation skip"); Assert.That(reveal.VisibleCards, Is.EqualTo(10));
                UiClick("50회 뽑기", UiNode("Fullscreen: 뽑기 결과")); Assert.That(ui.Diamonds, Is.EqualTo(88000));
                reveal = UiRoot.GetComponentInChildren<DoodleSummonReveal>();
                Assert.That(reveal.VisibleCards, Is.EqualTo(50)); Assert.That(reveal.transform.localScale, Is.EqualTo(Vector3.one));
                Object.Destroy(CaptureFrame("latest-summon-no-gauges.png", 720, 1520));
                ui.CloseFullscreen();
                int level = ui.SummonLevel("Armor"), experience = ui.SummonExperience("Armor");
                ui.ShowSummonProbabilities("Armor");
                Assert.That(UiNode("Previous probability level").GetComponent<Button>().interactable, Is.False);
                var god = ui.Items("Armor").Single(x => x.rarity == 6);
                Assert.That(ui.PreviewItemProbability(god, 1), Is.Zero);
                for (int page = 2; page <= 30; page++) { UiClick("Next probability level"); yield return null; }
                Assert.That(UiNode("Probability level title").GetComponent<Text>().text, Does.Contain("30"));
                Assert.That(UiNode("Next probability level").GetComponent<Button>().interactable, Is.False);
                Assert.That(UiNode("Probability_" + god.id).GetComponentsInChildren<Text>().Any(x => x.text == "1%"), Is.True);
                Assert.That(UiNode("Detail dim: 뽑기 확률").GetComponentsInChildren<Text>().Any(x => x.text.Contains("등장") || x.text.Contains("등급별 확률")), Is.False);
                Assert.That(ui.SummonLevel("Armor"), Is.EqualTo(level)); Assert.That(ui.SummonExperience("Armor"), Is.EqualTo(experience));
                Object.Destroy(CaptureFrame("latest-probability-level-30.png", 720, 1520));
                ui.CloseDetail(); ui.ShowSummonProbabilities("Relic");
                Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(x => x.name == "Next probability level"), Is.False);
                Assert.That(ui.Items("Relic").All(x => ui.PreviewItemProbability(x, 0) == 12.5), Is.True);
            } finally { ui.SkipSummonAnimations = oldSkip; }
        }
    }
}
