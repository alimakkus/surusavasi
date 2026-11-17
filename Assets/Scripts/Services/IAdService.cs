using System;

namespace SuruSavasi.Services
{
    /// <summary>Reklam gösterilen yerler. Analizde hangi yerin para kazandırdığını görmek için ayrı tutulur.</summary>
    public static class AdPlacement
    {
        public const string FreeDiamonds = "free_diamonds";
        public const string SpeedUpBuild = "speed_up_build";
        public const string SecondChance = "second_chance";
        public const string DoubleReward = "double_reward";
        public const string LevelEnd = "level_end"; // aralık (interstitial) reklamı
    }

    public interface IAdService
    {
        string Name { get; }
        void Initialize(bool personalizedAds, Action<bool> done);
        bool IsRewardedReady(string placement);
        /// <summary>done(true) = oyuncu reklamı sonuna kadar izledi, ödül verilir.</summary>
        void ShowRewarded(string placement, Action<bool> done);
        void ShowInterstitial(string placement);
        /// <summary>Elmas satın alan oyunculara aralık reklamı gösterilmez.</summary>
        bool InterstitialsDisabled { get; set; }
    }
}
