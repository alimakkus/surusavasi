using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    public class StoryQuest
    {
        public string kind;     // level, bld, hq, stars, hero, weapon, defense, raid, gather
        public BuildingKind bld;
        public int target;
        public string text;
        public LevelReward reward;
    }

    /// <summary>Hikâye görev zinciri: sırayla açılan kalıcı hedefler. Her an oyuncuya "şimdi ne yapmalıyım"ı söyler.
    /// Çoğu görev oyunun durumundan hesaplanır; savunma, gece baskını ve üretim toplama sayaçla izlenir.</summary>
    public static class Quests
    {
        public static readonly List<StoryQuest> Chain = Build();

        static List<StoryQuest> Build()
        {
            var L = new List<StoryQuest>();
            void Q(string kind, int target, string text, BuildingKind b = BuildingKind.Barracks)
            {
                int i = L.Count; float s = 1 + i * .35f + (float)Math.Pow(i, 1.6) * .05f;
                L.Add(new StoryQuest
                {
                    kind = kind, target = target, text = text, bld = b,
                    reward = new LevelReward { gold = (int)(60 * s), iron = (int)(30 * s), food = (int)(40 * s), gems = 3 + i / 6 },
                });
            }
            string N(BuildingKind b) => BaseLogic.Def(b).name;
            // Başlangıç: oyunu tanıtan sıra
            Q("level", 1, "1. bölümü geç");
            Q("bld", 1, $"{N(BuildingKind.Farm)} kur", BuildingKind.Farm);
            Q("gather", 1, "Üste üretim topla");
            Q("level", 3, "3. bölümü geç");
            Q("bld", 1, $"{N(BuildingKind.Mine)} kur", BuildingKind.Mine);
            Q("bld", 1, $"{N(BuildingKind.Barracks)} kur", BuildingKind.Barracks);
            Q("level", 5, "5. bölümü geç");
            Q("bld", 1, $"{N(BuildingKind.Quarry)} kur", BuildingKind.Quarry);
            Q("bld", 1, $"{N(BuildingKind.Armory)} kur", BuildingKind.Armory);
            Q("hq", 2, "Karargâh'ı 2. seviyeye çıkar", BuildingKind.Headquarters);
            Q("defense", 1, "Bir üs savunmasını kazan");
            Q("level", 10, "10. bölümün patronunu yen");
            Q("hero", 2, "Kahramanını 2. seviyeye çıkar");
            Q("bld", 1, $"{N(BuildingKind.Tower)} kur", BuildingKind.Tower);
            Q("bld", 1, $"{N(BuildingKind.Hospital)} kur", BuildingKind.Hospital);
            Q("bld", 1, $"{N(BuildingKind.Market)} kur", BuildingKind.Market);
            Q("raid", 3, "Gece baskınında 3 zombi yen");
            Q("stars", 30, "Toplam 30 yıldız topla");
            // Sonrası: dönen bir düzenle 100. bölüme kadar
            var rot = new[] { BuildingKind.Barracks, BuildingKind.Armory, BuildingKind.Mine, BuildingKind.Wall, BuildingKind.Farm, BuildingKind.Tower, BuildingKind.Quarry, BuildingKind.Hospital, BuildingKind.Market };
            int ri = 0, bl = 2, hq = 3, st = 45, def = 2, raid = 8, hero = 3, wpn = 1;
            for (int lv = 15; lv <= Balance.MaxLevel; lv += 5)
            {
                Q("level", lv, lv % 10 == 0 ? $"{lv}. bölümün patronunu yen" : $"{lv}. bölümü geç");
                var b = rot[ri++ % rot.Length]; Q("bld", bl, $"{N(b)} binasını {bl}. seviyeye çıkar", b);
                if (lv % 10 == 0) { Q("hq", hq, $"Karargâh'ı {hq}. seviyeye çıkar", BuildingKind.Headquarters); hq = Math.Min(Progress.HqMax, hq + 1); }
                if (lv % 10 == 5) { Q("stars", st, $"Toplam {st} yıldız topla"); st += 30; }
                if (lv % 15 == 0) { Q("defense", def, $"{def} üs savunması kazan"); def += 2; }
                if (lv % 20 == 0) { Q("raid", raid, $"Gece baskınında {raid} zombi yen"); raid += 10; }
                if (lv % 10 == 0 && hero < Progress.HeroMax) { Q("hero", hero, $"Kahramanını {hero}. seviyeye çıkar"); hero++; }
                if (lv % 20 == 5 && wpn < Progress.WeaponMax) { Q("weapon", wpn, $"Bir silahı {wpn}. seviyeye çıkar"); wpn++; }
                if (ri % rot.Length == 0) bl = Math.Min(Progress.BuildingMax, bl + 2);
            }
            return L;
        }

        // ---------- Sayaçlar ----------
        public static int Count(SaveData d, string key) { int i = d.questKeys.IndexOf(key); return i < 0 ? 0 : d.questVals[i]; }
        /// <summary>Savunma zaferi, gece baskını zombisi, üretim toplama gibi olayları sayar.</summary>
        public static void Add(SaveData d, string key, int n = 1)
        {
            int i = d.questKeys.IndexOf(key);
            if (i < 0) { d.questKeys.Add(key); d.questVals.Add(n); } else d.questVals[i] += n;
        }

        public static StoryQuest Current(SaveData d) => d.storyIndex < Chain.Count ? Chain[d.storyIndex] : null;

        public static int Value(SaveData d, StoryQuest q)
        {
            switch (q.kind)
            {
                case "level": return d.unlocked - 1;
                case "bld": return Progress.Level(d, q.bld);
                case "hq": return Progress.Level(d, BuildingKind.Headquarters);
                case "stars": return Progress.TotalStars(d);
                case "hero": int m = 0; for (int i = 0; i < 3; i++) m = Math.Max(m, Progress.HeroLevel(d, i)); return m;
                case "weapon": int w = 0; for (int i = 0; i < Progress.Weapons.Length; i++) w = Math.Max(w, Progress.WeaponLevel(d, i)); return w;
                default: return Count(d, q.kind) - BaseOf(d, q.kind);
            }
        }

        // Sayaçlı görevler, görev açıldığında sıfırdan sayılır
        static int BaseOf(SaveData d, string key) { int i = d.storyBase.IndexOf(key); return i < 0 ? 0 : d.storyBaseVals[i]; }
        static void SetBase(SaveData d, string key)
        {
            int i = d.storyBase.IndexOf(key), v = Count(d, key);
            if (i < 0) { d.storyBase.Add(key); d.storyBaseVals.Add(v); } else d.storyBaseVals[i] = v;
        }

        public static bool Done(SaveData d) { var q = Current(d); return q != null && Value(d, q) >= q.target; }

        /// <summary>Ödülü verir ve sıradaki göreve geçer. Ödülü döndürür.</summary>
        public static LevelReward Claim(SaveData d, Action<string, int> grant)
        {
            var q = Current(d); if (q == null || !Done(d)) return default;
            grant("gold", q.reward.gold); grant("iron", q.reward.iron); grant("food", q.reward.food); grant("gems", q.reward.gems);
            d.storyIndex++;
            var nx = Current(d); if (nx != null && IsCounter(nx.kind)) SetBase(d, nx.kind);
            return q.reward;
        }

        static bool IsCounter(string k) => k == "defense" || k == "raid" || k == "gather";
    }
}
