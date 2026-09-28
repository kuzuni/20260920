using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BackEnd;
using UnityEngine;
using UnityEngine.Purchasing;

namespace DoodleIdle
{
    /// <summary>Google Play consumables. Confirmation only follows verified, persisted fulfillment.</summary>
    public sealed class DoodleIapService : MonoBehaviour
    {
        [Serializable] sealed class ReceiptDetails { public string itemId, accountId, nonce; }
        [Serializable] sealed class UnityReceipt { public string Payload; }
        [Serializable] sealed class GoogleReceipt { public string json; }
        [Serializable] sealed class GooglePurchase { public string orderId; }
        DoodleUi owner;
        StoreController store;
        string accountId;
        bool connecting, fetching, buying, productsReady, disposed;
        readonly HashSet<string> processing = new HashSet<string>();
        readonly Dictionary<string, PendingOrder> pendingRetries = new Dictionary<string, PendingOrder>();
        double nextRetry;
        public string Status { get; private set; } = "Google Play 결제 준비 중";
        public bool Busy => connecting || fetching || buying || processing.Count > 0;
        public event Action Changed;

        public void Initialize(DoodleUi ui) { owner = ui; _ = Connect(); }
        public string Price(string id) => store?.GetProductById(id)?.metadata?.localizedPriceString;
        public bool CanBuy(string id) => !Busy && productsReady && Backend.IsLogin && Backend.UserInDate == accountId &&
                                        store?.GetProductById(id)?.availableToPurchase == true;
        void SetStatus(string value) { Status = value; Changed?.Invoke(); }

        async Task Connect()
        {
            if (connecting || disposed) return;
            if (Application.platform != RuntimePlatform.Android || Application.isEditor)
            { SetStatus("Google Play에서 설치한 Android 앱에서 구매할 수 있어요."); return; }
            var session = DoodleBackendSession.Instance;
            if (!session || !session.Ready || !session.Config.paymentsEnabled)
            { SetStatus("결제 준비 중 · 상품 등록 후 이용할 수 있어요."); return; }
            connecting = true;
            SetStatus("결제 서버에 연결 중…");
            try
            {
                accountId = session.AccountId;
                if (store == null)
                {
                    store = UnityIAPServices.StoreController();
                    store.OnProductsFetched += ProductsFetched;
                    store.OnProductsFetchFailed += ProductsFailed;
                    store.OnStoreDisconnected += Disconnected;
                    store.OnPurchasePending += PurchasePending;
                    store.OnPurchaseFailed += PurchaseFailed;
                    store.OnPurchaseDeferred += PurchaseDeferred;
                    store.OnPurchaseConfirmed += PurchaseConfirmed;
                    store.OnPurchasesFetched += PurchasesFetched;
                    store.OnPurchasesFetchFailed += PurchasesFailed;
                    store.ProcessPendingOrdersOnPurchasesFetched(false);
                }
                store.GooglePlayStoreExtendedService?.SetObfuscatedAccountId(DoodlePurchaseLedger.Hash(accountId));
                await store.Connect();
                if (disposed) return;
                fetching = true;
                store.FetchProductsWithNoRetries(owner.CurrencyProducts.Select(p => new ProductDefinition(DoodleIapCatalog.ProductId(p.amount), ProductType.Consumable)).ToList());
            }
            catch (Exception) { SetStatus("결제 연결 실패 · 네트워크를 확인해 주세요."); }
            finally { connecting = false; Changed?.Invoke(); }
        }

        public void Retry()
        {
            if (Busy) return;
            if (store == null || !productsReady) { _ = Connect(); return; }
            fetching = true;
            SetStatus("미완료 구매를 확인 중…");
            store.FetchPurchases();
        }

        public void Buy(string id)
        {
            if (!CanBuy(id)) return;
            buying = true;
            SetStatus("Google Play 결제창을 여는 중…");
            try { store.PurchaseProduct(id); }
            catch (Exception) { buying = false; SetStatus("결제를 시작하지 못했어요. 다시 시도해 주세요."); }
        }

        void ProductsFetched(List<Product> products)
        {
            if (disposed) return;
            productsReady = products.Count > 0;
            store.FetchPurchases();
        }
        void ProductsFailed(ProductFetchFailed failure) { fetching = false; productsReady = false; SetStatus("상품 정보를 받지 못했어요. 다시 시도해 주세요."); }
        void Disconnected(StoreConnectionFailureDescription failure) { fetching = buying = productsReady = false; SetStatus("스토어 연결이 끊겼어요. 다시 연결해 주세요."); }
        void PurchaseFailed(FailedOrder order) { buying = false; SetStatus(order.FailureReason == PurchaseFailureReason.UserCancelled ? "결제를 취소했어요." : "결제를 완료하지 못했어요. 다시 시도해 주세요."); }
        void PurchaseDeferred(DeferredOrder order) { buying = false; SetStatus("결제 승인을 기다리는 중이에요. 승인 후 자동 지급됩니다."); }
        void PurchaseConfirmed(Order order)
        {
            buying = false;
            SetStatus(order is FailedOrder ? "구매 확정 재시도가 필요해요. 구매 내역을 다시 확인해 주세요." : "구매가 완료됐어요.");
        }
        void PurchasesFailed(PurchasesFetchFailureDescription failure) { fetching = false; SetStatus("구매 내역을 확인하지 못했어요. 다시 시도해 주세요."); }
        void PurchasesFetched(Orders orders)
        {
            fetching = false;
            foreach (var pending in orders.PendingOrders) PurchasePending(pending);
            if (processing.Count == 0) SetStatus("Google Play 결제 · 구매 금액은 결제창에서 확인하세요.");
        }

