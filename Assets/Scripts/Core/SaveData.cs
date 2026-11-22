using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Core
{
    [Serializable]
    public class SaveData
    {
        public int gold = 120, iron = 60, food = 80, diamonds; // yeni oyuncunun başlangıç kaynakları
        public int unlocked = 1;        // açılmış en yüksek bölüm
        public int[] stars = new int[Balance.MaxLevel];
        public List<int> buildingLevels = new List<int>(); // eski alan, kullanılmıyor
        public long lastTick;       // UTC saniye, çevrimdışı üretim için

        // Kahraman ve silah
        public string hero = "ayaz";
        public string weapon = "pistol";
        public int[] heroLevels = { 1, 1, 1 };       // ayaz, kaya, mira
        public int[] weaponLevels = { 0, 0, 0, 0 };  // tabanca, delici, alev, roket

        // Üs: bina seviyeleri BuildingKind sırasıyla
        public int[] buildings = new int[10]; // 9 = Karargâh
        public int buildKind = -1;      // inşaattaki bina (-1 = yok)
        public int buildFrom;
        public long buildEnd;           // UTC milisaniye
        public int builders = 1;        // inşaatçı sayısı (2. elmasla alınır)
        public int build2Kind = -1, build2From;
        public long build2End;
        public float pendGold, pendIron, pendFood; // toplanmayı bekleyen üretim
        public long lastProd;           // UTC milisaniye

        // Günlük sistem
        public DailyState daily = new DailyState();
        public long defenseReadyAt; // savunma modu bekleme süresi
        public string language = "tr";
        public bool noAds;              // reklamsız paket ya da başlangıç paketi alındı
        public bool starterPackBought;
        public bool founderBought;          // Kurucu Paketi alındı
        public long founderOfferStart;      // teklifin başladığı an (UTC ms, 0 = henüz açılmadı)
        public int piggy;                   // kumbarada biriken elmas
        public int boostBattles, boostSoldiers; public float boostDmg;   // güç takviyesi
        public string adDay = ""; public int adBoost, adRes, adPiggy;    // günlük reklam hakları
        public long monthlyCardUntil;   // aylık kart bitiş zamanı (UTC saniye)

        // Üs teması (elmasla satılan çevre görünümü)
        public string theme = "green";
        public List<string> ownedThemes = new List<string> { "green" };

        // Kışla: asker eğitimi
        public int troops, trainQueue;
        public long trainEnd;

        // Kahraman ekipmanı
        public List<Meta.GearItem> gear = new List<Meta.GearItem>();
        public int[] equipped = new int[3];  // Silah, Zırh, Muska (eşya kimliği, 0 = boş)
        public int gearSeq, shards;

        // Senaryo: görülen hikâye sahneleri
        public List<string> seenScenes = new List<string>();

        // Hikâye görevleri
        public int storyIndex;
        public List<string> questKeys = new List<string>(), storyBase = new List<string>();
        public List<int> questVals = new List<int>(), storyBaseVals = new List<int>();
    }

    [Serializable]
    public class QuestState { public string id; public int p; public bool c; }

    [Serializable]
    public class DailyState
    {
        public int date;            // görevlerin üretildiği gün (yyyymmdd)
        public List<QuestState> quests = new List<QuestState>();
        public int streak;          // giriş serisi (1-7)
        public int last;            // son giriş ödülü alınan gün
        public bool bonus;          // üç görev bonusu alındı mı
        public int adsToday;        // bugün izlenen bedava elmas reklamı
        public int monthlyClaim;    // aylık kart son alım günü
    }

    public static class SaveSystem
    {
        const string Key = "surusavasi-v2";

        public static SaveData Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            var d = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            Migrate(d);
            return d;
        }

        /// <summary>Eski kayıtları yeni alanlara uyarlar.</summary>
        public static void Migrate(SaveData d)
        {
            const int kinds = 10;
            if (d.buildings == null) d.buildings = new int[kinds];
            if (d.buildings.Length < kinds) { var b = new int[kinds]; d.buildings.CopyTo(b, 0); d.buildings = b; }
            int hq = (int)BuildingKind.Headquarters;
            if (d.buildings[hq] <= 0)
            {
                // Karargâh yeni geldi: mevcut binaları kilitlemesin diye en yüksek binanın yarısından başlar
                int mx = 0; for (int i = 0; i < hq; i++) mx = Mathf.Max(mx, d.buildings[i]);
                d.buildings[hq] = Mathf.Max(1, Mathf.CeilToInt(mx / 2f));
            }
            if (string.IsNullOrEmpty(d.theme)) d.theme = "green";
            if (d.ownedThemes == null) d.ownedThemes = new List<string>();
            if (!d.ownedThemes.Contains("green")) d.ownedThemes.Add("green");
            if (!d.ownedThemes.Contains(d.theme)) d.theme = "green";
            if (d.builders < 1) { d.builders = 1; d.build2Kind = -1; }
            if (d.gear == null) d.gear = new List<Meta.GearItem>();
            if (d.equipped == null || d.equipped.Length < 3) d.equipped = new int[3];
            if (d.seenScenes == null) d.seenScenes = new List<string>();
            if (d.questKeys == null) d.questKeys = new List<string>();
            if (d.questVals == null) d.questVals = new List<int>();
            if (d.storyBase == null) d.storyBase = new List<string>();
            if (d.storyBaseVals == null) d.storyBaseVals = new List<int>();
        }

        public static void Save(SaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
