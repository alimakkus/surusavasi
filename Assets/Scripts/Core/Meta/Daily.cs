using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    public class QuestDef { public string id, text; public int n; }
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

        public const int FreeAdGems = 5, FreeAdsPerDay = 5, MonthlyDailyGems = 50, BonusGems = 5, BonusFood = 100;

        public static int Key(DateTime d) => d.Year * 10000 + d.Month * 100 + d.Day;
        public static int Today => Key(DateTime.Now);
        public static int Yesterday => Key(DateTime.Now.AddDays(-1));

        public static QuestDef Def(string id) { foreach (var q in Quests) if (q.id == id) return q; return Quests[0]; }

        /// <summary>Gün değiştiyse 3 yeni görev seçer (günün tarihine göre, herkes için aynı).</summary>
        public static DailyState Ensure(SaveData d)
        {
            if (d.daily == null) d.daily = new DailyState();
            var D = d.daily; int t = Today;
            if (D.date != t)
            {
                D.date = t; D.bonus = false; D.adsToday = 0;
                var rg = new Battle.Rng(t); var pool = new List<string>(); foreach (var q in Quests) pool.Add(q.id);
                D.quests = new List<QuestState>();
                for (int i = 0; i < 3; i++) { int j = (int)Math.Floor(rg.Next() * pool.Count); D.quests.Add(new QuestState { id = pool[j] }); pool.RemoveAt(j); }
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
