using System;
using System.Collections.Generic;
using System.Text;
using SuruSavasi.Core.Battle;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Oyunu kendi kendine oynayan bot: önündeki sıralara bakıp en kârlı şeridi seçer, yetenek hazır olunca kullanır.
    /// Ortalama bir oyuncuyu taklit eder; denge simülasyonunda kullanılır.</summary>
    public class AutoPilot
    {
        float think;
        public float reaction = .25f;   // karar aralığı (sn)
        public float skill = 1f;        // 1: iyi oyuncu, küçüldükçe hatalı seçim
        readonly Random rnd;
        public AutoPilot(int seed) { rnd = new Random(seed); }

        public void Tick(BattleSim s, float dt)
        {
            think -= dt;
            if (think > 0) return;
            think = reaction;
            if (s.SkillReady && (s.boss != null || CountEnemiesOnScreen(s) >= 2)) s.UseSkill();

            float best = float.MinValue; int bl = 1;
            for (int l = 0; l < 3; l++)
            {
                float lx = Field.Lanes[l], sc = Math.Abs(s.x - lx) < 30 ? .5f : 0;
                foreach (var o in s.objs)
                {
                    if (o.done) continue;
                    float sy = s.Osy(o);
                    if (sy < -320 || sy > Field.SQY - 10 || Math.Abs(o.x - lx) > 40) continue;
                    float w = 1f / (1f + (Field.SQY - sy) / 150f);
                    switch (o.kind)
                    {
                        case ObjKind.Barrel: sc += (o.v - o.rel) * w; break;
                        case ObjKind.Power: sc += (o.v - o.rel + 6) * w; break;
                        case ObjKind.Board: sc -= o.v * w * 1.2f; break;
                        case ObjKind.Enemy: sc -= o.contact * w * Math.Min(1, o.hp / Math.Max(1, Balance.DpsOf(s.n, s.lv) * 1.2f)); break;
                    }
                }
                if (skill < 1) sc += (float)(rnd.NextDouble() - .5) * 20 * (1 - skill);
                if (sc > best) { best = sc; bl = l; }
            }
            s.tx = Field.Lanes[bl];
        }

        static int CountEnemiesOnScreen(BattleSim s)
        {
            int c = 0;
            foreach (var o in s.objs) if (!o.done && o.kind == ObjKind.Enemy) { float sy = s.Osy(o); if (sy > 0 && sy < Field.SQY) c++; }
            return c;
        }

        /// <summary>Bölümü baştan sona oynatır. Dönen: kazandı mı, yıldız, süre.</summary>
        public static (bool win, int stars, float time) Play(int lv, BattleMods mods, int fails, int seed, float skill = 1f)
        {
            var s = new BattleSim(lv, mods, fails, seed);
            var bot = new AutoPilot(seed) { skill = skill };
            const float dt = 1f / 30f;
            while (s.Live && s.t < 400) { bot.Tick(s, dt); s.Step(dt); }
            return (s.state == BattleState.Won, s.stars, s.t);
        }
    }

    /// <summary>Oyuncunun 1'den 100'e ilerleyişini simüle eder: bölümleri bot oynar, ödülleri ve üretimi toplar,
    /// parayı en ucuz yükseltmeye harcar, tek inşaatçıyla bekler. Sonuç bölüm bölüm CSV rapordur.</summary>
    public static class BalanceModel
    {
        public class Row
        {
            public int lv, attempts, stars; public float hours; public int gold, iron, food, gems;
            public float avgBld; public int hq, hero, weapon; public float battleSec;
        }

        public class Options
        {
            public float skill = 1f;                // oyuncu becerisi
            public float sessionMinutes = 20;      // oturum uzunluğu
            public float breakHours = 4;           // oturumlar arası çevrimdışı süre
            public bool watchDoubleAds = false;    // ödülü 2 katına çıkaran reklamı izler mi
            public int maxAttempts = 12;           // bir bölümde vazgeçmeden önce deneme
            public int seed = 1;
        }

        public static List<Row> Run(Options o)
        {
            var d = new SaveData(); SaveSystem.Migrate(d);
            var rows = new List<Row>();
            double clock = 0;          // oyun içi geçen gerçek süre (sn)
            double sessionLeft = o.sessionMinutes * 60;
            int seed = o.seed;

            void Pass(double sec)
            {
                clock += sec;
                // üretim (dakikada) - 8 saat sınırı
                float mins = (float)(sec / 60);
                d.pendGold = Math.Min(BaseLogic.RateGold(d) * BaseLogic.CapMinutes, d.pendGold + BaseLogic.RateGold(d) * mins);
                d.pendIron = Math.Min(BaseLogic.RateIron(d) * BaseLogic.CapMinutes, d.pendIron + BaseLogic.RateIron(d) * mins);
                d.pendFood = Math.Min(BaseLogic.RateFood(d) * BaseLogic.CapMinutes, d.pendFood + BaseLogic.RateFood(d) * mins);
                // inşaat bitişi
                if (d.buildKind >= 0 && clock * 1000 >= d.buildEnd) { d.buildings[d.buildKind] = d.buildFrom + 1; d.buildKind = -1; }
            }
            void Collect()
            {
                d.gold += (int)d.pendGold; d.iron += (int)d.pendIron; d.food += (int)d.pendFood;
                d.pendGold -= (int)d.pendGold; d.pendIron -= (int)d.pendIron; d.pendFood -= (int)d.pendFood;
            }
            bool Spend(string r, int a)
            {
                if (a <= 0) return true;
                if (r == "gold") { if (d.gold < a) return false; d.gold -= a; }
                else if (r == "iron") { if (d.iron < a) return false; d.iron -= a; }
                else { if (d.food < a) return false; d.food -= a; }
                return true;
            }
            void SpendAll()
            {
                Collect();
                // Pazar takası: en çok olan kaynaktan en az olana (oyuncu bunu yapar)
                // Bina: inşaatçı boşsa en ucuz yükseltilebilir bina (Karargâh sınıra takılınca önce o)
                if (d.buildKind < 0)
                {
                    BuildingKind? pick = null; int bestCost = int.MaxValue;
                    foreach (var def in BaseLogic.Defs)
                    {
                        var k = def.kind; int L = Progress.Level(d, k);
                        if (L >= Progress.MaxLevel(k) || Progress.TotalStars(d) < BaseLogic.ReqStars(L)) continue;
                        if (k != BuildingKind.Headquarters && L >= Progress.LevelCap(d, k)) continue;
                        int cost = BaseLogic.CostGold(k, L) + BaseLogic.CostIron(k, L) + BaseLogic.CostFood(k, L);
                        if (k == BuildingKind.Headquarters) cost = (int)(cost * .7f); // oyuncu Karargâh'ı öne alır
                        if (cost < bestCost) { bestCost = cost; pick = k; }
                    }
                    if (pick.HasValue)
                    {
                        var k = pick.Value; int L = Progress.Level(d, k);
                        if (d.gold >= BaseLogic.CostGold(k, L) && d.iron >= BaseLogic.CostIron(k, L) && d.food >= BaseLogic.CostFood(k, L))
                        {
                            Spend("gold", BaseLogic.CostGold(k, L)); Spend("iron", BaseLogic.CostIron(k, L)); Spend("food", BaseLogic.CostFood(k, L));
                            d.buildKind = (int)k; d.buildFrom = L; d.buildEnd = (long)(clock * 1000) + BaseLogic.Duration(L) * 1000L;
                        }
                    }
                }
                // Kahraman (altın + elmas)
                int hi = Progress.HeroIndex(d.hero), hl = Progress.HeroLevel(d, hi);
                if (hl < Progress.HeroMax && d.gold >= Progress.HeroCostGold(hl) * 2 && d.diamonds >= Progress.HeroCostGems(hl))
                { d.gold -= Progress.HeroCostGold(hl); d.diamonds -= Progress.HeroCostGems(hl); d.heroLevels[hi]++; }
                // Silah: açık olan en iyi silahı seç ve yükselt
                for (int w = Progress.Weapons.Length - 1; w >= 0; w--) if (Progress.WeaponOpen(d, w)) { d.weapon = Progress.Weapons[w].id; break; }
                int wi = Progress.WeaponIndex(d.weapon), wl = Progress.WeaponLevel(d, wi);
                if (wl < Progress.WeaponMax && d.gold >= Progress.WeaponCostGold(wl) * 2 && d.iron >= Progress.WeaponCostIron(wl))
                { d.gold -= Progress.WeaponCostGold(wl); d.iron -= Progress.WeaponCostIron(wl); d.weaponLevels[wi]++; }
            }

            for (int lv = 1; lv <= Balance.MaxLevel; lv++)
            {
                int attempts = 0, stars = 0; float bsec = 0; bool won = false;
                while (!won && attempts < o.maxAttempts)
                {
                    var mods = Progress.BuildMods(d);
                    var r = AutoPilot.Play(lv, mods, attempts, seed++, o.skill);
                    attempts++; bsec += r.time;
                    double used = r.time + 25; // menü ve geçişler
                    Pass(used); sessionLeft -= used;
                    if (r.win)
                    {
                        won = true; stars = r.stars;
                        var rw = Rewards.ForLevel(lv, r.stars, true, mods);
                        int mul = o.watchDoubleAds ? 2 : 1;
                        d.gold += rw.gold * mul; d.iron += rw.iron * mul; d.food += rw.food * mul; d.diamonds += rw.gems;
                        d.stars[lv - 1] = r.stars; d.unlocked = Math.Max(d.unlocked, lv + 1);
                    }
                    SpendAll();
                    if (sessionLeft <= 0 || (!won && attempts % 3 == 0))
                    {   // oturum bitti ya da üç kez kaybetti: oyuncu ara verir, üs çalışır
                        Pass(o.breakHours * 3600); sessionLeft = o.sessionMinutes * 60;
                        d.diamonds += 5; // günlük giriş/görevden ortalama elmas
                        SpendAll();
                    }
                }
                float avg = 0; for (int i = 0; i < 9; i++) avg += d.buildings[i]; avg /= 9;
                rows.Add(new Row
                {
                    lv = lv, attempts = won ? attempts : -attempts, stars = stars, hours = (float)(clock / 3600),
                    gold = d.gold, iron = d.iron, food = d.food, gems = d.diamonds, avgBld = avg,
                    hq = Progress.Level(d, BuildingKind.Headquarters), hero = Progress.HeroLevel(d, Progress.HeroIndex(d.hero)),
                    weapon = Progress.WeaponLevel(d, Progress.WeaponIndex(d.weapon)), battleSec = bsec / Math.Max(1, attempts)
                });
                if (!won) break;
            }
            return rows;
        }

        /// <summary>Yükseltme yapmadan her bölümün kazanılabilirliği (zorluk eğrisinin çıplak hâli).</summary>
        public static string BareCurve(int seeds = 3)
        {
            var sb = new StringBuilder("bolum;kazanma_orani;ort_yildiz;ort_sure\n");
            var mods = Progress.BuildMods(NewSave());
            for (int lv = 1; lv <= Balance.MaxLevel; lv++)
            {
                int w = 0, st = 0; float tt = 0;
                for (int s = 0; s < seeds; s++) { var r = AutoPilot.Play(lv, mods, 0, 1000 + s * 31 + lv); if (r.win) { w++; st += r.stars; } tt += r.time; }
                sb.AppendLine($"{lv};{w / (float)seeds:0.00};{(w > 0 ? st / (float)w : 0):0.0};{tt / seeds:0}");
            }
            return sb.ToString();
        }

        static SaveData NewSave() { var d = new SaveData(); SaveSystem.Migrate(d); return d; }

        public static string Csv(List<Row> rows)
        {
            var sb = new StringBuilder("bolum;deneme;yildiz;saat;altin;demir;yiyecek;elmas;ort_bina;karargah;kahraman;silah;savas_sn\n");
            foreach (var r in rows)
                sb.AppendLine($"{r.lv};{r.attempts};{r.stars};{r.hours:0.0};{r.gold};{r.iron};{r.food};{r.gems};{r.avgBld:0.0};{r.hq};{r.hero};{r.weapon};{r.battleSec:0}");
            return sb.ToString();
        }
    }
}
