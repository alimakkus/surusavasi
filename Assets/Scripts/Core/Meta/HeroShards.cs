using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Parçayla açılan kahramanlar (Deniz, Cem). Parçalar modlardan, lig ve bölüm sandıklarından gelir;
    /// 50 parça toplanınca kahraman açılır. Parça, açılmamış kahramanlardan en çok parçası olana gider (önce biri bitsin).</summary>
    public static class HeroShards
    {
        public const int Need = 50, PackGems = 80, PackShards = 10;
        public static bool IsShardHero(int i) => i >= 3;
        public static bool Owned(SaveData d, int i) => (d.heroOwned & (1 << i)) != 0;
        public static bool CanUnlock(SaveData d, int i) => IsShardHero(i) && !Owned(d, i) && d.heroShards[i] >= Need;

        /// <summary>Parça verir. Hedef seçilmezse otomatik seçer. Dönen: kahraman sırası (-1: hepsi açık, parça verilmedi).</summary>
        public static int Give(SaveData d, int amount, int target = -1)
        {
            if (target < 0)
            {
                int best = -1;
                for (int i = 3; i < Progress.Heroes.Length; i++)
                    if (!Owned(d, i) && (best < 0 || d.heroShards[i] > d.heroShards[best])) best = i;
                target = best;
            }
            if (target < 0) return -1;
            d.heroShards[target] = Math.Min(Need, d.heroShards[target] + amount);
            return target;
        }

        public static string GiveText(SaveData d, int amount)
        {
            int h = Give(d, amount);
            return h < 0 ? "" : $"\n<color=#ffb02e><b>+{amount} {Progress.Heroes[h].name} parçası</b> ({d.heroShards[h]}/{Need})</color>";
        }

        public static bool Unlock(SaveData d, int i)
        {
            if (!CanUnlock(d, i)) return false;
            d.heroOwned |= 1 << i;
            return true;
        }

        public static bool AnyUnlockable(SaveData d) { for (int i = 3; i < Progress.Heroes.Length; i++) if (CanUnlock(d, i)) return true; return false; }
    }
}
