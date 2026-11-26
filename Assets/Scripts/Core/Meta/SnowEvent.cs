using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kar Fırtınası: hafta sonu etkinliği (cumartesi-pazar). Günde 3 hak. Fırtınada düşmanlar %30 yavaş ama
    /// %40 dayanıklı; iyi kapılar %25 güçlü. Zafer: kaynak ×1.5, 5 elmas ve sezon puanı (+30 SP, günlük sınıra sayılmaz).</summary>
    public static class SnowEvent
    {
        public const int TriesPerDay = 3, WinSP = 30, WinGems = 5;
        public const float Slow = .7f, HpMul = 1.4f, GateBoost = .25f, ResMul = 1.5f;

        /// <summary>Yerel saat kaynağı (testlerde ve otomatik görüntüde değiştirilir).</summary>
        public static Func<DateTime> Clock = () => DateTime.Now;
        public static DateTime Now => Clock();

        public static bool Unlocked(SaveData d) => Season.Open(d);
        public static bool Active(DateTime local) => local.DayOfWeek == DayOfWeek.Saturday || local.DayOfWeek == DayOfWeek.Sunday;
        public static bool Open(SaveData d, DateTime local) => Unlocked(d) && Active(local);

        /// <summary>Etkinliğin başlamasına (aktifse bitmesine) kalan süre.</summary>
        public static TimeSpan Until(DateTime local)
        {
            var day = local.Date;
            if (Active(local)) { var end = day.AddDays(1); while (Active(end)) end = end.AddDays(1); return end - local; }
            var start = day.AddDays(1); while (!Active(start)) start = start.AddDays(1);
            return start - local;
        }

        static string Today(DateTime local) => local.ToString("yyyyMMdd");
        public static int TriesLeft(SaveData d, DateTime local) => d.eventDay == Today(local) ? Math.Max(0, TriesPerDay - d.eventTries) : TriesPerDay;
        public static void UseTry(SaveData d, DateTime local)
        {
            string t = Today(local);
            if (d.eventDay != t) { d.eventDay = t; d.eventTries = 0; }
            d.eventTries++;
        }

        /// <summary>Etkinlik seviyesi: oyuncunun ulaştığı bölümün biraz gerisi (en az 4).</summary>
        public static int Level(SaveData d) => Math.Max(4, Math.Min(Balance.MaxLevel, d.unlocked - 1));

        public static LevelReward Reward(SaveData d, int stars, Battle.BattleMods m)
        {
            var b = Rewards.ForLevel(Level(d), Math.Max(1, stars), false, m);
            return new LevelReward { gold = (int)(b.gold * ResMul), iron = (int)(b.iron * ResMul), food = (int)(b.food * ResMul), gems = WinGems };
        }
    }
}
