using System;
using System.Collections.Generic;
using System.Linq;

// Sürü Savaşı — sunucu tarafı elmas kuralları (Unity Cloud Code modülü).
// Bu dosya Cloud Code SDK'sına bağlı değildir: yalnızca saf kurallar. Böylece Unity'nin derleyicisiyle
// bilgisayarda da test edilebilir (Tools/economy/run_tests.ps1).
namespace SuruEconomy
{
    /// <summary>Oyuncunun sunucudaki elmas durumu. Cloud Save "Protected" alanında durur: oyuncu okuyabilir, yalnızca sunucu yazabilir.</summary>
    public class EcoState
    {
        public int Bal { get; set; }                       // gerçek elmas bakiyesi
        public bool Seeded { get; set; }                   // ilk bakiye (sürüm öncesi kayıt) bir kez alındı mı
        public Dictionary<string, int> Used { get; set; } = new();   // "gün|kategori" → o gün kazanılan
        public Dictionary<string, int> Life { get; set; } = new();   // ömür boyu sınırlı kazançlar ve tek seferlik ürünler
        public List<string> Ids { get; set; } = new();     // son işlenen işlem kimlikleri (tekrar gönderime karşı)
        public List<string> Tx { get; set; } = new();      // kullanılmış mağaza işlem kimlikleri (aynı makbuz iki kez geçmez)
        public bool FirstDone { get; set; }                // ilk elmas paketi alındı mı (2 kat bonus bir kez)
        public long MonthlyUntil { get; set; }             // aylık kart bitişi (Unix sn)
        public Dictionary<string, int> Unverified { get; set; } = new(); // gün → doğrulanamayan mağazadan kredi sayısı
        public int Flags { get; set; }                     // şüpheli işlem sayacı (bakiye yetmeden harcama, sahte makbuz)
        public long LastSync { get; set; }
    }

    /// <summary>İstemciden gelen tek işlem. k: "e" kazanç, "s" harcama, "p" satın alma.</summary>
    public class Op
    {
        public string Id { get; set; } = "";
        public string K { get; set; } = "";
        public int N { get; set; }
        public string R { get; set; } = "";     // sebep, örn. "level", "quest:boss", "achievement:kill"
        public string D { get; set; } = "";     // işlemin günü (UTC, yyyyMMdd)
        public string Store { get; set; } = ""; // satın almada: google / apple / editor / samsung / amazon / huawei
        public string Pid { get; set; } = "";   // ürün kimliği
        public string Rc { get; set; } = "";    // mağaza makbuzu
    }

    public class SyncRequest
    {
        public int Base { get; set; }           // bekleyen işlemler öncesi istemci bakiyesi (yalnızca ilk eşitlemede, sınırlı kabul edilir)
        public List<Op> Ops { get; set; } = new();
    }

    public class SyncResult
    {
        public int Bal { get; set; }
        public List<string> Acked { get; set; } = new();
        public int Capped { get; set; }         // sınır yüzünden verilmeyen elmas
        public int RejSpend { get; set; }       // bakiye yetmediği için reddedilen harcama sayısı
        public int RejBuy { get; set; }         // doğrulanamayan satın alma sayısı
        public int Bought { get; set; }         // bu eşitlemede satın almadan gelen elmas
        public string Msg { get; set; } = "";
    }

    /// <summary>Makbuz doğrulamasının sonucu (EconomyModule tarafından, işlem uygulanmadan önce doldurulur).</summary>
    public class Verdict
    {
        public bool Ok;          // makbuz geçerli (ya da doğrulanamayan mağaza kuralıyla kabul edilebilir)
        public bool Verified;    // gerçekten mağaza imzası/sunucusuyla doğrulandı
        public string TxId = ""; // mağaza işlem kimliği
        public string Why = "";
        public const string Retry = "retry";
        public static Verdict No(string why) => new Verdict { Ok = false, Why = why };
    }

    public static class EconomyRules
    {
        public const int SeedCap = 5000;          // sürüm öncesi oyuncuların mevcut bakiyesinden en çok bu kadarı kabul edilir
        public const int UnverifiedPerDay = 3;    // doğrulayıcısı olmayan mağazadan günde en çok bu kadar satın alma kredilenir
        public const int KeepIds = 400, KeepTx = 1000, KeepDays = 8;
        public const int MaxOpsPerCall = 300;

