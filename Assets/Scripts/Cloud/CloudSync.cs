using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
#if SURU_UGS
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.CloudSave;
#endif

namespace SuruSavasi.Cloud
{
    /// <summary>Buluttaki kayıt: JSON + ne zaman kaydedildiği + ilerleme özeti.</summary>
    public class CloudSnapshot
    {
        public string json; public long savedAt; public int progress; public string device;
    }

    [Serializable] class Meta { public long savedAt; public int progress; public string device; }

    /// <summary>Unity Gaming Services ile bulut kayıt (Authentication + Cloud Save). Oyuncu önce sessizce anonim giriş yapar;
    /// kaydı başka cihaza taşımak için hesabına kullanıcı adı + şifre bağlar, yeni cihazda aynı bilgilerle girer.
    /// Paketler kurulu değilse (SURU_UGS tanımsız) her şey sessizce kapalı kalır, oyun yerel kayıtla çalışır.</summary>
    public static class CloudSync
    {
        public enum State { Off, Connecting, Ready, Error }
        public static State Status { get; private set; } = State.Off;
        public static string Message { get; private set; } = "";
        public static bool Supported =>
#if SURU_UGS
            true;
#else
            false;
#endif
        const string KeySave = "save", KeyMeta = "meta";

#if SURU_UGS
        public static bool SignedIn => UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn;
        public static bool IsAnonymous => !SignedIn || AuthenticationService.Instance.PlayerInfo?.Username == null;
        public static string Username => SignedIn ? AuthenticationService.Instance.PlayerInfo?.Username : null;
        public static string PlayerId => SignedIn ? AuthenticationService.Instance.PlayerId : null;

