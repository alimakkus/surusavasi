namespace SuruSavasi.Core.Meta
{
    public class ThemeDef
    {
        public string id, name, desc;
        public int price;      // elmas; 0 = bedava
        public int treeMap;    // Art.Tree(map) hücresi
        public string ground => "base_ground_" + id;
    }

    /// <summary>Üs çevresi temaları. Satın alınan temalar SaveData.ownedThemes'te tutulur.</summary>
    public static class Themes
    {
        public static readonly ThemeDef[] All =
        {
            new ThemeDef { id = "green", name = "Yeşil Vadi", desc = "Çiçekli çayırlar ve meşe ağaçları", price = 0, treeMap = 0 },
            new ThemeDef { id = "winter", name = "Kış Kalesi", desc = "Karla kaplı topraklar ve çam ağaçları", price = 300, treeMap = 10 },
            new ThemeDef { id = "desert", name = "Çöl Vahası", desc = "Kum tepeleri ve palmiyeler", price = 300, treeMap = 6 },
            new ThemeDef { id = "autumn", name = "Sonbahar", desc = "Kızıl yapraklar ve altın ışık", price = 500, treeMap = 2 },
            new ThemeDef { id = "lava", name = "Volkan", desc = "Kor çatlakları ve kül rengi toprak", price = 800, treeMap = 13 },
        };

        public static ThemeDef Get(string id) { foreach (var t in All) if (t.id == id) return t; return All[0]; }
        public static ThemeDef Current(SaveData d) => Get(d.theme);
        public static bool Owned(SaveData d, string id) => id == "green" || (d.ownedThemes != null && d.ownedThemes.Contains(id));
    }
}