        /// <summary>Günlük elmas sınırları (kategori → günde en çok). Dürüst oyuncunun ulaşabileceğinin rahatça üstünde tutulur.</summary>
        public static readonly Dictionary<string, int> Daily = new()
        {
            { "level", 400 },          // bölüm ilk geçişi: (yıldız + patron 5) × etkinlik 2
            { "quest", 60 },           // günlük görevler
            { "quest_bonus", 8 },
            { "story_quest", 80 },
            { "login", 10 },
            { "ad_free_gems", 25 },    // 5 reklam × 5
            { "piggy_ad", 20 },
            { "monthly_card", 50 },    // yalnızca aylık kart etkinse
            { "tower", 200 },
            { "hunt", 120 },
            { "survival", 100 },
            { "league", 300 },
            { "defense", 150 },
            { "base_chest", 20 },
            { "night_raid", 20 },
            { "chapter_chest", 400 },
            { "achievement", 300 },
            { "season", 1200 },
            { "event", 30 },           // Kar Fırtınası: zafer başına 5, günde 3 hak        // sezon bileti ödülleri (sezon başına da 1200 sınırı var)
            { "other", 50 },
        };

        /// <summary>Ömür boyu sınırlar (kategori → toplam).</summary>
        public static readonly Dictionary<string, int> Lifetime = new()
        {
            { "chapter_chest", 1100 }, // 10 bölüm × (15★ + 30★ sandığı)
            { "survival", 100 },       // 10/20/30/40. dalga ödülleri
            { "achievement", 1300 },   // 13 başarım × (10+25+60)
        };
        /// <summary>Alt anahtar başına ömür boyu sınır: "achievement:kills" başarım başına 95, "season:3" sezon başına 1200.</summary>
        public static readonly Dictionary<string, int> PerKey = new() { { "achievement", 95 }, { "season", 1200 } };

        /// <summary>Ürün başına en çok elmas. Elmas paketlerinde ilk alım ×2, hafta sonu ×1.5 ayrıca hesaplanır.</summary>
        public static readonly Dictionary<string, int> ProductGems = new()
        {
            { "diamonds_100", 100 }, { "diamonds_550", 550 }, { "diamonds_1200", 1200 }, { "diamonds_2600", 2600 }, { "diamonds_7000", 7000 },
            { "starter_pack", 500 }, { "monthly_card", 300 }, { "no_ads", 0 }, { "piggy_bank", 600 }, { "founder_pack", 500 },
            { "pack_hero_week", 600 }, { "pack_speed", 150 }, { "season_pass", 0 },
        };
        public static readonly HashSet<string> OneTime = new() { "starter_pack", "founder_pack", "no_ads" };

        public static string Day(DateTime utc) => utc.ToString("yyyyMMdd");

        public static string Category(string reason)
        {
            if (string.IsNullOrEmpty(reason)) return "other";
            int c = reason.IndexOf(':');
            string head = c >= 0 ? reason.Substring(0, c) : reason;
            return Daily.ContainsKey(head) ? head : "other";
        }

        public static bool IsDiamondPack(string pid) => pid.StartsWith("diamonds_");

        /// <summary>Hafta sonu bonusu oyuncunun saat dilimine göre cumartesi-pazar geçerli; UTC'de cuma-pazartesi kabul edilir (dilim payı).</summary>
        public static bool WeekendWindow(DateTime utc) =>
            utc.DayOfWeek == DayOfWeek.Friday || utc.DayOfWeek == DayOfWeek.Saturday || utc.DayOfWeek == DayOfWeek.Sunday || utc.DayOfWeek == DayOfWeek.Monday;

        public static int MaxGemsFor(EcoState s, string pid, DateTime utc)
        {
            if (!ProductGems.TryGetValue(pid, out int g)) return 0;
            if (IsDiamondPack(pid))
            {
                if (!s.FirstDone) return g * 2;
                if (WeekendWindow(utc)) return g * 3 / 2;
            }
            return g;
        }

        /// <summary>İşlemin sayılacağı gün: son 7 gün içindeyse kendi günü (çevrimdışı oynanan günler), değilse bugün.</summary>
        public static string OpDay(Op op, DateTime utc)
        {
            string today = Day(utc);
            if (string.IsNullOrEmpty(op.D) || op.D.Length != 8) return today;
            if (string.CompareOrdinal(op.D, today) > 0) return today;                       // gelecekteki gün: bugün say
            if (string.CompareOrdinal(op.D, Day(utc.AddDays(-(KeepDays - 1)))) < 0) return today; // çok eski: bugün say
            return op.D;
        }

        static int Get(Dictionary<string, int> m, string k) => m.TryGetValue(k, out int v) ? v : 0;
        static void Add(Dictionary<string, int> m, string k, int n) => m[k] = Get(m, k) + n;

