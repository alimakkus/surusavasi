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
                        case ObjKind.Gate: sc += s.GateWorth(o) * w * 1.5f; break;
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
        public static (bool win, int stars, float time) Play(int lv, BattleMods mods, int fails, int seed, float skill = 1f, float hardMul = 1f)
        {
            var s = new BattleSim(lv, mods, fails, seed, hardMul);
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
            public int maxAttempts = 80;           // bir bölümde vazgeçmeden önce deneme
            public int seed = 1;
            public int builders = 1;               // 2: ikinci inşaatçıyı almış oyuncu
        }

        public static List<Row> Run(Options o)
        {
            var d = new SaveData(); SaveSystem.Migrate(d); d.builders = o.builders;
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
                while (BaseLogic.CompleteAt(d, (long)(clock * 1000), false) != null) { }
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
                while (BaseLogic.FreeSlot(d) >= 0)
                {
                    BuildingKind? pick = null; int bestCost = int.MaxValue;
                    foreach (var def in BaseLogic.Defs)
                    {
                        var k = def.kind; int L = Progress.Level(d, k);
                        if (L >= Progress.MaxLevel(k) || Progress.TotalStars(d) < BaseLogic.ReqStars(L) || BaseLogic.Busy(d, k)) continue;
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
                            BaseLogic.SetSlot(d, BaseLogic.FreeSlot(d), (int)k, L, (long)(clock * 1000) + BaseLogic.Duration(L) * 1000L);
                        }
                        else break;
                    }
                    else break;
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

        /// <summary>Oyuncunun bu güçle kaldırabileceği en yüksek ek zorluk çarpanı (3 denemenin 2'si kazanılır).</summary>
        public static float Headroom(int lv, BattleMods mods, float skill = 1f, int iters = 12)
        {
            float lo = .05f, hi = 20000f;
            for (int it = 0; it < iters; it++)
            {
                float mid = (float)Math.Sqrt(lo * hi); int w = 0;
                for (int s = 0; s < 3; s++) if (AutoPilot.Play(lv, mods, 0, 500 + s * 17 + lv, skill, mid).win) w++;
                if (w >= 2) lo = mid; else hi = mid;
            }
            return (float)Math.Sqrt(lo * hi);
        }

        /// <summary>Yükseltme seviyesi verilen hayali oyuncunun savaş gücü (kayıt).</summary>
        public static SaveData Upgraded(int bld, int hq, int hero, int weapon, string heroId = "ayaz")
        {
            var d = NewSave();
            for (int i = 0; i < 9; i++) d.buildings[i] = bld;
            d.buildings[(int)BuildingKind.Headquarters] = hq;
            d.hero = heroId; for (int i = 0; i < 3; i++) d.heroLevels[i] = hero;
            for (int w = Progress.Weapons.Length - 1; w >= 0; w--) if (Progress.WeaponOpen(d, w)) { d.weapon = Progress.Weapons[w].id; break; }
            d.weaponLevels[Progress.WeaponIndex(d.weapon)] = weapon;
            for (int i = 0; i < d.stars.Length; i++) d.stars[i] = 3;
            return d;
        }

        public static string HeadroomCurve()
        {
            var prof = new (string name, SaveData d)[]
            {
                ("yukseltmesiz", NewSave()), ("bina5", Upgraded(5, 3, 3, 1)), ("bina10", Upgraded(10, 5, 5, 3)),
                ("bina15", Upgraded(15, 8, 8, 4)), ("bina20", Upgraded(20, 10, 10, 5)),
            };
            var sb = new StringBuilder("bolum"); foreach (var p in prof) sb.Append(";" + p.name); sb.AppendLine();
            foreach (int lv in new[] { 1, 5, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
            {
                sb.Append(lv);
                foreach (var p in prof) sb.Append(";" + Headroom(lv, Progress.BuildMods(p.d)).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>Hedef ilerleme: bölüm lv'ye gelen oyuncunun beklenen yükseltmeleri.</summary>
        public static SaveData Target(int lv)
        {
            int bld = Math.Min(20, (int)Math.Round(lv / 5.5f));
            return Upgraded(bld, Math.Max(1, (bld + 1) / 2), Math.Min(10, 1 + lv / 11), Math.Min(5, lv / 18));
        }

        /// <summary>Hedef oyuncunun güç payından zorluk tablosunu çıkarır (her 5 bölümde bir).
        /// Pay: ilk bölümlerde bol, sonra 2 kat (insan oyuncu bottan zayıf olduğu için).</summary>
        public static string Calibrate()
        {
            var sb = new StringBuilder("bolum;hedef_bina;hedef_pay;yukseltmesiz_pay;zorluk\n");
            var pts = new List<int> { 1 }; for (int l = 5; l <= 100; l += 5) pts.Add(l);
            var tbl = new List<float>(); float prev = 1;
            foreach (int lv in pts)
            {
                float hT = Headroom(lv, Progress.BuildMods(Target(lv)));
                float h0 = Headroom(lv, Progress.BuildMods(NewSave()));
                float margin = lv <= 5 ? 4f : lv <= 15 ? 2.6f : 2.1f;
                float D = Math.Max(prev, Math.Max(1, hT / margin)); prev = D; tbl.Add(D);
                sb.AppendLine($"{lv};{Math.Round(lv / 5.5f)};{hT:0.0};{h0:0.0};{D:0.00}");
            }
            sb.Append("TABLO: ");
            foreach (var v in tbl) sb.Append(v.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "f, ");
            return sb.ToString();
        }

        /// <summary>Her bölümün kendine özgü zorluğunu sabit bir oyuncuyla ölçer; komşu ortalamasından sapmayı düzeltme çarpanına çevirir.
        /// Dönen dizi LevelTuning tablosudur.</summary>
        public static float[] LevelFactors(Action<float> progress = null)
        {
            int N = Balance.MaxLevel; var lh = new double[N];
            for (int lv = 1; lv <= N; lv++)
            {
                progress?.Invoke(lv / (float)N);
                var d = Target(lv);
                float h = Headroom(lv, Progress.BuildMods(d), 1f, 8) * LevelTuning.Factor(lv); // mevcut düzeltmeyi geri al
                lh[lv - 1] = Math.Log(Math.Max(.01, h));
            }
            var f = new float[N];
            for (int i = 0; i < N; i++)
            {
                // komşu ortalaması (±5 bölüm, kendisi hariç), patron bölümleri kendi aralarında
                double sum = 0; int c = 0;
                for (int j = Math.Max(0, i - 5); j <= Math.Min(N - 1, i + 5); j++)
                    if (j != i && ((j + 1) % 10 == 0) == ((i + 1) % 10 == 0)) { sum += lh[j]; c++; }
                if ((i + 1) % 10 == 0) { sum = 0; c = 0; for (int j = Math.Max(9, i - 20); j <= Math.Min(N - 1, i + 20); j += 10) if (j != i) { sum += lh[j]; c++; } }
                double avg = c > 0 ? sum / c : lh[i];
                f[i] = (float)Math.Min(2.5, Math.Max(.4, Math.Exp(lh[i] - avg)));
            }
            return f;
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
