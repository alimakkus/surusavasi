using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Prototipin çizimlerinden üretilen sprite sayfalarını (Resources/Art) yükler ve keser.
    /// Sayfa düzeni Tools/artgen ile aynıdır: hücreler soldan sağa, yukarıdan aşağı.</summary>
    public static class Art
    {
        public static readonly string[] EnemyOrder = { "walker", "runner", "spitter", "armored", "splitter", "brute", "wraith", "giant", "mummy", "frost", "ember", "toxic" };
        public static readonly string[] EliteOrder = { "walker", "runner", "spitter", "armored", "splitter", "brute", "wraith", "giant" };

        static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();
        static readonly Dictionary<int, Sprite> bgs = new Dictionary<int, Sprite>();

        /// <summary>Sayfayı hücrelere böler. ppu: 1 oyun pikseli = 0,01 dünya birimi olacak şekilde.</summary>
        static Sprite[] Sheet(string name, int cols, int rows, int cell, float pivotY, float ppu)
        {
            if (sheets.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Art/" + name);
            if (tex == null) { Debug.LogWarning("[Art] Bulunamadı: " + name); sheets[name] = null; return null; }
            tex.filterMode = FilterMode.Bilinear;
            float k = tex.width / (float)(cols * cell); // içe aktarırken küçültüldüyse oranla
            var arr = new Sprite[cols * rows];
            for (int i = 0; i < arr.Length; i++)
            {
                int cx = i % cols, cy = i / cols;
                var r = new Rect(cx * cell * k, tex.height - (cy + 1) * cell * k, cell * k, cell * k);
                arr[i] = Sprite.Create(tex, r, new Vector2(.5f, pivotY), ppu * k, 0, SpriteMeshType.FullRect);
            }
            sheets[name] = arr;
            return arr;
        }

        static Sprite Pick(Sprite[] a, int i) => a != null && i >= 0 && i < a.Length ? a[i] : null;

        public static Sprite Enemy(int map, string typeId, int frame)
        {
            int ti = System.Array.IndexOf(EnemyOrder, typeId); if (ti < 0) ti = 0;
            return Pick(Sheet($"enemies_m{map:00}", 8, 3, 256, 28f / 128f, 200), ti * 2 + (frame & 1));
        }
        public static Sprite Boss(int i, int frame) => Pick(Sheet("bosses", 5, 4, 400, 30f / 200f, 200), i * 2 + (frame & 1));
        public static Sprite Elite(string typeId, int frame)
        {
            int ti = System.Array.IndexOf(EliteOrder, typeId); if (ti < 0) ti = 0;
            return Pick(Sheet("elites", 8, 2, 256, 18f / 128f, 200), ti * 2 + (frame & 1));
        }
        static readonly string[] HeroIds = { "ayaz", "kaya", "mira", "deniz", "cem" };
        /// <summary>Savaştaki kahraman. İlk üçü birim sayfasından, sonradan eklenenler ayrı dosyadan (Art/heroes/unit_<id>_<kare>).</summary>
        public static Sprite Hero(int heroIndex, int frame) => heroIndex < 3
            ? Pick(Sheet("units", 10, 1, 192, 34f / 96f, 200), heroIndex * 2 + (frame & 1))
            : Single($"heroes/unit_{HeroIds[Mathf.Clamp(heroIndex, 0, HeroIds.Length - 1)]}_{frame & 1}", 96, 34f / 96f);
        static readonly Texture2D[] portraits = new Texture2D[10];
        /// <summary>Kahraman ekranı için büyük görsel (Resources/Art/heroes/hero_<id>_<kare>.png; özel AI görseli gelince aynı adla değiştirilir).</summary>
        public static Texture2D HeroPortrait(int heroIndex, int frame)
        {
            var ids = HeroIds;
            int i = Mathf.Clamp(heroIndex, 0, ids.Length - 1) * 2 + (frame & 1);
            if (portraits[i] == null) { portraits[i] = Resources.Load<Texture2D>($"Art/heroes/hero_{ids[i / 2]}_{i % 2}"); if (portraits[i] != null) portraits[i].hideFlags = HideFlags.DontUnloadUnusedAsset; }
            return portraits[i];
        }
        public static Sprite Soldier(int k) => Pick(Sheet("units", 10, 1, 192, 34f / 96f, 200), 6 + (k & 3));
        /// <summary>0 fıçı, 1 çift ateş, 2 hızlı ateş, 3 patlayıcı, 4 tabela.</summary>
        public static Sprite Prop(int i) => Pick(Sheet("props", 5, 1, 256, .5f, 200), i);
        public static Sprite Tree(int map) => Pick(Sheet("trees", 8, 3, 256, 28f / 128f, 200), map);

        // ---------- Üs ----------
        static readonly string[] BldNames = { "barracks", "armory", "wall", "mine", "quarry", "farm", "hospital", "tower", "market", "hq" }; // BuildingKind sırası
        static readonly Dictionary<string, Sprite> singles = new Dictionary<string, Sprite>();

        /// <summary>Binanın seviyeye göre görseli (1-20). Pivot: binanın harita konumu + 8 piksel.</summary>
        public static Sprite Building(SuruSavasi.Core.BuildingKind k, int level)
        {
            int L = Mathf.Clamp(level, 1, 20);
            var custom = CustomBuilding(k, L);
            if (custom != null) return custom;
            if (k == SuruSavasi.Core.BuildingKind.Headquarters) return null;
            return Pick(Sheet("bld_" + BldNames[(int)k], 5, 4, 320, 36f / 160f, 200), L - 1);
        }

        // ---------- Çizerden / yapay zekadan gelen bina görselleri ----------
        // Dosya: Resources/Art/bld/<bina>_t<kademe>.png (Tools/artgen/normalize.py ile hazırlanır).
        // Tuval 512x768, zemin plakasının ortası (256, 600), plaka genişliği 420 px = oyunda 118 piksel.
        const float PlateTexWidth = 420f, PlateGameWidth = 118f;
        static readonly Dictionary<string, Sprite> custom = new Dictionary<string, Sprite>();

        public static int Tier(int level) => (Mathf.Clamp(level, 1, 20) + 3) / 4;

        /// <summary>Karargâh 10 seviyeli: her 2 seviyede bir yeni görsel.</summary>
        public static int TierOf(SuruSavasi.Core.BuildingKind k, int level) =>
            k == SuruSavasi.Core.BuildingKind.Headquarters ? (Mathf.Clamp(level, 1, 10) + 1) / 2 : Tier(level);

        static Sprite CustomBuilding(SuruSavasi.Core.BuildingKind k, int level)
        {
            string key = $"bld/{BldNames[(int)k]}_t{TierOf(k, level)}";
            if (custom.TryGetValue(key, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Art/" + key);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Bilinear;
                float k2 = tex.width / 512f; // içe aktarırken küçültüldüyse oranla
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, 168f / 768f),
                    100f * PlateTexWidth / PlateGameWidth * k2, 0, SpriteMeshType.FullRect);
            }
            custom[key] = s;
            return s;
        }

        public static bool HasCustomBuilding(SuruSavasi.Core.BuildingKind k, int level) => CustomBuilding(k, level) != null;

        /// <summary>Aynı kademe içinde seviye arttıkça bina hafifçe büyür (her seviye görünür bir fark).</summary>
        public static float LevelScale(int level) => 1f + .03f * ((Mathf.Clamp(level, 1, 20) - 1) % 4);
        public static float LevelScale(SuruSavasi.Core.BuildingKind k, int level) =>
            k == SuruSavasi.Core.BuildingKind.Headquarters ? 1f + .04f * ((Mathf.Clamp(level, 1, 10) - 1) % 2) : LevelScale(level);
        /// <summary>0 temel, 1 temel (inşaat), 2 işçi, 3 yük sandığı, 4 çekiç, 5 görev panosu, 6 hazine sandığı.</summary>
        public static Sprite BaseMisc(int i) => Pick(Sheet("base_misc", 7, 1, 320, 44f / 160f, 200), i);

        /// <summary>Tek parça görsel. logicalWidth: oyun pikseli cinsinden genişliği.</summary>
        public static Sprite Single(string name, float logicalWidth, float pivotY = .5f)
        {
            if (singles.TryGetValue(name, out var s)) return s;
            var tex = Resources.Load<Texture2D>("Art/" + name);
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, pivotY), 100f * tex.width / logicalWidth, 0, SpriteMeshType.FullRect) : null;
            if (tex != null) tex.filterMode = FilterMode.Bilinear;
            singles[name] = s;
            return s;
        }
        /// <summary>Yuvarlak üs zemini: 800x1200 oyun pikseli, ortası köy merkezi.</summary>
        public static Sprite ThemeGround(string themeId) => Single("base_ground_" + themeId, 800);
        /// <summary>Sur parçası (30x40) ve kule (46x76), alt ortadan.</summary>
        public static Sprite WallSeg(int tier) => Single("wall_seg_t" + Mathf.Clamp(tier, 0, 5), 30, 2f / 40f);
        public static Sprite WallTower(int tier) => Single("wall_tower_t" + Mathf.Clamp(tier, 0, 5), 46, 2f / 76f);
        public static Sprite WallGate(int tier) => Single("wall_gate_t" + Mathf.Clamp(tier, 0, 5), 160, 44f / 160f);

        /// <summary>Açılışta ön yüklenecek tüm görseller.</summary>
        public static List<string> AllNames()
        {
            var l = new List<string> { "bosses", "elites", "units", "props", "trees", "base_misc", "logo" };
            for (int m = 0; m < 20; m++) { l.Add($"enemies_m{m:00}"); l.Add($"bg_m{m:00}"); }
            foreach (var b in BldNames) if (b != "wall") { if (b != "hq") l.Add("bld_" + b); for (int t = 1; t <= 5; t++) l.Add($"bld/{b}_t{t}"); }
            for (int t = 0; t < 6; t++) { l.Add("wall_seg_t" + t); l.Add("wall_tower_t" + t); l.Add("wall_gate_t" + t); }
            l.Add("base_ground_green");
            return l;
        }

        public static Sprite Background(int map)
        {
            if (bgs.TryGetValue(map, out var s)) return s;
            var tex = Resources.Load<Texture2D>($"Art/bg_m{map:00}");
            s = tex != null ? Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100f * tex.width / 360f, 0, SpriteMeshType.FullRect) : null;
            bgs[map] = s;
            return s;
        }
    }
}
