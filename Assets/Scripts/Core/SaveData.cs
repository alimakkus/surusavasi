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
        public int[] heroLevels = { 1, 1, 1, 1, 1 }; // ayaz, kaya, mira, deniz, cem
        public int[] heroShards = new int[5];          // parçayla açılan kahramanlar (deniz, cem)
        public int heroOwned;                          // parçayla açılmış kahraman bitleri
        public int[] heroSkin = new int[5];            // kuşanılan görünüm (0 klasik, 1 altın, 2 gece, 3 bayram)
        public List<string> ownedSkins = new List<string>();   // "ayaz_gold" gibi
        public int leagueResSkin = -1;
        // Başarımlar: sayaçlar, alınan kademeler ("kills0" gibi), seçili unvan
        public List<string> statKeys = new List<string>();
        public List<long> statVals = new List<long>();
        public List<string> achClaimed = new List<string>();
        public string title = "";
        public List<string> tips = new List<string>();
        // Teklifler: ilk alım, haftalık kahraman paketi, hız paketi ve hızlandırıcılar
        public bool firstPurchaseDone;
        // Elmas defteri: sunucuya gönderilmeyi bekleyen elmas işlemleri ve son bilinen sunucu bakiyesi (Meta.GemLedger)
        public List<Meta.GemOp> gemOps = new List<Meta.GemOp>();
        public int gemSeq, gemServerBal = -1;
        public long gemSyncAt;
        // İttifak (Meta.Alliance): şimdilik botlarla
        public int allyId, allySeed, allyJoinDay, allyBotDay, allyXp, allyCoins, allyDonCount, allyHelpMask;
        public string allyDonDay = "", allyHelpDay = "", allyBossTriesDay = "";
        public int allyReqKind = -1, allyReqGot, allyReqMax;
        public long allyReqAt, allyReqEnd;
        public int allyBossWeek, allyBossMine, allyBossClaim, allyBossTries, allyShopWeek;
        public int[] allyShopBuys = new int[4];
        public List<string> allyMsgs = new List<string>();
        // Hafta sonu etkinliği (Kar Fırtınası)
        public string eventDay = "";
        public int eventTries, eventBest, eventWins;
        // Sezon bileti (Meta.Season)
        public int seasonId, seasonPts, seasonFree, seasonPrem, seasonBonus, seasonDayPts, seasonWeek = -1, seasonWeekDone;
        public bool seasonPremium, seasonSeen;
        public string seasonDay = "", seasonKeyDay = "";
        public int[] seasonWeekProg = new int[3];
        public List<string> seasonKeyKeys = new List<string>(), seasonTitles = new List<string>();
        public List<int> seasonKeyVals = new List<int>();
        public long savedAt;                   // son kayıt zamanı (Unix ms) — bulut kaydıyla karşılaştırmak için
        public int heroPackWeek, speedPackDay, speedTokens, offerSeenWeek, offerSeenDay;   // rehber: görülen ipuçları ve tanıtımlar                 // lig sonucunda kazanılan Bayram görünümü (kahraman sırası)
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

        // Oyun modları: Kule Tırmanışı (roguelite) ve Patron Avı (günlük patron)
        public string modeDay = "";
        public int towerRuns, towerAdRuns, towerBest, towerFloor;   // towerFloor > 0: devam eden koşunun sıradaki katı
        public bool towerRevived;
        public List<string> towerBoons = new List<string>();
        public int huntTries, huntAdTries, huntBest, huntTiers;     // huntBest: bugünkü en iyi hasar (binde), huntTiers: alınan kademe bitleri
        public int huntBestEver;
        public int survRuns, survAdRuns, survBest;
        public int[] talents = new int[45];   // yetenek ağacı: kahraman × 9 düğüm kademesi
        public bool talentResetUsed;
        public int[] chapterChests = new int[10];   // bölüm yıldız sandıkları (bit 0: 15★, bit 1: 30★)
        public int chapterRevealed = -1;            // "yeni bölge açıldı" gösterilen en yüksek bölüm                  // Hayatta Kalma: bugünkü koşular, en iyi dalga
        // Haftalık lig
        public int leagueWeek, leagueTier, leaguePts;
        public bool leagueResPending; public int leagueResRank, leagueResTier, leagueResNew, leagueResPts;

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
        public int ver;             // görev havuzu sürümü (değişince bugünkü görevler yenilenir)
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
            if (d.towerBoons == null) d.towerBoons = new List<string>();
            Meta.Talents.Ensure(d);
            if (d.heroLevels == null || d.heroLevels.Length < 5) { var hl = new int[] { 1, 1, 1, 1, 1 }; d.heroLevels?.CopyTo(hl, 0); d.heroLevels = hl; }
            if (d.heroShards == null || d.heroShards.Length < 5) { var hs = new int[5]; d.heroShards?.CopyTo(hs, 0); d.heroShards = hs; }
            if (d.chapterChests == null || d.chapterChests.Length < 10) { var cc = new int[10]; d.chapterChests?.CopyTo(cc, 0); d.chapterChests = cc; }
            if (d.modeDay == null) d.modeDay = "";
            if (d.questKeys == null) d.questKeys = new List<string>();
            if (d.questVals == null) d.questVals = new List<int>();
            if (d.storyBase == null) d.storyBase = new List<string>();
            if (d.storyBaseVals == null) d.storyBaseVals = new List<int>();
            if (d.gemOps == null) d.gemOps = new List<Meta.GemOp>();
            if (d.seasonWeekProg == null || d.seasonWeekProg.Length < 3) d.seasonWeekProg = new int[3];
            if (d.seasonKeyKeys == null) d.seasonKeyKeys = new List<string>();
            if (d.seasonKeyVals == null) d.seasonKeyVals = new List<int>();
            if (d.seasonTitles == null) d.seasonTitles = new List<string>();
            if (d.seasonDay == null) d.seasonDay = "";
            if (d.eventDay == null) d.eventDay = "";
            if (d.allyMsgs == null) d.allyMsgs = new List<string>();
            if (d.allyShopBuys == null || d.allyShopBuys.Length < 4) d.allyShopBuys = new int[4];
            if (d.allyDonDay == null) d.allyDonDay = ""; if (d.allyHelpDay == null) d.allyHelpDay = ""; if (d.allyBossTriesDay == null) d.allyBossTriesDay = "";
            if (d.seasonKeyDay == null) d.seasonKeyDay = "";
        }

        public static void Save(SaveData data)
        {
            data.savedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
    }
}
