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
                    Assert.That(Hit(category), Is.EqualTo(values[index++] * (category == pair[0] ? 1.1f : 1)).Within(.03f));
                relic.discovered = false; relic.level = 0;
            }
            var general = ui.Items("Relic").Single(x => x.effect == "attack");
            before = Hit("Companion"); general.level += 10;
            Assert.That(Hit("Companion"), Is.GreaterThan(before));
            GrowthLevels["crit2Chance"] = 4000;
            var critical = ui.Items("Relic").Single(x => x.effect == "critDamage"); critical.discovered = true; critical.level = 10;
            foreach (string category in new[] { "Basic", "Skill", "Companion" })
                Assert.That(Hit(category), Is.EqualTo(ui.CurrentAttackPower * 2.2f).Within(.03f));
            GrowthLevels["crit4Chance"] = 2000;
            Assert.That(Hit("Skill"), Is.EqualTo(ui.CurrentAttackPower * 4.4f).Within(.03f));
            foreach (string category in new[] { "Skill", "Companion" }) foreach (var item in ui.Items(category)) {
                Assert.That(ui.ItemHitPercent(item), Is.GreaterThan(0), item.id);
                Assert.That(ui.ItemExpectedDps(item), Is.GreaterThan(0), item.id);
            }
            var meteor = ui.Items("Skill").Single(x => x.ability == "Meteor");
            // Combat coefficients are floats; compare algebraically equivalent
            // total-budget and per-hit formulas at six significant digits.
            Assert.That(ui.ItemExpectedDps(meteor), Is.EqualTo(ui.ItemHitDamage(meteor) * 3 * ui.ExpectedCriticalMultiplier / 10).Within(ui.ItemExpectedDps(meteor) * .000001));
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
            string[] ticketCategories = { "Armor", "Club", "Necklace", "Skill", "Companion", "Relic", "DungeonRelic" };
            var ticketAmounts = ticketCategories.Select(ui.SummonTickets).ToArray();
            var rewards = new System.Collections.Generic.List<UiReward> { new UiReward { icon="Gold",amount=100 }, new UiReward { icon="Diamond",amount=100 } };
            foreach (string category in ticketCategories) rewards.Add(new UiReward { icon = DoodleUi.TicketIcon(category), amount = 10 });
            ui.ShowRewards("획득!", rewards);
            DoodlePopupMotion.CompleteAll(UiRoot); Canvas.ForceUpdateCanvases();
            UiClick("Reward dim");
            Assert.That(UiNode("Reward flight Gold").childCount, Is.EqualTo(8));
            Assert.That(UiNode("Reward flight Diamond").childCount, Is.EqualTo(8));
            foreach (var reward in rewards) {
                var particles = UiNode("Reward flight " + reward.icon).GetComponentsInChildren<Image>();
                Assert.That(particles.Length, Is.EqualTo(8), reward.icon);
                foreach (var particle in particles) {
                    Assert.That(particle.rectTransform.sizeDelta, Is.EqualTo(Vector2.one * 108), reward.icon + " matches diamond particle size");
                    Assert.That(particle.sprite, Is.SameAs(UiKit.Art(reward.icon)));
                }
            }
            Object.Destroy(CaptureFrame("latest-wallet-reward-flight.png", 720, 1520));
            var flight = UiNode("Flying Diamond"); Vector3 start = flight.position;
            ui.CloseDetail();
            // Capture intermediate motion before a slow CI frame can finish the entire flight.
            bool moved = false;
            for (int i = 0; i < 60 && flight; i++) {
                yield return null;
                if (flight && Vector3.Distance(start, flight.position) > 5) { moved = true; break; }
            }
            Assert.That(moved || !flight, Is.True);
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(x => x.name.StartsWith("Reward flight")), Is.False);
            Assert.That(ui.Gold, Is.EqualTo(gold)); Assert.That(ui.Diamonds, Is.EqualTo(diamonds));
            CollectionAssert.AreEqual(ticketAmounts, ticketCategories.Select(ui.SummonTickets).ToArray(), "Closing reward effects never grants the reward a second time.");
            var destination = typeof(DoodleUi).GetMethod("RewardFlightDestination", GrowthPrivate);
            UiOpen("Shop"); DoodlePopupMotion.CompleteAll(UiRoot); Canvas.ForceUpdateCanvases();
            foreach (string category in ticketCategories) {
                var target = (RectTransform)destination.Invoke(ui, new object[] { DoodleUi.TicketIcon(category) });
                Assert.That(target, Is.Not.Null);
                Assert.That(target.gameObject.activeInHierarchy, Is.True);
            }
        }

        [UnityTest]
        public IEnumerator SummonPricesSequentialRevealSkipRepeatAndAllProbabilityLevels()
        {
            game.TogglePause(); Time.timeScale = 0; var ui = game.Ui;
            bool oldSkip = ui.SkipSummonAnimations; ui.SkipSummonAnimations = false; ui.Diamonds = 100000;
            try {
                foreach (var pair in new[] { new[] { "Armor", "10" }, new[] { "Club", "10" }, new[] { "Skill", "20" }, new[] { "Companion", "20" }, new[] { "Relic", "10" } }) {
                    int unit = int.Parse(pair[1]); Assert.That(ui.SummonCost(pair[0], 1), Is.EqualTo(unit)); Assert.That(ui.SummonCost(pair[0], 10), Is.EqualTo(unit * 10)); Assert.That(ui.SummonCost(pair[0], 50), Is.EqualTo(unit * 50));
                }
                Assert.That(ui.SkillRefundUnitPrice, Is.EqualTo(20));
                Assert.That(ui.TrySummon("Skill", 10, false), Is.True); Assert.That(ui.Diamonds, Is.EqualTo(99800));
                var reveal = UiRoot.GetComponentInChildren<DoodleSummonReveal>();
                Assert.That(reveal.VisibleCards, Is.Zero);
                var cards = UiNode("SummonResultCards").GetComponentsInChildren<CanvasGroup>();
                yield return new WaitForSecondsRealtime(.24f);
                Assert.That(cards[0].alpha, Is.GreaterThan(cards.Last().alpha));
                Assert.That(UiNode("SummonResultCards").GetComponentsInChildren<Transform>().Any(x => x.name == "Quantity gauge"), Is.False);
                var gauge=UiNode("Summon experience gauge");
                Assert.That(gauge.GetComponentInChildren<Text>().text,Does.Contain("/"));
                var skip=UiNode("Summon animation skip");
                Assert.That(skip.GetComponentInChildren<Text>().text,Is.EqualTo("연출 스킵"));
                Assert.That(skip.Find("Skip toggle track/Toggle knob"),Is.Not.Null);
                Assert.That(gauge.GetSiblingIndex(),Is.LessThan(skip.parent.GetSiblingIndex()));
                Assert.That(skip.parent.GetSiblingIndex(),Is.LessThan(skip.parent.parent.Find("SummonActions").GetSiblingIndex()));
                Assert.That(UiNode("Fullscreen: 뽑기 결과").GetComponentsInChildren<Text>().Any(x=>x.text.Contains("상세 보기")),Is.False);
                Object.Destroy(CaptureFrame("summon-large-confetti-and-controls.png",720,1520));
                UiClick("Summon animation skip"); Assert.That(reveal.VisibleCards, Is.EqualTo(UiNode("SummonResultCards").childCount));
                UiClick("50회 뽑기", UiNode("Fullscreen: 뽑기 결과")); Assert.That(ui.Diamonds, Is.EqualTo(98800));
                reveal = UiRoot.GetComponentInChildren<DoodleSummonReveal>();
                Assert.That(reveal.VisibleCards, Is.EqualTo(UiNode("SummonResultCards").childCount)); Assert.That(reveal.transform.localScale, Is.EqualTo(Vector3.one));
                Object.Destroy(CaptureFrame("latest-summon-no-gauges.png", 720, 1520));
                ui.CloseFullscreen();
                int level = ui.SummonLevel("Armor"), experience = ui.SummonExperience("Armor");
                ui.ShowSummonProbabilities("Armor");
                Assert.That(UiNode("Previous probability level").GetComponent<Button>().interactable, Is.False);
                var god = ui.Items("Armor").Single(x => x.rarity == 6 && x.tier == 1);
                Assert.That(ui.PreviewItemProbability(god, 1), Is.Zero);
                for (int page = 2; page <= DoodleUi.MaxSummonLevel; page++) {
                    UiClick("Next probability level");
                    Assert.That(UiNode("Detail dim: 뽑기 확률").GetComponentInChildren<DoodlePopupMotion>().transform.localScale,Is.EqualTo(Vector3.one));
                    yield return null;
                }
                Assert.That(UiNode("Probability level title").GetComponent<Text>().text, Does.Contain("50"));
                Assert.That(UiNode("Next probability level").GetComponent<Button>().interactable, Is.False);
                Assert.That(UiNode("Probability_grade_8").GetComponentsInChildren<Text>().Any(x => x.text == "0.1%"), Is.True);
                Assert.That(UiNode("Detail dim: 뽑기 확률").GetComponentsInChildren<Text>().Any(x => x.text == "등급별 확률"), Is.True);
                Assert.That(ui.SummonLevel("Armor"), Is.EqualTo(level)); Assert.That(ui.SummonExperience("Armor"), Is.EqualTo(experience));
                Object.Destroy(CaptureFrame("latest-probability-level-30.png", 720, 1520));
                ui.CloseDetail(); ui.ShowSummonProbabilities("Relic");
                Assert.That(UiRoot.GetComponentsInChildren<Transform>().Any(x => x.name == "Next probability level"), Is.False);
                Assert.That(ui.Items("Relic").All(x => ui.PreviewItemProbability(x, 0) == 12.5), Is.True);
            } finally { ui.SkipSummonAnimations = oldSkip; }
        }
    }
}
