namespace SuruSavasi.Core.Battle
{
    /// <summary>Prototipteki "seeded" (Mulberry32) ile birebir aynı rastgele sayı üretici.
    /// Aynı bölüm numarası hep aynı dizilimi üretir.</summary>
    public class Rng
    {
        uint a;
        public Rng(int seed) { a = unchecked((uint)seed); }

        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>a ile b arası (ikisi dahil) tam sayı.</summary>
        public int Range(int a0, int b0) => a0 + (int)System.Math.Floor(Next() * (b0 - a0 + 1));
    }
}
