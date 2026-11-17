using UnityEngine;

namespace SuruSavasi.Core
{
    /// <summary>Denge formülleri. Prototipteki değerlerle birebir aynıdır.</summary>
    public static class Balance
    {
        public const float Fire = 0.35f;
        public const int MaxLevel = 100;

        public static float DmgPer(int lv) => 1f + 0.04f * lv;
        public static float Cbv(int lv) => 8f * (1f + 0.05f * lv);
        public static int StartSoldiers(int lv) => Mathf.CeilToInt(10f * (1f + 0.05f * lv));
        public static float EnemyRef(int lv, int i) => StartSoldiers(lv) + i * 0.6f * Cbv(lv);
        public static float DpsOf(float e, int lv) => e * DmgPer(lv) / Fire;
        public static float Tw(int lv) => 0.45f + lv * 0.004f;
        public static float TimeToKill0(int lv) => 6f + lv * 0.04f;
        public static float SquadTime(int lv) => 22f - lv * 0.06f;
        public static int RowsOf(int lv) => 8 + lv / 8;

        /// <summary>Zorluk eğrisi: düşman canı (×H), temas hasarı ve tabelalar (×√H). Oyuncunun yükseltmelerle büyüyen
        /// gücünü takip eder; yükseltme yapmayan oyuncu zamanla takılır. Denge simülasyonuyla ayarlanır.</summary>
        public static float Difficulty(int lv) => 1f;

        /// <summary>Oran = kalan asker / (0.33 * son düşman gücü). 1 ve üstü 3 yıldız, 0.5 ve üstü 2 yıldız.</summary>
        public static int Stars(float ratio) => ratio >= 1f ? 3 : ratio >= 0.5f ? 2 : 1;

        /// <summary>Üst üste yenilgide yardım: her yenilgi için %30, en çok 4 kademe.</summary>
        public static float FailAssist(int fails) => Mathf.Min(4, fails) * 0.30f;
    }
}
