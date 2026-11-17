using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    public class DefTier { public string name; public int waves, req; public float hp, rw; }
    public class DefTowerDef { public string id, name, color; public int cost, req; public float dmg, rate, range; }

    /// <summary>Üs savunması: kapıya yürüyen zombi dalgaları, oyuncu boş yerlere kule kurar.
    /// Kurallar prototipteki savunma moduyla aynı; düşman gücü ve ödüller oyuncunun ilerlemesine göre ölçeklenir.
    /// Koordinatlar oyun pikseli, y aşağı; zombiler aşağıdan (spawnY) yukarı, kapıya (gateY) yürür.</summary>
    public class DefenseSim
    {
        public static readonly DefTier[] Tiers =
        {
            new DefTier { name = "Kolay", waves = 5, hp = .8f, rw = 1f, req = 0 },
            new DefTier { name = "Normal", waves = 7, hp = 1.15f, rw = 1.7f, req = 30 },
            new DefTier { name = "Zor", waves = 10, hp = 1.7f, rw = 2.6f, req = 90 },
        };
        public static readonly DefTowerDef[] Towers =
        {
            new DefTowerDef { id = "arrow", name = "Okçu", cost = 20, dmg = 10, rate = .8f, range = 170, req = 0, color = "#c23b34" },
            new DefTowerDef { id = "cannon", name = "Top", cost = 35, dmg = 20, rate = 1.8f, range = 160, req = 3, color = "#4a4f5a" },
            new DefTowerDef { id = "ice", name = "Buz", cost = 30, dmg = 4, rate = 1f, range = 150, req = 6, color = "#4aa8e8" },
        };
        public const int CooldownMinutes = 15;
        public static long ReadyIn(SaveData d) => Math.Max(0, d.defenseReadyAt - BaseLogic.Now);

        public class Zed { public float x, y, hp, max, spd, dmg, sc = 1.05f, slow, flash, ph; public bool boss, big, run; public int seed; }
        public class Tower { public int slot, type, lv = 1; public float cd = .3f; }
        public class Shot { public float x1, y1, x2, y2, t; public string color; }

        public enum St { Run, Won, Lost }
        public St state = St.Run;
        public readonly DefTier T; public readonly int tier;
        public int wave, left, kills, bossAt, slots;
        public float spawnT, banner, gate, gmax, gold = 100, brk = -1, heroT, flash, t;
        public bool boss;
        public readonly List<Zed> zs = new List<Zed>();
        public readonly List<Tower> ts = new List<Tower>();
        public readonly List<Shot> shots = new List<Shot>();
        public event Action<string, float, float> Fx;

        readonly float gateX, gateY, spawnY, heroX, heroY;
        public readonly float[] slotX, slotY;
        readonly float wallReduce, towerDmg, towerRate, heroDmg, hpScale;
        readonly Random rnd = new Random();
        float R => (float)rnd.NextDouble();

        public DefenseSim(SaveData d, int tierIndex, float gateX, float gateY, float spawnY, float[] slotX, float[] slotY)
        {
            tier = Math.Max(0, Math.Min(Tiers.Length - 1, tierIndex)); T = Tiers[tier];
            this.gateX = gateX; this.gateY = gateY; this.spawnY = spawnY; this.slotX = slotX; this.slotY = slotY;
            heroX = gateX; heroY = gateY - 14;
            int wallL = Progress.Level(d, BuildingKind.Wall), towL = Progress.Level(d, BuildingKind.Tower);
            float wallE = Progress.Eff(d, BuildingKind.Wall), towE = Progress.Eff(d, BuildingKind.Tower), armE = Progress.Eff(d, BuildingKind.Armory);
            gmax = gate = 80 + 25 * wallL;
            wallReduce = (1 - .03f * wallE) * (1 - .04f * Progress.Milestone(d, BuildingKind.Wall));
            towerDmg = 1 + .05f * towE; towerRate = 1f / (1 + .012f * towE);
            int hl = Progress.HeroLevel(d, Progress.HeroIndex(d.hero));
            heroDmg = 6 * (1 + .05f * armE) * (1 + .03f * (hl - 1));
            slots = Math.Min(slotX.Length, 2 + towL / 4);
            hpScale = T.hp * (1 + Progress.TotalStars(d) / 120f);
            towerReq = towL;
            Next();
        }
        readonly int towerReq;
        public int TowerLevel => towerReq;

        void Emit(string k, float x, float y) => Fx?.Invoke(k, x, y);

        void Next()
        {
            wave++; left = 6 + 3 * Math.Min(wave, 8); spawnT = 1.5f; banner = 2.6f;
            boss = wave == T.waves || (T.waves >= 10 && wave == 5); bossAt = left / 2;
            Emit(boss ? "boss" : "board", gateX, gateY);
        }

        public float TowerDamage(Tower q) => Towers[q.type].dmg * (float)Math.Pow(1.5, q.lv - 1) * towerDmg;
        public static int UpgradeCost(Tower q) => q.lv == 1 ? 30 : 50;
        public static int SellValue(Tower q) => Towers[q.type].cost / 2;

        public bool Build(int slot, int type)
        {
            var w = Towers[type];
            if (slot >= slots || TowerAt(slot) != null || gold < w.cost || towerReq < w.req) return false;
            gold -= w.cost; ts.Add(new Tower { slot = slot, type = type }); Emit("board", slotX[slot], slotY[slot]); return true;
        }
        public bool Upgrade(int slot) { var q = TowerAt(slot); if (q == null || q.lv >= 3 || gold < UpgradeCost(q)) return false; gold -= UpgradeCost(q); q.lv++; Emit("power", q == null ? 0 : slotX[slot], slotY[slot]); return true; }
        public void Sell(int slot) { var q = TowerAt(slot); if (q == null) return; gold += SellValue(q); ts.Remove(q); Emit("pickup", slotX[slot], slotY[slot]); }
        public Tower TowerAt(int slot) { foreach (var q in ts) if (q.slot == slot) return q; return null; }

        Zed Nearest(float x, float y, float rng)
        {
            Zed best = null; float bd = rng;
            foreach (var z in zs) { float d = (float)Math.Sqrt((z.x - x) * (z.x - x) + (z.y - y) * (z.y - y)); if (d < bd) { bd = d; best = z; } }
            return best;
        }

        void Fire(float x, float y, Zed z, float dmg, string col)
        {
            z.hp -= dmg; z.flash = 1;
            shots.Add(new Shot { x1 = x, y1 = y, x2 = z.x, y2 = z.y - 14 * z.sc, t = .14f, color = col });
            Emit("hit", z.x, z.y);
        }

        public void Step(float dt)
        {
            t += dt;
            foreach (var sh in shots) sh.t -= dt; shots.RemoveAll(sh => sh.t <= 0);
            if (state != St.Run) return;
            banner = Math.Max(0, banner - dt); flash = Math.Max(0, flash - dt * 3);
            if (left > 0)
            {
                spawnT -= dt;
                if (spawnT <= 0)
                {
                    spawnT = Math.Max(.45f, 1.5f - .12f * Math.Min(wave, 8)); left--;
                    float hp = (10 + 7 * wave) * hpScale * (.9f + R * .2f);
                    var z = new Zed { x = gateX + (R - .5f) * 80, y = spawnY, ph = R * 6, seed = rnd.Next(40), dmg = 1.1f, spd = (19 + R * 8) * 1.5f };
                    if (boss && left == bossAt) { z.boss = z.big = true; hp = 1000 * hpScale * (1 + .12f * wave); z.sc = 2.3f; z.spd = 13; z.dmg = 7; z.seed = 3; Emit("bossin", z.x, z.y); }
                    else if (wave % 3 == 0 && left % 5 == 0) { z.big = true; hp *= 4; z.sc = 1.5f; z.spd = 19; z.dmg = 3.2f; }
                    else if (wave >= 2 && R < .25f) { z.run = true; hp *= .45f; z.spd = (40 + R * 8) * 1.5f; z.sc = .95f; z.dmg = .8f; }
                    z.hp = z.max = hp; zs.Add(z);
                }
            }
            foreach (var z in zs)
            {
                z.ph += dt * 6; z.flash = Math.Max(0, z.flash - dt * 5); z.slow = Math.Max(0, z.slow - dt);
                float ty = gateY + 18 + (z.boss ? 14 : 0), k = z.slow > 0 ? .45f : 1;
                if (z.y > ty) { z.y -= z.spd * k * dt; z.x += (gateX + (float)Math.Sin(z.ph * .3f) * 18 - z.x) * dt * .6f; }
                else { gate -= z.dmg * wallReduce * dt; flash = Math.Max(flash, .5f); if (R < dt * 3) Emit("hurt", z.x, z.y); }
            }
            heroT -= dt;
            if (heroT <= 0) { var hz = Nearest(heroX, heroY, 200); if (hz != null) { heroT = .45f; Fire(heroX, heroY - 18, hz, heroDmg, "#c9b8ff"); } }
            foreach (var q in ts)
            {
                q.cd -= dt; if (q.cd > 0) continue;
                var w = Towers[q.type]; float sx = slotX[q.slot], sy = slotY[q.slot] - 30;
                var tz = Nearest(slotX[q.slot], slotY[q.slot], w.range); if (tz == null) continue;
                q.cd = w.rate * towerRate; float dm = TowerDamage(q);
                if (w.id == "arrow") Fire(sx, sy, tz, dm, "#ffd23f");
                else if (w.id == "ice")
                {
                    Fire(sx, sy, tz, dm, "#9fe0ff"); tz.slow = 2.2f;
                    foreach (var o in zs) if (o != tz && Dist(o, tz) < 30) { o.slow = 2.2f; o.hp -= dm * .5f; }
                }
                else
                {
                    Fire(sx, sy, tz, dm, "#ff9a3a"); Emit("boom", tz.x, tz.y);
                    foreach (var o in zs) if (o != tz && Dist(o, tz) < 46) { o.hp -= dm * .7f; o.flash = 1; }
                }
            }
            for (int i = zs.Count - 1; i >= 0; i--)
            {
                var z = zs[i]; if (z.hp > 0) continue;
                int g = z.boss ? 60 : z.big ? 14 : 5; gold += g; kills++; zs.RemoveAt(i);
                Emit(z.boss ? "bigkill" : "kill", z.x, z.y); Emit("+" + g, z.x, z.y - 30);
            }
            if (gate <= 0) { gate = 0; state = St.Lost; Emit("lose", gateX, gateY); return; }
            if (left == 0 && zs.Count == 0)
            {
                if (brk < 0) brk = 2.2f; brk -= dt;
                if (brk <= 0) { brk = -1; if (wave >= T.waves) { state = St.Won; Emit("win", gateX, gateY); } else Next(); }
            }
        }

        static float Dist(Zed a, Zed b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.y - b.y) * (a.y - b.y));

        /// <summary>Savunma sonu ödülü. İlerledikçe büyür, böylece geç oyunda da anlamlı kalır.</summary>
        public LevelReward Reward(SaveData d)
        {
            int st = Progress.TotalStars(d); float prog = 1 + Math.Max(1, d.unlocked) / 20f, m = T.rw * prog;
            if (state != St.Won) return new LevelReward { gold = (int)(10 * wave * prog) };
            return new LevelReward
            {
                gold = (int)Math.Round((100 + Math.Min(300, st * 2)) * m),
                iron = (int)Math.Round((60 + Math.Min(200, st)) * m),
                food = (int)Math.Round((60 + Math.Min(200, st)) * m),
                gems = 2 + tier * 2,
            };
        }
    }
}
