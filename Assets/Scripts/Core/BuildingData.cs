using UnityEngine;

namespace SuruSavasi.Core
{
    public enum BuildingKind { Barracks, Armory, Wall, Mine, Quarry, Farm, Hospital, Tower, Market }

    /// <summary>Bina tanımı. Unity'de Create > SuruSavasi > Building ile her bina için bir asset oluşturulur.</summary>
    [CreateAssetMenu(menuName = "SuruSavasi/Building")]
    public class BuildingData : ScriptableObject
    {
        public const int MaxLevel = 20;
        public BuildingKind kind;
        public string displayNameTr, displayNameEn;
        public int baseGold, baseIron, baseFood;
        [Tooltip("Maliyet büyüme çarpanı (g)")] public float growth = 1.5f;

        public int Cost(int baseCost, int level) =>
            Mathf.CeilToInt(baseCost * Mathf.Pow(1f + (growth - 1f) * 0.45f, level));

        public static int BuildSeconds(int level) => Mathf.RoundToInt(8f * Mathf.Pow(1.3f, level));
        public static int RequiredStars(int level) => level < 2 ? 0 : (level - 1) * 6;
        public static int Tier(int level) => Mathf.CeilToInt(level / 4f);

        /// <summary>Savaş etkisi için etkin seviye: 10'a kadar doğrusal, sonrası yarı hızla.</summary>
        public static float EffectiveLevel(int level) => level <= 10 ? level : 10f + (level - 10) / 2f;
    }
}
