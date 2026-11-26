using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if SURU_CC
using Unity.Services.CloudCode;
#endif

namespace SuruSavasi.Cloud
{
    /// <summary>Sunucudaki elmas modülüne (Cloud Code "SuruEconomy") çağrı. İstek ve yanıt JSON metnidir.
    /// Cloud Code paketi kurulu değilse (SURU_CC tanımsız) her çağrı null döner.</summary>
    public static class CloudEconomy
    {
        public const string Module = "SuruEconomy";
        public static string LastError { get; private set; } = "";

        public static bool Supported =>
#if SURU_CC && SURU_UGS
            true;
#else
            false;
#endif

        /// <summary>Defteri gönderir, sunucu yanıtını (JSON) döndürür. Bağlantı yoksa ya da modül yüklenmemişse null.</summary>
        public static async Task<string> SyncAsync(string requestJson)
        {
#if SURU_CC && SURU_UGS
            if (!await CloudSync.InitAsync()) { LastError = CloudSync.Message; return null; }
            try
            {
                var res = await CloudCodeService.Instance.CallModuleEndpointAsync(Module, "Sync",
                    new Dictionary<string, object> { { "request", requestJson } });
                LastError = "";
                return res;
            }
            catch (CloudCodeException e)
            {
                string m = e.Message ?? "";
                LastError = m.Contains("not found") || m.Contains("404") || m.Contains("ModuleNotFound") ? "Sunucu modülü yüklenmemiş" : "Sunucu hatası";
                Debug.LogWarning("[Elmas] " + m);
                return null;
            }
            catch (Exception e)
            {
                LastError = "Bağlantı yok";
                Debug.LogWarning("[Elmas] " + e.Message);
                return null;
            }
#else
            await Task.CompletedTask;
            LastError = "Cloud Code paketi kurulu değil";
            return null;
#endif
        }
    }
}
