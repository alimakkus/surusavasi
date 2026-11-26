using System;
using System.Collections.Generic;
using SuruSavasi.Cloud;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Elmas defterini sunucuyla eşitler (Cloud Code SuruEconomy.Sync). Bekleyen işlem varsa 30 sn'de bir,
    /// yoksa 5 dk'da bir; satın almadan hemen sonra ve uygulama arka plana geçerken. Sunucuya ulaşılamazsa oyun
    /// yerel bakiyeyle sürer, işlemler sırada bekler.</summary>
    public static class EconomySync
    {
        [Serializable] class Req { public int @base; public List<GemOp> ops = new List<GemOp>(); }
        [Serializable] class Res { public int bal; public List<string> acked = new List<string>(); public int capped, rejSpend, rejBuy, bought; public string msg = ""; }

        public static bool Busy { get; private set; }
        public static DateTime? LastOk;
        public static string Status = "";
        static float next;
        static SaveData D => GameServices.Save?.Data;

        public static bool Verified => D != null && D.gemServerBal >= 0 && GemLedger.Pending(D) == 0;

        /// <summary>Kısa süre içinde eşitle (satın alma sonrası).</summary>
        public static void SyncSoon() => next = 0;

        /// <summary>CloudSaveManager.Update içinden çağrılır.</summary>
        public static void Tick()
        {
            if (Busy || D == null || !CloudEconomy.Supported || CloudSync.Status != CloudSync.State.Ready) return;
            if (CloudSaveManager.Busy || CloudSaveManager.Conflict != null) return; // önce kayıt eşitlemesi biter
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + (GemLedger.Pending(D) > 0 ? 30f : 300f);
            SyncNow();
        }

        public static async void SyncNow()
        {
            var d = D;
            if (Busy || d == null || !CloudEconomy.Supported) return;
            Busy = true;
            try
            {
                var req = new Req { @base = GemLedger.BaseBalance(d), ops = new List<GemOp>(d.gemOps) };
                if (req.ops.Count > 300) req.ops = req.ops.GetRange(0, 300);
                string json = await CloudEconomy.SyncAsync(JsonUtility.ToJson(req));
                if (json == null) { Status = CloudEconomy.LastError; return; }
                var res = JsonUtility.FromJson<Res>(json);
                if (res == null) { Status = "Sunucu yanıtı okunamadı"; return; }
                if (GameServices.Save.Data != d) return; // bu arada başka kayıt yüklendi
                GemLedger.ApplyServer(d, res.bal, res.acked);
                GameServices.Save.Save();
                LastOk = DateTime.Now; Status = "";
                Debug.Log($"[Elmas] Sunucu bakiyesi {res.bal}, onaylanan {res.acked.Count}, bekleyen {GemLedger.Pending(d)}, sınır {res.capped}, red harcama {res.rejSpend} satın alma {res.rejBuy}");
                if (res.rejBuy > 0) GameFlow.I?.ShowToast("Bir satın alma doğrulanamadı; elmaslar eklenmedi.");
                else if (res.capped > 0) Debug.Log($"[Elmas] Sınırlar nedeniyle {res.capped} elmas verilmedi");
                if (GemLedger.Pending(d) > 0) next = Time.unscaledTime + 5f; // kalanlar varsa kısa sürede devam
            }
            catch (Exception e) { Status = "Eşitleme hatası"; Debug.LogWarning("[Elmas] " + e.Message); }
            finally { Busy = false; }
        }
    }
}
