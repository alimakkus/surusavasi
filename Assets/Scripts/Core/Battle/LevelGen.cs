using System;
using System.Collections.Generic;
using System.Linq;

namespace SuruSavasi.Core.Battle
{
    public enum ObjKind { Barrel, Power, Board, Enemy }

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
                    else if (kind == ObjKind.Board) o.v = (float)Math.Ceiling(rg.Range(3, 9) * (1 + .03 * lv) * Math.Sqrt(H));
                    else
                    {
                        var T = BattleData.Types[PickType()];
                        float E = Balance.EnemyRef(lv, i);
                        o.type = T;
                        o.max = o.hp = Math.Max(4, (float)Math.Ceiling(DpsOf(E, lv) * Balance.Tw(lv) * T.mul * (.92 + rg.Next() * .16) * H));
                        o.chip = o.hp;
                        o.contact = Math.Max(1, (float)Math.Ceiling(E * T.dmg * Math.Sqrt(H)));
                    }
                    if (kind == ObjKind.Barrel || kind == ObjKind.Power)
                    {
                        o.max = o.hp = Math.Max(4, (float)Math.Ceiling(DpsOf(Balance.EnemyRef(lv, i), lv) * (.28 + .055 * (o.v / lvMul) * (kind == ObjKind.Power ? 2 : 1))));
                        o.chip = o.hp;
                    }
                    L.objs.Add(o);
                }
            }

            L.efin = Balance.EnemyRef(lv, R);
            bool isBoss = lv % 10 == 0;
            float bas = DpsOf(.33f * L.efin, lv) * Balance.TimeToKill0(lv) * H;
            if (isBoss)
            {
                int bi = Math.Min(BattleData.BossNames.Length - 1, lv / 10 - 1);
                L.fin = new FinalFoe { boss = true, bossIndex = bi, name = BattleData.BossNames[bi], color = BattleData.BossColors[bi], sc = 3.6f + lv / 100f * .8f, hp = (float)Math.Ceiling(bas * Balance.FinalHp) };
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
