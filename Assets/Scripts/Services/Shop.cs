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
                // Elmaslar defterde makbuzla birlikte "satın alma" işlemi olur; sunucu makbuzu doğrulayıp elmasları kesinleştirir
                GameServices.Wallet.BeginPurchase(StoreCode(store.Name), def.id, r.receipt);
                try { Grant(def); } finally { GameServices.Wallet.EndPurchase(); }
                Game.EconomySync.SyncSoon();
                GameServices.Analytics?.LogPurchase(def.id, def.localizedPrice, store.Name);
                done?.Invoke(true);
            });
        }

        /// <summary>Son paket içeriği (ekranda göstermek için).</summary>
        public static string LastPackText = "";

        /// <summary>Sunucunun makbuz doğrulayıcısını seçmesi için mağaza kodu.</summary>
        public static string StoreCode(string storeName)
        {
            string n = (storeName ?? "").ToLowerInvariant();
            if (n.Contains("editor")) return "editor";
            if (n.Contains("google")) return "google";
            if (n.Contains("app store") || n.Contains("apple")) return "apple";
            if (n.Contains("samsung")) return "samsung";
            if (n.Contains("amazon")) return "amazon";
            if (n.Contains("huawei")) return "huawei";
            return n;
        }

        public static bool IsDiamondPack(string id) => id.StartsWith("diamonds_");

        static void Grant(ProductDef def)
        {
            var data = GameServices.Save.Data;
            int gems = def.diamonds;
            // Elmas paketleri: ilk alım 2 kat, hafta sonu +%50
            if (IsDiamondPack(def.id)) gems = Core.Meta.Packs.DiamondsFor(data, def.diamonds, DateTime.Now);
            if (gems > 0) GameServices.Wallet.Add(Currency.Diamonds, gems, "purchase:" + def.id);
            if (IsDiamondPack(def.id)) data.firstPurchaseDone = true;
            LastPackText = "";
            if (def.id == Catalog.HeroPack)
            {
                LastPackText = Core.Meta.Packs.GrantHeroPack(data);
                if (LastPackText == "BONUS") { GameServices.Wallet.Add(Currency.Diamonds, Core.Meta.Packs.HeroPackGems, "purchase:" + def.id); LastPackText = $"+{Core.Meta.Packs.HeroPackGems} ek elmas"; }
            }
            if (def.id == Catalog.SeasonPass) { Core.Meta.Season.UnlockPremium(data); LastPackText = "Sezon Bileti açıldı"; }
            if (def.id == Catalog.SpeedPack) { Core.Meta.Packs.GrantSpeedPack(data); LastPackText = $"+{Core.Meta.Packs.SpeedPackTokens} hızlandırıcı"; }
            if (def.id == Catalog.NoAds || def.id == Catalog.StarterPack)
            {
                data.noAds = true;
                if (GameServices.Ads != null) GameServices.Ads.InterstitialsDisabled = true;
            }
            if (def.id == Catalog.StarterPack) data.starterPackBought = true;
            if (def.id == Catalog.PiggyBank)
            {
                int n = Core.Meta.Offers.PiggyTake(data, Core.Meta.Offers.PiggyCap);
                if (n > 0) GameServices.Wallet.Add(Currency.Diamonds, n, "purchase:" + def.id);
            }
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
