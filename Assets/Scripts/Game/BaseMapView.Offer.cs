using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Kurucu Paketi teklifi: üste geri sayımlı düğme, açılış penceresi.</summary>
    public partial class BaseMapView
    {
        static bool founderShownThisSession;

        /// <summary>Üs açılınca: teklif yeni başladıysa ya da bu oturumda hiç gösterilmediyse pencereyi açar.</summary>
        void FounderCheck()
        {
            long now = BaseLogic.Now;
            bool started = Offers.TryStartFounder(D, now);
            if (started) GameServices.Save.Save();
            if (!Offers.FounderActive(D, now) || popup != Popup.None || previewTheme != null || def != null) return;
            if (started || !founderShownThisSession)
            {
                founderShownThisSession = true; popup = Popup.Founder;
                GameServices.Analytics?.LogEvent("offer_shown", new System.Collections.Generic.Dictionary<string, object> { { "offer", "founder" }, { "first", started } });
            }
        }

        void FounderHudButton(float x0)
        {
            long now = BaseLogic.Now;
            if (!Offers.FounderActive(D, now)) return;
            float pu = .5f + .5f * Mathf.Sin(t * 4);
            var r = new Rect(x0, 800 - 160, 110, 58);
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f, .25f + .25f * pu), 0, 12);
            if (GUI.Button(r, $"Kurucu Paketi\n<size=12>{FmtLong(Offers.FounderSecondsLeft(D, now))}</size>", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14 }))
            { SoundFx.Play("click"); popup = Popup.Founder; }
        }

        static string FmtLong(float sec)
        {
            int s = Mathf.CeilToInt(sec), h = s / 3600, m = s / 60 % 60;
            return h > 0 ? $"{h}:{m:00}:{s % 60:00}" : $"{m}:{s % 60:00}";
        }

        void FounderPopup(float x, float y, float w, float h, GUIStyle head)
        {
            long now = BaseLogic.Now;
            if (!Offers.FounderActive(D, now)) { popup = Popup.None; return; }
            GUI.DrawTexture(new Rect(x + 4, y + 4, w - 8, 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f), 0, 3);
            GUI.Label(new Rect(x + 16, y + 12, w - 70, 36), "Kurucu Paketi", head);
            GUI.Label(new Rect(x + 16, y + 46, w - 32, 24), $"<color=#ff8a80><b>Sadece {FmtLong(Offers.FounderSecondsLeft(D, now))} kaldı · tek seferlik</b></color>", small);

            float ry = y + 80;
            void Line(string col, string text) { GUI.Label(new Rect(x + 24, ry, w - 48, 30), $"<color={col}>●</color>  {text}", new GUIStyle(label) { fontSize = 16 }); ry += 32; }
            if (D.builders < 2) Line("#ffd23f", "<b>İkinci İnşaatçı</b> (kalıcı)");
            else Line("#9fe8ff", $"<b>+{Offers.FounderBuilderGems} elmas</b> (inşaatçın zaten var)");
            Line("#9fe8ff", $"<b>{Offers.FounderGems} elmas</b>");
            Line("#c07bff", "<b>Kesin Epik ekipman</b>");
            Line("#ffd23f", $"<b>{Offers.FounderGold(D)} altın</b>");

            GUI.Label(new Rect(x, ry + 4, w, 30), "<color=#8be39d><b>%500 değer</b></color> · ayrı ayrı alınana göre", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            var store = GameServices.Store; var pd = Catalog.Find(Catalog.FounderPack);
            bool ready = store != null && store.IsReady;
            string price = pd != null && !string.IsNullOrEmpty(pd.localizedPrice) ? pd.localizedPrice : "—";
            GUI.enabled = ready;
            if (GUI.Button(new Rect(x + 20, y + h - 120, w - 40, 58), ready ? $"Satın al · {price}" : "Mağaza hazır değil", UiSkin.ButtonGold))
            {
                Services.Shop.Buy(Catalog.FounderPack, ok =>
                {
                    if (!ok) { flow.ShowToast("Satın alma tamamlanmadı"); return; }
                    popup = Popup.None; SoundFx.Play("power");
                    var g = Services.Shop.LastFounderGear;
                    var hp = Pos[BuildingKind.Headquarters];
                    AddFloat(hp.x, hp.y - 110, "Kurucu Paketi açıldı!", Gold);
                    if (g != null) AddFloat(hp.x, hp.y - 80, $"{Gear.Name(g)} (Epik)", Hex(Gear.RarityColors[g.rarity]));
                    Burst(hp.x, hp.y - 60, 40, new[] { Gold, Color.white, Gem }, 160, 80);
                });
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + 20, y + h - 54, w - 40, 40), "Daha sonra", new GUIStyle(btn) { fontSize = 14 })) popup = Popup.None;
        }
    }
}