        /// <summary>Servisleri başlatır ve (oturum yoksa) anonim giriş yapar. Proje UGS'ye bağlı değilse Error durumuna geçer.</summary>
        public static async Task<bool> InitAsync()
        {
            if (Status == State.Ready && SignedIn) return true;
            Status = State.Connecting; Message = "Bağlanıyor…";
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized) await UnityServices.InitializeAsync();
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    // Kayıtlı oturum varsa (anonim ya da sonradan kullanıcı adı bağlanmış) onunla, yoksa yeni anonim hesapla girer
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                }
                try { await AuthenticationService.Instance.GetPlayerInfoAsync(); } catch (Exception) { }
                Status = State.Ready; Message = "Bağlı";
                Debug.Log($"[Bulut] Giriş tamam: {AuthenticationService.Instance.PlayerId} (kullanıcı adı: {Username ?? "yok"})");
                return true;
            }
            catch (Exception e) { Fail(e); return false; }
        }

        public static async Task<CloudSnapshot> DownloadAsync()
        {
            if (!await InitAsync()) return null;
            try
            {
                var items = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { KeySave, KeyMeta });
                if (!items.TryGetValue(KeySave, out var s)) { Debug.Log("[Bulut] Bulutta kayıt yok"); return null; }
                var snap = new CloudSnapshot { json = s.Value.GetAs<string>() };
                if (items.TryGetValue(KeyMeta, out var m))
                {
                    var meta = JsonUtility.FromJson<Meta>(m.Value.GetAs<string>());
                    if (meta != null) { snap.savedAt = meta.savedAt; snap.progress = meta.progress; snap.device = meta.device; }
                }
                Debug.Log($"[Bulut] İndirildi: ilerleme {snap.progress}, {snap.json.Length} karakter");
                return snap;
            }
            catch (Exception e) { Fail(e); return null; }
        }

        public static async Task<bool> UploadAsync(string json, long savedAt, int progress)
        {
            if (!await InitAsync()) return false;
            try
            {
                var meta = JsonUtility.ToJson(new Meta { savedAt = savedAt, progress = progress, device = SystemInfo.deviceModel });
                await CloudSaveService.Instance.Data.Player.SaveAsync(new Dictionary<string, object> { { KeySave, json }, { KeyMeta, meta } });
                Message = "Yedeklendi " + DateTime.Now.ToString("HH:mm");
                Debug.Log($"[Bulut] Yüklendi: ilerleme {progress}");
                return true;
            }
            catch (Exception e) { Fail(e); return false; }
        }

        /// <summary>Mevcut (anonim) hesaba kullanıcı adı + şifre bağlar: oyuncu kimliği ve bulut kaydı aynı kalır. Hata varsa açıklamasını döndürür.</summary>
        public static async Task<string> LinkPasswordAsync(string user, string pass)
        {
            if (!await InitAsync()) return Message;
            try
            {
                await AuthenticationService.Instance.AddUsernamePasswordAsync(user, pass);
                try { await AuthenticationService.Instance.GetPlayerInfoAsync(); } catch (Exception) { }
                Debug.Log("[Bulut] Kullanıcı adı bağlandı: " + user);
                return null;
            }
            catch (AuthenticationException e) { return Explain(e); }
            catch (RequestFailedException e) { return Explain(e); }
        }

        /// <summary>Başka cihazdaki hesaba geçer: bu cihazın anonim oturumu kapanır, kullanıcı adı + şifre ile girilir.</summary>
        public static async Task<string> SignInPasswordAsync(string user, string pass)
        {
            if (!await InitAsync()) return Message;
            try
            {
                if (AuthenticationService.Instance.IsSignedIn) AuthenticationService.Instance.SignOut(true);
                await AuthenticationService.Instance.SignInWithUsernamePasswordAsync(user, pass);
                try { await AuthenticationService.Instance.GetPlayerInfoAsync(); } catch (Exception) { }
                Status = State.Ready; Message = "Bağlı";
                Debug.Log("[Bulut] Hesaba girildi: " + user);
                return null;
            }
            catch (Exception e)
            {
                string why = e is RequestFailedException rf ? Explain(rf) : e.Message;
                // Giriş olmadıysa yeniden anonim oturum açılır ki yedekleme sürsün
                try { if (!AuthenticationService.Instance.IsSignedIn) await AuthenticationService.Instance.SignInAnonymouslyAsync(); } catch (Exception) { }
                return why;
            }
        }

        static string Explain(RequestFailedException e)
        {
            if (e.ErrorCode == AuthenticationErrorCodes.AccountAlreadyLinked) return "Bu kullanıcı adı alınmış ya da hesap zaten bağlı.";
            string m = e.Message ?? "";
            if (m.Contains("WRONG_USERNAME_PASSWORD") || m.Contains("Invalid username or password")) return "Kullanıcı adı ya da şifre yanlış.";
            if (m.Contains("username") || m.Contains("password")) return "Kullanıcı adı ya da şifre kurallara uymuyor.";
            return "Hata: " + m;
        }

        static void Fail(Exception e)
        {
            Status = State.Error;
            Message = e.Message.Contains("cloud project") || e.Message.Contains("projectId") ? "Proje Unity Cloud'a bağlı değil" : "Bağlantı yok";
            Debug.LogWarning("[Bulut] " + e.Message);
        }
#else
        public static bool SignedIn => false;
        public static bool IsAnonymous => true;
        public static string Username => null;
        public static string PlayerId => null;
        public static Task<bool> InitAsync() { Message = "Bulut paketi kurulu değil"; return Task.FromResult(false); }
        public static Task<CloudSnapshot> DownloadAsync() => Task.FromResult<CloudSnapshot>(null);
        public static Task<bool> UploadAsync(string json, long savedAt, int progress) => Task.FromResult(false);
        public static Task<string> LinkPasswordAsync(string user, string pass) => Task.FromResult("Bulut paketi kurulu değil");
        public static Task<string> SignInPasswordAsync(string user, string pass) => Task.FromResult("Bulut paketi kurulu değil");
#endif
    }
}
