using System;
using System.Collections.Generic;
using SuruSavasi.Core.Meta;
using UnityEngine;

namespace SuruSavasi.Services
{
    /// <summary>Bildirim ölçümü: kaç bildirim planlandı, hangisine dokunuldu (notif_open), hangisinden sonraki 3 saatte
    /// oyuncu kendisi döndü (notif_return). Olaylar analiz servisine gider; ayrıca cihazda kimlik başına sayaç tutulur.</summary>
    public static class NotifStats
    {
        const string PlanKey = "surusavasi-notif-plan", TallyKey = "surusavasi-notif-tally";

        public static void Planned(List<PlannedNote> plan, DateTime now)
        {
            var parts = new List<string>(); int[] byCat = new int[4];
            foreach (var p in plan) { parts.Add($"{p.id}:{new DateTimeOffset(p.when).ToUnixTimeSeconds()}:{(int)p.cat}"); byCat[(int)p.cat]++; }
            PlayerPrefs.SetString(PlanKey, string.Join(";", parts)); PlayerPrefs.Save();
            GameServices.Analytics?.LogEvent("notif_planned", new Dictionary<string, object>
                { { "count", plan.Count }, { "base", byCat[0] }, { "event", byCat[1] }, { "alliance", byCat[2] }, { "return", byCat[3] } });
        }

        /// <summary>Bildirime dokunularak açıldı.</summary>
        public static void Opened(string data, DateTime now)
        {
            if (!NotificationPlan.TryParse(data, out var target, out int id, out long plannedAt, out int cat)) return;
            long mins = (new DateTimeOffset(now).ToUnixTimeSeconds() - plannedAt) / 60;
            GameServices.Analytics?.LogEvent("notif_open", new Dictionary<string, object>
                { { "id", id }, { "cat", NotificationPlan.CatNames[Math.Max(0, Math.Min(3, cat))] }, { "target", target }, { "minutes_since_planned", mins } });
            Tally(id, true);
            PlayerPrefs.DeleteKey(PlanKey);
        }

        /// <summary>Bildirime dokunmadan açıldı: son 3 saat içinde gitmiş bir bildirim varsa dönüş ona yazılır.</summary>
        public static void Returned(DateTime now)
        {
            string s = PlayerPrefs.GetString(PlanKey, ""); if (s == "") return;
            long t = new DateTimeOffset(now).ToUnixTimeSeconds(); int best = -1; long bestAgo = long.MaxValue;
            foreach (var part in s.Split(';'))
            {
                var p = part.Split(':'); if (p.Length < 3 || !int.TryParse(p[0], out int id) || !long.TryParse(p[1], out long when)) continue;
                long ago = t - when;
                if (ago >= 0 && ago <= 3 * 3600 && ago < bestAgo) { best = id; bestAgo = ago; }
            }
            PlayerPrefs.DeleteKey(PlanKey);
            if (best < 0) return;
            GameServices.Analytics?.LogEvent("notif_return", new Dictionary<string, object> { { "id", best }, { "minutes_after", bestAgo / 60 } });
            Tally(best, false);
        }

        /// <summary>Cihazdaki sayaç: "kimlik:dokunma:dönüş" (test ve hata ayıklama için).</summary>
        static void Tally(int id, bool opened)
        {
            var d = new Dictionary<int, int[]>();
            foreach (var part in PlayerPrefs.GetString(TallyKey, "").Split(';'))
            {
                var p = part.Split(':'); if (p.Length == 3 && int.TryParse(p[0], out int i) && int.TryParse(p[1], out int o) && int.TryParse(p[2], out int r)) d[i] = new[] { o, r };
            }
            if (!d.ContainsKey(id)) d[id] = new int[2];
            d[id][opened ? 0 : 1]++;
            var outp = new List<string>(); foreach (var kv in d) outp.Add($"{kv.Key}:{kv.Value[0]}:{kv.Value[1]}");
            PlayerPrefs.SetString(TallyKey, string.Join(";", outp)); PlayerPrefs.Save();
        }

        public static string TallyText() => PlayerPrefs.GetString(TallyKey, "");
    }
}
