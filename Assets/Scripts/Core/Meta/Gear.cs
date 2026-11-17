using System;
using System.Collections.Generic;
using SuruSavasi.Core.Battle;

namespace SuruSavasi.Core.Meta
{
    [Serializable]
    public class GearItem { public int id, slot, rarity, lvl = 1; }

    /// <summary>Kahraman ekipmanı: Silah (hasar), Zırh (alınan hasar ve can), Muska (yetenek gücü ve bekleme).
    /// Savaş ve savunma zaferlerinden düşer, sandıktan çıkar. Fazlası parçalanır, parçalarla yükseltilir.</summary>
    public static class Gear
    {
        public static readonly string[] SlotNames = { "Silah", "Zırh", "Muska" };
        public static readonly string[] RarityNames = { "Sıradan", "Nadir", "Epik", "Efsanevi" };
        public static readonly string[] RarityColors = { "#c3ccd4", "#5aa8ff", "#c07bff", "#ffb02e" };
        static readonly float[] RarityMul = { 1f, 2f, 3.5f, 6f };
        public const int MaxLevel = 10, BagSize = 24, ChestGems = 60;

        static readonly string[][] Names =
        {
            new[] { "Paslı Bıçak", "Çelik Kılıç", "Gök Mızrağı", "Ejder Dişi" },
            new[] { "Deri Yelek", "Zincir Zırh", "Kale Zırhı", "Titan Kalkanı" },
            new[] { "Kemik Kolye", "Ay Taşı", "Ruh Kristali", "Güneş Mührü" },
        };
        public static string Name(GearItem g) => Names[g.slot][g.rarity];

        // Seviye başına +%10 güç
        static float Pow(GearItem g) => RarityMul[g.rarity] * (1 + .1f * (g.lvl - 1));
        public static float DmgBonus(GearItem g) => g.slot == 0 ? .04f * Pow(g) : 0;          // Silah: hasar
        public static float GuardBonus(GearItem g) => g.slot == 1 ? .02f * Pow(g) : 0;        // Zırh: alınan hasar azalır
        public static float HpBonus(GearItem g) => g.slot == 1 ? .05f * Pow(g) : 0;           // Zırh: kahraman canı
        public static float SkillBonus(GearItem g) => g.slot == 2 ? .06f * Pow(g) : 0;        // Muska: yetenek gücü
        public static float CdBonus(GearItem g) => g.slot == 2 ? .015f * Pow(g) : 0;          // Muska: bekleme kısalır

        public static string Describe(GearItem g)
        {
            string P(float v) => "%" + Math.Round(v * 100);
            return g.slot == 0 ? $"Hasar +{P(DmgBonus(g))}" : g.slot == 1 ? $"Alınan hasar −{P(GuardBonus(g))}, can +{P(HpBonus(g))}" : $"Yetenek +{P(SkillBonus(g))}, bekleme −{P(CdBonus(g))}";
        }

        public static GearItem Find(SaveData d, int id) { foreach (var g in d.gear) if (g.id == id) return g; return null; }
        public static GearItem Equipped(SaveData d, int slot) => slot >= 0 && slot < d.equipped.Length ? Find(d, d.equipped[slot]) : null;

        /// <summary>Kuşanılan ekipmanı savaş çarpanlarına uygular.</summary>
        public static void Apply(SaveData d, BattleMods m)
        {
            if (d.gear == null || d.equipped == null) return;
            for (int s = 0; s < 3; s++)
            {
                var g = Equipped(d, s); if (g == null) continue;
                m.dmgMul *= 1 + DmgBonus(g);
                m.guard *= 1 - Math.Min(.6f, GuardBonus(g));
                m.heroHpMul += HpBonus(g);
                m.skillMul *= 1 + SkillBonus(g);
                m.skillCd *= 1 - Math.Min(.5f, CdBonus(g));
            }
        }

        public static int UpgradeShards(GearItem g) => 4 * g.lvl * (1 + g.rarity);
        public static int UpgradeGold(GearItem g) => (int)Math.Ceiling(150 * Math.Pow(1.6, g.lvl - 1) * (1 + g.rarity * .5));
        public static int DismantleShards(GearItem g) => (1 + g.rarity * 3) * 2 + (g.lvl - 1) * 2;

        /// <summary>Yeni eşya üretir. luck: 0 normal, 1 patron, 2 sandık.</summary>
        public static GearItem Roll(SaveData d, Random r, int luck)
        {
            int lv = Math.Max(1, d.unlocked);
            // Nadirlik ağırlıkları ilerledikçe ve şansla iyileşir
            float epic = .04f + lv * .0012f + luck * .06f, legend = .005f + lv * .0003f + luck * .03f, rare = .25f + lv * .002f + luck * .12f;
            double x = r.NextDouble(); int rar = x < legend ? 3 : x < legend + epic ? 2 : x < legend + epic + rare ? 1 : 0;
            var g = new GearItem { id = ++d.gearSeq, slot = r.Next(3), rarity = rar };
            return g;
        }

        /// <summary>Eşyayı çantaya koyar; çanta doluysa en zayıf kuşanılmamış eşya parçalanır.</summary>
        public static void AddToBag(SaveData d, GearItem g)
        {
            d.gear.Add(g);
            while (d.gear.Count > BagSize)
            {
                GearItem worst = null;
                foreach (var x in d.gear) if (!IsEquipped(d, x) && (worst == null || x.rarity * 100 + x.lvl < worst.rarity * 100 + worst.lvl)) worst = x;
                if (worst == null) break;
                Dismantle(d, worst);
            }
            // Boş yuvaya otomatik kuşan
            if (Equipped(d, g.slot) == null && d.gear.Contains(g)) d.equipped[g.slot] = g.id;
        }

        public static bool IsEquipped(SaveData d, GearItem g) => Array.IndexOf(d.equipped, g.id) >= 0;
        public static void Dismantle(SaveData d, GearItem g) { if (IsEquipped(d, g)) return; d.shards += DismantleShards(g); d.gear.Remove(g); }

        /// <summary>Savaş zaferinde düşme şansı: normal %30, patron bölümünde kesin ve daha iyi.</summary>
        public static GearItem BattleDrop(SaveData d, int level, Random r)
        {
            bool boss = level % 10 == 0;
            if (!boss && r.NextDouble() > .3) return null;
            var g = Roll(d, r, boss ? 1 : 0); AddToBag(d, g); return g;
        }
    }
}
