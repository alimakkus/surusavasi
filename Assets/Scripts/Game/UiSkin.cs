using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Geçici arayüz için yuvarlak köşeli, renkli buton ve kart stilleri (kodla üretilir).</summary>
    public static class UiSkin
    {
        static Texture2D Rounded(Color fill, Color border, Color top, int s = 32, int r = 9)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[s * s];
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Max(0, Mathf.Max(r - x - .5f, x + .5f - (s - r))), dy = Mathf.Max(0, Mathf.Max(r - y - .5f, y + .5f - (s - r)));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(r - d + .5f);
                    bool edge = d > r - 2.2f || x < 2 || y < 2 || x > s - 3 || y > s - 3;
                    var c = edge ? border : Color.Lerp(fill, top, y / (float)s * .9f);
                    c.a *= a; px[y * s + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static GUIStyle button, buttonGold, card, label, small, title, big;
        public static GUIStyle Button => Init() ? button : null;
        public static GUIStyle ButtonGold => Init() ? buttonGold : null;
        public static GUIStyle Card => Init() ? card : null;
        public static GUIStyle Label => Init() ? label : null;
        public static GUIStyle Small => Init() ? small : null;
        public static GUIStyle Title => Init() ? title : null;
        public static GUIStyle Big => Init() ? big : null;

        static bool Init()
        {
            if (button != null) return true;
            var b = new RectOffset(12, 12, 12, 12);
            Color c(string h) { ColorUtility.TryParseHtmlString(h, out var v); return v; }
            button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, richText = true, border = b, alignment = TextAnchor.MiddleCenter,
                normal = { background = Rounded(c("#2b5f9e"), c("#163257"), c("#4a86cc")), textColor = Color.white },
                hover = { background = Rounded(c("#3570b5"), c("#163257"), c("#5a97dd")), textColor = Color.white },
                active = { background = Rounded(c("#1f4a7c"), c("#163257"), c("#2b5f9e")), textColor = Color.white },
            };
            button.onNormal = button.normal; button.onHover = button.hover; button.onActive = button.active;
            buttonGold = new GUIStyle(button)
            {
                normal = { background = Rounded(c("#d99a1e"), c("#7a4f08"), c("#ffd23f")), textColor = c("#2a1a00") },
                hover = { background = Rounded(c("#e8aa2c"), c("#7a4f08"), c("#ffe06a")), textColor = c("#2a1a00") },
                active = { background = Rounded(c("#b97f10"), c("#7a4f08"), c("#d99a1e")), textColor = c("#2a1a00") },
            };
            card = new GUIStyle(GUI.skin.box) { border = b, normal = { background = Rounded(new Color(.09f, .11f, .16f, .92f), new Color(.35f, .42f, .55f, .9f), new Color(.14f, .17f, .24f, .92f)) } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true, alignment = TextAnchor.MiddleLeft, normal = { textColor = c("#e9edf5") } };
            small = new GUIStyle(label) { fontSize = 14 };
            title = new GUIStyle(label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = c("#ffd23f") } };
            big = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            return true;
        }
    }
}
