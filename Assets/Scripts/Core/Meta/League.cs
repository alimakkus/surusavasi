using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Haftalık lig: 30 kişilik grup (oyuncu + rakipler). Puan modlardan ve kampanyadan gelir.
    /// Hafta pazartesi 00:00'da biter: ilk 5 bir üst lige çıkar, son 5 düşer, sıralamaya göre sandık verilir.
    /// Şimdilik rakipler çevrimdışı botlardır (haftaya ve lige göre sabit, hafta ilerledikçe puan toplarlar);
    /// sunucu gelince aynı arayüz gerçek oyuncularla doldurulur.</summary>
    public static class League
    {
        public static readonly string[] TierNames = { "Bronz", "Gümüş", "Altın", "Elmas", "Usta" };
        public static readonly string[] TierColors = { "#d08a4a", "#cfd8dc", "#ffd23f", "#7fe8ff", "#c07bff" };
        public const int Size = 30, Promote = 5, Demote = 5;
        static readonly int[] TierMean = { 600, 1000, 1500, 2100, 2800 };

        // Puan kaynakları (ekranda da gösterilir)
        public const int PtsLevelFirst = 10, PtsLevelRepeat = 3, PtsSurvWave = 8, PtsTowerBase = 10, PtsHuntMax = 40;

        static readonly string[] Names =
        {
            "KurtAvcı", "Zeynep_K", "Barbaros34", "GeceBekçisi", "Mert.Usta", "AyşeSavaşçı", "Kılıç_Arslan", "Efe1903", "Derya", "Serdar_TR",
            "ZombiKıran", "Yağız", "Elif.88", "Komutan_Ali", "Selin", "KaraŞahin", "Burak07", "Defne", "Tolga_K", "ÇelikYumruk",
            "Emre", "Nisan", "Atlas", "Kaan_35", "Ece", "DemirKale", "Ozan", "Melis", "Batu", "SürüLideri",
            "Cem", "İpek", "Arda_06", "Ceren", "Yiğit", "Su_Perisi", "Onur", "Asya", "Kerem", "BozKurt42",
            "Ilgaz", "Pelin", "TaşDuvar", "Eylül", "Umut_61", "Hira", "Alp", "Nehir", "Doruk", "Gökay"
        };

        public class Entry { public string name; public int score; public bool me; public int tierBadge; }

        public static DateTime WeekStart(DateTime now) => now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
        public static int WeekKey(DateTime now) { var s = WeekStart(now); return s.Year * 1000 + s.DayOfYear; }
        public static float WeekFrac(DateTime now) => (float)Math.Max(0, Math.Min(1, (now - WeekStart(now)).TotalDays / 7));
        public static TimeSpan Left(DateTime now) => WeekStart(now).AddDays(7) - now;

        /// <summary>Grubun sıralı tablosu. frac: haftanın ne kadarı geçti (botların puanı buna göre).</summary>
        public static List<Entry> Table(int week, int tier, int myScore, float frac)
        {
            var r = new Random(week * 7 + tier * 1013 + 5);
            var pool = new List<string>(Names);
            var list = new List<Entry>();
            float mean = TierMean[Math.Max(0, Math.Min(TierMean.Length - 1, tier))];
            for (int i = 0; i < Size - 1; i++)
            {
                int pi = r.Next(pool.Count); string nm = pool[pi]; pool.RemoveAt(pi);
                double fin = mean * (.25 + 1.6 * Math.Pow(r.NextDouble(), 1.25));
                double e = .7 + r.NextDouble() * .7, d0 = r.NextDouble() * .18;
                double cur = frac <= d0 ? 0 : fin * Math.Pow((frac - d0) / (1 - d0), e);
                // Günlük basamak: puanlar gün içinde düzgün değil, oturum oturum artsın
                cur = Math.Floor(cur / 7) * 7;
                list.Add(new Entry { name = nm, score = (int)cur, tierBadge = r.Next(3) });
            }
            list.Add(new Entry { name = "Sen", score = myScore, me = true });
            list.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : (a.me ? -1 : b.me ? 1 : 0));
            return list;
        }

        public static int RankOf(List<Entry> t) { for (int i = 0; i < t.Count; i++) if (t[i].me) return i + 1; return t.Count; }

        /// <summary>Hafta değiştiyse geçen haftanın sonucunu hesaplar (ödül bekletilir) ve puanı sıfırlar.</summary>
        public static void Check(SaveData d)
        {
            var now = DateTime.Now; int wk = WeekKey(now);
            if (d.leagueWeek == wk) return;
            if (d.leagueWeek != 0 && d.leaguePts > 0)
            {
                var t = Table(d.leagueWeek, d.leagueTier, d.leaguePts, 1);
                int rank = RankOf(t);
                int nt = rank <= Promote ? Math.Min(TierNames.Length - 1, d.leagueTier + 1) : rank > Size - Demote ? Math.Max(0, d.leagueTier - 1) : d.leagueTier;
                d.leagueResPending = true; d.leagueResRank = rank; d.leagueResTier = d.leagueTier; d.leagueResNew = nt; d.leagueResPts = d.leaguePts;
                d.leagueTier = nt;
                // Bayram görünümü: ilk 3 ya da Altın (ve üstü) lige yükselme → seçili kahramana
                d.leagueResSkin = -1;
                int hi = Progress.HeroIndex(d.hero);
                if ((rank <= 3 || (nt > d.leagueResTier && nt >= 2)) && !Skins.Owned(d, hi, 3)) { Skins.Give(d, hi, 3); d.leagueResSkin = hi; }
            }
            else if (d.leagueWeek != 0) d.leagueTier = Math.Max(0, d.leagueTier - 1); // hiç oynamayan düşer
            d.leagueWeek = wk; d.leaguePts = 0;
        }

        public static void Add(SaveData d, int pts) { if (pts <= 0) return; Check(d); d.leaguePts += pts; }
        public static bool HasResult(SaveData d) { Check(d); return d.leagueResPending; }

        public static LevelReward RankReward(int rank, int tier)
        {
            int gems = rank == 1 ? 100 : rank <= 3 ? 60 : rank <= 10 ? 30 : rank <= 20 ? 15 : 5;
            gems = (int)Math.Round(gems * (1 + .5f * tier));
            return new LevelReward { gems = gems, gold = gems * 40, iron = gems * 15, food = gems * 20 };
        }
        /// <summary>Lig birincisine ek Epik ekipman.</summary>
        public static bool RankGear(int rank) => rank == 1;
    }
}
