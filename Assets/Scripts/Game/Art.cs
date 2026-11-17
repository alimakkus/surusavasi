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
        public static Sprite Hero(int heroIndex, int frame) => Pick(Sheet("units", 10, 1, 192, 34f / 96f, 200), heroIndex * 2 + (frame & 1));
        public static Sprite Soldier(int k) => Pick(Sheet("units", 10, 1, 192, 34f / 96f, 200), 6 + (k & 3));
        /// <summary>0 fıçı, 1 çift ateş, 2 hızlı ateş, 3 patlayıcı, 4 tabela.</summary>
        public static Sprite Prop(int i) => Pick(Sheet("props", 5, 1, 256, .5f, 200), i);
        public static Sprite Tree(int map) => Pick(Sheet("trees", 8, 3, 256, 28f / 128f, 200), map);

        // ---------- Üs ----------
        static readonly string[] BldNames = { "barracks", "armory", "wall", "mine", "quarry", "farm", "hospital", "tower", "market" }; // BuildingKind sırası
        static readonly Dictionary<string, Sprite> singles = new Dictionary<string, Sprite>();

        /// <summary>Binanın seviyeye göre görseli (1-20). Pivot: binanın harita konumu + 8 piksel.</summary>
        public static Sprite Building(SuruSavasi.Core.BuildingKind k, int level) =>
            Pick(Sheet("bld_" + BldNames[(int)k], 5, 4, 320, 36f / 160f, 200), Mathf.Clamp(level, 1, 20) - 1);
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
        public static Sprite BaseGround() => Single("base_ground", 360);
        public static Sprite WallRing(int tier) => Single("wall_ring_t" + Mathf.Clamp(tier, 0, 5), 360);
        public static Sprite WallGate(int tier) => Single("wall_gate_t" + Mathf.Clamp(tier, 0, 5), 160, 44f / 160f);

        /// <summary>Açılışta ön yüklenecek tüm görseller.</summary>
        public static List<string> AllNames()
        {
            var l = new List<string> { "bosses", "elites", "units", "props", "trees", "base_misc", "base_ground", "logo" };
            for (int m = 0; m < 20; m++) { l.Add($"enemies_m{m:00}"); l.Add($"bg_m{m:00}"); }
            foreach (var b in BldNames) if (b != "wall") l.Add("bld_" + b);
            for (int t = 0; t < 6; t++) { l.Add("wall_ring_t" + t); l.Add("wall_gate_t" + t); }
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
