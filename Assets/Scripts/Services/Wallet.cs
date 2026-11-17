using System;
using SuruSavasi.Core;

namespace SuruSavasi.Services
{
    public enum Currency { Gold, Iron, Food, Diamonds }

    /// <summary>Oyuncunun kaynakları. Tüm harcama ve kazanç buradan geçer.
    /// İleride elmas bakiyesi sunucuda (Unity Economy) tutulacak, bu sınıf onun önbelleği olacak.</summary>
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

        public bool CanAfford(Currency c, int amount) => amount >= 0 && Get(c) >= amount;

        public void Add(Currency c, int amount, string reason)
        {
            if (amount <= 0) return;
            Set(c, Get(c) + amount);
            GameServices.Analytics?.LogEvent("currency_add", new System.Collections.Generic.Dictionary<string, object>
                { { "currency", c.ToString() }, { "amount", amount }, { "reason", reason } });
        }

        public bool Spend(Currency c, int amount, string reason)
        {
            if (!CanAfford(c, amount)) return false;
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
