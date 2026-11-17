using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    public struct PlannedNote { public int id; public string title, body; public DateTime when; }

    /// <summary>Oyuncu oyundan çıkınca hangi bildirimlerin ne zaman gideceğini hesaplar.
    /// Gece 22:00-09:00 arasına düşen bildirimler sabah 09:00'a kaydırılır (rahatsız etmemek için).</summary>
    public static class NotificationPlan
    {
        public static List<PlannedNote> Make(SaveData d, DateTime now)
        {
            var L = new List<PlannedNote>();
            void Add(int id, string title, string body, DateTime when) { if (when > now.AddSeconds(30)) L.Add(new PlannedNote { id = id, title = title, body = body, when = Quiet(when) }); }

            // İnşaat bitti
            if (d.buildKind >= 0)
            {
                var end = DateTimeOffset.FromUnixTimeMilliseconds(d.buildEnd).LocalDateTime;
                Add(1, "İnşaat tamamlandı!", $"{BaseLogic.Def((BuildingKind)d.buildKind).name} {d.buildFrom + 1}. seviyeye ulaştı. İnşaatçın yeni iş bekliyor.", end);
            }
            // Depolar doldu (en erken dolan kaynak)
            BaseLogic.TickProduction(d);
            double mins = double.MaxValue;
            void Fill(float pend, float rate) { if (rate > 0) mins = Math.Min(mins, (rate * BaseLogic.CapMinutes - pend) / rate); }
            Fill(d.pendGold, BaseLogic.RateGold(d)); Fill(d.pendIron, BaseLogic.RateIron(d)); Fill(d.pendFood, BaseLogic.RateFood(d));
            if (mins < double.MaxValue && mins > 1) Add(2, "Depolar doldu", "Üretim duruyor! Toplamazsan kaynak boşa gider.", now.AddMinutes(mins));
            // Asker eğitimi bitti
            if (d.trainQueue > 0)
                Add(7, "Askerler hazır", $"Kışla'da {d.troops + d.trainQueue} asker savaşa hazır.", DateTimeOffset.FromUnixTimeMilliseconds(BaseLogic.Now).LocalDateTime.AddSeconds(Troops.RemainingSeconds(d, BaseLogic.Now)));
            // Savunma hazır
            if (d.defenseReadyAt > 0) Add(3, "Zombiler kapıda!", "Üs savunması yeniden hazır. Kuleleri kur, ödülü kap.", DateTimeOffset.FromUnixTimeMilliseconds(d.defenseReadyAt).LocalDateTime);
            // Günlük giriş ödülü: yarın sabah 10:00
            Add(4, "Günlük ödülün hazır", "Giriş serini bozma! Bugünün ödülü seni bekliyor.", now.Date.AddDays(1).AddHours(10));
            // Geri dönüş hatırlatmaları
            Add(5, "Sürü yaklaşıyor…", "Askerlerin seni bekliyor komutan. Bir bölüm daha?", now.AddDays(2).Date.AddHours(19));
            Add(6, "Üssün seni özledi", "Kaynakların birikti, zombiler çoğaldı. Geri dön!", now.AddDays(5).Date.AddHours(19));
            return L;
        }

        static DateTime Quiet(DateTime t)
        {
            if (t.Hour >= 22) return t.Date.AddDays(1).AddHours(9);
            if (t.Hour < 9) return t.Date.AddHours(9);
            return t;
        }
    }
}