        async void PurchasePending(PendingOrder order)
        {
            if (disposed || !owner || DoodleSecurity.Compromised) return;
            buying = false;
            var items = order.CartOrdered.Items().ToList();
            var google = order.Info.Google;
            string token = google?.PurchaseToken;
            if (items.Count != 1 || items[0].Quantity != 1 || string.IsNullOrEmpty(token) ||
                string.IsNullOrEmpty(order.Info.Receipt) || Backend.UserInDate != accountId ||
                google.ObfuscatedAccountId != DoodlePurchaseLedger.Hash(accountId))
            { SetStatus("구매 계정 또는 상품 정보를 확인할 수 없어요. 고객지원에 문의해 주세요."); return; }
            string productId = items[0].Product.definition.id;
            int index = Array.FindIndex(owner.CurrencyProducts, p => DoodleIapCatalog.ProductId(p.amount) == productId);
            if (index < 0) { SetStatus("등록되지 않은 상품이에요. 고객지원에 문의해 주세요."); return; }
            string hash = DoodlePurchaseLedger.Hash(token);
            if (!processing.Add(hash)) return;
            bool retry = false;
            SetStatus("구매를 확인하고 보상을 저장하는 중…");
            try
            {
                var attempt = owner.BeginPurchase(token, productId, accountId);
                var unityReceipt = JsonUtility.FromJson<UnityReceipt>(order.Info.Receipt);
                var googleReceipt = JsonUtility.FromJson<GoogleReceipt>(unityReceipt.Payload);
                var purchase = JsonUtility.FromJson<GooglePurchase>(googleReceipt.json);
                owner.RecordPurchaseReceipt(attempt, purchase.orderId, token, order.Info.Receipt);
                if (!attempt.delivered)
                {
                    var verified = new TaskCompletionSource<BackendReturnObject>();
                    var parameters = new ReceiptParam(order.Info.Receipt).AddDetailItemId(productId)
                        .AddDetail("accountId", accountId).AddDetail("nonce", attempt.nonce)
                        .AddDetail("orderId", purchase.orderId).AddDetail("productId", productId)
                        .AddDetail("diamonds", owner.CurrencyProducts[index].amount.ToString())
                        .AddDetail("mileageCoupons", owner.CurrencyProducts[index].mileageCoupons.ToString());
                    Backend.Receipt.ValidateReceipt(parameters, result => verified.TrySetResult(result));
                    var result = await verified.Task;
                    if (disposed || !owner || Backend.UserInDate != accountId) return;
                    bool valid = result.IsSuccess();
                    if (!valid && result.GetErrorCode() == "UsedReceipt")
                    {
                        try
                        {
                            var details = result.GetReturnValuetoJSON()["errorData"]["receiptInfo"]["details"].ToString();
                            var original = JsonUtility.FromJson<ReceiptDetails>(details);
                            valid = DoodlePurchaseLedger.MatchesRecovery(attempt, original.itemId, original.accountId, original.nonce);
                        }
                        catch (Exception) { valid = false; }
                    }
                    if (!valid)
                    {
                        int.TryParse(result.GetStatusCode(), out int status);
                        retry = status == 0 || status == 408 || status == 429 || status >= 500;
                        SetStatus("구매 검증을 완료하지 못했어요. 구매 내역을 다시 확인해 주세요."); return;
                    }
                    if (!owner.DeliverPurchase(index, attempt))
                    { SetStatus("보관 한도 또는 저장 상태를 확인한 후 다시 시도해 주세요."); return; }
                }
                var session = DoodleBackendSession.Instance;
                if (!session || session.AccountId != accountId || !await session.SaveCloud())
                { retry = true; SetStatus("보상은 보관 중이에요. 5초 뒤 서버 저장을 다시 시도합니다."); return; }
                if (disposed || !owner || !session.Ready || session.AccountId != accountId) return;
                store.ConfirmPurchase(order);
                SetStatus("보상을 지급했어요. 구매를 마무리하는 중…");
            }
            catch (Exception) { retry = true; SetStatus("보상 저장을 5초 뒤 다시 시도합니다."); }
            finally
            {
                if (retry && !disposed) { pendingRetries[hash] = order; nextRetry = Time.realtimeSinceStartupAsDouble + DoodleSaveSchedule.RetryInterval; }
                else pendingRetries.Remove(hash);
                processing.Remove(hash); Changed?.Invoke();
            }
        }

        void Update()
        {
            if (disposed || DoodleSecurity.Compromised || Busy || pendingRetries.Count == 0 || Time.realtimeSinceStartupAsDouble < nextRetry) return;
            nextRetry = Time.realtimeSinceStartupAsDouble + DoodleSaveSchedule.RetryInterval;
            // Copy because completion callbacks may remove entries synchronously.
            foreach (var order in pendingRetries.Values.ToArray()) PurchasePending(order);
        }

        void OnApplicationFocus(bool focus) { if (focus && store != null && !Busy) Retry(); }
        void OnDestroy()
        {
            disposed = true;
            if (store == null) return;
            store.OnProductsFetched -= ProductsFetched; store.OnProductsFetchFailed -= ProductsFailed;
            store.OnStoreDisconnected -= Disconnected; store.OnPurchasePending -= PurchasePending;
            store.OnPurchaseFailed -= PurchaseFailed; store.OnPurchaseDeferred -= PurchaseDeferred;
            store.OnPurchaseConfirmed -= PurchaseConfirmed; store.OnPurchasesFetched -= PurchasesFetched;
            store.OnPurchasesFetchFailed -= PurchasesFailed;
        }
    }
}
