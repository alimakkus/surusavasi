namespace SuruSavasi.Services.Platform
{
    /// <summary>Hedef mağazaya göre doğru servisleri seçer. Yeni mağaza eklemek = buraya bir satır.</summary>
    public static class ServiceFactory
    {
        public static void Create(StoreTarget t, out IStore store, out IAdService ads, out IAnalytics analytics)
        {
            analytics = new DebugAnalytics();
            switch (t)
            {
                case StoreTarget.GooglePlay: store = new GooglePlayStore(); ads = new PendingAds("LevelPlay"); break;
                case StoreTarget.AppStore:   store = new AppleStore();      ads = new PendingAds("LevelPlay"); break;
                case StoreTarget.Samsung:    store = new SamsungStore();    ads = new PendingAds("LevelPlay"); break;
                case StoreTarget.Amazon:     store = new AmazonStore();     ads = new PendingAds("LevelPlay"); break;
                case StoreTarget.Huawei:     store = new HuaweiStore();     ads = new PendingAds("Huawei Ads"); break;
                default:                     store = new EditorStore();     ads = new EditorAds(); break;
            }
        }
    }
}
