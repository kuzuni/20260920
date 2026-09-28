using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DoodleIdle
{
    // Input must come from an authenticated server-side Google Voided Purchases query.
    // Never accept a UI message, player-supplied JSON, or a client's refund assertion.
    public static class DoodleRefundReconciler
    {
        [Serializable]
        public sealed class VoidedOrder
        {
            public string orderId;
            public string tokenHash;
        }

        public static async Task<bool> ApplyAndSave(DoodleUi ui, string accountId,
            IReadOnlyList<VoidedOrder> orders, Func<Task<bool>> saveImmediately)
        {
            if (!ui || string.IsNullOrEmpty(accountId) || orders == null || saveImmediately == null)
                throw new ArgumentException("Refund reconciliation input is incomplete.");
            bool matched = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var order in orders)
            {
                if (order == null || string.IsNullOrEmpty(order.orderId) || string.IsNullOrEmpty(order.tokenHash))
                    throw new ArgumentException("Voided purchase identity is incomplete.");
            }
            foreach (var order in orders)
            {
                if (!seen.Add(order.orderId + ":" + order.tokenHash)) continue;
                // Product and reward quantities come from the saved original purchase.
                // No reward amount supplied by the lookup response is trusted.
                var purchase = ui.FindSavedPurchase(order.orderId, order.tokenHash, accountId);
                if (purchase == null) continue;
                if (!ui.RevokeVerifiedPurchase(purchase.orderId, purchase.tokenHash, purchase.productId, accountId))
                    return false;
                matched = true;
            }
            // Even a duplicate triggers a save retry: local recovery may have survived
            // an earlier cloud-save failure. RevokeVerifiedPurchase is idempotent.
            return !matched || await saveImmediately();
        }
    }
}
