using UnityEngine;

namespace SuruSavasi.Services
{
    /// <summary>Hedef mağaza. Derleme ayarlarındaki "Scripting Define Symbols" ile seçilir:
    /// STORE_GOOGLE, STORE_APPLE, STORE_SAMSUNG, STORE_AMAZON, STORE_HUAWEI. Hiçbiri yoksa Editor.</summary>
    public enum StoreTarget { Editor, GooglePlay, AppStore, Samsung, Amazon, Huawei }

    /// <summary>Tüm servislere tek giriş noktası. Oyun kodu mağazayı değil bu sınıfı bilir.</summary>
    public static class GameServices
    {
        public static StoreTarget Target { get; private set; }
        public static ISaveService Save { get; private set; }
        public static Wallet Wallet { get; private set; }
        public static IStore Store { get; private set; }
        public static IAdService Ads { get; private set; }
        public static IAnalytics Analytics { get; private set; }
        public static IConsent Consent { get; private set; }
        public static bool Ready { get; private set; }

        public static StoreTarget CurrentTarget()
        {
#if UNITY_EDITOR
            return StoreTarget.Editor;
#elif STORE_APPLE || UNITY_IOS
            return StoreTarget.AppStore;
#elif STORE_SAMSUNG
            return StoreTarget.Samsung;
#elif STORE_AMAZON
            return StoreTarget.Amazon;
#elif STORE_HUAWEI
            return StoreTarget.Huawei;
#else
            return StoreTarget.GooglePlay;
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            Ready = false;
            Target = CurrentTarget();
            Save = new LocalSaveService();
            Wallet = new Wallet(Save);
            Consent = new LocalConsent();
            Platform.ServiceFactory.Create(Target, out var store, out var ads, out var analytics);
            Store = store; Ads = ads; Analytics = analytics;

            Save.Load(loaded => Consent.Request(() =>
            {
                Analytics.Initialize(Consent.AnalyticsAllowed);
                Ads.InterstitialsDisabled = Save.Data.noAds;
                Ads.Initialize(Consent.PersonalizedAds, ok => { });
                Store.Initialize(Catalog.All, ok => { });
                Ready = true;
                Debug.Log($"[Services] Hazır. Mağaza: {Store.Name}, Reklam: {Ads.Name}");
            }));
        }
    }
}
