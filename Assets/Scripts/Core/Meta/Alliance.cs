using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    /// <summary>İttifak (klan). Şimdilik üyeler çevrimdışı botlardır: katıldığın ittifak, üyeleri ve onların bağış/yardım/patron
    /// katkıları ittifak kimliğinden ve zamandan hesaplanır (lig gibi). Sunucu gelince aynı kurallar gerçek oyuncularla çalışır.
    /// Oyuncu: günde 5 bağış yapar (katkı puanı + ittifak XP), inşaatına yardım ister, başkalarına yardım eder,
    /// haftalık ittifak patronuna vurur ve katkı puanını İttifak Mağazası'nda harcar.</summary>
    public static class Alliance
    {
        public const int OpenAt = 9, Members = 20, MaxLevel = 10;
        public const int DonatePerDay = 5, DonateCoins = 10, DonateXp = 20;
        public const int HelpsOthersPerDay = 8, HelpCoins = 5;
        public const int BossTriesPerDay = 2;
        public const float BossTime = 60f, BossRunPct = 5f; // tek savaşta patronun en çok %5'i
        public static readonly int[] BossMilestones = { 25, 50, 75, 100 };

        static readonly string[] Prefix = { "Demir", "Kızıl", "Gece", "Kurt", "Çelik", "Fırtına", "Yıldız", "Kara", "Gök", "Ateş", "Buz", "Aslan" };
        static readonly string[] Suffix = { "Muhafızlar", "Kalkan", "Sürüsü", "Lejyonu", "Kardeşliği", "Bekçileri", "Kartalları", "Ordusu", "Akıncıları", "Kalesi" };
        static readonly string[] Colors = { "#ff8a65", "#7fd6ff", "#8be39d", "#ffd23f", "#c98bff", "#ff6f91", "#b0bec5", "#ffb02e" };
        static readonly string[] Bots =
        {
            "KurtAvcı", "Zeynep_K", "Barbaros34", "GeceBekçisi", "Mert.Usta", "AyşeSavaşçı", "Kılıç_Arslan", "Efe1903", "Derya", "Serdar_TR",
            "ZombiKıran", "Yağız", "Elif.88", "Komutan_Ali", "Selin", "KaraŞahin", "Burak07", "Defne", "Tolga_K", "ÇelikYumruk",
            "Emre", "Nisan", "Atlas", "Kaan_35", "Ece", "DemirKale", "Ozan", "Melis", "Batu", "SürüLideri", "İpek", "Arda_06", "Ceren", "Yiğit", "Onur"
        };

        /// <summary>Saat kaynağı (testlerde değiştirilir). Yerel saat.</summary>
        public static Func<DateTime> Clock = () => DateTime.Now;
        static DateTime Now => Clock();
        static string Today => Now.ToString("yyyyMMdd");
        static int DayNo(DateTime t) => (int)(t.Date - new DateTime(2026, 1, 1)).TotalDays;

        public static bool Open(SaveData d) => d.unlocked >= OpenAt;
        public static bool Joined(SaveData d) => d.allyId != 0;

        // ---------------- İttifak tanımı (kimlikten türetilir) ----------------
        public class Info { public int id; public string name, tag, color; public int baseLevel; public float activity; public int members; }
        public class Member { public string name; public int power, role; public float activity; public bool me; } // role: 2 lider, 1 subay, 0 üye

        public static Info Describe(int id)
        {
            var r = new Random(id * 7919 + 17);
            string a = Prefix[r.Next(Prefix.Length)], b = Suffix[r.Next(Suffix.Length)];
            string tag = (a.Substring(0, 1) + b.Substring(0, 2)).ToUpperInvariant().Replace('İ', 'I');
            return new Info { id = id, name = a + " " + b, tag = tag, color = Colors[r.Next(Colors.Length)], baseLevel = 1 + r.Next(3), activity = .55f + (float)r.NextDouble() * .35f, members = Members - 1 - r.Next(3) };
        }

        /// <summary>Katılmak için önerilen 3 ittifak (cihaza ve haftaya göre sabit).</summary>
        public static List<Info> Suggestions(SaveData d)
        {
            var list = new List<Info>();
            int seed = Math.Abs((d.allySeed == 0 ? 1 : d.allySeed) * 31 + League.WeekKey(Now));
            for (int i = 0; i < 3; i++) list.Add(Describe(1000 + (seed + i * 977) % 90000));
            return list;
        }

        public static List<Member> MembersOf(SaveData d)
        {
            var info = Describe(d.allyId);
            var r = new Random(d.allyId * 131 + 7);
            var pool = new List<string>(Bots); var list = new List<Member>();
            int myPower = Power(d);
            for (int i = 0; i < info.members; i++)
            {
                int pi = r.Next(pool.Count); string nm = pool[pi]; pool.RemoveAt(pi);
                float act = Math.Max(.15f, Math.Min(1, info.activity + (float)(r.NextDouble() - .5) * .7f));
                int pw = (int)(myPower * (.45 + r.NextDouble() * 1.2));
                list.Add(new Member { name = nm, power = Math.Max(50, pw), role = i == 0 ? 2 : i < 3 ? 1 : 0, activity = act });
            }
            list.Add(new Member { name = "Sen", power = myPower, role = 0, activity = 1, me = true });
            list.Sort((x, y) => y.role != x.role ? y.role.CompareTo(x.role) : y.power.CompareTo(x.power));
            return list;
        }

        /// <summary>Oyuncunun gücü: ilerleme + kahraman seviyeleri + binalar (ittifak listesinde sıralama için).</summary>
        public static int Power(SaveData d)
        {
            int p = d.unlocked * 40 + Progress.TotalStars(d) * 10;
            if (d.heroLevels != null) foreach (var l in d.heroLevels) p += l * 25;
            if (d.buildings != null) foreach (var b in d.buildings) p += b * 15;
            return p;
        }

        public static void Join(SaveData d, int id)
        {
            d.allyId = id; d.allyJoinDay = DayNo(Now); d.allyXp = 0; d.allyBotDay = DayNo(Now);
            d.allyMsgs = new List<string>();
            Say(d, "Sistem", "İttifaka katıldın. Hoş geldin!");
            var m = MembersOf(d); Say(d, m[0].name, "Hoş geldin! Günlük bağışları unutma, patronu hafta bitmeden düşürelim.");
        }

        // ---------------- Seviye ----------------
        public static int XpFor(int level) => (int)(1000 * Math.Pow(level, 1.3));   // level → level+1 için gereken
        public static int Level(SaveData d)
        {
            int lv = Describe(d.allyId).baseLevel; long xp = d.allyXp;
            while (lv < MaxLevel && xp >= XpFor(lv)) { xp -= XpFor(lv); lv++; }
            return lv;
        }
        public static (int cur, int need) LevelProgress(SaveData d)
        {
            int lv = Describe(d.allyId).baseLevel; long xp = d.allyXp;
            while (lv < MaxLevel && xp >= XpFor(lv)) { xp -= XpFor(lv); lv++; }
            return lv >= MaxLevel ? (1, 1) : ((int)xp, XpFor(lv));
        }
        /// <summary>İnşaat başına gelen yardım sayısı.</summary>
        public static int HelpsPerBuild(SaveData d) => 4 + Level(d) / 2;
        /// <summary>Patron sandığı çarpanı.</summary>
        public static float BossBonus(SaveData d) => 1f + .05f * (Level(d) - 1);
        /// <summary>Mağaza indirimi (yüzde).</summary>
        public static int ShopDiscount(SaveData d) => Level(d) >= 8 ? 20 : Level(d) >= 5 ? 10 : 0;

        /// <summary>Botların günlük katkısı: katıldıktan sonra geçen her gün ittifak XP'si artar.</summary>
        public static void Tick(SaveData d)
        {
            if (!Joined(d)) return;
            int today = DayNo(Now);
            if (d.allyBotDay <= 0) d.allyBotDay = today;
            var info = Describe(d.allyId);
            while (d.allyBotDay < today)
            {
                d.allyBotDay++;
                d.allyXp += (int)(info.members * 60 * info.activity);
            }
            BossCheck(d);
            ApplyHelps(d);
        }

        // ---------------- Bağış ----------------
        public enum Res { Gold, Iron, Food }
        public static int DonateCost(SaveData d, Res r) { int b = r == Res.Gold ? 300 : r == Res.Iron ? 150 : 200; return (int)(b * (1 + Math.Min(100, d.unlocked) / 15f)); }
        public static int DonationsLeft(SaveData d) => d.allyDonDay == Today ? Math.Max(0, DonatePerDay - d.allyDonCount) : DonatePerDay;

        /// <summary>Kaynak ödendikten sonra çağrılır: katkı puanı ve ittifak XP'si verir.</summary>
        public static void Donated(SaveData d, Res r)
        {
            if (d.allyDonDay != Today) { d.allyDonDay = Today; d.allyDonCount = 0; }
            d.allyDonCount++; d.allyCoins += DonateCoins; d.allyXp += DonateXp;
            Say(d, "Sen", $"{DonateCost(d, r)} {(r == Res.Gold ? "altın" : r == Res.Iron ? "demir" : "yiyecek")} bağışladı.", true);
        }

        // ---------------- Yardım: başkalarına ----------------
        public class Request { public string who, what; public int idx; }
        public static List<Request> OthersRequests(SaveData d)
        {
            var list = new List<Request>();
            if (!Joined(d)) return list;
            var mem = MembersOf(d); var r = new Random(DayNo(Now) * 53 + d.allyId);
            string[] b = { "Kışla", "Çiftlik", "Altın Madeni", "Demir Ocağı", "Hastane", "Kale Duvarı", "Silahhane", "Karargâh" };
            for (int i = 0; i < HelpsOthersPerDay; i++)
            {
                var m = mem[r.Next(mem.Count)]; if (m.me) m = mem[0];
                list.Add(new Request { who = m.name, what = $"{b[r.Next(b.Length)]} Sv. {3 + r.Next(10)}", idx = i });
            }
            return list;
        }
        static int HelpMask(SaveData d) => d.allyHelpDay == Today ? d.allyHelpMask : 0;
        public static bool Helped(SaveData d, int i) => (HelpMask(d) & (1 << i)) != 0;
        public static int HelpsLeft(SaveData d) { int n = 0; for (int i = 0; i < HelpsOthersPerDay; i++) if (!Helped(d, i)) n++; return n; }
        public static int HelpOthers(SaveData d, int i)
        {
            if (Helped(d, i)) return 0;
            if (d.allyHelpDay != Today) { d.allyHelpDay = Today; d.allyHelpMask = 0; }
            d.allyHelpMask |= 1 << i; d.allyCoins += HelpCoins; d.allyXp += 5;
            return HelpCoins;
        }

        // ---------------- Yardım: kendi inşaatına ----------------
        /// <summary>Bu inşaat için yardım istenebilir mi (her inşaatta bir kez).</summary>
        public static bool CanAskHelp(SaveData d, BuildingKind k) =>
            Joined(d) && BaseLogic.Busy(d, k) && !(d.allyReqKind == (int)k && d.allyReqEnd == BuildTag(d, k));
        /// <summary>İnşaatın kimliği (bina + hedef seviye): her yükseltmede bir kez yardım istenir.</summary>
        static long BuildTag(SaveData d, BuildingKind k) { int s = BaseLogic.SlotOf(d, k); return s < 0 ? -1 : BaseLogic.SlotFrom(d, s) + 1; }
        public static bool Asking(SaveData d, BuildingKind k) => d.allyReqKind == (int)k && BaseLogic.Busy(d, k) && d.allyReqGot < d.allyReqMax;

        public static void AskHelp(SaveData d, BuildingKind k)
        {
            if (!CanAskHelp(d, k)) return;
            d.allyReqKind = (int)k; d.allyReqAt = BaseLogic.Now; d.allyReqGot = 0; d.allyReqMax = HelpsPerBuild(d);
            d.allyReqEnd = BuildTag(d, k);
            Say(d, "Sen", "inşaatı için yardım istedi.", true);
        }

        /// <summary>Gelen yardımlar: her yardım kalan sürenin %1'ini (en az 60 sn) kısaltır. Yardımlar istekten sonra birkaç dakikaya yayılır.</summary>
        public static int ApplyHelps(SaveData d)
        {
            if (d.allyReqKind < 0 || d.allyReqGot >= d.allyReqMax) return 0;
            var k = (BuildingKind)d.allyReqKind;
            if (!BaseLogic.Busy(d, k)) { d.allyReqGot = d.allyReqMax; return 0; }
            double secs = (BaseLogic.Now - d.allyReqAt) / 1000.0;
            int due = Math.Min(d.allyReqMax, (int)(secs / 25) + 1); // ilk yardım hemen, sonra ~25 sn arayla
            int got = 0;
            while (d.allyReqGot < due && BaseLogic.Busy(d, k))
            {
                float rem = BaseLogic.Remaining(d, k);
                int cut = (int)Math.Max(60, rem * .01f);
                BaseLogic.Shorten(d, k, cut);
                d.allyReqGot++; got++;
            }
            if (got > 0)
            {
                var mem = MembersOf(d); var r = new Random(d.allyReqGot * 31 + (int)(d.allyReqAt % 10000));
                Say(d, mem[r.Next(mem.Count - 1)].name, got > 1 ? $"ve {got - 1} kişi inşaatına yardım etti." : "inşaatına yardım etti.", true);
            }
            return got;
        }

        // ---------------- Haftalık ittifak patronu ----------------
        public static int BossIndex(SaveData d) => (League.WeekKey(Now) + d.allyId) % Battle.BattleData.BossNames.Length;

        static void BossCheck(SaveData d)
        {
            int wk = League.WeekKey(Now);
            if (d.allyBossWeek == wk) return;
            d.allyBossWeek = wk; d.allyBossMine = 0; d.allyBossClaim = 0; d.allyBossTriesDay = ""; d.allyBossTries = 0;
        }

        /// <summary>Botların patrona verdiği toplam hasar (%) — hafta ilerledikçe artar; aktif ittifak haftayı %70-95 bitirir.</summary>
        public static float BotsPct(SaveData d)
        {
            var info = Describe(d.allyId);
            float frac = League.WeekFrac(Now);
            float fin = 60 + 38 * info.activity;     // hafta sonunda botların toplamı
            return (float)(fin * Math.Pow(frac, .85));
        }
        public static float MinePct(SaveData d) => d.allyBossMine / 100f;
        public static float TotalPct(SaveData d) => Math.Min(100, BotsPct(d) + MinePct(d));
        public static int BossTriesLeft(SaveData d) => d.allyBossTriesDay == Today ? Math.Max(0, BossTriesPerDay - d.allyBossTries) : BossTriesPerDay;
        public static void BossUseTry(SaveData d)
        {
            BossCheck(d);
            if (d.allyBossTriesDay != Today) { d.allyBossTriesDay = Today; d.allyBossTries = 0; }
            d.allyBossTries++;
        }
        /// <summary>Savaş sonucu: frac = o savaşta patronun canının ne kadarı indirildi (0-1). Patrona eklenen yüzdeyi döndürür.</summary>
        public static float BossHit(SaveData d, float frac)
        {
            BossCheck(d);
            float pct = Math.Max(0, Math.Min(1, frac)) * BossRunPct;
            pct = Math.Min(pct, 100 - TotalPct(d));
            d.allyBossMine += (int)Math.Round(pct * 100);
            d.allyCoins += (int)Math.Round(frac * 20);
            if (pct > 0) Say(d, "Sen", $"patrona %{pct:0.0} hasar verdi.", true);
            return pct;
        }
        public static bool MilestoneReady(SaveData d, int i) => (d.allyBossClaim & (1 << i)) == 0 && TotalPct(d) >= BossMilestones[i];
        public static bool MilestoneClaimed(SaveData d, int i) => (d.allyBossClaim & (1 << i)) != 0;
        /// <summary>Sandık: kaynak + elmas + katkı. Patrona hiç vurmayan oyuncu sandığın yarısını alır.</summary>
        public static LevelReward MilestoneReward(SaveData d, int i)
        {
            float m = BossBonus(d) * (1 + Math.Min(100, d.unlocked) / 20f) * (d.allyBossMine > 0 ? 1 : .5f);
            int[] gems = { 10, 15, 20, 30 };
            int g = (int)(400 * (i + 1) * m);
            return new LevelReward { gold = g, iron = g / 3, food = g / 2, gems = d.allyBossMine > 0 ? gems[i] : gems[i] / 2 };
        }
        public static void ClaimMilestone(SaveData d, int i) { d.allyBossClaim |= 1 << i; d.allyCoins += 15 * (i + 1); }

        /// <summary>Üyelerin bu haftaki patron hasarı sıralaması (botların payı aktivitelerine göre bölünür).</summary>
        public static List<(string name, float pct, bool me)> BossRanking(SaveData d)
        {
            var mem = MembersOf(d); float bots = BotsPct(d); float sum = 0;
            foreach (var m in mem) if (!m.me) sum += m.activity * (.5f + m.power / 1000f);
            var list = new List<(string, float, bool)>();
            foreach (var m in mem) list.Add(m.me ? ("Sen", MinePct(d), true) : (m.name, sum <= 0 ? 0 : bots * m.activity * (.5f + m.power / 1000f) / sum, false));
            list.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return list;
        }

        // ---------------- İttifak Mağazası (katkı puanıyla) ----------------
        public class ShopItem { public string id, name, desc; public int price, weekly; }
        public static readonly ShopItem[] Shop =
        {
            new ShopItem { id = "token", name = "Hızlandırıcı", desc = "İnşaatı 1 saat kısaltır", price = 60, weekly = 3 },
            new ShopItem { id = "shards", name = "5 kahraman parçası", desc = "Kilitli kahramana", price = 80, weekly = 4 },
            new ShopItem { id = "res", name = "Kaynak sandığı", desc = "Altın, demir, yiyecek", price = 30, weekly = 5 },
            new ShopItem { id = "gear", name = "Nadir ekipman", desc = "Rastgele yuva", price = 150, weekly = 1 },
        };
        public static int PriceOf(SaveData d, ShopItem it) => (int)Math.Ceiling(it.price * (100 - ShopDiscount(d)) / 100f);
        public static int Bought(SaveData d, int i) => d.allyShopWeek == League.WeekKey(Now) && d.allyShopBuys != null && i < d.allyShopBuys.Length ? d.allyShopBuys[i] : 0;
        public static bool CanBuy(SaveData d, int i) => d.allyCoins >= PriceOf(d, Shop[i]) && Bought(d, i) < Shop[i].weekly;

        /// <summary>Satın alır; grant(kaynak, miktar) ile kaynak verir. Ekran metnini döndürür (null: alınamadı).</summary>
        public static string Buy(SaveData d, int i, Action<string, int> grant, Random rnd)
        {
            if (!CanBuy(d, i)) return null;
            int wk = League.WeekKey(Now);
            if (d.allyShopWeek != wk || d.allyShopBuys == null || d.allyShopBuys.Length < Shop.Length) { d.allyShopWeek = wk; d.allyShopBuys = new int[Shop.Length]; }
            d.allyCoins -= PriceOf(d, Shop[i]); d.allyShopBuys[i]++;
            float m = 1 + Math.Min(100, d.unlocked) / 20f;
            switch (Shop[i].id)
            {
                case "token": d.speedTokens++; return "+1 hızlandırıcı";
                case "shards": { int h = HeroShards.Give(d, 5); if (h >= 0) return $"+5 {Progress.Heroes[h].name} parçası"; int g = (int)(800 * m); grant("gold", g); return $"+{g} altın"; }
                case "res": { int g = (int)(500 * m); grant("gold", g); grant("iron", g / 3); grant("food", g / 2); return $"+{g} altın, +{g / 3} demir, +{g / 2} yiyecek"; }
                case "gear": { var it = new GearItem { id = ++d.gearSeq, slot = rnd.Next(3), rarity = 1 }; Gear.AddToBag(d, it); return $"{Gear.Name(it)} (Nadir)"; }
            }
            return "";
        }

        // ---------------- Sohbet / etkinlik akışı ----------------
        public static readonly string[] Quick = { "Merhaba herkese!", "Yardım lazım!", "Teşekkürler!", "Patrona saldırıyorum!", "Bağışımı yaptım.", "Harika iş çıkardınız!" };
        public static void Say(SaveData d, string who, string text, bool action = false)
        {
            if (d.allyMsgs == null) d.allyMsgs = new List<string>();
            d.allyMsgs.Add($"{Now:HH:mm}|{(action ? "*" : "")}{who}|{text}");
            while (d.allyMsgs.Count > 30) d.allyMsgs.RemoveAt(0);
        }

        /// <summary>Akış: oyuncunun kendi mesajları + botların bugünkü (saat ilerledikçe çoğalan) etkinlikleri, eskiden yeniye.</summary>
        public static List<(string time, string who, string text, bool action, bool me)> Feed(SaveData d)
        {
            var list = new List<(string, string, string, bool, bool, int)>();
            var now = Now; var mem = MembersOf(d); var r = new Random(DayNo(now) * 97 + d.allyId);
            string[] acts = { "{0} altın bağışladı.", "patrona %{1} hasar verdi.", "{2} kişinin inşaatına yardım etti.", "Kışla'yı Sv. {3} yaptı.", "kahramanını yükseltti." };
            string[] chat = { "Günaydın ittifak!", "Patron bu hafta düşer bence", "Bağışları unutmayın", "Kim kulede kaçıncı katta?", "Hayatta kalmada 20. dalgayı geçtim!", "Yardımlar için teşekkürler", "İyi geceler herkese" };
            for (int i = 0; i < 24; i++)
            {
                int minute = r.Next(24 * 60); var m = mem[r.Next(mem.Count)]; if (m.me) m = mem[0];
                bool isChat = r.NextDouble() < .35;
                string tx = isChat ? chat[r.Next(chat.Length)] : string.Format(acts[r.Next(acts.Length)], 200 + r.Next(30) * 50, (1 + r.NextDouble() * 4).ToString("0.0"), 2 + r.Next(6), 4 + r.Next(10));
                if (minute <= now.Hour * 60 + now.Minute) list.Add(($"{minute / 60:00}:{minute % 60:00}", m.name, tx, !isChat, false, minute));
            }
            if (d.allyMsgs != null)
                foreach (var s in d.allyMsgs)
                {
                    var p = s.Split('|'); if (p.Length < 3) continue;
                    var hm = p[0].Split(':'); int min = hm.Length == 2 && int.TryParse(hm[0], out int h) && int.TryParse(hm[1], out int mi) ? h * 60 + mi : 0;
                    bool act = p[1].StartsWith("*"); string who = act ? p[1].Substring(1) : p[1];
                    list.Add((p[0], who, p[2], act, who == "Sen", min + 100000)); // kendi mesajların en sonda (bugün)
                }
            list.Sort((a, b) => a.Item6.CompareTo(b.Item6));
            var outp = new List<(string, string, string, bool, bool)>();
            int start = Math.Max(0, list.Count - 25);
            for (int i = start; i < list.Count; i++) outp.Add((list[i].Item1, list[i].Item2, list[i].Item3, list[i].Item4, list[i].Item5));
            return outp;
        }

        /// <summary>Üs düğmesinde kırmızı nokta: yapılacak bir şey var.</summary>
        public static bool AnyTodo(SaveData d)
        {
            if (!Open(d)) return false;
            if (!Joined(d)) return true;
            Tick(d);
            if (DonationsLeft(d) > 0 || HelpsLeft(d) > 0 || BossTriesLeft(d) > 0) return true;
            for (int i = 0; i < BossMilestones.Length; i++) if (MilestoneReady(d, i)) return true;
            return false;
        }
    }
}
