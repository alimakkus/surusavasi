using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Services.Platform
{
    /// <summary>Unity Editor'de sahte mağaza: her satın alma başarılı olur, para çekilmez.</summary>
    public class EditorStore : IStore
    {
        readonly List<ProductDef> products = new List<ProductDef>();
        public string Name => "Editor (sahte)";
        public bool IsReady { get; private set; }
        public IReadOnlyList<ProductDef> Products => products;

        public void Initialize(IReadOnlyList<ProductDef> catalog, Action<bool> done)
        {
            products.Clear();
            foreach (var p in catalog) { p.localizedPrice = "TEST"; products.Add(p); }
            IsReady = true; done?.Invoke(true);
        }

        public void Purchase(string productId, Action<PurchaseResult> done)
        {
            Debug.Log("[EditorStore] Sahte satın alma: " + productId);
            done?.Invoke(new PurchaseResult { status = PurchaseStatus.Success, productId = productId, receipt = "editor" });
        }

        public void RestorePurchases(Action<bool> done) => done?.Invoke(true);
    }

    /// <summary>Editor'de sahte reklam: hemen "izlendi" sayılır.</summary>
    public class EditorAds : IAdService
    {
        public string Name => "Editor (sahte)";
        public bool InterstitialsDisabled { get; set; }
        public void Initialize(bool personalizedAds, Action<bool> done) => done?.Invoke(true);
        public bool IsRewardedReady(string placement) => true;
        public void ShowRewarded(string placement, Action<bool> done)
        { Debug.Log("[EditorAds] Sahte ödüllü reklam: " + placement); done?.Invoke(true); }
        public void ShowInterstitial(string placement) => Debug.Log("[EditorAds] Sahte aralık reklamı: " + placement);
    }

    public class DebugAnalytics : IAnalytics
    {
        bool on;
        public void Initialize(bool consentGiven) { on = true; }
        public void LogEvent(string name, Dictionary<string, object> data = null)
        {
            if (!on) return;
            var s = name;
            if (data != null) foreach (var kv in data) s += $" {kv.Key}={kv.Value}";
            Debug.Log("[Analytics] " + s);
        }
        public void LogPurchase(string productId, string localizedPrice, string store) =>
            LogEvent("purchase", new Dictionary<string, object> { { "product", productId }, { "price", localizedPrice }, { "store", store } });
        public void LogAd(string placement, bool completed) =>
            LogEvent("ad", new Dictionary<string, object> { { "placement", placement }, { "completed", completed } });
    }
}
