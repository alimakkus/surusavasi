using System;
using System.Collections.Generic;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Sunucuya gönderilmeyi bekleyen tek elmas işlemi. k: "e" kazanç, "s" harcama, "p" satın alma.</summary>
    [Serializable]
    public class GemOp
    {
        public string id = "", k = "", r = "", d = "";
        public int n;
        public string store = "", pid = "", rc = "";
    }

    /// <summary>Elmas defteri: her elmas kazancı/harcaması/satın alması burada sıraya girer ve sunucuya (Cloud Code
    /// SuruEconomy modülü) gönderilir. Sunucu gerçek bakiyeyi döndürür; ekrandaki bakiye = sunucu bakiyesi + bekleyenler.
    /// Böylece oyun çevrimdışı da oynanır, ama kayıt dosyasıyla oynayıp elmas yazmak işe yaramaz.</summary>
    public static class GemLedger
    {
        public const int MaxOps = 600;

        public static string Today => DateTime.UtcNow.ToString("yyyyMMdd");

        public static GemOp Record(SaveData d, string dev, string k, int n, string reason, string store = "", string pid = "", string rc = "")
        {
            if (d.gemOps == null) d.gemOps = new List<GemOp>();
            var op = new GemOp { id = dev + "-" + (++d.gemSeq), k = k, n = n, r = reason ?? "", d = Today, store = store, pid = pid, rc = rc };
            d.gemOps.Add(op);
            if (d.gemOps.Count > MaxOps) Compact(d, false);
            if (d.gemOps.Count > MaxOps) Compact(d, true);
            return op;
        }

        static int Effect(GemOp o) => o.k == "s" ? -o.n : o.n;

        /// <summary>Bekleyen işlemlerin bakiyeye net etkisi.</summary>
        public static int Net(SaveData d)
        {
            int s = 0; if (d.gemOps != null) foreach (var o in d.gemOps) s += Effect(o);
            return s;
        }

        public static int Pending(SaveData d) => d.gemOps?.Count ?? 0;

        /// <summary>Çok uzun çevrimdışı oyunda listeyi kısaltır: aynı gün ve sebepteki kazançları, aynı gündeki harcamaları birleştirir.
        /// ignoreDay: yine sığmazsa günleri de birleştirir (en son güne yazılır). Satın almalar hiç birleştirilmez.</summary>
        public static void Compact(SaveData d, bool ignoreDay)
        {
            var map = new Dictionary<string, GemOp>(); var outp = new List<GemOp>();
            foreach (var o in d.gemOps)
            {
                if (o.k == "p") { outp.Add(o); continue; }
                string key = o.k + "|" + (o.k == "e" ? o.r : "") + "|" + (ignoreDay ? "" : o.d);
                if (map.TryGetValue(key, out var m)) { m.n += o.n; if (string.CompareOrdinal(o.d, m.d) > 0) m.d = o.d; m.id = o.id; }
                else { var c = new GemOp { id = o.id, k = o.k, n = o.n, r = o.r, d = o.d }; map[key] = c; outp.Add(c); }
            }
            d.gemOps = outp;
        }

        /// <summary>Sunucu yanıtını uygular: onaylanan işlemler silinir, bakiye sunucununki + kalan bekleyenler olur.</summary>
        public static void ApplyServer(SaveData d, int serverBal, IList<string> acked)
        {
            if (d.gemOps == null) d.gemOps = new List<GemOp>();
            if (acked != null && acked.Count > 0)
            {
                var set = new HashSet<string>(acked);
                d.gemOps.RemoveAll(o => set.Contains(o.id));
            }
            d.gemServerBal = serverBal;
            d.diamonds = Math.Max(0, serverBal + Net(d));
            d.gemSyncAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        /// <summary>Bekleyenler öncesi bakiye: sunucu hesabı ilk kez açılırken mevcut elmasları taşımak için (sunucu sınırlı kabul eder).</summary>
        public static int BaseBalance(SaveData d) => Math.Max(0, d.diamonds - Net(d));
    }
}
