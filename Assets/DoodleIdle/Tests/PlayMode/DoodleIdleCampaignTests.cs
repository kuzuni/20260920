using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        static readonly string[] CampaignCategories = { "Armor", "Club", "Skill", "Companion", "Relic", "DungeonRelic" };
        static readonly string[] CampaignStats = { "attack", "health", "healthRegen", "crit2Chance" };

        void CampaignClose() => game.Ui.ClosePage();

        void CampaignManage()
        {
            var ui = game.Ui;
            // All purchases/claims call the same wallet and inventory operations as UI buttons.
            for (int tab = 0; tab < 3; tab++) {
                typeof(DoodleUi).GetField("questTab", ServicePrivate).SetValue(ui, tab);
                ui.ClaimQuests(-1); CampaignClose();
            }
            for (int pass = 0; pass < 8; pass++) {
                foreach (string category in CampaignCategories) {
                    while (ui.SummonTickets(category) > 0) {
                        int tickets = ui.SummonTickets(category);
                        Assert.That(ui.TrySummonTickets(category, tickets >= 50 ? 50 : tickets >= 10 ? 10 : 1), Is.True);
                        CampaignClose();
                    }
                    foreach (var item in ui.Items(category)) {
                        if (category == "Relic" || category == "DungeonRelic") {
                            bool success; while (ui.TryUpgradeRelic(item, out success, false)) { }
                        } else while (ui.UpgradeItem(item, false)) { }
                    }
                    if (category == "Armor" || category == "Club") ui.SynthesizeAll(category);
                    if (category != "Relic" && category != "DungeonRelic") ui.AutoEquip(category);
                }
                // Equal stat levels are the user's day-one target, bought with earned gold only.
                for (int upgrade = 0; upgrade < 10000; upgrade++) {
                    string stat = CampaignStats.OrderBy(ui.StatLevel).First();
                    if (!ui.UpgradeStat(stat, 1)) break;
                }
                bool claimed = false;
                while (ui.CanClaimMainMission) {
                    Assert.That(ui.ClaimMainMission(), Is.True); CampaignClose(); claimed = true;
                }
                if (!claimed) break;
            }
            // Keep enough diamonds for the next buffs, and distribute paid draws across four pools.
            var states = (System.Collections.Generic.Dictionary<string, DoodleUi.SummonState>)typeof(DoodleUi).GetField("summonStates", GrowthPrivate).GetValue(ui);
            for (int purchase = 0; purchase < 20; purchase++) {
                string category = CampaignCategories.Take(4).OrderBy(c => states[c].lifetimeDraws).First();
                int cost = ui.SummonCost(category, 10);
                if (ui.Diamonds - cost < 500) break;
                Assert.That(ui.TrySummon(category, 10, false), Is.True); CampaignClose();
            }
            // Enter each cave as its recommended field difficulty is reached; failed runs consume attempts.
            if (ui.ActiveDungeonIndex < 0) foreach (int cave in new[] { 0, 2 }) {
                if (ui.HighestMainStage >= DoodleUi.DungeonDifficultyStage(ui.DungeonChallengeStage(cave)) && ui.CanEnterDungeon(cave)) {
                    ui.EnterDungeon(cave); break;
                }
            }
            CampaignClose(); ui.Save();
        }

        [UnityTest, Timeout(7200000)]
        public IEnumerator ContinuousFirstDayCampaignReachesStage300()
        {
            if (!Environment.GetCommandLineArgs().Contains("-dayOneCampaign")) Assert.Ignore("Opt-in long continuous campaign; use the campaign Actions scope.");
            var ui = game.Ui;
            Assert.That(ui.Gold, Is.Zero); Assert.That(ui.Diamonds, Is.Zero);
            Assert.That(CampaignCategories.SelectMany(ui.Items).Any(x => x.discovered), Is.False);
            Assert.That(CampaignStats.All(x => ui.StatLevel(x) == 0), Is.True);
            typeof(DoodleUi).GetField("starterDamageBaseline", GrowthPrivate).SetValue(ui, 1f);
            int seed = 20260923;
            foreach (string field in new[] { "commerceRandom", "collectionRandom", "serviceRandom" })
                typeof(DoodleUi).GetField(field, GrowthPrivate).SetValue(ui, new System.Random(seed++));
            UnityEngine.Random.InitState(20260923);
            bool oldSkip = ui.SkipSummonAnimations;
            float oldMaximum = Time.maximumDeltaTime;
            int oldFrameRate = Application.targetFrameRate;
            ui.SkipSummonAnimations = true;
            game.autoPlay = game.summonSkillsEnabled = game.companionsEnabled = true;
            double start = Time.fixedTimeAsDouble, nextManage = 0, nextReport = 0;
            double goldBuffEnd = 0, attackBuffEnd = 0;
            int lastStage = 0, deaths = 0, lastReportStage = -1;
            string report = Path.Combine(Application.dataPath, "../artifacts/campaign.csv");
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            File.WriteAllText(report, "seconds,stage,highestStage,deaths,attackLevel,healthLevel,regenLevel,critLevel,attack,gold,diamonds,mission,goldCave,relicCave\n");
            void Report(double elapsed) {
                string row = string.Join(",", new[] { elapsed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), ui.MainStage.ToString(), ui.HighestMainStage.ToString(), deaths.ToString(),
                    ui.StatLevel("attack").ToString(), ui.StatLevel("health").ToString(), ui.StatLevel("healthRegen").ToString(), ui.StatLevel("crit2Chance").ToString(),
                    ui.CurrentAttackPower.ToString("R", System.Globalization.CultureInfo.InvariantCulture), ui.Gold.ToString(), ui.Diamonds.ToString(), ui.MainMissionNumber.ToString(), ui.GetDungeonStage(0).ToString(), ui.GetDungeonStage(2).ToString() });
                File.AppendAllText(report, row + "\n"); Debug.Log("CAMPAIGN " + row + " objective=" + ui.CurrentMainMission.label);
            }
            try {
                ui.ClaimAttendance(); CampaignClose();
                // Daily allowances are taken exactly once, without simulated day rollover.
                for (int i = 0; i < 30; i++) { Assert.That(ui.ClaimFreeDiamonds(), Is.True); CampaignClose(); }
                foreach (string category in CampaignCategories.Take(5)) for (int i = 0; i < 3; i++) {
                    Assert.That(ui.TrySummon(category, 5, true), Is.True); CampaignClose();
                }
                for (int i = 0; i < 5; i++) {
                    var spin = (IEnumerator)typeof(DoodleUi).GetMethod("SpinRoulette", ServicePrivate).Invoke(ui, new object[] { null });
                    yield return spin; CampaignClose();
                }
                Time.timeScale = 64; Time.maximumDeltaTime = 1; Application.targetFrameRate = 60;
                var tick = new WaitForFixedUpdate();
                while (ui.MainStage < 300 && Time.fixedTimeAsDouble - start < 12 * 3600) {
                    double elapsed = Time.fixedTimeAsDouble - start;
                    // Services use UTC. Remap only remaining buff time to the accelerated combat clock;
                    // activation still pays the real price and uses the real duration every time.
                    long now = DateTime.UtcNow.Ticks;
                    DayOneState("goldExpiry", now + (long)(Math.Max(0, goldBuffEnd - elapsed) * TimeSpan.TicksPerSecond));
                    DayOneState("attackExpiry", now + (long)(Math.Max(0, attackBuffEnd - elapsed) * TimeSpan.TicksPerSecond));
                    if (elapsed >= nextManage) {
                        CampaignManage();
                        var tuning = (DoodleUi.ServiceTuning)typeof(DoodleUi).GetField("serviceTuning", ServicePrivate).GetValue(ui);
                        if (goldBuffEnd <= elapsed) { ui.ExtendBuff(false); if (ui.GoldBuffSeconds > 0) goldBuffEnd = elapsed + tuning.buffSeconds; }
                        if (attackBuffEnd <= elapsed) { ui.ExtendBuff(true); if (ui.AttackBuffSeconds > 0) attackBuffEnd = elapsed + tuning.buffSeconds; }
                        nextManage = elapsed + 30;
                    }
                    if (ui.MainStage < lastStage) deaths++;
                    lastStage = ui.MainStage;
                    if (elapsed >= nextReport || (ui.MainStage % 10 == 0 && ui.MainStage != lastReportStage)) {
                        Report(elapsed); lastReportStage = ui.MainStage; nextReport = elapsed + 600;
                    }
                    yield return tick;
                }
                Report(Time.fixedTimeAsDouble - start);
                foreach (string category in CampaignCategories) Debug.Log("CAMPAIGN inventory " + category + " " + string.Join(";", ui.Items(category).Where(x => x.discovered).Select(x => x.id + ":" + x.level + (x.equipped ? " equipped" : ""))));
                Object.Destroy(CaptureFrame("campaign-final.png", 720, 1560));
                Assert.That(ui.MainStage, Is.GreaterThanOrEqualTo(300), "Report contains actual 12-hour progress if the fresh account cannot reach 300.");
            }
            finally { Time.timeScale = originalTimeScale; Time.maximumDeltaTime = oldMaximum; Application.targetFrameRate = oldFrameRate; ui.SkipSummonAnimations = oldSkip; }
        }
    }
}
