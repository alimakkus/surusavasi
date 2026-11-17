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
