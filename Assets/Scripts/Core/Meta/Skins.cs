using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kahraman görünümleri (yalnızca görsel, güç vermez). Klasik ücretsiz; Altın ve Gece elmasla;
    /// Bayram elmasla ya da ligden (ilk 3 veya Altın lige yükselince seçili kahramana) kazanılır.
    /// Buz: 1. sezona özel, satılmaz; yalnızca Sezon Bileti 30. kademede (5 kahramana birden).</summary>
    public static class Skins
    {
        public static readonly string[] Ids = { "default", "gold", "night", "fest", "ice" };
        public static readonly string[] Names = { "Klasik", "Altın", "Gece", "Bayram", "Buz" };
        public const int Ice = 4;
        /// <summary>Sezona özel görünüm: mağazada satılmaz, indirime ve kahraman paketine girmez.</summary>
        public static bool Exclusive(int skin) => skin == Ice;
        public static readonly string[] Descs =
        {
            "Kahramanın ilk görünümü.",
            "Altın zırh ve pelerin, etrafında parıltılar.",
            "Gece mavisi pelerin, parlayan gözler, yıldız tozu.",
            "Kırmızı-beyaz pelerin ve uçuşan konfeti. Ligden de kazanılır.",
            "Buz tacı, dönen kristaller, kar taneleri. 1. sezona özel.",
        };
        public static readonly int[] Price = { 0, 300, 200, 250, 0 };

        static void Ensure(SaveData d)
        {
            if (d.heroSkin == null || d.heroSkin.Length < 5) { var a = new int[5]; d.heroSkin?.CopyTo(a, 0); d.heroSkin = a; }
            if (d.ownedSkins == null) d.ownedSkins = new System.Collections.Generic.List<string>();
        }
        static string Key(int hero, int skin) => Progress.Heroes[hero].id + "_" + Ids[skin];

        public static bool Owned(SaveData d, int hero, int skin) { Ensure(d); return skin == 0 || d.ownedSkins.Contains(Key(hero, skin)); }
        public static int Current(SaveData d, int hero) { Ensure(d); int s = hero >= 0 && hero < d.heroSkin.Length ? d.heroSkin[hero] : 0; return s >= 0 && s < Ids.Length && Owned(d, hero, s) ? s : 0; }
        public static void Equip(SaveData d, int hero, int skin) { if (Owned(d, hero, skin)) d.heroSkin[hero] = skin; }
        public static void Give(SaveData d, int hero, int skin) { Ensure(d); if (!Owned(d, hero, skin)) d.ownedSkins.Add(Key(hero, skin)); }
        // ---------- Haftanın görünümü: her hafta bir kahraman + görünüm %40 indirimli ----------
        public const float DealOff = .4f;
        public static (int hero, int skin) WeeklyDeal()
        {
            int wk = League.WeekKey(DateTime.Now);
            int k = (int)((uint)(wk * 2654435761u) % (uint)(Progress.Heroes.Length * 3));
            return (k / 3, 1 + k % 3);
        }
        public static bool IsDeal(int hero, int skin) { var (h, s) = WeeklyDeal(); return h == hero && s == skin; }
        public static int PriceFor(int hero, int skin) => IsDeal(hero, skin) ? (int)Math.Round(Price[skin] * (1 - DealOff)) : Price[skin];

        /// <summary>Görünüme özel mermi rengi (null: silah rengi). Bayram her mermide başka renk.</summary>
        public static readonly string[] Bullet = { null, "#ffd23f", "#7fe8ff", null, "#c8f4ff" };
        public static readonly string[] IceColors = { "#e8fbff", "#9fe4ff", "#5fb4e6", "#ffffff" };
        public static readonly string[] FestColors = { "#ff6f91", "#ffd23f", "#5ad1ff", "#8be39d", "#ffffff" };

        public static int OwnedCount(SaveData d, int hero) { int c = 0; for (int s = 0; s < Ids.Length; s++) if (Owned(d, hero, s)) c++; return c; }
    }
}
