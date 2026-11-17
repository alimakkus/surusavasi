using System;
using System.Collections.Generic;
using UnityEngine;

namespace SuruSavasi.Core
{
    [Serializable]
    public class SaveData
    {
        public int gold, iron, food, diamonds;
        public int[] stars = new int[Balance.MaxLevel];
        public List<int> buildingLevels = new List<int>();
        public long lastTick;       // UTC saniye, çevrimdışı üretim için
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
