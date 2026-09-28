using CodeStage.AntiCheat.ObscuredTypes;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DoodleIdle
{
    // Purchase tokens and receipts are persisted only inside the encrypted account save.
    [Serializable]
    public sealed class DoodlePurchaseLedger
    {
        [Serializable]
        public sealed class Attempt
        {
            [NonSerialized] private ObscuredString protected_tokenHash;
            public string tokenHash { get => protected_tokenHash; set => protected_tokenHash = value; }
            [NonSerialized] private ObscuredString protected_productId;
            public string productId { get => protected_productId; set => protected_productId = value; }
            [NonSerialized] private ObscuredString protected_accountId;
            public string accountId { get => protected_accountId; set => protected_accountId = value; }
            [NonSerialized] private ObscuredString protected_nonce;
            public string nonce { get => protected_nonce; set => protected_nonce = value; }
            [NonSerialized] private ObscuredString protected_orderId;
            public string orderId { get => protected_orderId; set => protected_orderId = value; }
            [NonSerialized] private ObscuredString protected_purchaseToken;
            public string purchaseToken { get => protected_purchaseToken; set => protected_purchaseToken = value; }
            [NonSerialized] private ObscuredString protected_receipt;
            public string receipt { get => protected_receipt; set => protected_receipt = value; }
            [NonSerialized] private ObscuredInt protected_grantedDiamonds;
            public int grantedDiamonds { get => protected_grantedDiamonds; set => protected_grantedDiamonds = value; }
            [NonSerialized] private ObscuredInt protected_grantedCoupons;
            public int grantedCoupons { get => protected_grantedCoupons; set => protected_grantedCoupons = value; }
            [NonSerialized] private ObscuredBool protected_delivered;
            public bool delivered { get => protected_delivered; set => protected_delivered = value; }
            [NonSerialized] private ObscuredBool protected_revoked;
            public bool revoked { get => protected_revoked; set => protected_revoked = value; }
        }

        public List<Attempt> attempts = new List<Attempt>();

        public void SetReceipt(Attempt attempt, string orderId, string token, string receipt)
        {
            if (!attempts.Contains(attempt) || Hash(token) != attempt.tokenHash || string.IsNullOrWhiteSpace(orderId) || !orderId.StartsWith("GPA.", StringComparison.Ordinal))
                throw new InvalidOperationException("Invalid Google Play purchase identity.");
            if (!string.IsNullOrEmpty(attempt.orderId) && attempt.orderId != orderId) throw new InvalidOperationException("Order identity changed.");
            attempt.orderId = orderId; attempt.purchaseToken = token; attempt.receipt = receipt;
        }

        public Attempt FindRefund(string orderId, string tokenHash, string productId, string accountId)
        {
            return attempts.Find(a => a.delivered && a.orderId == orderId && a.tokenHash == tokenHash && a.productId == productId && a.accountId == accountId);
        }

        public static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        public Attempt Begin(string token, string product, string account)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(product) || string.IsNullOrWhiteSpace(account))
                throw new ArgumentException("Purchase identity is incomplete.");
            string hash = Hash(token);
            var existing = attempts.Find(x => x.tokenHash == hash);
            if (existing != null)
            {
                if (existing.productId != product || existing.accountId != account)
                    throw new InvalidOperationException("Purchase identity does not match the original attempt.");
                return existing;
            }
            var attempt = new Attempt { tokenHash = hash, productId = product, accountId = account, nonce = Guid.NewGuid().ToString("N") };
            attempts.Add(attempt);
            return attempt;
        }

        // A UsedReceipt response alone is NOT permission to grant another reward.
        public static bool MatchesRecovery(Attempt attempt, string product, string account, string nonce)
        {
            return attempt != null && !string.IsNullOrEmpty(nonce) && attempt.productId == product &&
                   attempt.accountId == account && attempt.nonce == nonce;
        }
    }

    public static class DoodleIapCatalog
    {
        public const string PackageName = "com.semobobo.game20260920";
        public static string ProductId(int diamonds)
        {
            switch (diamonds)
            {
                case 10000: case 70000: case 150000: case 500000: case 900000: case 2000000:
                    return "diamonds_" + diamonds;
                default: return null;
            }
        }
    }
}
