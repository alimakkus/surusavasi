using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kurucu Paketi: 5. bölüm geçilince açılan, 72 saat süren tek seferlik teklif.
    /// İçerik: ikinci inşaatçı (zaten varsa yerine elmas), elmas, kesin Epik ekipman ve altın.</summary>
    public static class Offers
    {
        public const int FounderUnlockLevel = 6;            // 5. bölüm geçilmiş olmalı
        public const long FounderDurationMs = 72L * 3600 * 1000;
        public const int FounderGems = 500, FounderBuilderGems = 350;
        public static int FounderGold(SaveData d) => 3000 + 300 * Math.Max(1, d.unlocked);

        /// <summary>Şartlar sağlandıysa teklifin süresini başlatır. Yeni başladıysa true döner.</summary>
        public static bool TryStartFounder(SaveData d, long nowMs)
        {
            if (d.founderBought || d.founderOfferStart > 0 || d.unlocked < FounderUnlockLevel) return false;
            d.founderOfferStart = nowMs; return true;
        }
        public static bool FounderActive(SaveData d, long nowMs) => !d.founderBought && d.founderOfferStart > 0 && nowMs < d.founderOfferStart + FounderDurationMs;
        public static float FounderSecondsLeft(SaveData d, long nowMs) => Math.Max(0, (d.founderOfferStart + FounderDurationMs - nowMs) / 1000f);

        // ---------- Günlük reklam hakları ----------
        public const int AdBoostPerDay = 3, AdResPerDay = 3, AdPiggyPerDay = 1;
        static void DayCheck(SaveData d) { string t = DateTime.Now.ToString("yyyyMMdd"); if (d.adDay != t) { d.adDay = t; d.adBoost = d.adRes = d.adPiggy = 0; } }
        public static int AdLeft(SaveData d, string kind)
        {
            DayCheck(d);
            return kind == "boost" ? AdBoostPerDay - d.adBoost : kind == "res" ? AdResPerDay - d.adRes : AdPiggyPerDay - d.adPiggy;
        }
        public static void AdUsed(SaveData d, string kind) { DayCheck(d); if (kind == "boost") d.adBoost++; else if (kind == "res") d.adRes++; else d.adPiggy++; }

        // ---------- Güç takviyesi: aynı bölümde 2 kez kaybedince önerilir ----------
        public const int BoostOfferFails = 2, BoostGems = 40;
        public const float AdBoostDmg = .3f, GemBoostDmg = .6f;
        public const int AdBoostSoldiers = 10, GemBoostSoldiers = 20, GemBoostBattles = 3;
        public static bool BoostActive(SaveData d) => d.boostBattles > 0;
        public static void GiveBoost(SaveData d, float dmg, int soldiers, int battles) { d.boostDmg = dmg; d.boostSoldiers = soldiers; d.boostBattles = battles; }
        /// <summary>Savaş başında etkin takviyeyi uygular ve bir savaş düşer. Uygulandıysa true.</summary>
        public static bool ApplyBoost(SaveData d, Battle.BattleMods m)
        {
            if (d.boostBattles <= 0) return false;
            m.dmgMul *= 1 + d.boostDmg; m.startBonus += d.boostSoldiers; d.boostBattles--;
            return true;
        }

        // ---------- Kumbara: zaferlerle elmas birikir, satın alınca ya da reklamla açılır ----------
        public const int PiggyCap = 600, PiggyMinOpen = 150, PiggyAdTake = 20;
        public static void PiggyAdd(SaveData d, bool firstClear) => d.piggy = Math.Min(PiggyCap, d.piggy + (firstClear ? 4 : 1));
        public static int PiggyTake(SaveData d, int max) { int n = Math.Min(max, d.piggy); d.piggy -= n; return n; }

        // ---------- Kaynak yardımı: yükseltme için eksik kaynağın bir kısmı reklamla ----------
        public const float ResHelpShare = .35f;
        /// <summary>Eksik kaynakları, maliyetin %35'ine kadar verir. Verilenleri döndürür (altın, demir, yiyecek).</summary>
        public static (int g, int i, int f) ResourceHelp(SaveData d, BuildingKind k)
        {
            int L = Progress.Level(d, k);
            int cg = BaseLogic.CostGold(k, L), ci = BaseLogic.CostIron(k, L), cf = BaseLogic.CostFood(k, L);
            int G(int cost, int have) => Math.Min(Math.Max(0, cost - have), (int)Math.Ceiling(cost * ResHelpShare));
            return (G(cg, d.gold), G(ci, d.iron), G(cf, d.food));
        }

        /// <summary>Satın alma sonrası içeriği verir. Elmaslar ayrıca ürün tanımından (500) gelir.</summary>
        public static GearItem GrantFounder(SaveData d, Random r, Action<string, int> grant)
        {
            d.founderBought = true;
            if (d.builders < 2) d.builders = 2; else grant("gems", FounderBuilderGems);
            grant("gold", FounderGold(d));
            var g = new GearItem { id = ++d.gearSeq, slot = r.Next(3), rarity = 2 };
            Gear.AddToBag(d, g);
            return g;
        }
    }
}
