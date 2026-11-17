using System;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Gece-gündüz döngüsü (telefonun saatine göre) ve tema önizlemesi.</summary>
    public partial class BaseMapView
    {
        // ---------- Gece-gündüz ----------
        /// <summary>Ayarlardan kapatılabilir: kapalıyken üs hep gündüz görünür.</summary>
        public static bool NightEnabled
        {
            get => PlayerPrefs.GetInt("surusavasi-night", 1) == 1;
            set { PlayerPrefs.SetInt("surusavasi-night", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        float nightNow, duskNow;
#if UNITY_EDITOR
        int nightDebug; // Editor testi: N tuşu → 0 saate göre, 1 gündüz, 2 akşam, 3 gece
        static readonly string[] NightDebugNames = { "saate göre", "gündüz", "akşam", "gece" };
#endif

        /// <summary>0 gündüz, 1 gece. Akşam 18:30-20:30 kararır, sabah 05:30-07:00 aydınlanır.</summary>
        static float NightByClock(float h)
        {
            if (h >= 20.5f || h < 5.5f) return 1;
            if (h >= 18.5f) return (h - 18.5f) / 2f;
            if (h < 7f) return 1 - (h - 5.5f) / 1.5f;
            return 0;
        }

        void NightTick(float dt)
        {
            float target;
            if (previewTheme != null) target = previewNight ? 1 : 0;
            else if (!NightEnabled) target = 0;
            else { var now = DateTime.Now; target = NightByClock(now.Hour + now.Minute / 60f); }
#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.N)) nightDebug = (nightDebug + 1) % 4;
            if (previewTheme == null && nightDebug > 0) target = nightDebug == 1 ? 0 : nightDebug == 2 ? .5f : 1;
#endif
            nightNow = Mathf.MoveTowards(nightNow, target, dt * .7f);
            duskNow = 4 * nightNow * (1 - nightNow);
            SoundFx.Ambient(ThemeId == "green" && nightNow > .6f ? "green_night" : ThemeId);
        }

        /// <summary>Karartma ve ışıklar. Binaların üstünde, efektlerin altında çizilir.</summary>
        void NightDraw()
        {
            float n = nightNow;
            if (n <= .01f) return;
            float vw = visW + 40, vh = visH + 40;
            if (duskNow > .01f) Draw(square, new Color(1, .45f, .18f, .2f * duskNow), camX, camY, vw, vh, 1990);
            Draw(square, new Color(.03f, .06f, .2f, .55f * n), camX, camY, vw, vh, 1991);

            void Glow(float x, float y, float w, float h, float a, float core)
            {
                float f = .88f + .12f * Mathf.Sin(t * 9 + x * .37f + y * .11f);
                Draw(glow, new Color(1, .68f, .28f, a * n * f), x, y, w * f, h * f, 1993);
                if (core > 0) Draw(circle, new Color(1, .9f, .55f, .95f * n), x, y, core, core, 1994);
            }

            // Bina pencereleri
            foreach (var kv in Pos)
            {
                var k = kv.Key; var p = kv.Value;
                if (k == BuildingKind.Wall || Progress.Level(D, k) <= 0) continue;
                float bs = BScale(k);
                Glow(p.x, p.y - 22 * bs, 150 * bs, 100 * bs, k == BuildingKind.Headquarters ? .55f : .45f, 0);
            }
            // Sur meşaleleri, kuleler, kapı
            for (int i = 0; i < wallSegs.Count; i += 3) Glow(wallSegs[i].x, wallSegs[i].y - 26, 34, 34, .6f, 3.5f);
            foreach (var p in wallTowers) Glow(p.x, p.y - 46, 60, 60, .75f, 5);
            float gy = Pos[BuildingKind.Wall].y;
            Glow(CX - 46, gy - 34, 52, 52, .75f, 4.5f); Glow(CX + 46, gy - 34, 52, 52, .75f, 4.5f);

            // Kamp ateşi (talim alanının yanında)
            float fx = CX + 70, fy = CY + 112;
            Glow(fx, fy - 6, 130, 96, .7f, 0);
            Draw(square, new Color(.35f, .2f, .1f, n), fx, fy + 2, 18, 4, 1995, 20);
            Draw(square, new Color(.35f, .2f, .1f, n), fx, fy + 2, 18, 4, 1995, -20);
            for (int i = 0; i < 3; i++)
            {
                float s = 9 + 4 * Mathf.Sin(t * 11 + i * 2.1f);
                Draw(circle, new Color(1, .45f + .2f * i, .1f, .9f * n), fx + (i - 1) * 3, fy - 4 - i * 3 - Mathf.Abs(Mathf.Sin(t * 7 + i)) * 2, s * .8f, s, 1996);
            }
        }

        // ---------- Tema önizleme ----------
        public static string previewTheme;
        static float previewUntil;
        bool previewNight;
        const float PreviewSeconds = 12f;

        string ThemeId => previewTheme ?? D.theme;

        /// <summary>Mağazadaki göz düğmesi: temayı üste birkaç saniyeliğine canlı uygular.</summary>
        public static void StartPreview(string themeId) { previewTheme = themeId; previewUntil = Time.unscaledTime + PreviewSeconds; }

        void EndPreview(bool backToShop)
        {
            previewTheme = null;
            if (backToShop) { MetaScreens.shopTab = 2; flow.Show(GameScreen.Shop); }
        }

        void PreviewTick()
        {
            if (previewTheme == null) return;
            if (dragging || pinching || Mathf.Abs(Input.mouseScrollDelta.y) > .01f) previewUntil = Mathf.Max(previewUntil, Time.unscaledTime + 5);
            if (Time.unscaledTime > previewUntil) EndPreview(true);
        }

        void PreviewGui()
        {
            var th = Themes.Get(previewTheme);
            float w = Mathf.Min(440, VW - 12), x0 = VW / 2 - w / 2;
            GUI.Box(new Rect(x0, 8, w, 58), "", card);
            var eye = UiSkin.Eye;
            if (eye != null) GUI.DrawTexture(new Rect(x0 + 12, 15, 44, 44), eye, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(x0 + 64, 8, w - 140, 58), $"<b>Önizleme: {th.name}</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, fontSize = 17 });
            int left = Mathf.CeilToInt(Mathf.Max(0, previewUntil - Time.unscaledTime));
            GUI.Label(new Rect(x0 + w - 74, 8, 64, 58), $"<color=#ffd23f><b>{left} sn</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontSize = 17 });

            float bh = 58, by = 800 - bh - 10;
            GUI.Box(new Rect(x0 - 4, by - 8, w + 8, bh + 16), "", card);
            float bw = (w - 16) / 3;
            var sb = new GUIStyle(btn) { fontSize = 15 };
            if (GUI.Button(new Rect(x0, by, bw, bh), previewNight ? "Gündüz" : "Gece", sb)) { previewNight = !previewNight; previewUntil = Mathf.Max(previewUntil, Time.unscaledTime + 5); SoundFx.Play("click"); }
            if (GUI.Button(new Rect(x0 + bw + 8, by, bw, bh), "Kapat", sb)) { SoundFx.Play("click"); EndPreview(true); return; }
            bool owned = Themes.Owned(D, th.id);
            if (GUI.Button(new Rect(x0 + 2 * bw + 16, by, bw, bh), owned ? "Seç" : $"Satın al\n<size=13>{th.price} elmas</size>", UiSkin.ButtonGold))
            {
                if (owned || Wl.Spend(Currency.Diamonds, th.price, "buy_theme_" + th.id))
                {
                    if (!D.ownedThemes.Contains(th.id)) D.ownedThemes.Add(th.id);
                    D.theme = th.id; GameServices.Save.Save();
                    SoundFx.Play("power"); flow.ShowToast(owned ? $"{th.name} seçildi" : $"{th.name} açıldı!");
                    if (!owned) GameServices.Analytics?.LogEvent("theme_buy", new System.Collections.Generic.Dictionary<string, object> { { "theme", th.id }, { "price", th.price }, { "from", "preview" } });
                    EndPreview(false);
                }
                else flow.ShowToast("Yeterli elmas yok");
            }
        }
    }
}
