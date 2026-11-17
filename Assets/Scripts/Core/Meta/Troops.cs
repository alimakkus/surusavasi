using System;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kışla'da asker eğitimi. Eğitilen askerler bir sonraki savaşa ek asker olarak katılır ve savaşta harcanır.
    /// Kapasite ve eğitim hızı Kışla seviyesiyle artar; bedeli çoğunlukla yiyecektir (yiyeceğe anlamlı bir harcama yeri).</summary>
    public static class Troops
    {
        public static int Capacity(SaveData d) { int L = Progress.Level(d, BuildingKind.Barracks); return L <= 0 ? 0 : 5 + 3 * L; }
        public static float UnitSeconds(SaveData d) => 15f / (1 + .06f * Progress.Eff(d, BuildingKind.Barracks));
        public static int FoodPer(SaveData d) => 8 + (int)(Math.Max(1, d.unlocked) * 1.5f);
        public static int GoldPer(SaveData d) => 2 + Math.Max(1, d.unlocked) / 2;
        public static int Free(SaveData d) => Math.Max(0, Capacity(d) - d.troops - d.trainQueue);

        /// <summary>Süresi dolan eğitimleri askere çevirir.</summary>
        public static void Tick(SaveData d, long nowMs)
        {
            while (d.trainQueue > 0 && nowMs >= d.trainEnd)
            {
                d.troops++; d.trainQueue--;
                if (d.trainQueue > 0) d.trainEnd += (long)(UnitSeconds(d) * 1000);
            }
        }

        /// <summary>n asker eğitimini başlatır (kaynak harcama dışarıda). Sıraya eklenir.</summary>
        public static void Queue(SaveData d, int n, long nowMs)
        {
            if (n <= 0) return;
            if (d.trainQueue == 0) d.trainEnd = nowMs + (long)(UnitSeconds(d) * 1000);
            d.trainQueue += n;
        }

        public static float RemainingSeconds(SaveData d, long nowMs) =>
            d.trainQueue <= 0 ? 0 : Math.Max(0, (d.trainEnd - nowMs) / 1000f) + (d.trainQueue - 1) * UnitSeconds(d);

        /// <summary>Hemen bitir bedeli: kalan her 2 dakika 1 elmas, son 15 saniye bedava.</summary>
        public static int SpeedCost(SaveData d, long nowMs) { float r = RemainingSeconds(d, nowMs); return r <= 15 ? 0 : (int)Math.Ceiling(r / 120); }
        public static void FinishNow(SaveData d) { d.troops += d.trainQueue; d.trainQueue = 0; }

        /// <summary>Savaş başlarken eğitimli askerler orduya katılır.</summary>
        public static int TakeForBattle(SaveData d) { int n = d.troops; d.troops = 0; return n; }
    }
}
