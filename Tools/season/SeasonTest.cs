using System;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;

// Sezon bileti: hız (kaç günde kaçıncı kademe) ve ödül kontrolleri. Çalıştırma: Tools/season/run_season.ps1
static class P
{
    static int fail;
    static void Check(bool ok, string n) { if (!ok) { fail++; Console.WriteLine("HATA: " + n); } }

    static (int tier, int day30, int bonus) Simulate(string name, int clears, int stars3, int boss, int upg, int train, int gather, int gates, int quests, bool bonus, int tower)
    {
        var d = new SaveData { unlocked = 30 }; SaveSystem.Migrate(d);
        var start = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
        int day30 = -1;
        for (int day = 0; day < Season.Days; day++)
        {
            var now = start.AddDays(day); Season.Clock = () => now;
            Season.OnEvent(d, "login");
            for (int i = 0; i < clears; i++) Season.OnEvent(d, "clear");
            Season.OnEvent(d, "stars3", stars3); Season.OnEvent(d, "boss", boss); Season.OnEvent(d, "upgrade", upg);
            Season.OnEvent(d, "train", train); Season.OnEvent(d, "gather", gather); Season.OnEvent(d, "gates", gates);
            Season.OnEvent(d, "tower_floor", tower);
            for (int q = 0; q < quests; q++) Season.OnEvent(d, "quest");
            if (bonus) Season.OnEvent(d, "quest_bonus");
            for (int i = 0; i < 3; i++) Season.ClaimWeekly(d, i);
            if (day30 < 0 && Season.Tier(d) >= Season.Tiers) day30 = day + 1;
        }
        Console.WriteLine($"{name,-10} kademe {Season.Tier(d),2}  ·  30. kademe: {(day30 < 0 ? "yok" : day30 + ". gün")}  ·  bonus sandık {Season.BonusEarned(d)}  ·  SP {d.seasonPts}");
        return (Season.Tier(d), day30, Season.BonusEarned(d));
    }

    static int Main()
    {
        var casual = Simulate("az oynayan", 4, 1, 1, 1, 1, 4, 6, 2, false, 0);
        var mid = Simulate("ortalama", 8, 2, 2, 2, 2, 8, 15, 4, true, 0);
        var hard = Simulate("çok oynayan", 20, 6, 6, 4, 5, 25, 40, 4, true, 12);
        Check(casual.tier >= 12 && casual.tier < 30, "az oynayan 12-29 arası");
        Check(mid.tier >= 25, "ortalama oyuncu 25+");
        Check(hard.day30 > 0 && hard.day30 >= 14, "çok oynayan 30'a ulaşır ama 2 haftadan önce değil");

        // Ödüller: tüm kademeleri al
        var d = new SaveData { unlocked = 40 }; SaveSystem.Migrate(d);
        var now = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc); Season.Clock = () => now;
        Season.Ensure(d); d.seasonPts = Season.Tiers * Season.TierPts; Season.UnlockPremium(d);
        int gems = 0, gold = 0; var rnd = new Random(1);
        Action<string, int> g = (k, n) => { if (k == "gems") gems += n; if (k == "gold") gold += n; };
        int claimed = 0;
        for (int t = 1; t <= 30; t++) { if (Season.ClaimFree(d, t, g, rnd) != null) claimed++; if (Season.ClaimPrem(d, t, g, rnd) != null) claimed++; }
        Check(claimed == 60 && Season.Claimable(d) == 0, "60 ödül alınır: " + claimed);
        Check(Season.ClaimFree(d, 1, g, rnd) == null, "aynı ödül iki kez alınmaz");
        Console.WriteLine($"Tüm yol: {gems} elmas, {gold} altın, {d.speedTokens} hızlandırıcı, {d.gear.Count} ekipman, görünüm {d.ownedSkins.Count}, unvan {string.Join(",", d.seasonTitles)}");
        Check(gems > 300 && gems < 1200, "sezon elması sunucu sınırının (1200) altında: " + gems);
        Check(d.seasonTitles.Contains("season1") && Season.TitleName("season1") == "Kış Uyanışı Gazisi", "unvan");
        d.title = "season1"; Check(Achievements.Title(d) == "Kış Uyanışı Gazisi", "unvan gösterimi");
        Check(d.ownedSkins.Count == 5 && Skins.Owned(d, 3, Skins.Ice), "Buz görünümü 5 kahramana verildi");
        Check(Skins.Current(d, Progress.HeroIndex(d.hero)) == Skins.Ice, "Buz kuşanıldı");
        var hp = Packs.HeroPackSkin(new SaveData { unlocked = 40 }); Check(!Skins.Exclusive(hp.skin), "kahraman paketi sezon görünümü vermez");

