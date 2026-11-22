using System.Collections.Generic;

namespace SuruSavasi.Core.Battle
{
    /// <summary>Savaş alanı sabitleri. Koordinatlar prototipteki gibi 360x560 piksel, y aşağı doğru artar.</summary>
    public static class Field
    {
        public const float W = 360f, H = 560f, SQY = 440f, GAP = 150f, SPEED = 90f, SKILL_CD = 8f, BARV = 0.4f;
        public static readonly float[] Lanes = { 66f, 180f, 294f };
    }

    public enum Power { None, Double, Rapid, Blast }

    public class EnemyType
    {
        public string id, nameTr, nameEn;
        public int unlock;
        public float mul, spd, dmg, sc, hw;
        public float shootInt, shootSpd;   // shootInt = 0 ise ateş etmez
        public string shootColor, skin;    // skin null ise haritanın düşman rengi
        public bool big, ghost, split;
        public int[] maps;                 // sadece bu haritalarda çıkar (null = her yerde)
    }

    public class MapInfo
    {
        public string nameTr;
        public string ground, ground2, road, road2, enemySkin, enemyShirt, eye;
        public MapInfo(string n, string g0, string g1, string r0, string r1, string es, string esh, string ee)
        { nameTr = n; ground = g0; ground2 = g1; road = r0; road2 = r1; enemySkin = es; enemyShirt = esh; eye = ee; }
    }

    public static class BattleData
    {
        public static readonly Dictionary<string, EnemyType> Types = new Dictionary<string, EnemyType>
        {
            { "walker",   new EnemyType { id = "walker",   nameTr = "Zombi",        nameEn = "Zombie",       unlock = 1,  mul = 1f,   spd = 16, dmg = .10f, sc = 1.05f, hw = 28 } },
            { "runner",   new EnemyType { id = "runner",   nameTr = "Koşucu",       nameEn = "Runner",       unlock = 4,  mul = .55f, spd = 52, dmg = .07f, sc = .95f,  hw = 22, skin = "#d1a56a" } },
            { "spitter",  new EnemyType { id = "spitter",  nameTr = "Tükürükçü",    nameEn = "Spitter",      unlock = 3,  mul = .8f,  spd = 9,  dmg = .08f, sc = 1.05f, hw = 24, shootInt = 2f,   shootSpd = 150, shootColor = "#b6ff5a", skin = "#9bd05a" } },
            { "armored",  new EnemyType { id = "armored",  nameTr = "Zırhlı Zombi", nameEn = "Armored",      unlock = 10, mul = 1.6f, spd = 14, dmg = .16f, sc = 1.15f, hw = 24, skin = "#93a58a" } },
            { "splitter", new EnemyType { id = "splitter", nameTr = "Bölünen",      nameEn = "Splitter",     unlock = 20, mul = 1.2f, spd = 20, dmg = .12f, sc = 1.2f,  hw = 24, split = true, skin = "#b57fcf" } },
            { "brute",    new EnemyType { id = "brute",    nameTr = "Canavar",      nameEn = "Brute",        unlock = 30, mul = 1.6f, spd = 10, dmg = .30f, sc = 2.2f,  hw = 30, big = true, shootInt = 3.6f, shootSpd = 120, shootColor = "#c98d49", skin = "#7a9a5a" } },
            { "wraith",   new EnemyType { id = "wraith",   nameTr = "Hayalet",      nameEn = "Wraith",       unlock = 45, mul = 1.4f, spd = 26, dmg = .14f, sc = 1.2f,  hw = 24, ghost = true, shootInt = 3.2f, shootSpd = 170, shootColor = "#8fe6ff", skin = "#9fd6e6" } },
            { "giant",    new EnemyType { id = "giant",    nameTr = "Dev",          nameEn = "Giant",        unlock = 60, mul = 2f,   spd = 8,  dmg = .45f, sc = 2.9f,  hw = 36, big = true, shootInt = 4f,   shootSpd = 110, shootColor = "#d8d0b0", skin = "#8a8f6a" } },
            { "mummy",    new EnemyType { id = "mummy",    nameTr = "Mumya",        nameEn = "Mummy",        unlock = 1,  mul = 1.5f, spd = 12, dmg = .15f, sc = 1.12f, hw = 24, maps = new[] { 5, 6, 7 }, skin = "#d8c9a0" } },
            { "frost",    new EnemyType { id = "frost",    nameTr = "Buz Zombisi",  nameEn = "Frost Zombie", unlock = 1,  mul = 1.1f, spd = 22, dmg = .11f, sc = 1.1f,  hw = 24, maps = new[] { 9, 10, 11 }, shootInt = 3f, shootSpd = 140, shootColor = "#bfeaff", skin = "#a9dcf0" } },
            { "ember",    new EnemyType { id = "ember",    nameTr = "Alev Zombisi", nameEn = "Ember Zombie", unlock = 1,  mul = 1.2f, spd = 20, dmg = .13f, sc = 1.1f,  hw = 24, maps = new[] { 12, 13 }, shootInt = 2.6f, shootSpd = 160, shootColor = "#ff8a3a", skin = "#5a3a30" } },
            { "toxic",    new EnemyType { id = "toxic",    nameTr = "Zehirli Zombi",nameEn = "Toxic Zombie", unlock = 1,  mul = 1.3f, spd = 16, dmg = .12f, sc = 1.12f, hw = 24, maps = new[] { 16, 17 }, shootInt = 2.4f, shootSpd = 150, shootColor = "#9bff4a", skin = "#8fd04a" } },
        };

