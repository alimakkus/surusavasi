using System;
using System.Collections.Generic;
using System.Linq;

namespace SuruSavasi.Core.Battle
{
    public enum ObjKind { Barrel, Power, Board, Enemy, Gate }

    /// <summary>Kapı etkisi. Kapılar yolu ikiye bölen çiftler halinde gelir; ordu hangi yarıdan geçerse o uygulanır.</summary>
    public enum GateFx { Add, Mul, Dmg, Fire, Heal, Sub, MulDown }

    public class BattleObj
    {
        public ObjKind kind;
        public float x, wy, adv;
        public bool done;
        public int seed;
        public Power pk;
        public float v;                 // fıçı: içindeki asker, tabela: eksilteceği asker
        public float hp, max, chip, flash, rel;
        public EnemyType type;
        public float contact, acc, accT, shootT = -1f;
        public GateFx gk;               // kapı: etkisi (v = miktar), seed = çift kimliği
    }

    public class FinalFoe
    {
        public bool boss;
        public int bossIndex;     // patron sırası (0-9)
        public string typeId;     // elit ise düşman türü
        public string name;
        public string color;
        public float sc, hp;
    }

    public class LevelLayout
    {
        public List<BattleObj> objs = new List<BattleObj>();
        public int rows;
        public float efin;
        public FinalFoe fin;
        public string fresh;   // bu bölümde ilk kez çıkan düşman
        public float hard = 1; // zorluk çarpanı (düşman canı ve hasarı)
    }

    /// <summary>Prototipteki buildLevel ile aynı kurallar ve aynı rastgele dizilim.</summary>
    public static class LevelGen
    {
        /// <summary>Seçim kapıları: 2. bölümden itibaren her bölümde 1-3 çift. Satırların arasına yerleşir;
        /// mevcut dizilimi bozmamak için ayrı rastgele dizi kullanır.</summary>
        static void AddGates(LevelLayout L, int lv, int R)
        {
            if (lv < 2 || R < 4) return;
            var g = new Rng(lv * 104729 + 71);
            int count = lv < 10 ? 1 : lv < 40 ? 2 : 3;
            float lvMul = 1f + .05f * lv;
            for (int k = 0; k < count; k++)
            {
                int row = Math.Max(1, Math.Min(R - 2, (int)Math.Round((R - 1) * (k + 1f) / (count + 1f))));
                float wy = -Field.GAP * (row + 1) - Field.GAP / 2;
                float add = (float)Math.Ceiling(g.Range(7, 13) * lvMul * Field.BARV * 1.15f);
                GateFx a, b; float va, vb;
                int pick = g.Range(0, lv >= 15 ? 5 : 4);
                switch (pick)
                {
                    case 0: a = GateFx.Add; va = add; b = GateFx.Mul; vb = 1.4f; break;               // toplama mı çarpma mı?
                    case 1: a = GateFx.Add; va = add; b = GateFx.Dmg; vb = .2f; break;               // asker mi güç mü?
                    case 2: a = GateFx.Mul; va = 1.25f; b = GateFx.Fire; vb = 10f; break;     // kalıcı mı anlık mı?
                    case 3: a = GateFx.Heal; va = .5f; b = GateFx.Add; vb = (float)Math.Ceiling(add * .8f); break; // can mı asker mi?
                    case 4: a = GateFx.Sub; va = (float)Math.Ceiling(add * 1.2f); b = GateFx.MulDown; vb = .75f; break; // iki kötüden az kötü
                    default: a = GateFx.Add; va = add; b = GateFx.Mul; vb = 1.4f; break;
                }
                if (g.Next() < .5) { var t = a; a = b; b = t; var tv = va; va = vb; vb = tv; }
                int id = 1000 + k;
                L.objs.Add(new BattleObj { kind = ObjKind.Gate, x = Field.W * .27f, wy = wy, gk = a, v = va, seed = id });
                L.objs.Add(new BattleObj { kind = ObjKind.Gate, x = Field.W * .73f, wy = wy, gk = b, v = vb, seed = id });
            }
        }

