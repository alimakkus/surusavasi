using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Geçici arayüz için yuvarlak köşeli, renkli buton ve kart stilleri (kodla üretilir).</summary>
    public static class UiSkin
    {
        static Texture2D Rounded(Color fill, Color border, Color top, int s = 32, int r = 9)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave }; // UnloadUnusedAssets silmesin
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

        /// <summary>Altıgen buton dokusu (üs menüsü).</summary>
        public static Texture2D Hex(Color fill, Color top, Color border, int s = 96)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave }; // UnloadUnusedAssets silmesin
            var px = new Color[s * s]; float c = s / 2f, R = s / 2f - 2;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float dx = Mathf.Abs(x + .5f - c), dy = Mathf.Abs(y + .5f - c);
                    // sivri uçları üstte-altta olan altıgen: uzaklık
                    float d = Mathf.Max(dx, dx * .5f + dy * .8660254f);
                    float a = Mathf.Clamp01(R * .8660254f - d + .5f);
                    var col = d > R * .8660254f - 5 ? border : Color.Lerp(fill, top, y / (float)s);
                    col.a *= a; px[y * s + x] = col;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static Texture2D eye;
        /// <summary>Göz simgesi (önizleme düğmesi): beyaz badem göz, mavi iris, parlak nokta.</summary>
        public static Texture2D Eye
        {
            get
            {
                if (eye != null) return eye;
                int s = 96; eye = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var px = new Color[s * s]; float c = s / 2f;
                var white = new Color(1, 1, 1); var outline = new Color(.12f, .16f, .26f); var iris = new Color(.25f, .55f, .95f); var pupil = new Color(.06f, .08f, .14f);
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float u = (x + .5f - c) / (s * .46f), v = (y + .5f - c) / (s * .46f);
                        float lid = .58f * (1 - u * u);                 // badem şekli: üst ve alt kapak
                        float edge = Mathf.Abs(v) - lid;                // <0 içeride
                        Color col = new Color(0, 0, 0, 0);
                        if (Mathf.Abs(u) <= 1 && edge < .07f)
                        {
                            col = edge > -.02f ? outline : white;
                            float r = Mathf.Sqrt(u * u + v * v);
                            if (edge <= -.02f && r < .36f) col = r < .17f ? pupil : (r > .32f ? outline : iris);
                            float hx = u + .1f, hy = v - .1f;
                            if (edge <= -.02f && hx * hx + hy * hy < .006f) col = white; // parıltı
                            col.a = Mathf.Clamp01((.07f - edge) * 40);
                        }
                        px[y * s + x] = col;
                    }
                eye.SetPixels(px); eye.Apply();
                return eye;
            }
        }

        static GUIStyle button, buttonGold, buttonGreen, buttonRed, buttonGray, card, cardGold, cardDark, label, small, title, big, ribbon;
        public static GUIStyle Button => Init() ? button : null;
        public static GUIStyle ButtonGold => Init() ? buttonGold : null;
        public static GUIStyle ButtonGreen => Init() ? buttonGreen : null;
        public static GUIStyle ButtonRed => Init() ? buttonRed : null;
        public static GUIStyle ButtonGray => Init() ? buttonGray : null;
        public static GUIStyle Card => Init() ? card : null;
        public static GUIStyle CardGold => Init() ? cardGold : null;
        public static GUIStyle CardDark => Init() ? cardDark : null;
        public static GUIStyle Label => Init() ? label : null;
        public static GUIStyle Small => Init() ? small : null;
        public static GUIStyle Title => Init() ? title : null;
        public static GUIStyle Big => Init() ? big : null;
        /// <summary>Kırmızı şerit başlık (ortalı, beyaz kalın yazı).</summary>
        public static GUIStyle Ribbon => Init() ? ribbon : null;

        static Texture2D T(string name)
        {
            var t = Resources.Load<Texture2D>("UI/" + name);
            if (t != null) t.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return t;
        }

        static readonly System.Collections.Generic.Dictionary<string, Texture2D> icons = new System.Collections.Generic.Dictionary<string, Texture2D>();
        /// <summary>Simge: gold, iron, food, gem, star, shard.</summary>
        public static Texture2D Icon(string name)
        {
            if (!icons.TryGetValue(name, out var t) || t == null) icons[name] = t = T("ic_" + name);
            return t;
        }

        /// <summary>Simge + sayı (sola hizalı). Genişliği döner.</summary>
        public static float IconValue(float x, float y, float h, string icon, string text, Color col)
        {
            var ic = Icon(icon);
            if (ic != null) GUI.DrawTexture(new Rect(x, y + h * .1f, h * .8f, h * .8f), ic, ScaleMode.ScaleToFit);
            var st = new GUIStyle(Small) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 15, wordWrap = false, normal = { textColor = col } };
            float tw = st.CalcSize(new GUIContent(text)).x;
            GUI.Label(new Rect(x + h * .85f, y, tw + 4, h), text, st);
            return h * .85f + tw + 4;
        }

        /// <summary>Üst kaynak çubuğu: altın, demir, yiyecek, elmas — simgeli, eşit aralıklı.</summary>
        public static void ResourceBar(float x0, float y, float w, int gold, int iron, int food, int gems)
        {
            GUI.Box(new Rect(x0, y, w, 38), "", CardDark);
            float cw = (w - 16) / 4;
            IconValue(x0 + 8, y + 2, 34, "gold", Short(gold), new Color(1f, .86f, .35f));
            IconValue(x0 + 8 + cw, y + 2, 34, "iron", Short(iron), new Color(.85f, .89f, .93f));
            IconValue(x0 + 8 + cw * 2, y + 2, 34, "food", Short(food), new Color(.62f, .93f, .62f));
            IconValue(x0 + 8 + cw * 3, y + 2, 34, "gem", Short(gems), new Color(.62f, .91f, 1f));
        }

        /// <summary>Kısa sayı: 12.400 → 12,4B; 1.250.000 → 1,25M.</summary>
        public static string Short(int v)
        {
            if (v >= 1000000) return (v / 1000000f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "M";
            if (v >= 10000) return (v / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "B";
            return v.ToString();
        }

        static GUIStyle Btn(string name, Color text, Texture2D fbN, Texture2D fbH, Texture2D fbA)
        {
            var n = T("btn_" + name); var h = T("btn_" + name + "_hover"); var a = T("btn_" + name + "_press");
            var st = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, richText = true, alignment = TextAnchor.MiddleCenter,
                border = n != null ? new RectOffset(16, 16, 15, 19) : new RectOffset(12, 12, 12, 12),
                padding = new RectOffset(8, 8, 3, 8), overflow = new RectOffset(0, 0, 0, 0),
                normal = { background = n ?? fbN, textColor = text },
                hover = { background = h ?? fbH, textColor = text },
                active = { background = a ?? fbA, textColor = text },
            };
            st.onNormal = st.normal; st.onHover = st.hover; st.onActive = st.active;
            return st;
        }

        static bool Init()
        {
            if (button != null) return true;
            Color c(string h) { ColorUtility.TryParseHtmlString(h, out var v); return v; }
            var fb = Rounded(c("#2b5f9e"), c("#163257"), c("#4a86cc"));
            button = Btn("blue", Color.white, fb, fb, fb);
            var fg = Rounded(c("#d99a1e"), c("#7a4f08"), c("#ffd23f"));
            buttonGold = Btn("gold", c("#2a1700"), fg, fg, fg);
            buttonGreen = Btn("green", Color.white, fb, fb, fb);
            buttonRed = Btn("red", Color.white, fb, fb, fb);
            buttonGray = Btn("gray", c("#e3e7ec"), fb, fb, fb);
            var cb = new RectOffset(22, 22, 22, 22);
            card = new GUIStyle(GUI.skin.box) { border = cb, normal = { background = T("card") ?? Rounded(new Color(.09f, .11f, .16f, .92f), new Color(.35f, .42f, .55f, .9f), new Color(.14f, .17f, .24f, .92f)) } };
            cardGold = new GUIStyle(card) { normal = { background = T("card_gold") ?? card.normal.background } };
            cardDark = new GUIStyle(card) { border = new RectOffset(20, 20, 20, 20), normal = { background = T("card_dark") ?? card.normal.background } };
            label = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, richText = true, alignment = TextAnchor.MiddleLeft, normal = { textColor = c("#e9edf5") } };
            small = new GUIStyle(label) { fontSize = 14 };
            title = new GUIStyle(label) { fontSize = 44, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = c("#ffd23f") } };
            big = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            ribbon = new GUIStyle(GUI.skin.box)
            {
                border = new RectOffset(26, 26, 10, 14), padding = new RectOffset(26, 26, 0, 8), alignment = TextAnchor.MiddleCenter,
                fontSize = 18, fontStyle = FontStyle.Bold, richText = true, normal = { background = T("ribbon"), textColor = Color.white },
            };
            return true;
        }
    }
}
