using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Sezon bileti: 28 günlük sezonlar (pazartesi başlar). Oynadıkça sezon puanı (SP) toplanır; her 100 SP bir kademe.
    /// 30 kademede iki ödül yolu: ücretsiz ve Sezon Bileti (ücretli). Her hafta 3 sezon görevi büyük SP verir.
    /// 30. kademeden sonra her 200 SP bir bonus sandık.</summary>
    public static class Season
    {
        public const int OpenAt = 4;   // 4. bölümden sonra açılır (yeni oyuncuyu boğmasın)
        public static bool Open(SaveData d) => d.unlocked >= OpenAt;
        public const int Days = 28, Tiers = 30, TierPts = 100, DailyCap = 350, WeeklyPts = 150, BonusPts = 200, SkipGems = 60;
        static readonly DateTime Epoch = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc); // 1. sezon: 23 Kasım 2026 pazartesi

        public static readonly string[] Names = { "Kış Uyanışı", "Buz Sürüsü", "Bahar Kaçışı", "Kızıl Yaz", "Kum Fırtınası", "Hasat Gecesi", "Sis Ayı", "Demir Kış" };
        public static readonly string[] Colors = { "#7fd6ff", "#a8e6ff", "#8be39d", "#ff8a65", "#ffcc66", "#ffb02e", "#b39ddb", "#cfd8e0" };

        /// <summary>Saat kaynağı (testlerde değiştirilir).</summary>
        public static Func<DateTime> Clock = () => DateTime.UtcNow;
        static DateTime Now => Clock();

        public static int Id(DateTime utc) => Math.Max(1, (int)Math.Floor((utc - Epoch).TotalDays / Days) + 1);
        public static int Current => Id(Now);
        public static string Name(int id) => Names[(id - 1) % Names.Length];
        public static string Color(int id) => Colors[(id - 1) % Colors.Length];
        public static DateTime Start(int id) => Epoch.AddDays((id - 1) * Days);
        public static DateTime End(int id) => Start(id).AddDays(Days);
        public static TimeSpan Left => End(Current) - Now;
        public static int Week(DateTime utc) => Math.Min(3, (int)((utc - Start(Id(utc))).TotalDays / 7)); // 0-3
        static string Today => Now.ToString("yyyyMMdd");

        // ---------------- Ödüller ----------------
        public struct Reward { public string kind; public int n; public Reward(string k, int n) { kind = k; this.n = n; } }

        /// <summary>Ücretsiz yol ödülü (kademe 1-30).</summary>
        public static Reward Free(int tier)
        {
            switch (tier)
            {
                case 5: return new Reward("gems", 20);
                case 10: return new Reward("shards", 10);
                case 15: return new Reward("gear", 1);
                case 20: return new Reward("shards", 10);
                case 25: return new Reward("gems", 40);
                case 30: return new Reward("title", 1);
            }
            switch (tier % 4) { case 1: return new Reward("gold", 300); case 2: return new Reward("food", 300); case 3: return new Reward("iron", 200); default: return new Reward("gems", 10); }
        }

        /// <summary>Sezon Bileti yolu ödülü.</summary>
        public static Reward Premium(int tier)
        {
            switch (tier)
            {
                case 5: return new Reward("tokens", 2);
                case 10: return new Reward("shards", 20);
                case 15: return new Reward("gear", 2);
                case 20: return new Reward("shards", 20);
                case 25: return new Reward("tokens", 3);
                case 30: return new Reward("skin", 1);
            }
            switch (tier % 3) { case 1: return new Reward("gems", 25); case 2: return new Reward("gold", 800); default: return new Reward("tokens", 1); }
        }

        /// <summary>Kademe 30'da Sezon Bileti ile ayrıca verilen elmas.</summary>
        public const int FinalPremiumGems = 150;

        public static string RewardText(Reward r, SaveData d)
        {
            float m = ResMul(d);
            switch (r.kind)
            {
                case "gold": return $"{(int)(r.n * m)} altın";
                case "iron": return $"{(int)(r.n * m)} demir";
                case "food": return $"{(int)(r.n * m)} yiyecek";
                case "gems": return $"{r.n} elmas";
                case "shards": return $"{r.n} kahraman parçası";
                case "tokens": return r.n == 1 ? "1 hızlandırıcı" : $"{r.n} hızlandırıcı";
                case "gear": return r.n >= 2 ? "Epik ekipman" : "Nadir ekipman";
                case "title": return "Sezon unvanı";
                case "skin": return "Kahraman görünümü";
            }
            return r.kind;
        }

        public static string RewardIcon(Reward r)
        {
            switch (r.kind)
            {
                case "gold": return "<color=#ffd23f>◎</color>";
                case "iron": return "<color=#cfd8e0>■</color>";
                case "food": return "<color=#8be39d>♣</color>";
                case "gems": return "<color=#9fe8ff>♦</color>";
                case "shards": return "<color=#ffb02e>▲</color>";
                case "tokens": return "<color=#ff9a3a>»</color>";
                case "gear": return "<color=#c98bff>♜</color>";
                case "title": return "<color=#ffd23f>♛</color>";
                case "skin": return "<color=#ff6f91>★</color>";
            }
            return "";
        }

        /// <summary>Kaynak ödülleri oyuncunun ilerlemesiyle büyür (geç oyunda da işe yarasın).</summary>
        public static float ResMul(SaveData d) => 1f + Math.Min(100, d.unlocked) / 20f;

        // ---------------- Durum ----------------
        /// <summary>Sezon değiştiyse sezon durumunu sıfırlar.</summary>
        public static void Ensure(SaveData d)
        {
            int id = Current;
            if (d.seasonId == id) { WeekCheck(d); return; }
            d.seasonId = id; d.seasonPts = 0; d.seasonPremium = false; d.seasonFree = 0; d.seasonPrem = 0;
            d.seasonBonus = 0; d.seasonDay = ""; d.seasonDayPts = 0; d.seasonWeek = -1; d.seasonWeekProg = new int[3]; d.seasonWeekDone = 0;
            d.seasonKeyDay = ""; d.seasonKeyKeys = new List<string>(); d.seasonKeyVals = new List<int>(); d.seasonSeen = false;
            WeekCheck(d);
        }

        public static int Tier(SaveData d) { Ensure(d); return Math.Min(Tiers, d.seasonPts / TierPts); }
        public static int TierProgress(SaveData d) { Ensure(d); return d.seasonPts >= Tiers * TierPts ? (d.seasonPts - Tiers * TierPts) % BonusPts : d.seasonPts % TierPts; }
        public static int BonusEarned(SaveData d) { Ensure(d); return Math.Max(0, (d.seasonPts - Tiers * TierPts) / BonusPts); }
        public static bool FreeClaimed(SaveData d, int t) => (d.seasonFree & (1 << (t - 1))) != 0;
        public static bool PremClaimed(SaveData d, int t) => (d.seasonPrem & (1 << (t - 1))) != 0;
        public static bool CanFree(SaveData d, int t) => Tier(d) >= t && !FreeClaimed(d, t);
        public static bool CanPrem(SaveData d, int t) => d.seasonPremium && Tier(d) >= t && !PremClaimed(d, t);

        public static int Claimable(SaveData d)
        {
            int n = 0; for (int t = 1; t <= Tier(d); t++) { if (CanFree(d, t)) n++; if (CanPrem(d, t)) n++; }
            n += BonusEarned(d) - d.seasonBonus;
            for (int i = 0; i < 3; i++) if (WeeklyReady(d, i)) n++;
            return n;
        }

        // ---------------- Sezon puanı ----------------
        /// <summary>Olay başına SP ve günlük olay sınırı (Daily.Add'den gelir).</summary>
        static readonly Dictionary<string, (int pts, int cap)> EventPts = new Dictionary<string, (int, int)>
        {
            { "clear", (15, 300) }, { "stars3", (5, 60) }, { "boss", (8, 80) }, { "tower_floor", (4, 80) }, { "hunt_tier", (8, 40) },
            { "surv_wave", (2, 60) }, { "talent", (5, 25) }, { "upgrade", (12, 72) }, { "train", (4, 40) }, { "gather", (1, 20) },
            { "gates", (1, 20) }, { "turret", (1, 10) }, { "dice6", (2, 10) }, { "quest", (20, 80) }, { "quest_bonus", (30, 30) }, { "login", (20, 20) },
        };

        static int KeyGet(SaveData d, string k) { int i = d.seasonKeyKeys.IndexOf(k); return i < 0 ? 0 : d.seasonKeyVals[i]; }
        static void KeySet(SaveData d, string k, int v) { int i = d.seasonKeyKeys.IndexOf(k); if (i < 0) { d.seasonKeyKeys.Add(k); d.seasonKeyVals.Add(v); } else d.seasonKeyVals[i] = v; }

        /// <summary>Oyun olayı: SP verir (günlük sınırlarla) ve haftalık görevleri ilerletir. Kazanılan SP'yi döndürür.</summary>
        public static int OnEvent(SaveData d, string key, int n = 1)
        {
            if (!Open(d) || n <= 0) return 0;
            Ensure(d);
            WeekCheck(d);
            for (int i = 0; i < 3; i++)
            {
                var w = WeeklyDef(d, i);
                if (w.key == key && (d.seasonWeekDone & (1 << i)) == 0) d.seasonWeekProg[i] = Math.Min(w.n, d.seasonWeekProg[i] + n);
            }
            if (!EventPts.TryGetValue(key, out var e)) return 0;
            string today = Today;
            if (d.seasonKeyDay != today) { d.seasonKeyDay = today; d.seasonKeyKeys.Clear(); d.seasonKeyVals.Clear(); }
            int used = KeyGet(d, key);
            int give = Math.Min(e.pts * n, e.cap - used);
            if (give <= 0) return 0;
            give = AddPts(d, give, true);
            KeySet(d, key, used + give);
            return give;
        }

        /// <summary>SP ekler; daily=true ise günlük toplam sınırına (350) tabi.</summary>
        public static int AddPts(SaveData d, int pts, bool daily)
        {
            Ensure(d);
            if (daily)
            {
                string today = Today;
                if (d.seasonDay != today) { d.seasonDay = today; d.seasonDayPts = 0; }
                pts = Math.Min(pts, DailyCap - d.seasonDayPts);
                if (pts <= 0) return 0;
                d.seasonDayPts += pts;
            }
            d.seasonPts += pts;
            return pts;
        }

        // ---------------- Haftalık sezon görevleri ----------------
        public struct WeeklyTask { public string key, text; public int n; public Func<SaveData, bool> req; }
        static readonly WeeklyTask[] Pool =
        {
            new WeeklyTask { key = "clear", n = 25, text = "25 savaş kazan" },
            new WeeklyTask { key = "boss", n = 8, text = "8 patron ya da elit yen" },
            new WeeklyTask { key = "upgrade", n = 6, text = "6 bina geliştir" },
            new WeeklyTask { key = "stars3", n = 10, text = "10 bölümü 3 yıldızla bitir" },
            new WeeklyTask { key = "train", n = 10, text = "10 kez asker eğit" },
            new WeeklyTask { key = "gates", n = 60, text = "60 iyi kapıdan geç" },
            new WeeklyTask { key = "quest", n = 12, text = "12 günlük görev tamamla" },
            new WeeklyTask { key = "tower_floor", n = 15, text = "Kulede 15 kat çık", req = d => Modes.TowerOpen(d) },
            new WeeklyTask { key = "surv_wave", n = 40, text = "Hayatta Kalma'da 40 dalga geç", req = d => Modes.SurvOpen(d) },
            new WeeklyTask { key = "hunt_tier", n = 5, text = "Patron Avı'nda 5 kademe aç", req = d => Modes.HuntOpen(d) },
            new WeeklyTask { key = "talent", n = 3, text = "3 yetenek puanı harca", req = d => d.unlocked >= 12 },
        };

        static void WeekCheck(SaveData d)
        {
            int w = Week(Now);
            if (d.seasonWeek == w) return;
            d.seasonWeek = w; d.seasonWeekProg = new int[3]; d.seasonWeekDone = 0;
        }

        /// <summary>Bu haftanın i. görevi (sezon ve haftaya göre sabit; oyuncunun açtığı modlara göre süzülür).</summary>
        static string cacheKey; static List<WeeklyTask> cache;
        public static WeeklyTask WeeklyDef(SaveData d, int i)
        {
            string ck = d.seasonId + "|" + d.seasonWeek + "|" + Modes.TowerOpen(d) + Modes.SurvOpen(d) + Modes.HuntOpen(d) + (d.unlocked >= 12);
            if (ck == cacheKey) return cache[i % cache.Count];
            var list = new List<WeeklyTask>();
            foreach (var t in Pool) if (t.req == null || t.req(d)) list.Add(t);
            int seed = d.seasonId * 7919 + Math.Max(0, d.seasonWeek) * 104729;
            var r = new Random(seed);
            for (int k = list.Count - 1; k > 0; k--) { int j = r.Next(k + 1); var tmp = list[k]; list[k] = list[j]; list[j] = tmp; }
            cacheKey = ck; cache = list;
            return list[i % list.Count];
        }

        public static bool WeeklyReady(SaveData d, int i) { Ensure(d); WeekCheck(d); return (d.seasonWeekDone & (1 << i)) == 0 && d.seasonWeekProg[i] >= WeeklyDef(d, i).n; }

        public static bool ClaimWeekly(SaveData d, int i)
        {
            if (!WeeklyReady(d, i)) return false;
            d.seasonWeekDone |= 1 << i;
            AddPts(d, WeeklyPts, false);
            return true;
        }

        public static TimeSpan WeekLeft()
        {
            var now = Now; var s = Start(Id(now)).AddDays((Week(now) + 1) * 7);
            if (Week(now) == 3) s = End(Id(now));
            return s - now;
        }

        // ---------------- Talep ----------------
        /// <summary>Ödülü verir. grant(currency, n) kaynak/elmas eklemesini yapar (Wallet). Ekranda gösterilecek metni döndürür.</summary>
        public static string Give(SaveData d, Reward r, Action<string, int> grant, Random rnd)
        {
            float m = ResMul(d);
            switch (r.kind)
            {
                case "gold": case "iron": case "food": { int n = (int)(r.n * m); grant(r.kind, n); return RewardText(r, d); }
                case "gems": grant("gems", r.n); return $"{r.n} elmas";
                case "tokens": d.speedTokens += r.n; return RewardText(r, d);
                case "shards":
                    {
                        int h = HeroShards.Give(d, r.n);
                        if (h >= 0) return $"{r.n} {Progress.Heroes[h].name} parçası";
                        int g = (int)(1500 * m); grant("gold", g); return $"{g} altın (tüm kahramanlar açık)";
                    }
                case "gear":
                    {
                        var it = new GearItem { id = ++d.gearSeq, slot = rnd.Next(3), rarity = r.n >= 2 ? 2 : 1 };
                        Gear.AddToBag(d, it);
                        return $"{Gear.Name(it)} ({Gear.RarityNames[it.rarity]})";
                    }
                case "title":
                    {
                        string key = "season" + d.seasonId;
                        if (!d.seasonTitles.Contains(key)) d.seasonTitles.Add(key);
                        return $"Unvan: {TitleName(key)}";
                    }
                case "skin":
                    {
                        grant("gems", FinalPremiumGems);
                        var (h, s) = Packs.HeroPackSkin(d);
                        if (h >= 0) { Skins.Give(d, h, s); return $"{Progress.Heroes[h].name} {Skins.Names[s]} görünümü + {FinalPremiumGems} elmas"; }
                        grant("gems", 200); return $"{FinalPremiumGems + 200} elmas (tüm görünümler sende)";
                    }
            }
            return "";
        }

        public static string ClaimFree(SaveData d, int t, Action<string, int> grant, Random rnd)
        {
            if (!CanFree(d, t)) return null;
            d.seasonFree |= 1 << (t - 1);
            return Give(d, Free(t), grant, rnd);
        }

        public static string ClaimPrem(SaveData d, int t, Action<string, int> grant, Random rnd)
        {
            if (!CanPrem(d, t)) return null;
            d.seasonPrem |= 1 << (t - 1);
            return Give(d, Premium(t), grant, rnd);
        }

        /// <summary>Bonus sandık (30. kademeden sonra her 200 SP): kaynak + bilette 10 elmas.</summary>
        public static string ClaimBonus(SaveData d, Action<string, int> grant)
        {
            if (BonusEarned(d) <= d.seasonBonus) return null;
            d.seasonBonus++;
            int g = (int)(400 * ResMul(d)); grant("gold", g); grant("food", g / 2);
            if (d.seasonPremium) grant("gems", 10);
            return $"Bonus sandık: {g} altın, {g / 2} yiyecek" + (d.seasonPremium ? ", 10 elmas" : "");
        }

        /// <summary>Elmasla bir kademe atla (sezon sonuna 7 günden az kalınca açılır; kademe 30'a kadar).</summary>
        public static bool CanSkip(SaveData d) => Tier(d) < Tiers && Left.TotalDays < 7;
        public static void Skip(SaveData d) { int need = (Tier(d) + 1) * TierPts - d.seasonPts; d.seasonPts += Math.Max(0, need); }

        public static void UnlockPremium(SaveData d) { Ensure(d); d.seasonPremium = true; }

        public static string TitleName(string key)
        {
            if (!key.StartsWith("season")) return null;
            int id; if (!int.TryParse(key.Substring(6), out id)) return null;
            return $"{Name(id)} Gazisi";
        }
    }
}