        // ---------- Hayatta Kalma dalgaları için tekil nesne üreticileri (bölüm kurallarıyla aynı formüller) ----------
        public static BattleObj MakeEnemy(int lv, int row, string typeId, float H, float x, float wy, double r01, float hpMul = 1)
        {
            var T = BattleData.Types[typeId];
            float E = Balance.EnemyRef(lv, row);
            var o = new BattleObj { kind = ObjKind.Enemy, type = T, x = x, wy = wy, seed = (int)(r01 * 40) };
            o.max = o.hp = o.chip = Math.Max(4, (float)Math.Ceiling(DpsOf(E, lv) * Balance.Tw(lv) * T.mul * (.92 + r01 * .16) * H * hpMul));
            o.contact = Math.Max(1, (float)Math.Ceiling(E * T.dmg * Math.Pow(H, .25)));
            return o;
        }

        public static BattleObj MakeBarrel(int lv, int row, float x, float wy, double r01)
        {
            float lvMul = 1f + .05f * lv;
            var o = new BattleObj { kind = ObjKind.Barrel, x = x, wy = wy, seed = (int)(r01 * 40) };
            o.v = Math.Max(2, (float)Math.Ceiling((4 + r01 * 8) * lvMul * Field.BARV));
            o.max = o.hp = o.chip = Math.Max(4, (float)Math.Ceiling(DpsOf(Balance.EnemyRef(lv, row), lv) * (.28 + .055 * (o.v / lvMul))));
            return o;
        }

        /// <summary>Bu bölümde çıkabilen düşman türleri (sırayla açılanlar + haritaya özel olanlar).</summary>
        public static List<string> TypesFor(int lv)
        {
            int map = BattleData.MapOf(lv);
            var l = BattleData.Order.Where(k => BattleData.Types[k].unlock <= lv).ToList();
            l.AddRange(BattleData.Themed.Where(k => Array.IndexOf(BattleData.Types[k].maps, map) >= 0));
            return l;
        }

        public static string GateText(BattleObj o)
        {
            switch (o.gk)
            {
                case GateFx.Add: return "+" + o.v;
                case GateFx.Mul: return "×" + o.v.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
                case GateFx.Dmg: return $"Hasar +%{o.v * 100:0}";
                case GateFx.Fire: return $"Hızlı ateş {o.v:0} sn";
                case GateFx.Heal: return $"Can +%{o.v * 100:0}";
                case GateFx.Sub: return "−" + o.v;
                case GateFx.MulDown: return "×" + o.v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');
            }
            return "";
        }
        public static bool GateGood(BattleObj o) => o.gk != GateFx.Sub && o.gk != GateFx.MulDown;

        static float DpsOf(float e, int lv) => Balance.DpsOf(e, lv);

