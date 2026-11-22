using System;
using System.Collections.Generic;
using SuruSavasi.Core.Battle;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kampanya dışındaki oyun modlarının kuralları (arayüzden bağımsız).
    /// Kule Tırmanışı: kat kat zorlaşan savaşlar, her kattan sonra 3 güçlendirmeden biri seçilir, yenilince koşu biter.
    /// Patron Avı: bölümün patronuna karşı süreli savaş; verilen hasara göre günlük kademe ödülleri.</summary>
    public static class Modes
    {
        static void DayCheck(SaveData d)
        {
            string t = DateTime.Now.ToString("yyyyMMdd");
            if (d.modeDay == t) return;
            d.modeDay = t;
            d.towerRuns = d.towerAdRuns = 0;
            d.huntTries = d.huntAdTries = d.huntBest = d.huntTiers = 0;
        }

        // ================= Kule Tırmanışı =================
        public const int TowerUnlock = 6;              // 5. bölüm geçilmiş olmalı
        public const int TowerFreePerDay = 1, TowerAdPerDay = 1, TowerGemCost = 20;
        public static readonly int[] TowerMilestones = { 5, 10, 15, 20, 30, 40, 50 };

        public class Boon
        {
            public string id, name, desc, color; public int max;
            public Action<BattleMods> apply;
        }

        public static readonly Boon[] Boons =
        {
            new Boon { id = "dmg",    name = "Keskin Mermi", desc = "+%20 mermi hasarı",            color = "#ff8a5a", max = 9, apply = m => m.dmgMul += .2f },
            new Boon { id = "army",   name = "Takviye",      desc = "+%25 başlangıç askeri",        color = "#8be39d", max = 9, apply = m => m.startMul += .25f },
            new Boon { id = "fire",   name = "Hızlı Tetik",  desc = "%12 daha hızlı ateş",          color = "#ffd23f", max = 6, apply = m => m.fireMul *= .88f },
            new Boon { id = "guard",  name = "Kalkan",       desc = "%15 daha az hasar al",         color = "#9fe8ff", max = 6, apply = m => m.guard *= .85f },
            new Boon { id = "focus",  name = "Odak",         desc = "Yetenek %20 daha sık",         color = "#c9b8ff", max = 5, apply = m => m.skillCd *= .8f },
            new Boon { id = "skill",  name = "Yıkım",        desc = "Yetenek hasarı +%50",          color = "#c07bff", max = 6, apply = m => m.skillMul += .75f },
            new Boon { id = "life",   name = "Can Pınarı",   desc = "+%20 kahraman canı, can yenilenir", color = "#ff6f91", max = 5, apply = m => { m.heroHpMul += .2f; m.regen += .01f; } },
            new Boon { id = "pierce", name = "Delici Mermi", desc = "Mermiler +1 düşmanı deler",    color = "#ffb02e", max = 2, apply = m => m.pierce += 1 },
            new Boon { id = "blast",  name = "Patlayıcı",    desc = "Mermiler çevreye patlar",      color = "#ff5a4d", max = 1, apply = m => m.blast = true },
            new Boon { id = "loot",   name = "Ganimetçi",    desc = "Kat ödülleri +%30",            color = "#ffe27a", max = 4, apply = m => m.goldMul += .3f },
        };

        public static Boon FindBoon(string id) { foreach (var b in Boons) if (b.id == id) return b; return null; }
        public static int BoonCount(SaveData d, string id) { int c = 0; foreach (var s in d.towerBoons) if (s == id) c++; return c; }

        public static bool TowerOpen(SaveData d) => d.unlocked >= TowerUnlock;
        public static bool TowerInRun(SaveData d) => d.towerFloor > 0;
        public static int TowerFreeLeft(SaveData d) { DayCheck(d); return Math.Max(0, TowerFreePerDay - d.towerRuns); }
        public static int TowerAdLeft(SaveData d) { DayCheck(d); return Math.Max(0, TowerAdPerDay - d.towerAdRuns); }

        /// <summary>Yeni koşu başlatır. how: "free", "ad" ya da "gem" (elmas düşülmesi çağırana aittir).</summary>
        public static void TowerStart(SaveData d, string how)
        {
            DayCheck(d);
            if (how == "free") d.towerRuns++; else if (how == "ad") d.towerAdRuns++;
            d.towerFloor = 1; d.towerRevived = false; d.towerBoons.Clear();
        }

        /// <summary>Kattaki bölüm: oyuncunun ulaştığı bölümün biraz altından başlar, her kat bir bölüm ilerler.</summary>
        public static int TowerLevel(SaveData d, int floor) => Math.Max(1, Math.Min(Balance.MaxLevel, Math.Max(1, d.unlocked - 4) + floor - 1));
        /// <summary>Ek zorluk: her kat %7; 100. bölüm aşılınca kalan katlar da zorluğa eklenir.</summary>
        public static float TowerHard(SaveData d, int floor)
        {
            int raw = Math.Max(1, d.unlocked - 4) + floor - 1, over = Math.Max(0, raw - Balance.MaxLevel);
            return (1 + .07f * (floor - 1)) * (float)Math.Pow(1.05, over);
        }

        public static void ApplyBoons(SaveData d, BattleMods m) { foreach (var id in d.towerBoons) FindBoon(id)?.apply(m); }

        /// <summary>Kat sonrası 3 farklı güçlendirme seçeneği (sınıra ulaşanlar çıkmaz). Aynı kat için hep aynı seçenekler gelir.</summary>
        public static List<Boon> BoonChoices(SaveData d, int floor)
        {
            var pool = new List<Boon>();
            foreach (var b in Boons) if (BoonCount(d, b.id) < b.max) pool.Add(b);
            int.TryParse(d.modeDay, out int dk);
            var r = new Random(floor * 7919 + d.towerBoons.Count * 31 + dk % 100000);
            var res = new List<Boon>();
            while (res.Count < 3 && pool.Count > 0) { int i = r.Next(pool.Count); res.Add(pool[i]); pool.RemoveAt(i); }
            return res;
        }

        public static LevelReward FloorReward(SaveData d, int floor, BattleMods m)
        {
            int lv = TowerLevel(d, floor);
            var rw = Rewards.ForLevel(lv, 3, false, m);
            float mul = 1.1f + .08f * floor;
            return new LevelReward { gold = (int)(rw.gold * mul), iron = (int)(rw.iron * mul), food = (int)(rw.food * mul), gems = Array.IndexOf(TowerMilestones, floor) >= 0 ? 10 + floor : 0 };
        }

        /// <summary>Kilometre taşı sandığı: 5. katta Nadir, 10'da Epik, 20 ve sonrasında Efsanevi şansı.</summary>
        public static GearItem MilestoneGear(SaveData d, int floor, Random r)
        {
            if (Array.IndexOf(TowerMilestones, floor) < 0) return null;
            int rar = floor >= 20 ? (r.NextDouble() < .35 ? 3 : 2) : floor >= 10 ? 2 : 1;
            var g = new GearItem { id = ++d.gearSeq, slot = r.Next(3), rarity = rar };
            Gear.AddToBag(d, g);
            return g;
        }

        /// <summary>Kat kazanıldı: sıradaki kat, rekor güncellenir.</summary>
        public static void TowerFloorWon(SaveData d, int floor) { d.towerBest = Math.Max(d.towerBest, floor); d.towerFloor = floor + 1; }
        public static void TowerPickBoon(SaveData d, string id) => d.towerBoons.Add(id);
        public static void TowerEnd(SaveData d) { d.towerFloor = 0; d.towerBoons.Clear(); d.towerRevived = false; }

        // ================= Patron Avı =================
        public const int HuntUnlock = 11;              // ilk patron yenilmiş olmalı
        public const int HuntTriesPerDay = 3, HuntAdPerDay = 1;
        public const float HuntTime = 75f, HuntHpMul = 3f;
        public static readonly string[] TierNames = { "Bronz", "Gümüş", "Altın", "Elmas" };
        public static readonly string[] TierColors = { "#d08a4a", "#cfd8dc", "#ffd23f", "#7fe8ff" };
        public static readonly float[] TierAt = { .25f, .5f, .75f, 1f };

        public static bool HuntOpen(SaveData d) => d.unlocked >= HuntUnlock;
        public static int HuntTriesLeft(SaveData d) { DayCheck(d); return Math.Max(0, HuntTriesPerDay + d.huntAdTries - d.huntTries); }
        public static int HuntAdLeft(SaveData d) { DayCheck(d); return Math.Max(0, HuntAdPerDay - d.huntAdTries); }
        public static void HuntUseTry(SaveData d) { DayCheck(d); d.huntTries++; }
        public static void HuntAddAdTry(SaveData d) { DayCheck(d); d.huntAdTries++; }

        /// <summary>Günün patronu: oyuncunun geçtiği patronlar arasında her gün sırayla değişir.</summary>
        public static int HuntBoss(SaveData d)
        {
            int beaten = Math.Max(1, Math.Min(10, (d.unlocked - 1) / 10));
            return (int)((DateTime.Now.Date - new DateTime(2026, 1, 1)).TotalDays % beaten);
        }
        public static int HuntLevel(SaveData d) => Math.Max(10, Math.Min(Balance.MaxLevel, d.unlocked));

        /// <summary>Savaşı patron avına çevirir: koşu atlanır, ordu hazır gelir, patronun canı katlanır.</summary>
        public static void HuntSetup(SaveData d, BattleSim s)
        {
            int bi = HuntBoss(d);
            var F = s.L.fin;
            if (!F.boss) F.hp *= Balance.BossHp * Balance.BossEase(s.lv);
            F.boss = true; F.bossIndex = bi; F.name = BattleData.BossNames[bi]; F.color = BattleData.BossColors[bi];
            F.sc = 3.6f + s.lv / 100f * .8f; F.typeId = null;
            F.hp = (float)Math.Ceiling(F.hp * HuntHpMul);
            s.SkipToFinal(1.1f);
        }

        public static float HuntFrac(BattleSim s) => s.boss == null ? 0 : Math.Max(0, Math.Min(1, 1 - s.boss.hp / s.boss.max));
        public static int TierOf(float frac) { int t = -1; for (int i = 0; i < TierAt.Length; i++) if (frac >= TierAt[i] - 1e-4f) t = i; return t; }

        public static LevelReward TierReward(SaveData d, int tier)
        {
            int lv = HuntLevel(d);
            float g = (40 + 8 * lv) * (1 + tier);
            return new LevelReward { gold = (int)g, iron = (int)(g * .45f), food = (int)(g * .55f), gems = tier >= 2 ? (tier == 2 ? 15 : 30) : 0 };
        }

        /// <summary>Denemenin sonucu: yeni ulaşılan kademelerin ödül toplamını döndürür (bugün alınmış kademeler tekrar verilmez).</summary>
        public static LevelReward HuntResult(SaveData d, float frac, out List<int> newTiers)
        {
            DayCheck(d);
            int per = (int)Math.Round(frac * 1000);
            d.huntBest = Math.Max(d.huntBest, per); d.huntBestEver = Math.Max(d.huntBestEver, per);
            newTiers = new List<int>();
            var sum = new LevelReward();
            int top = TierOf(frac);
            for (int t = 0; t <= top; t++)
            {
                if ((d.huntTiers & (1 << t)) != 0) continue;
                d.huntTiers |= 1 << t; newTiers.Add(t);
                var r = TierReward(d, t); sum.gold += r.gold; sum.iron += r.iron; sum.food += r.food; sum.gems += r.gems;
            }
            return sum;
        }

        /// <summary>Üs ekranındaki "Modlar" düğmesinde kırmızı nokta: oynanabilecek ücretsiz hak var.</summary>
        public static bool AnyFree(SaveData d) =>
            (TowerOpen(d) && (TowerInRun(d) || TowerFreeLeft(d) > 0)) || (HuntOpen(d) && HuntTriesLeft(d) > 0);
    }
}
