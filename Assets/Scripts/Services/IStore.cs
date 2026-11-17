using System;
using System.Collections.Generic;

namespace SuruSavasi.Services
{
    public enum ProductKind { Consumable, NonConsumable, Subscription }

    /// <summary>Satılan ürün tanımı (örn. 100 elmas paketi).</summary>
    [Serializable]
    public class ProductDef
    {
        public string id;          // mağazadaki ürün kimliği, tüm mağazalarda aynı tutulur
        public ProductKind kind;
        public int diamonds;       // verilecek elmas
        public string localizedPrice = ""; // mağazadan gelir, örn. "₺49,99"

        public ProductDef(string id, ProductKind kind, int diamonds)
        { this.id = id; this.kind = kind; this.diamonds = diamonds; }
    }

    public enum PurchaseStatus { Success, Cancelled, Failed, Pending }

    public struct PurchaseResult
    {
        public PurchaseStatus status;
        public string productId;
        public string receipt;     // sunucu doğrulaması için mağaza makbuzu
        public string error;
    }

    /// <summary>Her mağaza (Google Play, App Store, Samsung, Amazon, Huawei) bu arayüzü uygular.</summary>
    public interface IStore
    {
        string Name { get; }
        bool IsReady { get; }
        IReadOnlyList<ProductDef> Products { get; }
        void Initialize(IReadOnlyList<ProductDef> catalog, Action<bool> done);
        void Purchase(string productId, Action<PurchaseResult> done);
        void RestorePurchases(Action<bool> done);
    }
}
