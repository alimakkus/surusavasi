using System.Collections.Generic;

namespace SuruSavasi.Services
{
    public interface IAnalytics
    {
        void Initialize(bool consentGiven);
        void LogEvent(string name, Dictionary<string, object> data = null);
        void LogPurchase(string productId, string localizedPrice, string store);
        void LogAd(string placement, bool completed);
    }

    /// <summary>Analiz olay adları tek yerde.</summary>
    public static class AnalyticsEvent
    {
        public const string LevelStart = "level_start";
        public const string LevelEnd = "level_end";
        public const string BuildingUpgrade = "building_upgrade";
        public const string DiamondSpend = "diamond_spend";
        public const string StoreOpen = "store_open";
    }
}
