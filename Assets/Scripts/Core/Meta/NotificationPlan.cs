using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Bildirim türleri (ayarlardan ayrı ayrı kapatılabilir).</summary>
    public enum NoteCat { Base, Event, Alliance, Return }

    public struct PlannedNote { public int id, pri; public NoteCat cat; public string title, body; public DateTime when; }

    /// <summary>Oyuncu oyundan çıkınca hangi bildirimlerin ne zaman gideceğini hesaplar.
    /// Kurallar: gece 22:00-09:00 arası sessiz (sabaha kayar); bir günde en çok 2 bildirim; aynı gündeki iki bildirim
    /// arasında en az 2 saat; çakışmada önemli olan kalır (inşaat > etkinlik/ittifak > hatırlatma). En çok 7 gün ileri.</summary>
    public static class NotificationPlan
    {
        public const int PerDay = 2, MaxTotal = 12;
        public static readonly string[] CatNames = { "İnşaat ve üretim", "Sezon ve etkinlikler", "İttifak", "Hatırlatmalar" };

        /// <summary>Bildirime dokununca açılacak ekran (GameFlow.Route çözer).</summary>
        public static string TargetOf(int id)
        {
            switch (id)
            {
                case 10: case 11: case 13: return "season";
                case 12: return "season_tasks";
                case 20: case 21: return "modes";
                case 31: case 32: case 34: return "alliance:2";
                case 33: return "alliance:1";
                case 40: return "daily";
                default: return "base";
            }
        }

        /// <summary>Bildirimin taşıdığı veri: "hedef|kimlik|planlandığı an (Unix sn)|tür". Dokunulunca ölçüm ve yönlendirme için.</summary>
        public static string DataOf(PlannedNote n, DateTime now) => $"{TargetOf(n.id)}|{n.id}|{new DateTimeOffset(now).ToUnixTimeSeconds()}|{(int)n.cat}";

        public static bool TryParse(string data, out string target, out int id, out long plannedAt, out int cat)
        {
            target = null; id = 0; plannedAt = 0; cat = 0;
            if (string.IsNullOrEmpty(data)) return false;
            var p = data.Split('|');
            if (p.Length < 4 || !int.TryParse(p[1], out id) || !long.TryParse(p[2], out plannedAt) || !int.TryParse(p[3], out cat)) return false;
            target = p[0]; return true;
        }

        public static List<PlannedNote> Make(SaveData d, DateTime now, Func<NoteCat, bool> enabled = null)
        {
            var all = new List<PlannedNote>();
            void Add(int id, NoteCat cat, int pri, string title, string body, DateTime when)
            {
                if (enabled != null && !enabled(cat)) return;
                when = Quiet(when);
                if (when <= now.AddSeconds(30) || when > now.AddDays(7)) return;
                all.Add(new PlannedNote { id = id, cat = cat, pri = pri, title = title, body = body, when = when });
            }
            DateTime Local(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
            int v = now.DayOfYear; // metinleri günden güne değiştirmek için

            // ---------- Üs: inşaat, depo, asker, savunma ----------
            for (int s = 0; s < BaseLogic.MaxBuilders; s++)
            {
                int kind = BaseLogic.SlotKind(d, s); if (kind < 0) continue;
                Add(1 + s, NoteCat.Base, 100, "İnşaat tamamlandı!", $"{BaseLogic.Def((BuildingKind)kind).name} {BaseLogic.SlotFrom(d, s) + 1}. seviyeye ulaştı. İnşaatçın yeni iş bekliyor.", Local(BaseLogic.SlotEnd(d, s)));
            }
            BaseLogic.TickProduction(d);
            double mins = double.MaxValue;
            void Fill(float pend, float rate) { if (rate > 0) mins = Math.Min(mins, (rate * BaseLogic.CapMinutes - pend) / rate); }
            Fill(d.pendGold, BaseLogic.RateGold(d)); Fill(d.pendIron, BaseLogic.RateIron(d)); Fill(d.pendFood, BaseLogic.RateFood(d));
            if (mins < double.MaxValue && mins > 1) Add(3, NoteCat.Base, 60, "Depolar doldu", "Üretim duruyor! Toplamazsan kaynak boşa gider.", now.AddMinutes(mins));
            if (d.trainQueue > 0)
                Add(4, NoteCat.Base, 50, "Askerler hazır", $"Kışla'da {d.troops + d.trainQueue} asker savaşa hazır.", Local(BaseLogic.Now).AddSeconds(Troops.RemainingSeconds(d, BaseLogic.Now)));
            if (d.defenseReadyAt > 0) Add(5, NoteCat.Base, 55, "Zombiler kapıda!", "Üs savunması yeniden hazır. Kuleleri kur, ödülü kap.", Local(d.defenseReadyAt));

            // ---------- Sezon ----------
            if (Season.Open(d))
            {
                int sid = Season.Current; var end = Season.End(sid).ToLocalTime();
                int tier = Season.Tier(d), missing = Season.Tiers - tier;
                if (missing > 0)
                    Add(10, NoteCat.Event, 85, $"Sezonun bitmesine 2 gün kaldı", $"{Season.Name(sid)}: {tier}. kademedesin, {missing} kademe eksik. Ödülleri kaçırma!", end.AddHours(-48).Date.AddHours(19));
                Add(11, NoteCat.Event, 75, $"Yeni sezon başladı: {Season.Name(sid + 1)}", "Yeni ödüller, yeni sezon görevleri. Kademe 1'den başla!", end.ToLocalTime().Date.AddHours(12));
                var wk = Season.Start(sid).AddDays((Season.Week(DateTime.UtcNow) + 1) * 7).ToLocalTime();
                if (wk < end) Add(12, NoteCat.Event, 40, "Yeni sezon görevleri", $"Bu haftanın 3 görevi seni bekliyor: her biri +{Season.WeeklyPts} SP.", wk.Date.AddHours(12));
                int cl = Season.Claimable(d);
                if (cl > 0) Add(13, NoteCat.Event, 45, "Alınmamış sezon ödüllerin var", $"{cl} ödül seni bekliyor. Sezon Bileti ekranından al!", now.AddHours(3));
            }

            // ---------- Kar Fırtınası ----------
            if (SnowEvent.Unlocked(d))
            {
                if (!SnowEvent.Active(now))
                {
                    var sat = now.Date.AddDays(1); while (sat.DayOfWeek != DayOfWeek.Saturday) sat = sat.AddDays(1);
                    Add(20, NoteCat.Event, 80, "Kar Fırtınası başladı!", $"Hafta sonu etkinliği: düşmanlar yavaş ama dayanıklı. Her zafer +{SnowEvent.WinSP} SP ve elmas!", sat.AddHours(11));
                }
                else
                {
                    var last = now.DayOfWeek == DayOfWeek.Saturday ? now.Date.AddDays(1) : now.Date;
                    if (SnowEvent.TriesLeft(d, now) > 0 || last > now.Date)
                        Add(21, NoteCat.Event, 70, "Fırtına yakında diniyor", "Kar Fırtınası bu gece bitiyor. Haklarını kullan, sezon puanını kap!", last.AddHours(17));
                }
            }

            // ---------- İttifak ----------
            if (Alliance.Joined(d))
            {
                if (d.allyReqKind >= 0 && d.allyReqGot < d.allyReqMax && BaseLogic.Busy(d, (BuildingKind)d.allyReqKind))
                    Add(30, NoteCat.Alliance, 65, "İttifaktan yardım geldi", $"Üyeler {d.allyReqMax} kez yardım etti, inşaatın kısaldı. Teşekkür etmeyi unutma!", Local(d.allyReqAt).AddSeconds(d.allyReqMax * 25 + 5));
                // Patron: bir sonraki alınmamış sandığa ne zaman ulaşılır (botların hızına göre)
                float mine = Alliance.MinePct(d);
                int ready = 0; for (int i = 0; i < Alliance.BossMilestones.Length; i++) if (Alliance.MilestoneReady(d, i)) ready++;
                if (ready > 0) Add(34, NoteCat.Alliance, 62, "Patron sandığın hazır", ready > 1 ? $"İttifak patronunda {ready} sandık seni bekliyor." : "İttifak patronunda bir sandık seni bekliyor.", now.AddHours(2));
                else for (int i = 0; i < Alliance.BossMilestones.Length - 1; i++)
                {
                    if (Alliance.MilestoneClaimed(d, i)) continue;
                    var at = BossReach(d, now, Alliance.BossMilestones[i] - mine);
                    if (at.HasValue)
                    {
                        Add(31, NoteCat.Alliance, 70, $"İttifak patronu %{Alliance.BossMilestones[i]}'te!", $"%{Alliance.BossMilestones[i]} sandığı açıldı. Gel al, sonra patrona sen de vur!", at.Value);
                        break;
                    }
                }
                var weekEnd = League.WeekStart(now).AddDays(7);
                if (Alliance.TotalPct(d) < 100)
                    Add(32, NoteCat.Alliance, 72, "Patron devrilmek üzere", "Hafta bitmeden ittifak patronuna son darbeyi sen vur!", weekEnd.AddDays(-1).AddHours(18));
                Add(33, NoteCat.Alliance, 30, v % 2 == 0 ? "İttifakın bağışını bekliyor" : "Bugünkü bağışını yaptın mı?", "Günde 5 bağış: katkı puanı topla, ittifakın seviye atlasın.", now.Date.AddDays(1).AddHours(19).AddMinutes(30));
            }

            // ---------- Hatırlatmalar ----------
            string[] t1 = { "Günlük ödülün hazır", "Giriş ödülün seni bekliyor", "Serini bozma!" };
            Add(40, NoteCat.Return, 35, t1[v % t1.Length], "Bugünün ödülünü al, günlük görevlere bak.", now.Date.AddDays(1).AddHours(10));
            Add(41, NoteCat.Return, 20, "Sürü yaklaşıyor…", "Askerlerin seni bekliyor komutan. Bir bölüm daha?", now.AddDays(2).Date.AddHours(19));
            Add(42, NoteCat.Return, 20, "Üssün seni özledi", "Kaynakların birikti, zombiler çoğaldı. Geri dön!", now.AddDays(5).Date.AddHours(19));

            return Pick(all);
        }

        /// <summary>Önceliğe göre seçer: günde en çok PerDay, aynı günde en az 2 saat arayla (çakışırsa 2 saat sonraya, gece olmayacaksa, kayar).</summary>
        public static List<PlannedNote> Pick(List<PlannedNote> all)
        {
            all.Sort((a, b) => b.pri != a.pri ? b.pri.CompareTo(a.pri) : a.when.CompareTo(b.when));
            var outp = new List<PlannedNote>();
            foreach (var n0 in all)
            {
                var n = n0;
                for (int tries = 0; tries < 4; tries++)
                {
                    int same = 0; bool close = false;
                    foreach (var o in outp)
                    {
                        if (o.when.Date == n.when.Date) same++;
                        if (Math.Abs((o.when - n.when).TotalMinutes) < 120) close = true;
                    }
                    if (same >= PerDay) { n.when = DateTime.MinValue; break; }
                    if (!close) break;
                    var moved = n.when.AddHours(2);
                    if (moved.Hour >= 22 || moved.Date != n.when.Date) { n.when = DateTime.MinValue; break; }
                    n.when = moved;
                }
                if (n.when == DateTime.MinValue) continue;
                bool ok = true; foreach (var o in outp) if (Math.Abs((o.when - n.when).TotalMinutes) < 120) ok = false;
                if (!ok) continue;
                outp.Add(n);
                if (outp.Count >= MaxTotal) break;
            }
            outp.Sort((a, b) => a.when.CompareTo(b.when));
            return outp;
        }

        /// <summary>Botların patron payı hedefe (yüzde) ne zaman ulaşır? Bu hafta içinde değilse null.</summary>
        static DateTime? BossReach(SaveData d, DateTime now, float targetBots)
        {
            var clock = Alliance.Clock;
            try
            {
                if (Alliance.BotsPct(d) >= targetBots) return null; // zaten ulaşıldı (oyuncu açınca görür)
                var end = League.WeekStart(now).AddDays(7);
                for (var t = now.AddHours(1); t < end; t = t.AddHours(1))
                {
                    var tt = t; Alliance.Clock = () => tt;
                    if (Alliance.BotsPct(d) >= targetBots) return t;
                }
                return null;
            }
            finally { Alliance.Clock = clock; }
        }

        static DateTime Quiet(DateTime t)
        {
            if (t.Hour >= 22) return t.Date.AddDays(1).AddHours(9);
            if (t.Hour < 9) return t.Date.AddHours(9);
            return t;
        }
    }
}
