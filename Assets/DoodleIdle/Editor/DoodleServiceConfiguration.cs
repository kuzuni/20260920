using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Purchasing;

namespace DoodleIdle.Editor
{
    // The ignored local JSON is the source of truth for SDK Inspector assets.
    [InitializeOnLoad]
    public static class DoodleServiceConfiguration
    {
        const string ConfigPath = "Assets/DoodleIdle/Resources/DoodleIdle/BackendSettings.json";

        static DoodleServiceConfiguration()
        {
            EditorApplication.delayCall += () => {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(ConfigPath))
                    Sync();
            };
        }

        [MenuItem("Doodle Idle/Backend/Sync SDK Settings and IAP Catalog")]
        public static void Sync()
        {
            if (!File.Exists(ConfigPath)) throw new BuildFailedException("Local BackendSettings.json is missing.");
            var config = JsonUtility.FromJson<DoodleBackendSession.Settings>(File.ReadAllText(ConfigPath));
            if (config == null || string.IsNullOrWhiteSpace(config.clientAppId) ||
                string.IsNullOrWhiteSpace(config.signatureKey) || string.IsNullOrWhiteSpace(config.googleWebClientId) ||
                string.IsNullOrWhiteSpace(config.chatUuid))
                throw new BuildFailedException("Local BACKND, Google and chat settings must all be populated.");

            var backend = LoadSettings("Assets/TheBackend/Resources/TheBackendSettings.asset", "TheBackendSettings");
            SetString(backend, "clientAppID", config.clientAppId);
            SetString(backend, "signatureKey", config.signatureKey);
            SetString(backend, "packageName", DoodleIapCatalog.PackageName);
            backend.FindProperty("sendLogReport").boolValue = false;
            backend.FindProperty("timeOutSec").intValue = 30;
            backend.ApplyModifiedPropertiesWithoutUndo();

            var google = LoadSettings("Assets/TheBackend/Resources/TheBackendGoogleSettingsForAndroid.asset",
                "TheBackend.ToolKit.GoogleLogin.Settings.Android.TheBackendGoogleSettingsForAndroid");
            SetString(google, "webClientID", config.googleWebClientId);
            google.ApplyModifiedPropertiesWithoutUndo();

            var chat = LoadSettings("Assets/BACKND/Resources/BackndChatSettings.asset", "Backnd.ChatSettings.BackndChatSettings");
            SetString(chat, "chatUUID", config.chatUuid);
            chat.FindProperty("sendLogReport").boolValue = false;
            chat.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            SyncCatalog();
            Debug.Log("[DoodleConfig] BACKND, Google and chat Inspector settings synchronized; IAP catalog contains six products.");
        }

        static SerializedObject LoadSettings(string path, string typeName)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (!asset) {
                var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
                if (type == null) throw new BuildFailedException("Required SDK settings type is missing: " + typeName);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                asset = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
            }
            return new SerializedObject(asset);
        }

        static void SetString(SerializedObject asset, string property, string value)
        {
            var field = asset.FindProperty(property);
            if (field == null) throw new BuildFailedException("SDK setting field is missing: " + property);
            field.stringValue = value;
        }

        static void SyncCatalog()
        {
            var catalog = File.Exists(ProductCatalog.kCatalogPath)
                ? ProductCatalog.Deserialize(File.ReadAllText(ProductCatalog.kCatalogPath)) : new ProductCatalog();
            // DoodleIapService owns initialization and receipt delivery.
            catalog.enableCodelessAutoInitialization = false;
            catalog.enableUnityGamingServicesAutoInitialization = false;
            foreach (var empty in catalog.allProducts.Where(p => string.IsNullOrWhiteSpace(p.id)).ToArray()) catalog.Remove(empty);
            foreach (var product in new DoodleUi.CommerceTuning().products) {
                string id = DoodleIapCatalog.ProductId(product.amount);
                var item = catalog.allProducts.FirstOrDefault(p => p.id == id);
                if (item == null) { item = new ProductCatalogItem { id = id }; catalog.Add(item); }
                item.type = ProductType.Consumable;
                item.defaultDescription.googleLocale = TranslationLocale.en_US;
                item.defaultDescription.Title = product.amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " Diamonds";
                item.defaultDescription.Description = "Receive " + product.amount + " diamonds" +
                    (product.mileageCoupons > 0 ? " and " + product.mileageCoupons + " mileage coupon(s)." : ".");
                var korean = item.GetOrCreateDescription(TranslationLocale.ko_KR);
                korean.Title = "다이아 " + product.amount.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + "개";
                korean.Description = "다이아 " + product.amount + "개 지급" +
                    (product.mileageCoupons > 0 ? ", 마일리지 쿠폰 " + product.mileageCoupons + "개 추가 지급." : ".");
                item.Payouts.Clear();
                item.Payouts.Add(new ProductCatalogPayout { type = ProductCatalogPayout.ProductCatalogPayoutType.Currency,
                    subtype = "diamonds", quantity = product.amount });
                if (product.mileageCoupons > 0) item.Payouts.Add(new ProductCatalogPayout {
                    type = ProductCatalogPayout.ProductCatalogPayoutType.Item, subtype = "mileageCoupons", quantity = product.mileageCoupons });
                // Actual regional prices are managed by Google Play, not the legacy USD CSV field.
            }
            string json = ProductCatalog.Serialize(catalog);
            Directory.CreateDirectory(Path.GetDirectoryName(ProductCatalog.kCatalogPath));
            if (!File.Exists(ProductCatalog.kCatalogPath) || File.ReadAllText(ProductCatalog.kCatalogPath) != json) {
                File.WriteAllText(ProductCatalog.kCatalogPath, json);
                AssetDatabase.ImportAsset(ProductCatalog.kCatalogPath);
            }
        }
    }
}
