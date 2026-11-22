using System;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Hikâye sahnesi oynatıcı: karartılmış ekran, konuşanın portresi, isim şeridi, daktilo yazısı.
    /// Dokununca sıradaki satır; "Atla" ile sahne biter. Açıkken savaş durur.</summary>
    public static class DialogBox
    {
        static Scene scene; static int idx, level; static float shown, t0; static Action done;
        public static bool Active => scene != null;

        public static void Play(Scene s, int lv, Action onDone)
        {
            if (s == null) { onDone?.Invoke(); return; }
            scene = s; idx = 0; level = lv; shown = 0; t0 = Time.unscaledTime; done = onDone;
            SoundFx.Play("power");
        }

        static void Finish()
        {
            var d = GameServices.Save?.Data; if (d != null && scene != null) { Story.MarkSeen(d, scene.id); GameServices.Save.Save(); }
            var cb = done; scene = null; done = null; cb?.Invoke();
        }

        static Color Tint(string who) =>
            who == "ayaz" ? new Color(.48f, .36f, 1f) : who == "kaya" ? new Color(1f, .42f, .23f) : who == "mira" ? new Color(.16f, .84f, .69f) :
            who == "nuri" ? new Color(.85f, .7f, .35f) : new Color(.85f, .2f, .25f);

        internal static void DrawSprite(Rect r, Sprite sp, bool flip = false)
        {
            if (sp == null) return;
            var tex = sp.texture; var tr = sp.textureRect;
            var uv = new Rect(tr.x / tex.width, tr.y / tex.height, tr.width / tex.width, tr.height / tex.height);
            if (flip) { uv.x += uv.width; uv.width = -uv.width; }
            float asp = tr.width / tr.height, w = r.width, h = r.width / asp;
            if (h > r.height) { h = r.height; w = h * asp; }
            GUI.DrawTextureWithTexCoords(new Rect(r.x + (r.width - w) / 2, r.yMax - h, w, h), tex, uv);
        }

        /// <summary>OnGUI içinde, diğer her şeyden önce çağrılır. Sahne açıksa true döner (çağıran başka bir şey çizmemeli).</summary>
        public static bool Draw(float VW)
        {
            if (scene == null) return false;
            var line = scene.lines[Mathf.Clamp(idx, 0, scene.lines.Count - 1)];
            bool left = line.who == "ayaz" || line.who == "kaya" || line.who == "mira" || line.who == "nuri";
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .62f), 0, 0);

            // Başlık
            if (idx == 0 || Time.unscaledTime - t0 < 2.5f)
                GUI.Box(new Rect(VW / 2 - 170, 90, 340, 46), scene.title, new GUIStyle(UiSkin.Ribbon) { fontSize = 18 });

            // Portre
            float ph = 330, pw = 260, py = 800 - 250 - ph + 40;
            var pr = left ? new Rect(0, py, pw, ph) : new Rect(VW - pw, py, pw, ph);
            var tint = Tint(line.who);
            GUI.DrawTexture(new Rect(pr.x + 30, pr.yMax - 140, pr.width - 60, 120), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(tint.r, tint.g, tint.b, .25f), 0, 60);
            int fr = Mathf.FloorToInt(Time.unscaledTime * 1.6f);
            switch (line.who)
            {
                case "ayaz": case "kaya": case "mira":
                    var tex = Art.HeroPortrait(line.who == "ayaz" ? 0 : line.who == "kaya" ? 1 : 2, fr);
                    if (tex != null) GUI.DrawTexture(new Rect(pr.x, pr.y, pr.width, pr.width), tex, ScaleMode.ScaleToFit);
                    break;
                case "nuri": DrawSprite(new Rect(pr.x + 40, pr.y + 80, pr.width - 80, ph - 90), Art.Soldier(0)); break;
                case "lord": DrawSprite(new Rect(pr.x + 10, pr.y + 30, pr.width - 20, ph - 40), Art.Boss(9, fr), true); break;
                default: DrawSprite(new Rect(pr.x + 10, pr.y + 30, pr.width - 20, ph - 40), Art.Boss(Story.Chapter(level), fr), true); break;
            }

            // Konuşma kutusu
            float bw = Mathf.Min(460, VW - 16), bx = VW / 2 - bw / 2, by = 800 - 250, bh = 200;
            GUI.Box(new Rect(bx, by, bw, bh), "", UiSkin.Card);
            string name = Story.SpeakerName(line.who, level);
            var nameSt = new GUIStyle(UiSkin.Ribbon) { fontSize = 16 };
            GUI.Box(left ? new Rect(bx + 12, by - 22, 190, 40) : new Rect(bx + bw - 202, by - 22, 190, 40), name, nameSt);
            shown += Time.unscaledDeltaTime * 45;
            int n = Mathf.Min(line.text.Length, Mathf.FloorToInt(shown));
            GUI.Label(new Rect(bx + 22, by + 26, bw - 44, bh - 60), line.text.Substring(0, n), new GUIStyle(UiSkin.Label) { fontSize = 18, alignment = TextAnchor.UpperLeft });
            GUI.Label(new Rect(bx + 22, by + bh - 34, bw - 44, 26), $"<color=#9aa6b8>{idx + 1}/{scene.lines.Count}   Devam için dokun ›</color>", new GUIStyle(UiSkin.Small) { alignment = TextAnchor.MiddleRight, fontSize = 13 });

            if (GUI.Button(new Rect(VW - 96, 20, 84, 40), "Atla", new GUIStyle(UiSkin.ButtonGray) { fontSize = 15 })) { Finish(); return true; }
            // Ekranın kalanına dokunma: önce yazıyı tamamla, sonra sıradaki satır
            if (GUI.Button(new Rect(0, 0, VW, 800), "", GUIStyle.none))
            {
                if (n < line.text.Length) shown = line.text.Length;
                else { idx++; shown = 0; SoundFx.Play("click"); if (idx >= scene.lines.Count) Finish(); }
            }
            return true;
        }
    }
}
