using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    public class QuestDef { public string id, text; public int n; public bool mode; public Func<SaveData, bool> req; }
    public class EventDef { public string key, name, desc; public float all = 1, gold = 1, iron = 1, gem = 1; }

    /// <summary>Günlük görevler, 7 günlük giriş ödülü, haftanın günlerine bağlı etkinlikler, aylık kart ve bedava elmas reklamı.
    /// Prototipteki kurallarla aynı. Gün değişimi cihazın yerel saatine göre.</summary>
    public static class Daily
    {
        public static readonly QuestDef[] Quests =
        {
            new QuestDef { id = "clear", text = "{0} bölüm geç", n = 3 },
            new QuestDef { id = "kills", text = "{0} düşman öldür", n = 50 },
            new QuestDef { id = "barrels", text = "{0} fıçı topla", n = 12 },
            new QuestDef { id = "power", text = "{0} güçlendirme topla", n = 3 },
            new QuestDef { id = "skill", text = "Yeteneği {0} kez kullan", n = 3 },
            new QuestDef { id = "stars3", text = "{0} bölümü 3 yıldızla bitir", n = 2 },
            new QuestDef { id = "upgrade", text = "{0} bina yükseltmeye başla", n = 1 },
            new QuestDef { id = "gather", text = "Üste {0} kez üretim topla", n = 1 },
            // Oyun tarzı görevleri: her gün 2 tane, oyuncunun açtığı modlardan
            new QuestDef { id = "gates", text = "{0} iyi kapıdan geç", n = 4, mode = true, req = d => d.unlocked >= 3 },
            new QuestDef { id = "boss", text = "{0} patron ya da elit yen", n = 2, mode = true },
            new QuestDef { id = "tower_floor", text = "Kule Tırmanışı'nda {0} kat çık", n = 5, mode = true, req = d => Modes.TowerOpen(d) },
            new QuestDef { id = "hunt_tier", text = "Patron Avı'nda {0} kez Gümüş ya da üstü al", n = 1, mode = true, req = d => Modes.HuntOpen(d) },
            new QuestDef { id = "surv_wave", text = "Hayatta Kalma'da toplam {0} dalga geç", n = 8, mode = true, req = d => Modes.SurvOpen(d) },
            new QuestDef { id = "league", text = "Ligde {0} puan topla", n = 60, mode = true, req = d => d.unlocked >= 6 },
            new QuestDef { id = "turret", text = "Deniz ile {0} kez taret kur", n = 3, mode = true, req = d => HeroShards.Owned(d, 3) },
            new QuestDef { id = "dice6", text = "Cem ile zarda 6 at", n = 1, mode = true, req = d => HeroShards.Owned(d, 4) },
            new QuestDef { id = "talent", text = "Yetenek ağacına {0} puan ver", n = 1, mode = true, req = d => Talents.Free(d, Progress.HeroIndex(d.hero)) > 0 },
        };

        /// <summary>7 günlük giriş ödülü: (altın, demir, yiyecek, elmas).</summary>
        public static readonly int[,] Login = { { 150, 0, 0, 0 }, { 0, 100, 0, 0 }, { 0, 0, 150, 0 }, { 0, 0, 0, 3 }, { 400, 0, 0, 0 }, { 0, 250, 0, 0 }, { 0, 0, 0, 10 } };

        static readonly Dictionary<string, EventDef> Events = new Dictionary<string, EventDef>
        {
            { "wk", new EventDef { key = "wk", name = "Hafta Sonu Şöleni", desc = "Tüm bölüm ödülleri ×1,5", all = 1.5f } },
            { "gold", new EventDef { key = "gold", name = "Altın Hücumu", desc = "Bölüm altını ×2", gold = 2 } },
            { "iron", new EventDef { key = "iron", name = "Demir Fırtınası", desc = "Bölüm demiri ×2", iron = 2 } },
            { "gem", new EventDef { key = "gem", name = "Elmas Cuması", desc = "İlk geçişte elmas ×2", gem = 2 } },
        };
        static readonly string[] EventByDay = { "wk", "gold", "gold", "iron", "iron", "gem", "wk" }; // Pazar=0

        public const int FreeAdGems = 5, FreeAdsPerDay = 5, MonthlyDailyGems = 50, BonusGems = 8, BonusFood = 150;

        public static int Key(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;
        public static int Today => Key(DateTime.Now);
        public static int Yesterday => Key(DateTime.Now.AddDays(-1));

        public static QuestDef Def(string id) { foreach (var q in Quests) if (q.id == id) return q; return Quests[0]; }

        /// <summary>Gün değiştiyse 3 yeni görev seçer (günün tarihine göre, herkes için aynı).</summary>
        public static DailyState Ensure(SaveData d)
        {
            if (d.daily == null) d.daily = new DailyState();
            var D = d.daily; int t = Today;
            if (D.date != t || D.ver < 2)
            {
                if (D.date != t) { D.bonus = false; D.adsToday = 0; }
                D.date = t; D.ver = 2;
                // 2 klasik + 2 oyun tarzı görevi (açılmamış modların görevleri çıkmaz)
                var rg = new Battle.Rng(t); var core = new List<string>(); var modes = new List<string>();
                foreach (var q in Quests) { if (!q.mode) core.Add(q.id); else if (q.req == null || q.req(d)) modes.Add(q.id); }
                D.quests = new List<QuestState>();
                for (int i = 0; i < 4; i++)
                {
                    var pool = i >= 2 && modes.Count > 0 ? modes : core;
                    int j = (int)Math.Floor(rg.Next() * pool.Count); D.quests.Add(new QuestState { id = pool[j] }); pool.RemoveAt(j);
                }
            }
            return D;
        }

        public static void Add(SaveData d, string id, int n = 1)
        {
            var D = Ensure(d);
            foreach (var q in D.quests) if (q.id == id && !q.c) q.p = Math.Min(Def(id).n, q.p + n);
        }

        public static bool CanLogin(SaveData d) => Ensure(d).last != Today;
        /// <summary>Bugün alınacak giriş günü (0-6).</summary>
        public static int LoginIndex(SaveData d) { var D = Ensure(d); return D.last == Yesterday ? D.streak % 7 : 0; }
        public static bool QuestDone(QuestState q) => q.p >= Def(q.id).n;
        public static bool AllClaimed(SaveData d) { foreach (var q in Ensure(d).quests) if (!q.c) return false; return true; }
        public static bool MonthlyActive(SaveData d) => d.monthlyCardUntil > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public static bool CanClaimMonthly(SaveData d) => MonthlyActive(d) && Ensure(d).monthlyClaim != Today;
        public static int MonthlyDaysLeft(SaveData d) => (int)Math.Ceiling(Math.Max(0, d.monthlyCardUntil - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / 86400.0);

        /// <summary>Görev ödülü: 2 elmas + bölüme göre altın.</summary>
        public static (int gold, int gems) QuestReward(SaveData d) => (50 + 3 * Math.Min(100, d.unlocked), 2);

        /// <summary>Alınacak bir şey varsa true (kırmızı nokta için).</summary>
        public static bool Ready(SaveData d)
        {
            var D = Ensure(d);
            if (CanLogin(d) || CanClaimMonthly(d)) return true;
            foreach (var q in D.quests) if (!q.c && QuestDone(q)) return true;
            return AllClaimed(d) && !D.bonus;
        }

        public static EventDef CurrentEvent() => Events[EventByDay[(int)DateTime.Now.DayOfWeek]];

        public static TimeSpan EventLeft()
        {
            var now = DateTime.Now; string k = EventByDay[(int)now.DayOfWeek];
            var end = now.Date.AddDays(1);
            while (EventByDay[(int)end.DayOfWeek] == k) end = end.AddDays(1);
            return end - now;
        }
    }
}
