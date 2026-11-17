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
        public int[] buildings = new int[9];
        public int buildKind = -1;      // inşaattaki bina (-1 = yok)
        public int buildFrom;
        public long buildEnd;           // UTC milisaniye
        public float pendGold, pendIron, pendFood; // toplanmayı bekleyen üretim
        public long lastProd;           // UTC milisaniye
        public long defenseReadyAt; // savunma modu bekleme süresi
        public string language = "tr";
        public bool noAds;              // reklamsız paket ya da başlangıç paketi alındı
        public bool starterPackBought;
        public long monthlyCardUntil;   // aylık kart bitiş zamanı (UTC saniye)
    }

    public static class SaveSystem
    {
        const string Key = "surusavasi-v2";

        public static SaveData Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            return string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
        }

        public static void Save(SaveData data)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