        // Kar Fırtınası
        var sat = new DateTime(2026, 11, 28, 13, 0, 0); var thu = new DateTime(2026, 11, 26, 13, 0, 0);
        var ev = new SaveData { unlocked = 20 }; SaveSystem.Migrate(ev);
        Check(SnowEvent.Open(ev, sat) && !SnowEvent.Open(ev, thu), "fırtına yalnızca hafta sonu");
        Check(SnowEvent.Until(thu) == new TimeSpan(1, 11, 0, 0), "cumartesiye kalan: " + SnowEvent.Until(thu));
        Check(SnowEvent.Until(sat) == new TimeSpan(1, 11, 0, 0), "pazar sonuna kalan: " + SnowEvent.Until(sat));
        for (int i = 0; i < 3; i++) SnowEvent.UseTry(ev, sat);
        Check(SnowEvent.TriesLeft(ev, sat) == 0 && SnowEvent.TriesLeft(ev, sat.AddDays(1)) == 3, "günde 3 hak");
        var er = SnowEvent.Reward(ev, 3, new SuruSavasi.Core.Battle.BattleMods());
        Check(er.gems == SnowEvent.WinGems && er.gold > 0, "fırtına ödülü");
        Check(!SnowEvent.Open(new SaveData(), sat), "4. bölümden önce kapalı");

        // Bilet yoksa ücretli yol kilitli
        var e = new SaveData { unlocked = 40 }; SaveSystem.Migrate(e); Season.Ensure(e); e.seasonPts = 500;
        Check(Season.ClaimPrem(e, 1, g, rnd) == null && Season.ClaimFree(e, 1, g, rnd) != null, "biletsiz ücretli yol kilitli");
        Check(Season.ClaimFree(e, 6, g, rnd) == null, "ulaşılmamış kademe alınmaz");

        // Yeni sezon sıfırlar
        now = new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc); Season.Ensure(e);
        Check(e.seasonId == 3 && e.seasonPts == 0 && !e.seasonPremium && e.seasonFree == 0, "yeni sezon sıfırlanır");

        // Erken oyuncu SP kazanmaz
        var f = new SaveData(); SaveSystem.Migrate(f); Check(Season.OnEvent(f, "clear") == 0, "4. bölümden önce kapalı");

        // Kademe atlama son haftada
        now = new DateTime(2027, 2, 13, 10, 0, 0, DateTimeKind.Utc); Season.Ensure(e);
        Check(Season.CanSkip(e), "son haftada atlanabilir");
        Season.Skip(e); Check(Season.Tier(e) == 1, "atlama 1 kademe");
        now = new DateTime(2027, 1, 20, 10, 0, 0, DateTimeKind.Utc);
        Check(!Season.CanSkip(e), "erken atlama yok");

        // Haftalık görevler sezon ve hafta içinde sabit
        var a = Season.WeeklyDef(e, 0).key; Check(a == Season.WeeklyDef(e, 0).key, "haftalık görev sabit");

        Console.WriteLine(fail == 0 ? "Sezon testleri: hepsi geçti" : $"Sezon testleri: {fail} hata");
        return fail == 0 ? 0 : 1;
    }
}
