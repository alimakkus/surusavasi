using SuruSavasi.Cloud;
using SuruSavasi.Core;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Hesap ve bulut kayıt ekranı: durum, şimdi yedekle, hesabı koru (kullanıcı adı + şifre bağla),
    /// başka cihazdaki hesapla giriş (kaydı bu cihaza getirir).</summary>
    public static class AccountScreen
    {
        static string user = "", pass = "", info = "";
        static int mode;          // 0 durum, 1 hesabı koru, 2 başka hesapla giriş
        static bool working;
        static SaveData D => GameServices.Save.Data;

        public static void Draw(GameFlow flow, float x0, float w)
        {
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { if (mode != 0) { mode = 0; info = ""; } else flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), "<b>Hesap ve Bulut Kayıt</b>", UiSkin.Label);
            var lab = new GUIStyle(UiSkin.Label) { richText = true, wordWrap = true, fontSize = 15 };
            var sm = new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true, fontSize = 13 };

            // Durum kartı
            var r = new Rect(x0, 98, w, 150);
            GUI.Box(r, "", UiSkin.Card);
            string st = !CloudSync.Supported ? "<color=#ff8a80>Bulut paketi kurulu değil</color>"
                : CloudSync.Status == CloudSync.State.Ready ? "<color=#8be39d>Bağlı</color>"
                : CloudSync.Status == CloudSync.State.Connecting ? "<color=#ffd23f>Bağlanıyor…</color>"
                : $"<color=#ff8a80>{CloudSync.Message}</color>";
            string acc = CloudSync.Username != null ? $"<color=#8be39d>Korunuyor</color> · kullanıcı adı: <b>{CloudSync.Username}</b>" : "<color=#ffb02e>Korunmuyor</color> (sadece bu cihaza bağlı)";
            string last = CloudSaveManager.LastUpload.HasValue ? CloudSaveManager.LastUpload.Value.ToString("HH:mm") : "—";
            GUI.Label(new Rect(r.x + 16, r.y + 10, r.width - 32, 130),
                $"Bulut: {st}\nHesap: {acc}\nSon yedek: <b>{last}</b>   ·   Seviye {D.unlocked}, ★ {Core.Meta.Progress.TotalStars(D)}\n<size=11><color=#7d8696>Oyuncu kimliği: {CloudSync.PlayerId ?? "—"}</color></size>", lab);

            float y = r.yMax + 12;
            if (mode == 0)
            {
                if (CloudSync.Status == CloudSync.State.Ready)
                {
                    GUI.enabled = !CloudSaveManager.Busy;
                    if (GUI.Button(new Rect(x0, y, w, 56), "Şimdi yedekle", UiSkin.ButtonGreen)) { CloudSaveManager.UploadNow(); info = "Yedeklendi"; }
                    GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = CloudSync.Supported && CloudSync.Status != CloudSync.State.Connecting;
                    if (GUI.Button(new Rect(x0, y, w, 56), "Tekrar bağlan", UiSkin.Button)) Reconnect();
                    GUI.enabled = true;
                }
                y += 66;
                if (CloudSync.Username == null)
                {
                    GUI.Label(new Rect(x0, y, w, 60), "<b>Kaydını koru:</b> bir kullanıcı adı ve şifre belirle. Telefonun değişirse ya da oyunu silersen, aynı bilgilerle başka cihazda kaldığın yerden devam edersin.", sm);
                    y += 62;
                    if (GUI.Button(new Rect(x0, y, w, 56), "Hesabımı koru (kullanıcı adı + şifre)", UiSkin.ButtonGold)) { mode = 1; info = ""; }
                    y += 66;
                }
                GUI.Label(new Rect(x0, y, w, 44), "<b>Başka cihazda oynadın mı?</b> O hesabın bilgileriyle gir, kaydın bu cihaza gelsin.", sm);
                y += 46;
                if (GUI.Button(new Rect(x0, y, w, 56), "Başka cihazdaki hesabımla giriş yap", UiSkin.Button)) { mode = 2; info = ""; }
                y += 66;
            }
            else
            {
                GUI.Label(new Rect(x0, y, w, 64), mode == 1
                    ? "Kullanıcı adı: 3-20 karakter (harf, rakam, . - @ _).\nŞifre: 8-30 karakter; büyük harf, küçük harf, rakam ve sembol içermeli."
                    : "<color=#ffb02e>Dikkat:</color> giriş yapınca o hesabın kaydı bu cihazdakinin <b>yerine geçer</b>. Bu cihazdaki ilerleme korunmamışsa kaybolur.", sm);
                y += 70;
                GUI.Label(new Rect(x0, y, w, 22), "Kullanıcı adı", sm); y += 22;
                user = GUI.TextField(new Rect(x0, y, w, 44), user, 20, new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4) });
                y += 52;
                GUI.Label(new Rect(x0, y, w, 22), "Şifre", sm); y += 22;
                pass = GUI.PasswordField(new Rect(x0, y, w, 44), pass, '•', 30, new GUIStyle(GUI.skin.textField) { fontSize = 18, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 4, 4) });
                y += 56;
                GUI.enabled = !working && user.Length >= 3 && pass.Length >= 8;
                if (GUI.Button(new Rect(x0, y, w, 58), working ? "Lütfen bekle…" : mode == 1 ? "HESABIMI KORU" : "GİRİŞ YAP VE KAYDI GETİR", UiSkin.ButtonGold))
                { if (mode == 1) Link(flow); else SignIn(flow); }
                GUI.enabled = true;
                y += 68;
            }
            if (!string.IsNullOrEmpty(info)) GUI.Label(new Rect(x0, y, w, 60), info, new GUIStyle(sm) { alignment = TextAnchor.MiddleCenter });
        }

        static async void Reconnect() { info = "Bağlanıyor…"; info = await CloudSync.InitAsync() ? "Bağlandı" : CloudSync.Message; }

        static async void Link(GameFlow flow)
        {
            working = true; info = "";
            string err = await CloudSync.LinkPasswordAsync(user.Trim(), pass);
            working = false;
            if (err == null) { info = "<color=#8be39d>Hesabın korunuyor! Bu bilgileri unutma.</color>"; pass = ""; mode = 0; CloudSaveManager.UploadNow(); SoundFx.Play("chest"); }
            else info = $"<color=#ff8a80>{err}</color>";
        }

        static async void SignIn(GameFlow flow)
        {
            working = true; info = "";
            string err = await CloudSync.SignInPasswordAsync(user.Trim(), pass);
            if (err != null) { working = false; info = $"<color=#ff8a80>{err}</color>"; return; }
            var snap = await CloudSync.DownloadAsync();
            working = false; pass = "";
            if (snap != null && !string.IsNullOrEmpty(snap.json))
            {
                CloudSaveManager.Apply(snap.json);
                SoundFx.Play("promote");
                flow.ShowToast($"Hoş geldin! Seviye {D.unlocked} kaydın yüklendi");
                mode = 0;
            }
            else { info = "Bu hesapta kayıt bulunamadı; bu cihazdaki ilerleme bu hesaba yedeklenecek."; mode = 0; CloudSaveManager.UploadNow(); }
        }
    }
}
