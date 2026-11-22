using System;
using SuruSavasi.Core.Meta;

/// <summary>Unity'siz denge simülasyonu girişi (Tools/balance/run_sim.ps1 derler).</summary>
public static class SimMain
{
    public static void Main()
    {
        var profiles = new (string, BalanceModel.Options)[]
        {
            ("iyi", new BalanceModel.Options { skill = 1f }),
            ("ortalama", new BalanceModel.Options { skill = .7f }),
            ("reklamci", new BalanceModel.Options { skill = .7f, watchDoubleAds = true }),
            ("iki_insaatci", new BalanceModel.Options { skill = .7f, builders = 2 }),
        };
        foreach (var (n, o) in profiles)
        {
            var rows = BalanceModel.Run(o); var last = rows[rows.Count - 1];
            Console.Write($"{n}: son {last.lv}, {last.hours:0} sa. ");
            foreach (int m in new[] { 10, 25, 50, 75, 100 }) if (m <= rows.Count) Console.Write($"B{m}={rows[m - 1].hours:0.0} ");
            Console.WriteLine();
            if (n == "ortalama") System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "simrun", "ortalama.csv"), BalanceModel.Csv(rows));
        }
    }
}
