using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kalıcı başarımlar: her biri 3 kademe (Bronz, Gümüş, Altın). Kademe dolunca ödül elle alınır;
    /// Altın kademesi alınan başarımın adı unvan olarak seçilebilir (ligde adının yanında görünür).
    /// Değerin bir kısmı sayaçtan (öldürme, kapı…), bir kısmı kayıttaki mevcut ilerlemeden okunur.</summary>
    public static class Achievements
    {
        public class Def
        {
            public string id, name, title, unit; public int[] tiers; public string icon;
            public Func<SaveData, long> value;
        }

        public static readonly string[] TierNames = { "Bronz", "Gümüş", "Altın" };
        public static readonly string[] TierColors = { "#d08a4a", "#cfd8dc", "#ffd23f" };
        public static readonly int[] TierGems = { 10, 25, 60 };

        public static long Stat(SaveData d, string k) { Ensure(d); int i = d.statKeys.IndexOf(k); return i < 0 ? 0 : d.statVals[i]; }
        public static void Add(SaveData d, string k, long n = 1)
        {
            Ensure(d); int i = d.statKeys.IndexOf(k);
            if (i < 0) { d.statKeys.Add(k); d.statVals.Add(n); } else d.statVals[i] += n;
        }
        public static void Max(SaveData d, string k, long v) { if (v > Stat(d, k)) Add(d, k, v - Stat(d, k)); }

        static void Ensure(SaveData d)
        {
            if (d.statKeys == null) d.statKeys = new List<string>();
            if (d.statVals == null) d.statVals = new List<long>();
            if (d.achClaimed == null) d.achClaimed = new List<string>();
        }

        static int Owned(SaveData d) { int c = 0; for (int i = 0; i < Progress.Heroes.Length; i++) if (Progress.HeroOpen(d, i)) c++; return c; }

        public static readonly Def[] All =
        {
            new Def { id = "kills", name = "Zombi Avcısı", title = "Zombi Avcısı", icon = "✖", unit = "zombi", tiers = new[] { 500, 5000, 50000 }, value = d => Stat(d, "kills") },
            new Def { id = "levels", name = "Yolun Sonu", title = "Kurtarıcı", icon = "▲", unit = "seviye", tiers = new[] { 10, 50, 100 }, value = d => Math.Min(Balance.MaxLevel, d.unlocked - 1 + (d.stars[Balance.MaxLevel - 1] > 0 ? 1 : 0)) },
            new Def { id = "stars", name = "Yıldız Toplayıcı", title = "Yıldız Avcısı", icon = "★", unit = "yıldız", tiers = new[] { 30, 150, 300 }, value = d => Progress.TotalStars(d) },
            new Def { id = "bosses", name = "Patron Devirici", title = "Dev Avcısı", icon = "☠", unit = "patron/elit", tiers = new[] { 10, 60, 250 }, value = d => Stat(d, "bosses") },
            new Def { id = "tower", name = "Kule Fatihi", title = "Kule Efendisi", icon = "♜", unit = "kat (rekor)", tiers = new[] { 10, 25, 50 }, value = d => d.towerBest },
            new Def { id = "hunt", name = "Patron Avcısı", title = "Baş Avcı", icon = "◎", unit = "% hasar (rekor)", tiers = new[] { 50, 75, 100 }, value = d => d.huntBestEver / 10 },
            new Def { id = "surv", name = "Hayatta Kalan", title = "Yıkılmaz", icon = "♥", unit = "dalga (rekor)", tiers = new[] { 10, 20, 30 }, value = d => d.survBest },
            new Def { id = "gates", name = "Kapı Ustası", title = "Kapı Ustası", icon = "▬", unit = "iyi kapı", tiers = new[] { 50, 300, 1500 }, value = d => Stat(d, "gates") },
            new Def { id = "jackpot", name = "Şanslı Zar", title = "Talih Kuşu", icon = "♦", unit = "büyük ikramiye", tiers = new[] { 1, 10, 50 }, value = d => Stat(d, "jackpot") },
            new Def { id = "turret", name = "Mühendislik", title = "Baş Mühendis", icon = "♖", unit = "taret", tiers = new[] { 10, 100, 500 }, value = d => Stat(d, "turret") },
            new Def { id = "league", name = "Lig Yıldızı", title = "Lig Ustası", icon = "♛", unit = "en yüksek lig", tiers = new[] { 1, 2, 4 }, value = d => Stat(d, "leagueBest") },
            new Def { id = "heroes", name = "Koleksiyoncu", title = "Koleksiyoncu", icon = "♣", unit = "kahraman", tiers = new[] { 3, 4, 5 }, value = d => Owned(d) },
            new Def { id = "hq", name = "Usta İnşaatçı", title = "Kale Mimarı", icon = "■", unit = "Karargâh seviyesi", tiers = new[] { 3, 6, 10 }, value = d => Progress.Level(d, BuildingKind.Headquarters) },
        };

        /// <summary>Ulaşılan kademe sayısı (0-3).</summary>
        public static int Reached(SaveData d, Def a) { long v = a.value(d); int t = 0; foreach (var x in a.tiers) if (v >= x) t++; return t; }
        public static int ClaimedTiers(SaveData d, Def a) { Ensure(d); int t = 0; for (int i = 0; i < 3; i++) if (d.achClaimed.Contains(a.id + i)) t++; return t; }
        public static bool CanClaim(SaveData d, Def a) => Reached(d, a) > ClaimedTiers(d, a);
        public static int Claim(SaveData d, Def a) { int t = ClaimedTiers(d, a); d.achClaimed.Add(a.id + t); return TierGems[t]; }
        public static bool AnyClaim(SaveData d) { foreach (var a in All) if (CanClaim(d, a)) return true; return false; }
        public static int Score(SaveData d) { int s = 0; foreach (var a in All) s += ClaimedTiers(d, a); return s; }
        public static string Title(SaveData d)
        {
            if (string.IsNullOrEmpty(d.title)) return null;
            foreach (var a in All) if (a.id == d.title && ClaimedTiers(d, a) >= 3) return a.title;
            return null;
        }
    }
}