        /// <summary>Kazanç: günlük ve ömür boyu sınırlar içinde kalan kısmı verilir. Verilen miktarı döndürür.</summary>
        public static int AllowEarn(EcoState s, Op op, DateTime utc)
        {
            if (op.N <= 0) return 0;
            string cat = Category(op.R);
            if (op.R.StartsWith("purchase:")) return 0; // satın alma elmasları yalnızca makbuzla gelir
            if (cat == "monthly_card" && s.MonthlyUntil < new DateTimeOffset(utc).ToUnixTimeSeconds()) return 0;
            string dk = OpDay(op, utc) + "|" + cat;
            int allow = Math.Max(0, Daily[cat] - Get(s.Used, dk));
            if (Lifetime.TryGetValue(cat, out int life)) allow = Math.Min(allow, Math.Max(0, life - Get(s.Life, cat)));
            if (PerKey.TryGetValue(cat, out int per))
            {
                string sub = op.R.Length > cat.Length + 1 ? op.R.Substring(cat.Length + 1) : "";
                string ak = (cat == "achievement" ? "ach:" : cat + ":") + sub;
                allow = Math.Min(allow, Math.Max(0, per - Get(s.Life, ak)));
                if (Math.Min(op.N, allow) > 0) Add(s.Life, ak, Math.Min(op.N, allow));
            }
            int give = Math.Min(op.N, allow);
            if (give <= 0) return 0;
            Add(s.Used, dk, give);
            if (Lifetime.ContainsKey(cat)) Add(s.Life, cat, give);
            return give;
        }

        /// <summary>İstemcinin gönderdiği işlemleri sırayla uygular. verdicts: satın alma işlem kimliği → makbuz kararı.</summary>
        public static SyncResult Apply(EcoState s, SyncRequest req, Dictionary<string, Verdict> verdicts, DateTime utc)
        {
            var res = new SyncResult();
            if (!s.Seeded)
            {
                s.Bal = Math.Max(0, Math.Min(req.Base, SeedCap));
                s.Seeded = true;
                if (req.Base > SeedCap) { res.Msg = "seed_capped"; s.Flags++; }
            }
            var seen = new HashSet<string>(s.Ids);
            var txs = new HashSet<string>(s.Tx);
            foreach (var op in req.Ops.Take(MaxOpsPerCall))
            {
                if (string.IsNullOrEmpty(op.Id)) continue;
                if (seen.Contains(op.Id)) { res.Acked.Add(op.Id); continue; } // daha önce işlendi (yanıt kaybolmuş)
                switch (op.K)
                {
                    case "e":
                        int got = AllowEarn(s, op, utc);
                        s.Bal += got; res.Capped += Math.Max(0, op.N - got);
                        break;
                    case "s":
                        if (op.N > 0 && s.Bal >= op.N) s.Bal -= op.N;
                        else if (op.N > 0) { res.RejSpend++; s.Flags++; }
                        break;
                    case "p":
                        verdicts.TryGetValue(op.Id, out var v);
                        if (v != null && v.Why == Verdict.Retry) { res.Msg = "retry"; continue; } // mağaza sunucusuna ulaşılamadı: sonra yeniden denenir
                        if (v == null || !v.Ok) { res.RejBuy++; s.Flags++; break; }
                        string tx = op.Store + ":" + v.TxId;
                        if (txs.Contains(tx)) break;  // aynı makbuz zaten kredilendi
                        if (!v.Verified)
                        {
                            string ud = Day(utc);
                            if (Get(s.Unverified, ud) >= UnverifiedPerDay) { res.RejBuy++; break; }
                            Add(s.Unverified, ud, 1);
                        }
                        txs.Add(tx); s.Tx.Add(tx);
                        if (OneTime.Contains(op.Pid))
                        {
                            if (Get(s.Life, "buy:" + op.Pid) > 0) break; // tek seferlik ürün ikinci kez elmas vermez
                            Add(s.Life, "buy:" + op.Pid, 1);
                        }
                        int gems = Math.Max(0, Math.Min(op.N, MaxGemsFor(s, op.Pid, utc)));
                        res.Capped += Math.Max(0, op.N - gems);
                        s.Bal += gems; res.Bought += gems;
                        if (IsDiamondPack(op.Pid)) s.FirstDone = true;
                        if (op.Pid == "monthly_card")
                        {
                            long now = new DateTimeOffset(utc).ToUnixTimeSeconds();
                            s.MonthlyUntil = Math.Max(s.MonthlyUntil, now) + 30L * 86400;
                        }
                        break;
                    default: continue; // bilinmeyen tür: onaylanmaz
                }
                seen.Add(op.Id); s.Ids.Add(op.Id); res.Acked.Add(op.Id);
            }
            Prune(s, utc);
            s.LastSync = new DateTimeOffset(utc).ToUnixTimeSeconds();
            res.Bal = s.Bal;
            return res;
        }

        static void Prune(EcoState s, DateTime utc)
        {
            if (s.Ids.Count > KeepIds) s.Ids.RemoveRange(0, s.Ids.Count - KeepIds);
            if (s.Tx.Count > KeepTx) s.Tx.RemoveRange(0, s.Tx.Count - KeepTx);
            string oldest = Day(utc.AddDays(-KeepDays));
            foreach (var k in s.Used.Keys.ToList()) if (string.CompareOrdinal(k.Substring(0, 8), oldest) < 0) s.Used.Remove(k);
            foreach (var k in s.Unverified.Keys.ToList()) if (string.CompareOrdinal(k, oldest) < 0) s.Unverified.Remove(k);
        }
    }
}
