using System;

namespace SuruSavasi.Services
{
    /// <summary>Satın alma ve ödüllü reklamın oyun tarafı: ödülü verir, analize yazar.</summary>
    public static class Shop
    {
        /// <summary>Son Kurucu Paketi'nden çıkan ekipman (ekranda göstermek için).</summary>
        public static Core.Meta.GearItem LastFounderGear;

        public static void Buy(string productId, Action<bool> done = null)
        {
            var store = GameServices.Store;
            var def = Catalog.Find(productId);
            if (store == null || !store.IsReady || def == null) { done?.Invoke(false); return; }

            store.Purchase(productId, r =>
            {
                if (r.status != PurchaseStatus.Success) { done?.Invoke(false); return; }
                // TODO: makbuzu (r.receipt) sunucuda doğrula, elmasları sunucu versin.
                Grant(def);
                GameServices.Analytics?.LogPurchase(def.id, def.localizedPrice, store.Name);
                done?.Invoke(true);
            });
        }

        static void Grant(ProductDef def)
        {
            if (def.diamonds > 0) GameServices.Wallet.Add(Currency.Diamonds, def.diamonds, "purchase:" + def.id);
            var data = GameServices.Save.Data;
            if (def.id == Catalog.NoAds || def.id == Catalog.StarterPack)
            {
                data.noAds = true;
                if (GameServices.Ads != null) GameServices.Ads.InterstitialsDisabled = true;
            }
            if (def.id == Catalog.StarterPack) data.starterPackBought = true;
            if (def.id == Catalog.FounderPack)
                LastFounderGear = Core.Meta.Offers.GrantFounder(data, new Random(), (c, n) =>
                    GameServices.Wallet.Add(c == "gold" ? Currency.Gold : Currency.Diamonds, n, "purchase:" + def.id));
            if (def.id == Catalog.MonthlyCard)
                data.monthlyCardUntil = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds();
            GameServices.Save.Save();
        }

        /// <summary>Ödüllü reklam: izlenirse reward çalışır.</summary>
        public static void WatchAd(string placement, Action reward, Action failed = null)
        {
            var ads = GameServices.Ads;
            if (ads == null || !ads.IsRewardedReady(placement)) { failed?.Invoke(); return; }
            ads.ShowRewarded(placement, ok =>
            {
                GameServices.Analytics?.LogAd(placement, ok);
                if (ok) reward?.Invoke(); else failed?.Invoke();
            });
        }

        public static void MaybeShowInterstitial(string placement)
        {
            var ads = GameServices.Ads;
            if (ads == null || ads.InterstitialsDisabled || GameServices.Save.Data.noAds) return;
            ads.ShowInterstitial(placement);
        }
    }
}
