using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kahraman görünümleri (yalnızca görsel, güç vermez). Klasik ücretsiz; Altın ve Gece elmasla;
    /// Bayram elmasla ya da ligden (ilk 3 veya Altın lige yükselince seçili kahramana) kazanılır.</summary>
    public static class Skins
    {
        public static readonly string[] Ids = { "default", "gold", "night", "fest" };
        public static readonly string[] Names = { "Klasik", "Altın", "Gece", "Bayram" };
        public static readonly string[] Descs =
        {
            "Kahramanın ilk görünümü.",
            "Altın zırh ve pelerin, etrafında parıltılar.",
            "Gece mavisi pelerin, parlayan gözler, yıldız tozu.",
            "Kırmızı-beyaz pelerin ve uçuşan konfeti. Ligden de kazanılır.",
        };
        public static readonly int[] Price = { 0, 300, 200, 250 };

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
        public static int OwnedCount(SaveData d, int hero) { int c = 0; for (int s = 0; s < Ids.Length; s++) if (Owned(d, hero, s)) c++; return c; }
    }
}
