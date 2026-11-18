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
