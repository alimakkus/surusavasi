using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Kahraman görünümleri ekranı: büyük önizleme + 4 görünüm kartı + Kuşan / Satın al.</summary>
    public static class SkinScreen
    {
        static int hero, preview;
        static float buyPop;
        static SaveData D => GameServices.Save.Data;
        static readonly string[] Bg = { "#2a3550", "#5a4512", "#101836", "#5a1420" };

        public static void Open(GameFlow flow, int h) { hero = h; preview = Skins.Current(D, h); flow.Show(GameScreen.Skins); }

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var d = D; var H = Progress.Heroes[hero];
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Heroes); return; }
            GUI.Label(new Rect(x0 + 100, 44, w - 100, 26), $"<b>{H.name} · Görünümler</b>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(x0 + 100, 68, w - 100, 22), $"{Skins.OwnedCount(d, hero)}/{Skins.Ids.Length} görünüm  ·  <size=11>yalnızca görsel, güç vermez</size>", new GUIStyle(UiSkin.Small) { richText = true });

            // Büyük önizleme
            buyPop = Mathf.Max(0, buyPop - Time.deltaTime * 2);
            var pv = new Rect(x0, 96, w, 300);
            GUI.Box(pv, "", preview == 1 ? UiSkin.CardGold : UiSkin.CardDark);
            var bg = ModeUi.C(Bg[preview]);
            GUI.DrawTexture(new Rect(pv.x + 10, pv.y + 10, pv.width - 20, pv.height - 60), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, bg, 0, 12);
            var port = Art.HeroPortrait(hero, Mathf.FloorToInt(Time.time * 1.6f), preview);
            float bob = Mathf.Sin(Time.time * 2.2f) * 4, ps = 270 * (1 + buyPop * .15f);
            if (port != null) GUI.DrawTexture(new Rect(pv.center.x - ps / 2, pv.y + 2 + bob - (ps - 270) / 2, ps, ps), port, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(pv.x, pv.yMax - 48, pv.width, 24), $"<b><size=20>{Skins.Names[preview]}</size></b>", ModeUi.Center);
            GUI.Label(new Rect(pv.x + 10, pv.yMax - 26, pv.width - 20, 20), $"<size=12>{Skins.Descs[preview]}</size>", ModeUi.Small);

            // Görünüm kartları (2x2)
            float tw = (w - 10) / 2, th = 96, ty = 404;
            for (int s = 0; s < Skins.Ids.Length; s++)
            {
                var r = new Rect(x0 + (s % 2) * (tw + 10), ty + (s / 2) * (th + 8), tw, th);
                bool owned = Skins.Owned(d, hero, s), on = Skins.Current(d, hero) == s, sel = preview == s;
                GUI.Box(r, "", sel ? UiSkin.CardGold : UiSkin.Card);
                GUI.DrawTexture(new Rect(r.x + 8, r.y + 8, 80, 80), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(Bg[s]), 0, 8);
                var mini = Art.HeroPortrait(hero, 0, s);
                if (mini != null) GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, 92, 92), mini, ScaleMode.ScaleToFit);
                GUI.Label(new Rect(r.x + 96, r.y + 10, r.width - 100, 24), $"<b>{Skins.Names[s]}</b>", new GUIStyle(UiSkin.Label) { richText = true });
                string st = on ? "<color=#8be39d>Kuşanıldı</color>" : owned ? "<color=#c9d3e0>Sahipsin</color>" : $"<color=#9fe8ff>{Skins.Price[s]} elmas</color>" + (s == 3 ? "\n<size=10>ya da lig ödülü</size>" : "");
                GUI.Label(new Rect(r.x + 96, r.y + 36, r.width - 100, 50), st, new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true });
                if (GUI.Button(r, "", GUIStyle.none)) { preview = s; SoundFx.Play("click"); }
            }

            // Eylem düğmesi
            var br = new Rect(x0 + 20, 620, w - 40, 64);
            bool pOwned = Skins.Owned(d, hero, preview), pOn = Skins.Current(d, hero) == preview;
            if (pOn) { GUI.enabled = false; GUI.Button(br, "Kuşanıldı", UiSkin.ButtonGray); GUI.enabled = true; }
            else if (pOwned)
            {
                if (GUI.Button(br, "KUŞAN", UiSkin.ButtonGreen)) { Skins.Equip(d, hero, preview); GameServices.Save.Save(); SoundFx.Play("power"); buyPop = 1; }
            }
            else if (GUI.Button(br, $"SATIN AL  <size=14>({Skins.Price[preview]} elmas)</size>", UiSkin.ButtonGold))
            {
                if (GameServices.Wallet.Spend(Currency.Diamonds, Skins.Price[preview], $"skin:{H.id}_{Skins.Ids[preview]}"))
                { Skins.Give(d, hero, preview); Skins.Equip(d, hero, preview); GameServices.Save.Save(); SoundFx.Play("chest"); buyPop = 1; flow.Toast($"{H.name}: {Skins.Names[preview]} görünümü!"); }
                else flow.Toast("Yeterli elmas yok");
            }
        }
    }
}
