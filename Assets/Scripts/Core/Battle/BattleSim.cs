using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Battle
{
    public enum BattleState { Play, Boss, Won, Lost }

    public class Bullet { public float x, y, vx, vy, dmg, life; public bool blast; }
    public class EnemyBullet { public float x, y, vx, vy, dmg; public string color; }
    public class FloatText { public float x, y, life; public string text, color; }

    public class BossState
    {
        public float x, y, hp, max, chip, sc, flash, sum, shootT = 1.5f, acc, accT;
        public string color, name;
        public bool isBoss;
    }

    /// <summary>Tek bir bölümün savaş simülasyonu. Çizimden bağımsızdır: Unity sahnesi bunu okuyup çizer.
    /// Kurallar prototipteki step() fonksiyonuyla aynıdır.</summary>
    public class BattleSim
    {
        public readonly int lv;
        public readonly int map;
        public readonly LevelLayout L;
        public readonly BattleMods mods;
        public readonly float mapScale;
        readonly System.Random rnd;

        public BattleState state = BattleState.Play;
        public float n, hv0, hhp, peak;   // n = toplam güç (kahraman + askerler)
        public float x = Field.W / 2, tx = Field.W / 2;
        public float scroll, t, cd, fireT, endScroll;
        public float buffDouble, buffRapid, buffBlast;
        public int assist;                // üst üste yenilgi desteği (0-4)
        public bool revived;
        public BossState boss;
        public int stars;

        public readonly List<BattleObj> objs;
        public readonly List<Bullet> bullets = new List<Bullet>();
        public readonly List<EnemyBullet> eb = new List<EnemyBullet>();
        public readonly List<FloatText> floats = new List<FloatText>();

        /// <summary>Görünüm için olaylar: vuruş, patlama, ölüm vb. (ses ve efekt burada bağlanır).</summary>
        public event Action<string, float, float> Fx;

        public BattleSim(int level, BattleMods m = null, int fails = 0, int seed = 0)
        {
            lv = level; mods = m ?? new BattleMods();
            map = BattleData.MapOf(lv); mapScale = BattleData.MapScale(map);
            rnd = new System.Random(seed == 0 ? Environment.TickCount : seed);
            L = LevelGen.Build(lv);
            objs = L.objs;
            assist = Math.Min(4, fails);
            float st0 = (float)Math.Ceiling(Balance.StartSoldiers(lv) * mods.startMul) + mods.startBonus;
            n = hv0 = peak = st0;
            hhp = HeroMax;
            endScroll = Field.GAP * (L.rows + 1);
        }

        public float HeroMax => hv0 * mods.heroHpMul;
        public float Soldiers => Math.Max(0, n - hv0);
        public float Radius => 14 + (float)Math.Sqrt(Math.Max(Soldiers, 1)) * 4;
        public float SkillCdMax => Field.SKILL_CD * mods.skillCd;
        public bool Live => state == BattleState.Play || state == BattleState.Boss;
        public float Osy(BattleObj o) => o.wy + scroll + o.adv;
        float Hw(EnemyType T) => T.hw * mapScale;
        float Sc(EnemyType T) => T.sc * mapScale;
        float Rand => (float)rnd.NextDouble();
        void Emit(string kind, float fx, float fy) => Fx?.Invoke(kind, fx, fy);

        public void AddFloat(float fx, float fy, string text, string color) =>
            floats.Add(new FloatText { x = fx, y = fy, text = text, color = color, life = 1f });

        /// <summary>Hasarı önce askerler, sonra kahraman alır.</summary>
        public void DamageSquad(float d)
        {
            float s0 = Math.Max(0, n - hv0), ab = Math.Min(d, s0);
            n -= ab; d -= ab;
            if (d > 0) { hhp -= d; if (hhp <= 0) { hhp = 0; n = 0; } }
        }

        void HitBarrel(BattleObj o, float dm)
        {
            if (o.done) return;
            o.hp -= dm; o.flash = .1f;
            float tgt = o.hp <= 0 ? o.v : (float)Math.Floor(o.v * (1 - o.hp / o.max)), nw = tgt - o.rel, sy = Osy(o);
            if (nw > 0)
            {
                o.rel = tgt; n += nw; if (n > peak) peak = n;
                AddFloat(o.x, sy - 22, "+" + nw, "#8be39d"); Emit("pickup", o.x, sy);
            }
            if (o.hp <= 0)
            {
                o.done = true; Emit("barrel", o.x, sy);
                if (o.kind == ObjKind.Power)
                {
                    if (o.pk == Power.Double) buffDouble = 10; else if (o.pk == Power.Rapid) buffRapid = 10; else buffBlast = 10;
                    AddFloat(o.x, sy - 40, PowerName(o.pk), "#ffffff"); Emit("power", o.x, sy);
                }
            }
        }

        public static string PowerName(Power p) => p == Power.Double ? "ÇİFT ATEŞ" : p == Power.Rapid ? "HIZLI ATEŞ" : "PATLAYICI";

        void KillEnemy(BattleObj e, float sy)
        {
            e.done = true; e.hp = 0;
            Emit(e.type.big ? "bigkill" : "kill", e.x, sy);
            if (e.type.split)
            {
                for (int k = -1; k <= 1; k += 2)
                {
                    float hp = Math.Max(3, (float)Math.Ceiling(e.max * .3f));
                    objs.Add(new BattleObj { kind = ObjKind.Enemy, type = BattleData.Types["walker"], x = Math.Max(40, Math.Min(Field.W - 40, e.x + k * 22)),
                        wy = sy - scroll, max = hp, hp = hp, chip = hp, contact = Math.Max(1, (float)Math.Ceiling(e.contact * .4f)), seed = e.seed + k });
                }
            }
        }

        void Hurt(BattleObj e, float sy, float dmg)
        {
            if (e.type.ghost) dmg *= .5f;
            e.hp -= dmg; e.flash = .08f; e.acc += dmg; e.adv -= e.type.big ? .1f : .3f;
            if (e.hp <= 0) KillEnemy(e, sy);
        }

        void Explode(float ex, float ey, float dmg, BattleObj skip)
        {
            Emit("boom", ex, ey);
            foreach (var e in objs.ToArray())
            {
                if (e.done || e.kind != ObjKind.Enemy || e == skip) continue;
                float y2 = Osy(e); if (Math.Abs(e.x - ex) < 62 && Math.Abs(y2 - ey) < 50) Hurt(e, y2, dmg * .5f);
            }
            if (boss != null && Math.Abs(boss.x - ex) < 70 + 10 * boss.sc && Math.Abs(boss.y - 10 - ey) < 60 + 12 * boss.sc) { boss.hp -= dmg * .5f; boss.flash = .08f; }
        }

        void EnemyShoot(float sx, float sy, float spd, float dm, string col, float spread = 0)
        {
            float dx = x + spread - sx, dy = Field.SQY - sy, len = Math.Max(1, (float)Math.Sqrt(dx * dx + dy * dy));
            eb.Add(new EnemyBullet { x = sx, y = sy, vx = dx / len * spd, vy = dy / len * spd, dmg = dm, color = col });
        }

        float EnemyShotDamage(BattleObj o) => Math.Max(1, (float)Math.Ceiling(Math.Min(o.contact * .5f, n * .1f) * mods.guard));

        public bool SkillReady => cd <= 0 && Live;

        public void UseSkill()
        {
            if (!SkillReady) return;
            cd = SkillCdMax; Emit("skill", x, Field.SQY);
            float dmg = Balance.DpsOf(n, lv) * mods.skillMul * mods.dmgMul * (1 + .3f * assist);
            foreach (var o in objs.ToArray())
            {
                if (o.done) continue;
                float sy = Osy(o); if (sy < -10 || sy > Field.SQY) continue;
                if (o.kind == ObjKind.Enemy) { AddFloat(o.x, sy - 20, Fmt(dmg), "#c9b8ff"); Hurt(o, sy, dmg); }
                else if (o.kind == ObjKind.Barrel || o.kind == ObjKind.Power) HitBarrel(o, dmg * .6f);
                else if (o.kind == ObjKind.Board)
                {
                    // Yetenek tabelayı yarı değerli fıçıya çevirir
                    o.kind = ObjKind.Barrel; o.v = Math.Max(3, (float)Math.Round(o.v / 2)); o.rel = 0;
                    o.max = o.hp = Math.Max(4, (float)Math.Ceiling(Balance.DpsOf(hv0, lv) * .5f)); o.chip = o.hp;
                    AddFloat(o.x, sy, "Fıçı", "#c9b8ff");
                }
            }
            if (boss != null) { boss.hp -= dmg; boss.flash = .15f; AddFloat(boss.x, boss.y - 40, Fmt(dmg), "#c9b8ff"); CheckBossDead(); }
        }

        void CheckBossDead()
        {
            if (boss != null && boss.hp <= 0 && state == BattleState.Boss) { boss.hp = 0; Emit("bossdead", boss.x, boss.y); Finish(true); }
        }

        /// <summary>Ödüllü reklam sonrası ikinci şans: güç yarıya kadar döner, düşman mermileri silinir.</summary>
        public bool CanRevive => state == BattleState.Lost && !revived;
        public void Revive()
        {
            if (!CanRevive) return;
            revived = true;
            n = Math.Max(hv0, (float)Math.Ceiling(peak * .5f));
            hhp = HeroMax;
            eb.Clear();
            foreach (var o in objs) if (!o.done && o.kind == ObjKind.Enemy && Osy(o) > Field.SQY - 140) o.done = true;
            state = boss != null ? BattleState.Boss : BattleState.Play;
            AddFloat(x, Field.SQY - 60, "İKİNCİ ŞANS", "#8be39d");
            Emit("revive", x, Field.SQY);
        }

        void Finish(bool win)
        {
            state = win ? BattleState.Won : BattleState.Lost;
            bullets.Clear();
            if (win) { float ratio = n / (.33f * L.efin); stars = ratio >= 1 ? 3 : ratio >= .5f ? 2 : 1; }
            Emit(win ? "win" : "lose", x, Field.SQY);
        }

        public static string Fmt(float v)
        {
            v = Math.Max(0, (float)Math.Ceiling(v));
            if (v >= 10000) return Math.Round(v / 1000) + "K";
            if (v >= 1000) return (v / 1000).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "K";
            return v.ToString("0");
        }

        public void Step(float dt)
        {
            t += dt;
            if (cd > 0) { cd = Math.Max(0, cd - dt); if (cd == 0 && Live) AddFloat(x, Field.SQY - 70, "YETENEK HAZIR", "#c9b8ff"); }
            buffDouble = Math.Max(0, buffDouble - dt); buffRapid = Math.Max(0, buffRapid - dt); buffBlast = Math.Max(0, buffBlast - dt);

            x += (tx - x) * Math.Min(1, dt * 14);
            float rad = Radius;
            x = Math.Max(rad * .6f + 20, Math.Min(Field.W - rad * .6f - 20, x));

            if (Live)
            {
                scroll += Field.SPEED * Math.Min(1, .35f + t * .65f) * dt;
                float contactR = 38 + rad * .6f; bool anyTarget = false;
                foreach (var o in objs.ToArray())
                {
                    if (o.done) continue;
                    float sy = Osy(o);
                    if (o.kind == ObjKind.Enemy)
                    {
                        var T = o.type;
                        if (sy > -30)
                        {
                            o.adv += T.spd * dt;
                            float dx = x - o.x, mv = (T.spd > 40 ? 40 : 22) * dt; o.x += Math.Max(-mv, Math.Min(mv, dx));
                            anyTarget = true;
                            if (T.shootInt > 0 && sy > 0 && sy < Field.SQY - 90)
                            {
                                o.shootT = (o.shootT < 0 ? .3f + Rand * .6f : o.shootT) - dt;
                                if (o.shootT <= 0) { o.shootT = T.shootInt * (.8f + Rand * .4f); EnemyShoot(o.x, sy - 12, T.shootSpd, EnemyShotDamage(o), T.shootColor); Emit("spit", o.x, sy); }
                            }
                        }
                        if (o.flash > 0) o.flash -= dt;
                        if (o.chip > o.hp) o.chip = Math.Max(o.hp, o.chip - o.max * dt * 1.4f);
                        o.accT += dt; if (o.acc > 0 && o.accT > .35f) { AddFloat(o.x + 8, sy - 30, Fmt(o.acc), "#ffe9a8"); o.acc = 0; o.accT = 0; }
                        if (sy > Field.SQY - 26 && Math.Abs(o.x - x) < contactR + Hw(T) * .4f)
                        {
                            float cdm = Math.Max(1, Math.Min(o.contact, (float)Math.Ceiling(n * Math.Min(.35f, T.dmg * 1.6f))));
                            cdm = Math.Max(1, (float)Math.Ceiling(cdm * mods.guard));
                            o.done = true; DamageSquad(cdm); AddFloat(o.x, sy - 10, "−" + cdm, "#ff8a80"); Emit("hurt", o.x, sy);
                        }
                        else if (sy > Field.H + 30) o.done = true;
                    }
                    else if (o.kind == ObjKind.Board)
                    {
                        if (sy > 0 && sy < Field.SQY) anyTarget = true;
                        if (sy > Field.SQY - 22 && sy < Field.SQY + 22 && Math.Abs(o.x - x) < contactR)
                        {
                            float pv = Math.Max(1, (float)Math.Ceiling(Math.Ceiling(o.v) * mods.guard));
                            o.done = true; DamageSquad(pv); AddFloat(o.x, sy, "−" + pv, "#ff8a80"); Emit("hurt", o.x, sy);
                        }
                        else if (sy > Field.H + 30) o.done = true;
                    }
                    else
                    {
                        if (o.flash > 0) o.flash -= dt;
                        if (sy > 0 && sy < Field.SQY) anyTarget = true;
                        if (sy > Field.SQY + 30) o.done = true;
                    }
                }
                if (boss != null) anyTarget = true;

                // Ateş
                fireT += dt;
                float fi = (buffRapid > 0 ? Balance.Fire * .55f : Balance.Fire) * mods.fireMul;
                if (fireT >= fi)
                {
                    fireT = 0;
                    if (anyTarget && n >= 1)
                    {
                        bool dbl = buffDouble > 0, blast = buffBlast > 0;
                        int nb = Math.Min(10, 1 + (int)Math.Floor(n / 5));
                        float each = n * Balance.DmgPer(lv) * mods.dmgMul / nb * (dbl ? .8f : 1) * (1 + .3f * assist);
                        for (int j = 0; j < nb; j++)
                            for (int dd = 0; dd < (dbl ? 2 : 1); dd++)
                            {
                                float bx = x + (Rand - .5f) * rad * 1.4f + (dbl ? (dd == 1 ? 4 : -4) : 0);
                                bullets.Add(new Bullet { x = bx, y = Field.SQY - 14 - dd * 6, vy = -520, dmg = each, blast = blast, life = 9 });
                            }
                        Emit("shot", x, Field.SQY);
                    }
                }

                // Mermiler
                for (int bi = bullets.Count - 1; bi >= 0; bi--)
                {
                    var bu = bullets[bi]; bool gone = false;
                    BattleObj near = null; float nd = 1e9f;
                    foreach (var o in objs)
                    {
                        if (o.done || o.kind != ObjKind.Enemy) continue;
                        float sy = Osy(o);
                        if (sy < bu.y && sy > -20 && Math.Abs(o.x - bu.x) < 90) { float d = bu.y - sy; if (d < nd) { nd = d; near = o; } }
                    }
                    if (near != null) bu.vx = Math.Max(-140, Math.Min(140, (near.x - bu.x) * 4));
                    else if (boss != null && boss.y > 0 && bu.y > boss.y) bu.vx = Math.Max(-140, Math.Min(140, (boss.x - bu.x) * 3));
                    else bu.vx *= .9f;
                    bu.y += bu.vy * dt; bu.x += bu.vx * dt; bu.life -= dt;
                    if (bu.y < -12 || bu.x < 0 || bu.x > Field.W || bu.life <= 0) gone = true;

                    for (int ej = eb.Count - 1; ej >= 0 && !gone; ej--)
                    {
                        var e2 = eb[ej];
                        if (Math.Abs(bu.x - e2.x) < 10 && Math.Abs(bu.y - e2.y) < 12) { eb.RemoveAt(ej); gone = true; Emit("block", e2.x, e2.y); }
                    }
                    if (!gone)
                    {
                        for (int k = 0; k < objs.Count && !gone; k++)
                        {
                            var o2 = objs[k]; if (o2.done) continue;
                            float sy2 = Osy(o2);
                            if (o2.kind == ObjKind.Enemy)
                            {
                                var T2 = o2.type; float hh2 = 15 * Sc(T2) * (T2.big ? 1 : 1.4f);
                                if (sy2 > -10 && Math.Abs(bu.x - o2.x) < Hw(T2) && bu.y < sy2 + hh2 * .6f && bu.y > sy2 - hh2 * 1.7f)
                                { Hurt(o2, sy2, bu.dmg); Emit("hit", bu.x, bu.y); if (bu.blast) Explode(bu.x, bu.y, bu.dmg, o2); gone = true; }
                            }
                            else if (o2.kind == ObjKind.Barrel || o2.kind == ObjKind.Power)
                            {
                                if (sy2 > -5 && Math.Abs(bu.x - o2.x) < 32 && bu.y < sy2 + 16 && bu.y > sy2 - 26) { HitBarrel(o2, bu.dmg); gone = true; }
                            }
                            else if (o2.kind == ObjKind.Board)
                            {
                                if (sy2 > 0 && Math.Abs(bu.x - o2.x) < 38 && bu.y < sy2 + 14 && bu.y > sy2 - 26)
                                {
                                    o2.v -= .34f; gone = true;
                                    if (o2.v <= .4f) { o2.done = true; AddFloat(o2.x, sy2, "Yıkıldı", "#8be39d"); Emit("board", o2.x, sy2); }
                                }
                            }
                        }
                        if (!gone && boss != null && Math.Abs(bu.x - boss.x) < 12 + 11 * boss.sc && bu.y < boss.y + 8 && bu.y > boss.y - 22 * boss.sc)
                        {
                            boss.hp -= bu.dmg; boss.flash = .08f; boss.acc += bu.dmg; gone = true; Emit("hit", bu.x, bu.y);
                            if (bu.blast) Explode(bu.x, bu.y, bu.dmg, null);
                        }
                    }
                    if (gone) bullets.RemoveAt(bi);
                }

                if (boss != null)
                {
                    if (boss.flash > 0) boss.flash -= dt;
                    if (boss.chip > boss.hp) boss.chip = Math.Max(boss.hp, boss.chip - boss.max * dt * 1.2f);
                    boss.accT += dt; if (boss.acc > 0 && boss.accT > .4f) { AddFloat(boss.x + (Rand - .5f) * 60, boss.y - 22 * boss.sc, Fmt(boss.acc), "#ffe9a8"); boss.acc = 0; boss.accT = 0; }
                    CheckBossDead();
                }

                if (state == BattleState.Play)
                {
                    if (scroll >= endScroll)
                    {
                        var F = L.fin;
                        state = BattleState.Boss;
                        boss = new BossState { x = Field.W / 2, y = -170, hp = F.hp, max = F.hp, chip = F.hp, sc = F.sc, color = F.color, name = F.name, isBoss = F.boss };
                        Emit("bossin", boss.x, 0);
                    }
                }
                else if (boss != null && state == BattleState.Boss)
                {
                    if (boss.y < 170) boss.y += 80 * dt;
                    else
                    {
                        DamageSquad(Math.Max(.3f, n / Balance.SquadTime(lv) * .55f * mods.guard) * dt);
                        boss.sum += dt; boss.shootT -= dt;
                        if (boss.shootT <= 0)
                        {
                            boss.shootT = boss.isBoss ? 2.6f : 3.4f;
                            float bdm = Math.Max(1, (float)Math.Ceiling(n * (boss.isBoss ? .04f : .03f) * mods.guard));
                            for (int sp = boss.isBoss ? -1 : 0; sp <= (boss.isBoss ? 1 : 0); sp++) EnemyShoot(boss.x + sp * 24, boss.y + 10, 150, bdm, "#ff6a5a", sp * 55);
                            Emit("spit", boss.x, boss.y);
                        }
                        if (boss.isBoss && boss.sum >= 7)
                        {
                            boss.sum = 0;
                            float mhp = Math.Max(4, (float)Math.Ceiling(Balance.DpsOf(.33f * L.efin, lv) * Balance.Tw(lv) * .5f));
                            for (int s2 = -1; s2 <= 1; s2 += 2)
                                objs.Add(new BattleObj { kind = ObjKind.Enemy, type = BattleData.Types["walker"], x = boss.x + s2 * 70, wy = boss.y + 40 - scroll,
                                    max = mhp, hp = mhp, chip = mhp, contact = Math.Max(1, (float)Math.Ceiling(L.efin * .04f)), seed = s2 + 3 });
                        }
                    }
                }
                if (Live && n < .5f) { n = 0; Finish(false); }
            }

            foreach (var f in floats) { f.y -= 40 * dt; f.life -= dt * .9f; }
            floats.RemoveAll(f => f.life <= 0);

            for (int ei = eb.Count - 1; ei >= 0; ei--)
            {
                var e = eb[ei]; e.x += e.vx * dt; e.y += e.vy * dt;
                if (Live && e.y > Field.SQY - 24 && Math.Abs(e.x - x) < 38 + Radius * .6f)
                { DamageSquad(e.dmg); AddFloat(e.x, e.y - 10, "−" + e.dmg, "#ff8a80"); Emit("hurt", e.x, e.y); eb.RemoveAt(ei); }
                else if (e.y > Field.H + 20 || e.x < -20 || e.x > Field.W + 20 || !Live) eb.RemoveAt(ei);
            }
            if (objs.Count > 90) objs.RemoveAll(o => o.done && Osy(o) >= Field.H);
            if (Live && n < .5f) { n = 0; Finish(false); }
        }
    }
}
