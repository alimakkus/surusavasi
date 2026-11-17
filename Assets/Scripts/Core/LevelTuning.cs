namespace SuruSavasi.Core
{
    /// <summary>Bölüm başına zorluk düzeltmesi (1 = değişiklik yok). Sürü Savaşı > Bölüm Düzeltme menüsü bu dosyayı yeniden yazar:
    /// botla her bölümün kendi zorluğunu ölçer, komşularından çok zor/kolay olanları dengeler.</summary>
    public static class LevelTuning
    {
        public static float Factor(int lv) => lv >= 1 && lv <= T.Length ? T[lv - 1] : 1f;
        static readonly float[] T = new float[100] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f };
    }
}
