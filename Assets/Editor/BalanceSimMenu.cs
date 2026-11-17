using System.IO;
using System.Text;
using SuruSavasi.Core.Meta;
using UnityEditor;
using UnityEngine;

namespace SuruSavasi.EditorTools
{
    /// <summary>Menü: Sürü Savaşı > Denge Simülasyonu. Botla 100 bölümü oynatır, raporları proje kökündeki Balance klasörüne yazar.</summary>
    public static class BalanceSimMenu
    {
        [MenuItem("Sürü Savaşı/Güç Payı Ölçümü")]
        public static void Headroom()
        {
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Balance");
            Directory.CreateDirectory(dir);
            var t0 = System.DateTime.Now;
            EditorUtility.DisplayProgressBar("Denge", "Güç payı ölçülüyor", .5f);
            try { File.WriteAllText(Path.Combine(dir, "guc_payi.csv"), BalanceModel.HeadroomCurve()); }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log($"[Denge] Güç payı yazıldı ({(System.DateTime.Now - t0).TotalSeconds:0} sn)");
        }

        [MenuItem("Sürü Savaşı/Zorluk Kalibrasyonu")]
        public static void Calibrate()
        {
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Balance");
            Directory.CreateDirectory(dir);
            var t0 = System.DateTime.Now;
            EditorUtility.DisplayProgressBar("Denge", "Zorluk kalibre ediliyor", .5f);
            try { File.WriteAllText(Path.Combine(dir, "kalibrasyon.csv"), BalanceModel.Calibrate()); }
            finally { EditorUtility.ClearProgressBar(); }
            Debug.Log($"[Denge] Kalibrasyon yazıldı ({(System.DateTime.Now - t0).TotalSeconds:0} sn)");
        }

        [MenuItem("Sürü Savaşı/Bölüm Düzeltme")]
        public static void Tune()
        {
            var t0 = System.DateTime.Now;
            float[] f;
            try { f = BalanceModel.LevelFactors(p => EditorUtility.DisplayProgressBar("Denge", "Bölümler ölçülüyor", p)); }
            finally { EditorUtility.ClearProgressBar(); }
            var sb = new StringBuilder();
            sb.AppendLine("namespace SuruSavasi.Core");
            sb.AppendLine("{");
            sb.AppendLine("    /// <summary>Bölüm başına zorluk düzeltmesi (1 = değişiklik yok). Sürü Savaşı > Bölüm Düzeltme menüsü bu dosyayı yeniden yazar:");
            sb.AppendLine("    /// botla her bölümün kendi zorluğunu ölçer, komşularından çok zor/kolay olanları dengeler.</summary>");
            sb.AppendLine("    public static class LevelTuning");
            sb.AppendLine("    {");
            sb.AppendLine("        public static float Factor(int lv) => lv >= 1 && lv <= T.Length ? T[lv - 1] : 1f;");
            sb.Append("        static readonly float[] T = new float[100] { ");
            for (int i = 0; i < f.Length; i++) sb.Append((i > 0 ? ", " : "") + f[i].ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + "f");
            sb.AppendLine(" };");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            File.WriteAllText(Path.Combine(Application.dataPath, "Scripts/Core/LevelTuning.cs"), sb.ToString());
            AssetDatabase.Refresh();
            Debug.Log($"[Denge] Bölüm düzeltme tablosu yazıldı ({(System.DateTime.Now - t0).TotalSeconds:0} sn)");
        }

        [MenuItem("Sürü Savaşı/Denge Simülasyonu")]
        public static void Run()
        {
            var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Balance");
            Directory.CreateDirectory(dir);
            var t0 = System.DateTime.Now;
            var sum = new StringBuilder();
            try
            {
                EditorUtility.DisplayProgressBar("Denge", "Yükseltmesiz zorluk eğrisi", .1f);
                File.WriteAllText(Path.Combine(dir, "egri_yukseltmesiz.csv"), BalanceModel.BareCurve());

                (string name, BalanceModel.Options o)[] profiles =
                {
                    ("iyi", new BalanceModel.Options { skill = 1f }),
                    ("ortalama", new BalanceModel.Options { skill = .7f }),
                    ("reklamci", new BalanceModel.Options { skill = .7f, watchDoubleAds = true }),
                };
                for (int i = 0; i < profiles.Length; i++)
                {
                    var p = profiles[i];
                    EditorUtility.DisplayProgressBar("Denge", "İlerleme: " + p.name, .3f + .2f * i);
                    var rows = BalanceModel.Run(p.o);
                    File.WriteAllText(Path.Combine(dir, $"ilerleme_{p.name}.csv"), BalanceModel.Csv(rows));
                    var last = rows[rows.Count - 1];
                    sum.Append($"{p.name}: son bölüm {last.lv}, toplam {last.hours:0} saat. ");
                    foreach (int m in new[] { 10, 25, 50, 75, 100 }) if (m <= rows.Count) sum.Append($"B{m}={rows[m - 1].hours:0.0}sa ");
                    sum.AppendLine();
                }
            }
            finally { EditorUtility.ClearProgressBar(); }
            sum.AppendLine($"Süre: {(System.DateTime.Now - t0).TotalSeconds:0} sn. Klasör: {dir}");
            File.WriteAllText(Path.Combine(dir, "ozet.txt"), sum.ToString());
            Debug.Log("[Denge] " + sum);
        }
    }
}
