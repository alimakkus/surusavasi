using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Bölüm yıldız sandıkları: her hikâye bölümünde (10 seviye, 30 yıldız) 15★ ve 30★ sandığı.
    /// Üst bölümlerde ödül büyür; 30★ sandığı ekipman da verir.</summary>
    public static class ChapterChests
    {
        public static readonly int[] Need = { 15, 30 };

        public static int Stars(SaveData d, int ch) { int s = 0; for (int i = 0; i < 10; i++) s += d.stars[ch * 10 + i]; return s; }
        public static bool Claimed(SaveData d, int ch, int k) => (d.chapterChests[ch] & (1 << k)) != 0;
        public static bool Ready(SaveData d, int ch, int k) => !Claimed(d, ch, k) && Stars(d, ch) >= Need[k];
        public static bool AnyReady(SaveData d) { for (int c = 0; c < 10; c++) for (int k = 0; k < 2; k++) if (Ready(d, c, k)) return true; return false; }

        public static LevelReward Reward(int ch, int k)
        {
            int gems = k == 0 ? 15 + 3 * ch : 40 + 8 * ch;
            int gold = (k == 0 ? 600 : 1500) * (ch + 1);
            return new LevelReward { gems = gems, gold = gold, iron = gold / 3, food = gold / 2 };
        }
        public static int GearRarity(int ch, int k) => k == 0 ? -1 : ch >= 5 ? 2 : 1;

        /// <summary>Sandığı açar; ekipman çıkarsa döndürür (kaynaklar çağıran tarafından cüzdana eklenir).</summary>
        public static GearItem Claim(SaveData d, int ch, int k, Random r)
        {
            d.chapterChests[ch] |= 1 << k;
            int rar = GearRarity(ch, k);
            if (rar < 0) return null;
            var g = new GearItem { id = ++d.gearSeq, slot = r.Next(3), rarity = rar };
            Gear.AddToBag(d, g);
            return g;
        }
    }
}