        /// <summary>Prototipteki sıra: bölüm üretici bu sırayla seçer (aynı dizilim için önemli).</summary>
        public static readonly string[] Order = { "walker", "runner", "spitter", "armored", "splitter", "brute", "wraith", "giant" };
        /// <summary>Haritaya özel türler, prototipteki Object.keys sırasıyla.</summary>
        public static readonly string[] Themed = { "mummy", "frost", "ember", "toxic" };

        public static readonly string[] BossNames =
        {
            "Şapkalı Zombi", "Zırhlı Dev", "Zehir Kraliçesi", "Buz Golemi", "Mezar Bekçisi",
            "Alev Canavarı", "Karanlık Şövalye", "Çürük Dev", "Gölge Avcı", "Boşluk Lordu"
        };
        public static readonly string[] BossColors =
        {
            "#8dbd80", "#93a58a", "#a46fd0", "#9ad3e8", "#d8d4c0", "#d8663a", "#5b6270", "#7a8a4a", "#3d3a55", "#6a3fb0"
        };

        public static readonly MapInfo[] Maps =
        {
            new MapInfo("Çayır", "#3f7a3a", "#4f9444", "#7a8576", "#98a392", "#9ccf8a", "#5b7a9a", "#e9f26c"),
            new MapInfo("Orman", "#2a4a2c", "#335533", "#4f5d63", "#68767b", "#8dbd80", "#6b3f3f", "#e9f26c"),
            new MapInfo("Köy Yolu", "#5a7a3a", "#6e8f46", "#8a7458", "#a58d6a", "#a8c48a", "#8a5a3a", "#f2e66c"),
            new MapInfo("Bataklık", "#2c4a34", "#34573b", "#4a5a4a", "#5e6e5a", "#6f9a5a", "#3a4a2a", "#d8ff6a"),
            new MapInfo("Mezarlık", "#34383f", "#3f444c", "#59595f", "#72727a", "#b8c4b0", "#3a3a50", "#ff6a6a"),
            new MapInfo("Çöl Kenarı", "#a8935a", "#b8a468", "#c9b67e", "#dccb94", "#c9a56a", "#8a5a2a", "#ff7a3a"),
            new MapInfo("Çöl", "#c49a54", "#d6ae62", "#d9bd84", "#ecd29c", "#d1a56a", "#7a4a2a", "#ff7a3a"),
            new MapInfo("Harabeler", "#a39560", "#b3a672", "#8f8a7c", "#aaa595", "#b5bfa0", "#6a5a4a", "#ffd23f"),
            new MapInfo("Kayalık", "#6a6458", "#7a7466", "#5d5a56", "#77736d", "#9aa58a", "#5a4a3a", "#ffcf5a"),
            new MapInfo("Dağ Geçidi", "#5f6b78", "#6f7c8a", "#4a525c", "#626c78", "#8fa8a0", "#3a4a5a", "#e0f0ff"),
            new MapInfo("Kar Ovası", "#cfdce6", "#e3edf4", "#a9bccb", "#c4d3de", "#bfe0ea", "#4a6a8a", "#ffffff"),
            new MapInfo("Buzul", "#9cc8de", "#b4dcee", "#6f9bb5", "#8fb8d0", "#9ad3e8", "#3a6a9a", "#ffffff"),
            new MapInfo("Yanardağ Eteği", "#4a2f28", "#5a382e", "#3a3230", "#524844", "#d8663a", "#5a2a1a", "#fff1a0"),
            new MapInfo("Lav Nehri", "#2a1c1a", "#33231f", "#5a2418", "#9a3a1c", "#e0803a", "#6a1a10", "#fff1a0"),
            new MapInfo("Terk Şehir", "#3a3f44", "#454b50", "#2e3338", "#444a50", "#9aaf9a", "#4a4a56", "#ffd23f"),
            new MapInfo("Fabrika", "#4a4238", "#5a5044", "#3a3a3a", "#555555", "#8fa070", "#7a5a2a", "#ffcf3a"),
            new MapInfo("Zehirli Çorak", "#3a4a2a", "#4a5e30", "#4a3a5a", "#66507a", "#a4d04a", "#4a2f6b", "#ffffff"),
            new MapInfo("Ölüm Vadisi", "#5a5448", "#6e675a", "#7a7468", "#968f80", "#d8d4c0", "#3a3a44", "#ff5a4d"),
            new MapInfo("Gölge Diyarı", "#1c1a2e", "#26233c", "#2a2840", "#3a3858", "#4a4770", "#15131f", "#6be9ff"),
            new MapInfo("Boşluk", "#120a22", "#1a1030", "#241440", "#3a1f66", "#7a4fd0", "#1a1030", "#ffffff"),
        };

