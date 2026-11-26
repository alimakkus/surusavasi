using System;
using SuruSavasi.Core;

namespace SuruSavasi.Services
{
    public enum Currency { Gold, Iron, Food, Diamonds }

    /// <summary>Oyuncunun kaynakları. Tüm harcama ve kazanç buradan geçer.
    /// Elmaslar ayrıca elmas defterine (Core.Meta.GemLedger) yazılır; sunucu (Cloud Code) gerçek bakiyeyi tutar,
    /// buradaki değer onun önbelleğidir (sunucu bakiyesi + henüz gönderilmemiş işlemler).</summary>
    public class Wallet
    {
        readonly ISaveService save;
        public event Action<Currency, int> Changed;

        public Wallet(ISaveService save) { this.save = save; }

        public int Get(Currency c)
        {
            var d = save.Data;
            switch (c)
            {
                case Currency.Gold: return d.gold;
                case Currency.Iron: return d.iron;
                case Currency.Food: return d.food;
                default: return d.diamonds;
            }
        }

        /// <summary>Elmas işlemleri sunucu defterine yazılsın mı (bulut paketi kuruluysa evet).</summary>
        public static bool LedgerOn => Cloud.CloudSync.Supported;

        /// <summary>Bu cihazın kalıcı kısa kimliği: işlem kimlikleri cihazlar arasında çakışmasın diye (kayıtla taşınmaz).</summary>
        public static string DeviceTag
        {
            get
            {
                const string k = "surusavasi-dev";
                var t = UnityEngine.PlayerPrefs.GetString(k, "");
                if (string.IsNullOrEmpty(t)) { t = Guid.NewGuid().ToString("N").Substring(0, 8); UnityEngine.PlayerPrefs.SetString(k, t); UnityEngine.PlayerPrefs.Save(); }
                return t;
            }
        }

        Core.Meta.GemOp purchase;

        /// <summary>Satın alma ödülü verilirken çağrılır: aradaki elmas eklemeleri tek bir "satın alma" işlemine (makbuzla) toplanır.</summary>
        public void BeginPurchase(string store, string productId, string receipt)
        { purchase = new Core.Meta.GemOp { k = "p", store = store, pid = productId, rc = receipt ?? "" }; }

        public void EndPurchase()
        {
            var p = purchase; purchase = null;
            if (p == null || p.n <= 0 || !LedgerOn) return;
            Core.Meta.GemLedger.Record(save.Data, DeviceTag, "p", p.n, "purchase:" + p.pid, p.store, p.pid, p.rc);
            save.Save();
        }

        public bool CanAfford(Currency c, int amount) => amount >= 0 && Get(c) >= amount;

        public void Add(Currency c, int amount, string reason)
        {
            if (amount <= 0) return;
            if (c == Currency.Diamonds)
            {
                if (purchase != null) purchase.n += amount;
                else if (LedgerOn) Core.Meta.GemLedger.Record(save.Data, DeviceTag, "e", amount, reason);
            }
            Set(c, Get(c) + amount);
            GameServices.Analytics?.LogEvent("currency_add", new System.Collections.Generic.Dictionary<string, object>
                { { "currency", c.ToString() }, { "amount", amount }, { "reason", reason } });
        }

        public bool Spend(Currency c, int amount, string reason)
        {
            if (!CanAfford(c, amount)) return false;
            if (c == Currency.Diamonds && amount > 0 && LedgerOn) Core.Meta.GemLedger.Record(save.Data, DeviceTag, "s", amount, reason);
            Set(c, Get(c) - amount);
            if (c == Currency.Diamonds)
                GameServices.Analytics?.LogEvent(AnalyticsEvent.DiamondSpend, new System.Collections.Generic.Dictionary<string, object>
                    { { "amount", amount }, { "reason", reason } });
            return true;
        }

        void Set(Currency c, int value)
        {
            var d = save.Data;
            switch (c)
            {
                case Currency.Gold: d.gold = value; break;
                case Currency.Iron: d.iron = value; break;
                case Currency.Food: d.food = value; break;
                default: d.diamonds = value; break;
            }
            save.Save();
            Changed?.Invoke(c, value);
        }
    }
}
