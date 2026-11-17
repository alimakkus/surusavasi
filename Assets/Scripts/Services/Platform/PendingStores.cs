using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Services.Platform
{
    /// <summary>Mağaza SDK'sı henüz eklenmemiş mağazalar için ortak taban.
    /// Satın alma "Failed" döner, oyun çökmez. SDK eklenince her sınıf kendi kodunu alacak.</summary>
    public abstract class PendingStore : IStore
    {
        public abstract string Name { get; }
        public bool IsReady => false;
        public IReadOnlyList<ProductDef> Products => Array.Empty<ProductDef>();
        public void Initialize(IReadOnlyList<ProductDef> catalog, Action<bool> done)
        { Debug.LogWarning($"[{Name}] Mağaza SDK'sı henüz eklenmedi."); done?.Invoke(false); }
        public void Purchase(string productId, Action<PurchaseResult> done) =>
            done?.Invoke(new PurchaseResult { status = PurchaseStatus.Failed, productId = productId, error = "not_implemented" });
        public void RestorePurchases(Action<bool> done) => done?.Invoke(false);
    }

    public class GooglePlayStore : PendingStore { public override string Name => "Google Play"; }   // Unity IAP
    public class AppleStore : PendingStore { public override string Name => "App Store"; }          // Unity IAP
    public class SamsungStore : PendingStore { public override string Name => "Samsung Galaxy Store"; }
    public class AmazonStore : PendingStore { public override string Name => "Amazon Appstore"; }
    public class HuaweiStore : PendingStore { public override string Name => "Huawei AppGallery"; } // HMS IAP

    public class PendingAds : IAdService
    {
        public PendingAds(string name) { Name = name + " (eklenmedi)"; }
        public string Name { get; }
        public bool InterstitialsDisabled { get; set; }
        public void Initialize(bool personalizedAds, Action<bool> done) => done?.Invoke(false);
        public bool IsRewardedReady(string placement) => false;
        public void ShowRewarded(string placement, Action<bool> done) => done?.Invoke(false);
        public void ShowInterstitial(string placement) { }
    }
}