        public static int MapOf(int lv) => System.Math.Max(0, System.Math.Min(Maps.Length - 1, (lv - 1) / 5));
        /// <summary>Haritaya göre düşman boyu büyür (prototipte applyMap).</summary>
        public static float MapScale(int map) => 0.86f + 0.0145f * map;
    }

    /// <summary>Kahraman, silah ve bina etkileri. Varsayılan: Ayaz, tabanca, bina yok. Progress.BuildMods doldurur.</summary>
    public class BattleMods
    {
        public string heroId = "ayaz", weaponId = "pistol", weaponColor = "#ffe27a";
        public float skillMul = 1.5f;      // yetenek hasarı (ateş gücünün kaç saniyesi)
        public float skillCd = 0.85f;      // yetenek bekleme çarpanı
        public float fireMul = 1f;         // ateş aralığı çarpanı (küçük = hızlı)
        public float guard = 1f;           // alınan hasar çarpanı
        public float dmgMul = 1f;          // mermi hasarı çarpanı
        public float startMul = 1f;        // kışla
        public int startBonus = 0;
        public float heroHpMul = 1f;       // çiftlik ve hastane
        public float regen = 0f;           // saniyede kahraman canı yenilenmesi (oran)
        public float heal = 0f;            // Kaya: yetenekte asker geri getirme
        public float buffs = 0f;           // Mira: yetenekte çift+hızlı ateş süresi
        public float rapidOnSkill = 0f;    // Ayaz 5. seviye
        public int pierce = 0;             // Delici tüfek
        public float bulletLife = 9f, bulletVy = -520f;
        public bool blast;                 // Roketatar
        public float goldMul = 1f, marketMul = 1f; // ödül çarpanları
        public float barrelMul = 1f;       // fıçılardan gelen asker çarpanı (yetenek ağacı)
        public float turretMul = 1f;       // savunma kulesi hasar çarpanı
        public float boardMul = 1f;        // tabelanın götürdüğü asker çarpanı
        public int startTurret;            // Hayatta Kalma'ya hazır kule
    }
}
