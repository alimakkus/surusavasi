using System;
using SuruSavasi.Core.Battle;

namespace SuruSavasi.Core.Meta
{
    public class HeroDef
    {
        public string id, name, role, skill, skillDesc, perk, lv5;
        public int reqStars;
        public float sk, cd, fire, guard, heal, buffs, g5 = 1f;
    }

    public class WeaponDef
    {
        public string id, name, desc;
        public int reqArmory;
        public float rate, dmg, life = 9f, vy = -520f;
        public int pierce;
        public bool blast;
        public string color;
    }

    /// <summary>Kahraman, silah ve binaların savaşa etkisi. Prototipteki formüllerle aynı.</summary>
    public static class Progress
    {
        public const int HeroMax = 10, WeaponMax = 5, BuildingMax = 20;

        public static readonly HeroDef[] Heroes =
        {
            new HeroDef { id = "ayaz", name = "Ayaz", role = "Boşluk Büyücüsü", reqStars = 0, sk = 1.5f, cd = .85f, fire = 1, guard = 1,
                skill = "Boşluk Dalgası", skillDesc = "Ekrandaki herkese ateş gücünün 1,5 saniyesi kadar hasar verir", perk = "Yetenek %15 daha hızlı dolar", lv5 = "Yetenek ayrıca 4 sn hızlı ateş verir" },
            new HeroDef { id = "kaya", name = "Kaya", role = "Zırhlı Komutan", reqStars = 12, sk = 1, cd = 1, fire = 1, guard = .8f, heal = .15f, g5 = .9f,
                skill = "Siper Çağrısı", skillDesc = "Ekrana hasar verir ve askerlerinin %15'ini geri getirir", perk = "Düşman ve tabela hasarı %20 azalır", lv5 = "Alınan hasar ayrıca %10 azalır" },
            new HeroDef { id = "mira", name = "Mira", role = "Keskin Nişancı", reqStars = 36, sk = .8f, cd = 1, fire = .86f, guard = 1, buffs = 8,
                skill = "Kurşun Fırtınası", skillDesc = "Hafif hasar verir, 8 sn çift ve hızlı ateş açar", perk = "Ateş hızı %14 artar", lv5 = "Çift ve hızlı ateş 12 sn sürer" },
        };

        public static readonly WeaponDef[] Weapons =
        {
            new WeaponDef { id = "pistol", name = "Tabanca", reqArmory = 0, rate = 1, dmg = 1, desc = "Dengeli, her işe yarar", color = "#ffe27a" },
            new WeaponDef { id = "pierce", name = "Delici Tüfek", reqArmory = 1, rate = 1, dmg = .8f, pierce = 1, desc = "Mermi bir düşmanı delip arkasındakine de vurur", color = "#9fd6ff" },
            new WeaponDef { id = "flame", name = "Alev Atar", reqArmory = 3, rate = .55f, dmg = .62f, life = .34f, desc = "Çok hızlı ama kısa menzilli alev", color = "#ff8a3a" },
            new WeaponDef { id = "rocket", name = "Roketatar", reqArmory = 5, rate = 1.7f, dmg = 2.1f, blast = true, vy = -420, desc = "Yavaş ama her mermi patlar", color = "#ffb070" },
        };

        public static int HeroIndex(string id) { for (int i = 0; i < Heroes.Length; i++) if (Heroes[i].id == id) return i; return 0; }
        public static int WeaponIndex(string id) { for (int i = 0; i < Weapons.Length; i++) if (Weapons[i].id == id) return i; return 0; }

