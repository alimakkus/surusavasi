using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Mağaza teklifleri ve takvimi (ürün fiyatları mağaza panelinde; burada içerik ve kurallar):
    /// - İlk elmas alımı 2 kat
    /// - Hafta sonu (Cumartesi-Pazar) elmas paketlerinde +%50
    /// - Haftalık kahraman paketi: kilitli kahramana 30 parça (hepsi açıksa haftanın görünümü) + 300 elmas, haftada 1
    /// - Hız paketi: 3 hızlandırıcı (her biri inşaatı 1 saat kısaltır) + 150 elmas, günde 1</summary>
    public static class Packs
    {
        public const int HeroPackShards = 30, HeroPackGems = 300, SpeedPackTokens = 3, SpeedPackGems = 150, TokenSeconds = 3600;
        public const float WeekendBonus = .5f;

        public static bool Weekend(DateTime now) => now.DayOfWeek == DayOfWeek.Saturday || now.DayOfWeek == DayOfWeek.Sunday;
        public static bool FirstDouble(SaveData d) => !d.firstPurchaseDone;

        /// <summary>Elmas paketinden verilecek toplam elmas (ilk alım ×2, hafta sonu +%50; ikisi üst üste binmez, büyük olan).</summary>
        public static int DiamondsFor(SaveData d, int baseGems, DateTime now)
        {
            float m = 1;
            if (FirstDouble(d)) m = 2;
            else if (Weekend(now)) m = 1 + WeekendBonus;
            return (int)Math.Round(baseGems * m);
        }

        public static TimeSpan WeekendLeft(DateTime now)
        {
            if (Weekend(now)) { var end = now.Date.AddDays(now.DayOfWeek == DayOfWeek.Saturday ? 2 : 1); return end - now; }
            int days = ((int)DayOfWeek.Saturday - (int)now.DayOfWeek + 7) % 7;
            return now.Date.AddDays(days) - now;   // başlamasına kalan
        }

        // ---------- Haftalık kahraman paketi ----------
        /// <summary>Paketin hedefi: kilitli parça kahramanlarından en çok parçası olan; hepsi açıksa -1 (görünüm verilir).</summary>
        public static int HeroPackTarget(SaveData d)
        {
            int best = -1;
            for (int i = 3; i < Progress.Heroes.Length; i++)
                if (!HeroShards.Owned(d, i) && (best < 0 || d.heroShards[i] > d.heroShards[best])) best = i;
            return best;
        }
        public static bool HeroPackBought(SaveData d) => d.heroPackWeek == League.WeekKey(DateTime.Now);

        public static string HeroPackDesc(SaveData d)
        {
            int t = HeroPackTarget(d);
            if (t >= 0) return $"{HeroPackShards} {Progress.Heroes[t].name} parçası + {HeroPackGems} elmas";
            var (h, s) = Skins.WeeklyDeal();
            return $"{Progress.Heroes[h].name} {Skins.Names[s]} görünümü + {HeroPackGems} elmas";
        }

        /// <summary>İçeriği verir (elmas hariç; elmas ürün tanımından gelir). Dönen yazı ekranda gösterilir.</summary>
        public static string GrantHeroPack(SaveData d)
        {
            d.heroPackWeek = League.WeekKey(DateTime.Now);
            int t = HeroPackTarget(d);
            if (t >= 0) { HeroShards.Give(d, HeroPackShards, t); return $"+{HeroPackShards} {Progress.Heroes[t].name} parçası"; }
            var (h, s) = Skins.WeeklyDeal();
            if (!Skins.Owned(d, h, s)) { Skins.Give(d, h, s); return $"{Progress.Heroes[h].name} {Skins.Names[s]} görünümü"; }
            // Görünüm de varsa: başka bir eksik görünüm
            for (int i = 0; i < Progress.Heroes.Length; i++) for (int k = 1; k < Skins.Ids.Length; k++)
                    if (Progress.HeroOpen(d, i) && !Skins.Owned(d, i, k)) { Skins.Give(d, i, k); return $"{Progress.Heroes[i].name} {Skins.Names[k]} görünümü"; }
            return "";
        }

        // ---------- Hız paketi ----------
        public static bool SpeedPackBought(SaveData d) => d.speedPackDay == Daily.Today;
        public static void GrantSpeedPack(SaveData d) { d.speedPackDay = Daily.Today; d.speedTokens += SpeedPackTokens; }
        public static bool UseToken(SaveData d, BuildingKind k)
        {
            if (d.speedTokens <= 0 || !BaseLogic.Busy(d, k)) return false;
            d.speedTokens--; BaseLogic.Shorten(d, k, TokenSeconds);
            return true;
        }

        /// <summary>Mağaza düğmesinde kırmızı nokta: alınabilir yeni teklif var.</summary>
        public static bool AnyNew(SaveData d) =>
            d.unlocked >= 3 && ((!HeroPackBought(d) && d.offerSeenWeek != League.WeekKey(DateTime.Now)) || (Weekend(DateTime.Now) && d.offerSeenDay != Daily.Today));
        public static void MarkSeen(SaveData d) { d.offerSeenWeek = League.WeekKey(DateTime.Now); d.offerSeenDay = Daily.Today; }
    }
}
