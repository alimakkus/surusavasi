using System;
using SuruSavasi.Core.Battle;

namespace SuruSavasi.Core.Meta
{
    public struct LevelReward { public int gold, iron, food, gems; }

    /// <summary>Bölüm sonu ödülleri. Savaş ekranı ve denge simülasyonu aynı formülü kullanır.</summary>
    public static class Rewards
    {
        public static LevelReward ForLevel(int level, int stars, bool first, BattleMods mods, EventDef ev = null)
        {
            float all = ev?.all ?? 1, eg = ev?.gold ?? 1, ei = ev?.iron ?? 1, egem = ev?.gem ?? 1;
            float mu = (first ? 1f : .4f) * (.7f + .15f * stars);
            return new LevelReward
            {
                gold = (int)Math.Round((30 + 6 * level) * mu * mods.goldMul * all * eg),
                iron = (int)Math.Round((14 + 3 * level) * mu * mods.marketMul * all * ei),
                food = (int)Math.Round((18 + 4 * level) * mu * mods.marketMul * all),
                gems = first ? (int)Math.Round((stars + (level % 10 == 0 ? 5 : 0)) * egem) : 0,
            };
        }
    }
}