        // ---------- Binalar ----------
        public static int Level(SaveData d, BuildingKind k) => d.buildings != null && (int)k < d.buildings.Length ? d.buildings[(int)k] : 0;
        public const int HqMax = 10;
        public static int MaxLevel(BuildingKind k) => k == BuildingKind.Headquarters ? HqMax : BuildingMax;
        /// <summary>Karargâh seviyesi diğer binaların tavanını belirler: Sv. 2 × Karargâh.</summary>
        public static int CapFromHq(int hq) => Math.Min(BuildingMax, 2 * Math.Max(1, hq));
        public static int LevelCap(SaveData d, BuildingKind k) => k == BuildingKind.Headquarters ? HqMax : CapFromHq(Level(d, BuildingKind.Headquarters));
        public static float Eff(SaveData d, BuildingKind k) => BuildingData.EffectiveLevel(Level(d, k));
        /// <summary>5. ve 10. seviyede açılan ek bonus kademesi (0, 1, 2).</summary>
        public static int Milestone(SaveData d, BuildingKind k) { int L = Level(d, k); return L >= 10 ? 2 : L >= 5 ? 1 : 0; }

        public static int TotalStars(SaveData d) { int t = 0; foreach (var s in d.stars) t += s; return t; }

        public static bool HeroOpen(SaveData d, int i) => TotalStars(d) >= Heroes[i].reqStars;
        public static bool WeaponOpen(SaveData d, int i) => Level(d, BuildingKind.Armory) >= Weapons[i].reqArmory;
        public static int HeroLevel(SaveData d, int i) => d.heroLevels != null && i < d.heroLevels.Length ? Math.Max(1, d.heroLevels[i]) : 1;
        public static int WeaponLevel(SaveData d, int i) => d.weaponLevels != null && i < d.weaponLevels.Length ? d.weaponLevels[i] : 0;

        public static int HeroCostGold(int L) => (int)Math.Ceiling(100 * Math.Pow(1.9, L - 1));
        public static int HeroCostGems(int L) => L;
        public static int WeaponCostGold(int L) => (int)Math.Ceiling(90 * Math.Pow(2.2, L));
        public static int WeaponCostIron(int L) => (int)Math.Ceiling(60 * Math.Pow(2.2, L));

        /// <summary>Kayıttan savaş çarpanlarını hesaplar.</summary>
        public static BattleMods BuildMods(SaveData d)
        {
            int hi = HeroIndex(d.hero);
            if (!HeroOpen(d, hi)) hi = 0;
            int wi = WeaponIndex(d.weapon);
            if (!WeaponOpen(d, wi)) wi = 0;
            var H = Heroes[hi]; var W = Weapons[wi];
            int hl = HeroLevel(d, hi);
            float hmul = 1 + .03f * (hl - 1), wmul = 1 + .08f * WeaponLevel(d, wi);
            float armory = 1 + .05f * Eff(d, BuildingKind.Armory);

            var mods = new BattleMods
            {
                heroId = H.id,
                weaponId = W.id,
                weaponColor = W.color,
                skillMul = H.sk * armory * (1 + .05f * Milestone(d, BuildingKind.Armory)) * hmul,
                skillCd = H.cd * (1 - .02f * Eff(d, BuildingKind.Quarry)),
                fireMul = H.fire * W.rate * (1 - .012f * Eff(d, BuildingKind.Tower)),
                guard = H.guard * (hl >= 5 ? H.g5 : 1) * (1 - .04f * Milestone(d, BuildingKind.Wall)) * (1 - .03f * Eff(d, BuildingKind.Wall)),
                dmgMul = armory * W.dmg * wmul * hmul,
                startMul = 1 + .06f * Eff(d, BuildingKind.Barracks),
                startBonus = 2 * Milestone(d, BuildingKind.Barracks),
                heroHpMul = 1 + .04f * Eff(d, BuildingKind.Farm) + .02f * Eff(d, BuildingKind.Hospital),
                regen = .002f * Eff(d, BuildingKind.Hospital),
                heal = H.heal,
                buffs = H.buffs > 0 ? H.buffs + (H.id == "mira" && hl >= 5 ? 4 : 0) : 0,
                rapidOnSkill = H.id == "ayaz" && hl >= 5 ? 4 : 0,
                pierce = W.pierce, bulletLife = W.life, bulletVy = W.vy, blast = W.blast,
                goldMul = 1 + .04f * Eff(d, BuildingKind.Mine),
                marketMul = 1 + .01f * Eff(d, BuildingKind.Market),
            };
            Gear.Apply(d, mods);
            return mods;
        }
    }
}
