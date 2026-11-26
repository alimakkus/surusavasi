using System;
using SuruSavasi.Cloud;
using SuruSavasi.Core;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Kaydın bulutla eşitlenmesi: açılışta buluttaki kayıtla karşılaştırır, değişiklikleri en çok dakikada bir
    /// ve uygulama arka plana geçince yükler. Yeni cihazda (ilerlemesiz) bulutta kayıt varsa onu yükler; iki tarafta da
    /// farklı ilerleme varsa oyuncuya sorar.</summary>
    public class CloudSaveManager : MonoBehaviour
    {
        public static CloudSaveManager I { get; private set; }
        static SaveData D => GameServices.Save?.Data;

        /// <summary>Çakışma: bulutta bu cihazdakinden farklı (daha yeni) kayıt var; oyuncu seçecek.</summary>
        public static CloudSnapshot Conflict;
        public static DateTime? LastUpload;
        public static bool Busy;

        long uploadedAt; float nextCheck;
        const float UploadEvery = 60f;

        public static int Progress(SaveData d) => d == null ? 0 : d.unlocked * 1000 + Core.Meta.Progress.TotalStars(d);   // seviye × 1000 + yıldız
        public static bool Fresh(SaveData d) => d != null && d.unlocked <= 1 && Core.Meta.Progress.TotalStars(d) == 0;

        public static void Ensure()
        {
            if (I != null) return;
            var go = new GameObject("CloudSave"); DontDestroyOnLoad(go);
            I = go.AddComponent<CloudSaveManager>();
        }

        /// <summary>Açılışta (kayıt yüklendikten sonra) bir kez.</summary>
        public static async void Begin()
        {
            Ensure();
            if (!CloudSync.Supported || D == null) return;
            Busy = true;
            try
            {
                var snap = await CloudSync.DownloadAsync();
                var d = D;
                if (CloudSync.Status != CloudSync.State.Ready) return;
                if (snap == null || string.IsNullOrEmpty(snap.json)) { await I.Upload(); return; }
                if (Math.Abs(snap.savedAt - d.savedAt) < 5000 && snap.progress == Progress(d)) { I.uploadedAt = d.savedAt; return; }
                if (Fresh(d) && snap.progress > Progress(d))
                {
                    Apply(snap.json);
                    GameFlow.I?.ShowToast("Bulut kaydın yüklendi!");
                    return;
                }
                if (snap.savedAt > d.savedAt && snap.progress != Progress(d)) { Conflict = snap; return; }
                await I.Upload();
            }
            finally { Busy = false; EconomySync.SyncSoon(); }
        }

        /// <summary>Buluttaki kaydı bu cihaza uygular (yerindeki veri nesnesi üzerine yazılır, ekranlar hemen günceli görür).</summary>
        public static void Apply(string json)
        {
            var d = D; if (d == null) return;
            JsonUtility.FromJsonOverwrite(json, d);
            SaveSystem.Migrate(d);
            GameServices.Save.Save();
            if (I != null) I.uploadedAt = d.savedAt;
            Debug.Log("[Bulut] Buluttaki kayıt uygulandı");
            EconomySync.SyncSoon(); // elmaslar bu hesabın sunucu bakiyesine çekilir
            if (GameFlow.I != null && !GameFlow.I.InBattle) GameFlow.I.Show(GameScreen.Base);
        }

        public static async void ResolveConflict(bool useCloud)
        {
            var c = Conflict; Conflict = null;
            if (c == null) return;
            if (useCloud) { Apply(c.json); GameFlow.I?.ShowToast("Buluttaki kayıt yüklendi"); }
            else if (I != null) { await I.Upload(); GameFlow.I?.ShowToast("Bu cihazdaki kayıt buluta yazıldı"); }
        }

        public async System.Threading.Tasks.Task Upload()
        {
            var d = D; if (d == null || Conflict != null) return;
            if (d.savedAt == 0) GameServices.Save.Save();
            if (await CloudSync.UploadAsync(JsonUtility.ToJson(d), d.savedAt, Progress(d)))
            { uploadedAt = d.savedAt; LastUpload = DateTime.Now; }
        }

        public static async void UploadNow() { if (I != null) { Busy = true; try { await I.Upload(); } finally { Busy = false; } } }

        void Update()
        {
            EconomySync.Tick();
            if (Busy || Conflict != null || CloudSync.Status != CloudSync.State.Ready || D == null) return;
            if (Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + UploadEvery;
            if (D.savedAt > uploadedAt) UploadNow();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && D != null && Core.Meta.GemLedger.Pending(D) > 0) EconomySync.SyncNow();
            if (paused && !Busy && Conflict == null && CloudSync.Status == CloudSync.State.Ready && D != null && D.savedAt > uploadedAt) UploadNow();
        }

        // ---------------- Çakışma penceresi (GameFlow.OnGUI içinden) ----------------
        public static bool DrawConflict(float VW)
        {
            var c = Conflict; if (c == null || D == null) return false;
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .7f), 0, 0);
            float w = Mathf.Min(390, VW - 24), x = VW / 2 - w / 2, y = 200;
            GUI.Box(new Rect(x, y, w, 380), "", UiSkin.CardGold);
            var rib = new Rect(x + 20, y - 16, w - 40, 40); GUI.Box(rib, "", UiSkin.Ribbon);
            GUI.Label(rib, "<b>Bulutta başka bir kayıt var</b>", new GUIStyle(ModeUi.Center) { fontSize = 16 });
            var cd = DateTimeOffset.FromUnixTimeMilliseconds(c.savedAt).LocalDateTime;
            var ld = DateTimeOffset.FromUnixTimeMilliseconds(D.savedAt).LocalDateTime;
            GUI.Label(new Rect(x + 20, y + 40, w - 40, 60), "Hesabındaki bulut kaydı bu cihazdakinden farklı. Hangisiyle devam etmek istersin? Seçmediğin kayıt silinir.", new GUIStyle(UiSkin.Small) { wordWrap = true, fontSize = 14 });
            Box(new Rect(x + 20, y + 106, (w - 50) / 2, 110), "BULUTTAKİ", c.progress, cd, c.device);
            Box(new Rect(x + 30 + (w - 50) / 2, y + 106, (w - 50) / 2, 110), "BU CİHAZDAKİ", Progress(D), ld, SystemInfo.deviceModel);
            if (GUI.Button(new Rect(x + 20, y + 230, w - 40, 56), "Buluttakini yükle", UiSkin.ButtonGold)) { SoundFx.Play("click"); ResolveConflict(true); }
            if (GUI.Button(new Rect(x + 20, y + 296, w - 40, 56), "Bu cihazdakiyle devam et", UiSkin.Button)) { SoundFx.Play("click"); ResolveConflict(false); }
            GUI.Button(new Rect(0, 0, VW, 800), "", GUIStyle.none);
            return true;
        }

        static void Box(Rect r, string title, int progress, DateTime when, string device)
        {
            GUI.Box(r, "", UiSkin.CardDark);
            GUI.Label(new Rect(r.x, r.y + 6, r.width, 20), $"<b><size=12>{title}</size></b>", ModeUi.Center);
            GUI.Label(new Rect(r.x + 6, r.y + 28, r.width - 12, 80),
                $"<b>Bölüm {Mathf.Max(1, progress / 1000)}</b>\n★ {progress % 1000}\n<size=11>{when:dd.MM HH:mm}\n{device}</size>", ModeUi.Small);
        }
    }
}
