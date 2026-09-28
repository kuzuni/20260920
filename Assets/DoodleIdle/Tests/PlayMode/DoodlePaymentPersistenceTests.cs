using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DoodleIdle.Tests
{
    public partial class DoodleIdlePlayModeTests
    {
        [UnityTest]
        public IEnumerator VoidedOrdersSaveImmediatelyAndRetryWithoutDoubleReclaim()
        {
            game.TogglePause();
            var ui = game.Ui;
            ui.Diamonds = 0;
            object extras = typeof(DoodleUi).GetField("commerceExtras", ServicePrivate).GetValue(ui);
            extras.GetType().GetProperty("mileageCoupons").SetValue(extras, 0);
            int productIndex = Array.FindIndex(ui.CurrencyProducts, p => p.amount == 2000000);
            var purchase = ui.BeginPurchase("voided-save-token", "diamonds_2000000", "voided-save-account");
            ui.RecordPurchaseReceipt(purchase, "GPA.voided-save", "voided-save-token", "test-receipt");
            Assert.That(ui.DeliverPurchase(productIndex, purchase), Is.True);
            ui.Diamonds = 0;
            extras.GetType().GetProperty("mileageCoupons").SetValue(extras, 0);
            var order = new DoodleRefundReconciler.VoidedOrder { orderId = purchase.orderId, tokenHash = purchase.tokenHash };
            int saves = 0;
            var first = DoodleRefundReconciler.ApplyAndSave(ui, purchase.accountId, new[] { order, order }, () => {
                saves++;
                Assert.That(ui.Diamonds, Is.EqualTo(-2000000));
                Assert.That(ui.MileageCoupons, Is.EqualTo(-2));
                return System.Threading.Tasks.Task.FromResult(false);
            });
            Assert.That(first.Result, Is.False);
            Assert.That(saves, Is.EqualTo(1));
            typeof(DoodleUi).GetMethod("InitCommerceExtras", ServicePrivate).Invoke(ui, null);
            var retry = DoodleRefundReconciler.ApplyAndSave(ui, purchase.accountId, new[] { order }, () => {
                saves++; return System.Threading.Tasks.Task.FromResult(true);
            });
            Assert.That(retry.Result, Is.True);
            Assert.That(saves, Is.EqualTo(2));
            Assert.That(ui.Diamonds, Is.EqualTo(-2000000));
            Assert.That(ui.MileageCoupons, Is.EqualTo(-2));
            var foreign = DoodleRefundReconciler.ApplyAndSave(ui, "other-account", new[] { order }, () => {
                Assert.Fail("Another account's order must not affect this wallet.");
                return System.Threading.Tasks.Task.FromResult(true);
            });
            Assert.That(foreign.Result, Is.True);
            Assert.That(saves, Is.EqualTo(2));
            yield return null;
        }
        [UnityTest]
        public IEnumerator RefundReclaimsTheOriginalDiamondsAndCouponsBelowZeroOnlyOnce()
        {
            game.TogglePause();
            var ui = game.Ui;
            ui.Diamonds = 0;
            object extras = typeof(DoodleUi).GetField("commerceExtras", ServicePrivate).GetValue(ui);
            extras.GetType().GetProperty("mileageCoupons").SetValue(extras, 0);
            int productIndex = Array.FindIndex(ui.CurrencyProducts, p => p.amount == 2000000);
            Assert.That(productIndex, Is.GreaterThanOrEqualTo(0));
            var attempt = ui.BeginPurchase("test-refund-token", "diamonds_2000000", "test-refund-account");
            ui.RecordPurchaseReceipt(attempt, "GPA.test-refund", "test-refund-token", "test-receipt");
            Assert.That(ui.DeliverPurchase(productIndex, attempt), Is.True);
            Assert.That(ui.Diamonds, Is.EqualTo(2000000));
            Assert.That(ui.MileageCoupons, Is.EqualTo(2));
            Assert.That(ui.DeliverPurchase(productIndex, attempt), Is.True);
            Assert.That(ui.Diamonds, Is.EqualTo(2000000), "Store retries must never duplicate rewards.");
            ui.Diamonds = 0;
            extras.GetType().GetProperty("mileageCoupons").SetValue(extras, 0);
            Assert.That(ui.RevokeVerifiedPurchase(attempt.orderId, attempt.tokenHash, "wrong-product", attempt.accountId), Is.False);
            Assert.That(ui.RevokeVerifiedPurchase(attempt.orderId, attempt.tokenHash, attempt.productId, attempt.accountId), Is.True);
            Assert.That(ui.Diamonds, Is.EqualTo(-2000000));
            Assert.That(ui.MileageCoupons, Is.EqualTo(-2));
            Assert.That(ui.RevokeVerifiedPurchase(attempt.orderId, attempt.tokenHash, attempt.productId, attempt.accountId), Is.True);
            Assert.That(ui.Diamonds, Is.EqualTo(-2000000));
            typeof(DoodleUi).GetMethod("InitCommerceExtras", ServicePrivate).Invoke(ui, null);
            Assert.That(ui.Diamonds, Is.EqualTo(-2000000), "Reloading must not clamp refund debt to zero.");
            Assert.That(ui.MileageCoupons, Is.EqualTo(-2));
            yield return null;
        }
    }
}
