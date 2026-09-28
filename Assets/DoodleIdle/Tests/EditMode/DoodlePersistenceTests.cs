using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using CodeStage.AntiCheat.ObscuredTypes;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DoodleIdle.Tests
{
    public sealed class DoodlePersistenceTests
    {
        [Test]
        public void ServerDeadlineIsFiveMinutesAndRetriesAfterFiveSecondsWithoutOverlap()
        {
            var schedule = new DoodleSaveSchedule(300, 5);
            schedule.Reset(20);
            Assert.That(schedule.TryBegin(319.999), Is.False);
            Assert.That(schedule.TryBegin(320), Is.True);
            Assert.That(schedule.TryBegin(700, true), Is.False, "A long in-flight request must not overlap.");
            schedule.Complete(701, false);
            Assert.That(schedule.TryBegin(705.999), Is.False);
            Assert.That(schedule.TryBegin(706), Is.True);
            schedule.Complete(707, true);
            Assert.That(schedule.TryBegin(1006.999), Is.False);
            Assert.That(schedule.TryBegin(1007), Is.True);
        }

        [Test]
        public void PurchaseBypassesDeadlineAndLocalIntervalIsOneSecond()
        {
            var server = new DoodleSaveSchedule(300, 5);
            server.Reset(0);
            Assert.That(server.TryBegin(2, true), Is.True);
            server.Complete(3, true);
            Assert.That(server.NextAttempt, Is.EqualTo(303));
            var local = new DoodleSaveSchedule(1, 1);
            local.Reset(0);
            Assert.That(local.TryBegin(.999), Is.False);
            Assert.That(local.TryBegin(1), Is.True);
            local.Complete(1, false);
            Assert.That(local.TryBegin(2), Is.True);
        }

        [Test]
        public void JsonSaveIsEncryptedRandomizedAndBoundToAccount()
        {
            const string json = "{\"diamonds\":-2000000,\"coupons\":-2}";
            string first = DoodleProtectedJson.Encrypt(json, "test-a");
            Assert.That(first, Does.Not.Contain("diamonds"));
            Assert.That(JObject.Parse(first)["version"].Value<int>(), Is.EqualTo(2));
            Assert.That(DoodleProtectedJson.Decrypt(first, "test-a"), Is.EqualTo(json));
            Assert.That(DoodleProtectedJson.Encrypt(json, "test-a"), Is.Not.EqualTo(first));
            Assert.Throws<CryptographicException>(() => DoodleProtectedJson.Decrypt(first, "test-b"));
        }

        [TestCase("iv")]
        [TestCase("payload")]
        [TestCase("mac")]
        public void EditingAnyAuthenticatedFieldIsRejected(string field)
        {
            var envelope = JObject.Parse(DoodleProtectedJson.Encrypt("{}", "account"));
            string text = (string)envelope[field];
            envelope[field] = (text[0] == 'A' ? "B" : "A") + text.Substring(1);
            Assert.Throws<CryptographicException>(() => DoodleProtectedJson.Decrypt(envelope.ToString(), "account"));
        }

        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("{\"version\":2,\"iv\":\"?\",\"payload\":\"?\",\"mac\":\"?\"}")]
        public void MalformedSaveUsesTheSameTamperFailurePath(string invalid)
        {
            Assert.Throws<CryptographicException>(() => DoodleProtectedJson.Decrypt(invalid, "account"));
        }

        [Test]
        public void AtomicReplacementLeavesACompleteJsonFile()
        {
            string folder = Path.Combine(Path.GetTempPath(), "doodle-save-test-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "save.json");
            try
            {
                DoodleProtectedJson.WriteAtomic(path, "{\"revision\":1}");
                DoodleProtectedJson.WriteAtomic(path, "{\"revision\":2}");
                Assert.That(JObject.Parse(File.ReadAllText(path))["revision"].Value<int>(), Is.EqualTo(2));
                Assert.That(File.Exists(path + ".tmp"), Is.False);
            }
            finally { if (File.Exists(path)) File.Delete(path); if (Directory.Exists(folder)) Directory.Delete(folder); }
        }

        [Test]
        public void ProtectedModelsKeepTheExistingPlainJsonSchema()
        {
            var item = DoodleJson.FromJson<UiItem>("{\"id\":\"club\",\"count\":42,\"level\":7,\"equipped\":true}");
            Assert.That(item.count, Is.EqualTo(42));
            var json = JObject.Parse(DoodleJson.ToJson(item));
            Assert.That(json["count"].Value<int>(), Is.EqualTo(42));
            Assert.That(json["level"].Value<int>(), Is.EqualTo(7));
            Assert.That(json.ToString(), Does.Not.Contain("protected_"));
            var skin = new UiSkin { owned = true, equipped = true, tint = UnityEngine.Color.white };
            Assert.That(DoodleJson.FromJson<UiSkin>(DoodleJson.ToJson(skin)).tint, Is.EqualTo(UnityEngine.Color.white));

            Type stateType = typeof(DoodleUi).GetNestedType("ServiceState", BindingFlags.NonPublic);
            object state = DoodleJson.FromJson("{\"mainStage\":812,\"daily\":[1,2],\"dailyClaimed\":[true,false]}", stateType);
            Assert.That(stateType.GetProperty("mainStage").GetValue(state), Is.EqualTo(812));
            var daily = (ObscuredInt[])stateType.GetField("daily").GetValue(state);
            Assert.That((int)daily[1], Is.EqualTo(2));
            Assert.That(JObject.Parse(DoodleJson.ToJson(state))["daily"][0].Value<int>(), Is.EqualTo(1));
        }

        [Test]
        public void UsedReceiptRecoveryRequiresTheOriginalAccountProductAndNonce()
        {
            var ledger = new DoodlePurchaseLedger();
            var attempt = ledger.Begin("test-token", "diamonds_2000000", "test-account");
            Assert.That(ledger.Begin("test-token", "diamonds_2000000", "test-account"), Is.SameAs(attempt));
            Assert.Throws<InvalidOperationException>(() => ledger.Begin("test-token", "diamonds_10000", "test-account"));
            Assert.Throws<InvalidOperationException>(() => ledger.Begin("test-token", "diamonds_2000000", "another-account"));
            Assert.That(DoodlePurchaseLedger.MatchesRecovery(attempt, attempt.productId, attempt.accountId, attempt.nonce), Is.True);
            Assert.That(DoodlePurchaseLedger.MatchesRecovery(attempt, attempt.productId, attempt.accountId, "another-nonce"), Is.False);
            ledger.SetReceipt(attempt, "GPA.test-order", "test-token", "receipt");
            attempt.delivered = true; attempt.grantedDiamonds = 2000000; attempt.grantedCoupons = 2;
            var restored = DoodleJson.FromJson<DoodlePurchaseLedger>(DoodleJson.ToJson(ledger));
            var found = restored.FindRefund("GPA.test-order", attempt.tokenHash, attempt.productId, attempt.accountId);
            Assert.That(found.grantedDiamonds, Is.EqualTo(2000000));
            Assert.That(found.grantedCoupons, Is.EqualTo(2));
            Assert.That(restored.FindRefund("GPA.test-order", attempt.tokenHash, "wrong-product", attempt.accountId), Is.Null);
        }

        [Test]
        public void AccountChangesStayInMemoryUntilFlushAndAnOldUploadCannotClearNewProgress()
        {
            string account = "save-test-" + Guid.NewGuid().ToString("N");
            try
            {
                DoodlePrefs.UseAccount(account);
                DoodlePrefs.SetInt("DoodleUi.Diamonds", 1);
                DoodlePrefs.Flush();
                long uploadedRevision = DoodlePrefs.Revision;
                DoodlePrefs.SetInt("DoodleUi.Diamonds", 2);
                DoodlePrefs.MarkSynced(uploadedRevision);
                Assert.That(DoodlePrefs.Dirty, Is.True, "An old upload must not mark newer changes synced.");
                DoodlePrefs.Save(); // Ordinary game mutations never perform a disk or network write.
                DoodlePrefs.UseAccount(account);
                Assert.That(DoodlePrefs.GetInt("DoodleUi.Diamonds"), Is.EqualTo(1));
                DoodlePrefs.SetInt("DoodleUi.Diamonds", -2000000);
                DoodlePrefs.Flush();
                DoodlePrefs.UseAccount(account);
                Assert.That(DoodlePrefs.GetInt("DoodleUi.Diamonds"), Is.EqualTo(-2000000));
                Assert.That(DoodlePrefs.Dirty, Is.True);
                DoodlePrefs.MarkSynced(DoodlePrefs.Revision);
                DoodlePrefs.UseAccount(account);
                Assert.That(DoodlePrefs.Dirty, Is.False);
            }
            finally { DoodlePrefs.DeleteAccountCache(); }
        }
    }
}