        public static LevelLayout Build(int lv, float hardMul = 1)
        {
            var rg = new Rng(lv * 7919 + 13);
            int R = Balance.RowsOf(lv);
            float H = Balance.Difficulty(lv) * hardMul;
            var L = new LevelLayout { rows = R, hard = H };
            int map = BattleData.MapOf(lv);
            var avail = BattleData.Order.Where(k => BattleData.Types[k].unlock <= lv).ToList();
            var themed = BattleData.Themed.Where(k => Array.IndexOf(BattleData.Types[k].maps, map) >= 0).ToList();

            string PickType()
            {
                if (themed.Count > 0 && rg.Next() < .35) return themed[(int)Math.Floor(rg.Next() * themed.Count)];
                var w = new List<int>();
                for (int i = 0; i < avail.Count; i++) w.Add(avail[i] == "spitter" ? 3 : (i >= avail.Count - 2 ? 2 : 1));
                double r = rg.Next() * w.Sum();
                for (int i = 0; i < avail.Count; i++) { r -= w[i]; if (r < 0) return avail[i]; }
                return avail[0];
            }

            Power[] powers = { Power.Double, Power.Rapid, Power.Blast };
            for (int i = 0; i < R; i++)
            {
                float wy = -Field.GAP * (i + 1);
                int bl = (int)Math.Floor(rg.Next() * 3);
                for (int l = 0; l < 3; l++)
                {
                    ObjKind kind; bool none = false;
                    if (i == 0 || l == bl) kind = ObjKind.Barrel;
                    else
                    {
                        double r = rg.Next(), enP = .3 + Math.Min(.2, lv * .003);
                        if (r < .2) kind = ObjKind.Board;
                        else if (r < .2 + enP) kind = ObjKind.Enemy;
                        else if (r < .85) kind = ObjKind.Barrel;
                        else { kind = ObjKind.Barrel; none = true; }
                    }
                    if (none) continue;
                    Power pk = Power.None;
                    if (kind == ObjKind.Barrel && l != bl && i > 0 && lv >= 3 && rg.Next() < .16)
                    { kind = ObjKind.Power; pk = powers[(int)Math.Floor(rg.Next() * 3)]; }

                    var o = new BattleObj { kind = kind, x = Field.Lanes[l], wy = wy, pk = pk, seed = rg.Range(0, 40) };
                    float lvMul = 1f + .05f * lv;
                    if (kind == ObjKind.Barrel) o.v = Math.Max(2, (float)Math.Ceiling(rg.Range(i == 0 ? 3 : 4, i == 0 ? 6 : 12) * lvMul * Field.BARV));
                    else if (kind == ObjKind.Power) o.v = Math.Max(2, (float)Math.Ceiling(rg.Range(4, 12) * lvMul * .5f * Field.BARV));
                    else if (kind == ObjKind.Board) o.v = (float)Math.Ceiling(rg.Range(3, 9) * (1 + .03 * lv) * Math.Pow(H, .12));
                    else
                    {
                        var T = BattleData.Types[PickType()];
                        float E = Balance.EnemyRef(lv, i);
                        o.type = T;
                        o.max = o.hp = Math.Max(4, (float)Math.Ceiling(DpsOf(E, lv) * Balance.Tw(lv) * T.mul * (.92 + rg.Next() * .16) * H));
                        o.chip = o.hp;
                        o.contact = Math.Max(1, (float)Math.Ceiling(E * T.dmg * Math.Pow(H, .25)));
                    }
                    if (kind == ObjKind.Barrel || kind == ObjKind.Power)
                    {
                        o.max = o.hp = Math.Max(4, (float)Math.Ceiling(DpsOf(Balance.EnemyRef(lv, i), lv) * (.28 + .055 * (o.v / lvMul) * (kind == ObjKind.Power ? 2 : 1))));
                        o.chip = o.hp;
                    }
                    L.objs.Add(o);
                }
            }

            AddGates(L, lv, R);
            L.efin = Balance.EnemyRef(lv, R);
            bool isBoss = lv % 10 == 0;
            float bas = DpsOf(.33f * L.efin, lv) * Balance.TimeToKill0(lv) * H;
            if (isBoss)
            {
                int bi = Math.Min(BattleData.BossNames.Length - 1, lv / 10 - 1);
                L.fin = new FinalFoe { boss = true, bossIndex = bi, name = BattleData.BossNames[bi], color = BattleData.BossColors[bi], sc = 3.6f + lv / 100f * .8f, hp = (float)Math.Ceiling(bas * Balance.FinalHp * Balance.BossHp * Balance.BossEase(lv)) };
            }
            else
            {
                var eid = avail[(int)Math.Floor(rg.Next() * avail.Count)];
                var et = BattleData.Types[eid];
                L.fin = new FinalFoe { boss = false, typeId = eid, name = "Elit " + et.nameTr, color = et.skin ?? BattleData.Maps[map].enemySkin, sc = 2.6f, hp = (float)Math.Ceiling(bas * Balance.FinalHp) };
            }
            var fresh = BattleData.Order.FirstOrDefault(k => BattleData.Types[k].unlock == lv);
            L.fresh = fresh != null ? BattleData.Types[fresh].nameTr : null;
            return L;
        }
    }
}
