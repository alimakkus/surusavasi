using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>İlk açılışta çıkan gizlilik onay penceresi. Cevap verilmeden kapanmaz.</summary>
    public static class ConsentScreen
    {
        /// <summary>Gizlilik politikası sayfası (mağaza kayıtlarında da bu adres kullanılır).</summary>
        public const string PrivacyUrl = "https://claude.ai/artifact/N2obxUpEe9HR6jJZBp9NFx";

        public static bool Needed => GameServices.Consent != null && !GameServices.Consent.HasAnswered;

        public static void Draw(float VW)
        {
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .7f), 0, 0);
            float w = Mathf.Min(400, VW - 24), x = VW / 2 - w / 2, h = 470, y = 400 - h / 2;
            GUI.Box(new Rect(x, y, w, h), "", UiSkin.Card);
            GUI.Label(new Rect(x + 18, y + 14, w - 36, 36), "<b>Gizliliğin bizim için önemli</b>", new GUIStyle(UiSkin.Label) { fontSize = 20 });
            GUI.Label(new Rect(x + 18, y + 56, w - 36, 250),
                "Sürü Savaşı ücretsizdir ve reklamlarla desteklenir. Reklam ortaklarımız, sana daha ilgili reklamlar göstermek için " +
                "cihaz kimliği gibi verileri kullanabilir.\n\n" +
                "<b>Kabul et</b>: Sana göre seçilmiş reklamlar.\n" +
                "<b>Reddet</b>: Kişiselleştirilmemiş reklamlar. Oyunun hiçbir özelliği kısıtlanmaz.\n\n" +
                "Seçimini istediğin zaman Ayarlar'dan değiştirebilirsin.",
                new GUIStyle(UiSkin.Small) { fontSize = 14, wordWrap = true });
            if (GUI.Button(new Rect(x + 18, y + h - 178, w - 36, 30), "<color=#9fe8ff><b>Gizlilik politikasını oku ›</b></color>", new GUIStyle(UiSkin.Small) { alignment = TextAnchor.MiddleCenter, fontSize = 14 })) Application.OpenURL(PrivacyUrl);
            if (GUI.Button(new Rect(x + 18, y + h - 140, w - 36, 56), "Kabul et", UiSkin.ButtonGold)) { LocalConsent.SetAnswer(true); SoundFx.Play("click"); }
            if (GUI.Button(new Rect(x + 18, y + h - 74, w - 36, 52), "Reddet", UiSkin.Button)) { LocalConsent.SetAnswer(false); SoundFx.Play("click"); }
        }
    }
}
