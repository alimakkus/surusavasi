using System.Collections.Generic;
using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Savaşı çizer (prototip çizimlerinden üretilen sprite'larla), efekt ve ses çalar,
    /// dokunmayı okur, sonuç ekranını ve ödüllü reklamı yönetir.</summary>
    public class BattleView : MonoBehaviour
    {
        static readonly Dictionary<int, int> Fails = new Dictionary<int, int>();

        BattleSim sim;
        int level;
        Camera cam;
        Sprite square, circle;
        Material spriteMat;
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        int used;
        string rewardText = "";
        bool rewardGiven, doubled;
        int completedThisSession;
        readonly Dictionary<string, Color> colors = new Dictionary<string, Color>();
        bool ready;

        // Efektler
        class Part { public float x, y, vx, vy, life, max, size; public Color c; }
        class Decal { public float x, wy, r; public Color c; }
        readonly List<Part> parts = new List<Part>();
        readonly List<Decal> decals = new List<Decal>();
        float shake, flash;
        int heroIndex;

        /// <summary>Menüye dön butonuna basılınca çağrılır (GameFlow bağlar).</summary>
        public System.Action onMenu;

        void Init()
        {
            if (ready) return;
            ready = true;
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); cam = go.AddComponent<Camera>(); go.tag = "MainCamera"; }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(.08f, .08f, .1f);
            cam.transform.position = new Vector3(0, 0, -10);

            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            spriteMat = sh != null ? new Material(sh) : null;
            square = MakeSquare();
            circle = MakeCircle(64);
        }

        public void Begin(int lv)
        {
            Init();
            gameObject.SetActive(true);
            StartLevel(lv);
        }

        public void Stop()
        {
            sim = null;
            foreach (var r in pool) if (r != null) r.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        void GoMenu() { Stop(); onMenu?.Invoke(); }

        void StartLevel(int lv)
        {
            level = lv;
            Fails.TryGetValue(lv, out int f);
            var mods = GameServices.Save != null ? Progress.BuildMods(GameServices.Save.Data) : new BattleMods();
            sim = new BattleSim(lv, mods, f);
            sim.Fx += OnFx;
            parts.Clear(); decals.Clear(); shake = 0; flash = 0;
            heroIndex = Progress.HeroIndex(mods.heroId);
            rewardText = ""; rewardGiven = false; doubled = false;
            GameServices.Analytics?.LogEvent(AnalyticsEvent.LevelStart, new Dictionary<string, object> { { "level", lv } });
        }

        // ---------- Kare ve daire sprite'ları kodla üretilir (dosya gerekmez) ----------
        static Sprite MakeSquare()
        {
            var tex = new Texture2D(4, 4); var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = Color.white;
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
        }

        static Sprite MakeCircle(int s)
        {
            var tex = new Texture2D(s, s); var px = new Color[s * s]; float r = s / 2f;
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - r) * (x + .5f - r) + (y + .5f - r) * (y + .5f - r));
                    px[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(r - d));
                }
            tex.SetPixels(px); tex.Apply(); tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(.5f, .5f), s);
        }

        Color C(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            if (!colors.TryGetValue(hex, out var c)) { ColorUtility.TryParseHtmlString(hex, out c); colors[hex] = c; }
            return c;
        }

        // Prototip koordinatı (360x560, y aşağı) -> dünya koordinatı
        static Vector3 W(float px, float py) => new Vector3((px - 180f) / 100f, (280f - py) / 100f, 0);

        void Draw(Sprite s, Color c, float px, float py, float w, float h, int order)
        {
            SpriteRenderer r;
            if (used < pool.Count) r = pool[used];
            else
            {
                var go = new GameObject("p"); go.transform.SetParent(transform);
                r = go.AddComponent<SpriteRenderer>();
                if (spriteMat != null) r.sharedMaterial = spriteMat;
                pool.Add(r);
            }
            used++;
            r.gameObject.SetActive(true);
            r.sprite = s; r.color = c; r.sortingOrder = order;
            r.transform.position = W(px, py);
            r.transform.localScale = new Vector3(w / 100f, h / 100f, 1);
        }

        void DrawSprite(Sprite s, float px, float py, float scale, int order, Color? tint = null)
        {
            if (s == null) return;
            Draw(s, tint ?? Color.white, px, py, scale * 100f, scale * 100f, order);
        }

        // ---------- Efektler ----------
        void Burst(float x, float y, int n, string[] cols, float speed, float up)
        {
            for (int i = 0; i < n && parts.Count < 400; i++)
            {
                float a = Random.value * Mathf.PI * 2, sp = speed * (.3f + Random.value * .7f);
                parts.Add(new Part { x = x, y = y, vx = Mathf.Cos(a) * sp, vy = Mathf.Sin(a) * sp - up, life = .4f + Random.value * .4f, size = 3 + Random.value * 3, c = C(cols[Random.Range(0, cols.Length)]) });
                parts[parts.Count - 1].max = parts[parts.Count - 1].life;
            }
        }

        void OnFx(string k, float x, float y)
        {
            if (GameServices.Save != null)
            {
                var sd = GameServices.Save.Data;
                if (k == "kill" || k == "bigkill") Daily.Add(sd, "kills");
                else if (k == "barrel") Daily.Add(sd, "barrels");
                else if (k == "power") Daily.Add(sd, "power");
                else if (k == "skill") Daily.Add(sd, "skill");
            }
            switch (k)
            {
                case "shot": SoundFx.Play(sim.mods.weaponId == "pistol" ? "shot" : "shot_" + sim.mods.weaponId); Burst(x, Field.SQY - 16, 2, new[] { "#fff6c2", "#ffd23f" }, 25, 40); break;
                case "hit": SoundFx.Play("hit"); Burst(x, y, 3, new[] { "#ffe27a", "#ffffff", "#ff9a3a" }, 60, 15); break;
                case "pickup": SoundFx.Play("pickup"); break;
                case "barrel": SoundFx.Play("board"); Burst(x, y - 8, 22, new[] { "#c98d49", "#6a3a14", "#ffd23f" }, 100, 50); break;
                case "power": SoundFx.Play("power"); Burst(x, y - 10, 26, new[] { "#ffffff", "#ffd23f", "#2fb6c9" }, 130, 60); break;
                case "kill": SoundFx.Play("kill"); Burst(x, y - 8, 20, new[] { "#8dbd80", "#5f8f55", "#e9f26c" }, 110, 30); Splat(x, y); break;
                case "bigkill": SoundFx.Play("boom"); shake = Mathf.Max(shake, 6); Burst(x, y - 20, 30, new[] { "#ffd23f", "#ffffff", "#ff7a3a" }, 150, 40); Splat(x, y); break;
                case "boom": SoundFx.Play("boom"); shake = Mathf.Max(shake, 3); Burst(x, y, 16, new[] { "#ff9a3a", "#ffd23f", "#ffffff" }, 110, 30); break;
                case "spit": SoundFx.Play("spit"); break;
                case "hurt": SoundFx.Play("hurt"); shake = Mathf.Max(shake, 6); Burst(x, y, 22, new[] { "#ff5a4d", "#b8322b", "#8dbd80" }, 110, 30); break;
                case "board": SoundFx.Play("board"); Burst(x, y, 18, new[] { "#b8322b", "#ffb3ab", "#6c4a2e" }, 90, 30); break;
                case "block": Burst(x, y, 8, new[] { "#ffffff", "#b6ff5a" }, 60, 10); break;
                case "skill": SoundFx.Play("skill"); shake = Mathf.Max(shake, 5); flash = 1; break;
                case "bossin": SoundFx.Play("boss"); break;
                case "bossdead": shake = 9; Burst(x, y - 20, 60, new[] { "#ffd23f", "#ffffff", "#8be39d", "#cbb8ff" }, 160, 60); break;
                case "win": SoundFx.Play("win"); break;
                case "lose": SoundFx.Play("lose"); shake = 8; Burst(x, Field.SQY - 6, 40, new[] { "#2f6fb3", "#cfe3f7", "#ff5a4d" }, 130, 40); break;
                case "revive": SoundFx.Play("power"); Burst(x, Field.SQY, 30, new[] { "#8be39d", "#ffffff" }, 120, 40); break;
            }
        }

        void Splat(float x, float y)
        {
            decals.Add(new Decal { x = x, wy = y - sim.scroll, r = 8 + Random.value * 7, c = new Color(.27f, .47f, .24f, .5f) });
            if (decals.Count > 40) decals.RemoveAt(0);
        }

        static float Hh(int i) { float v = Mathf.Sin(i * 127.1f + 311.7f) * 43758.5453f; return v - Mathf.Floor(v); }

        void Bar(float px, float py, float w, float frac, float chip, Color col)
        {
            Draw(square, new Color(0, 0, 0, .6f), px, py, w + 2, 6, 40);
            Draw(square, new Color(1, 1, 1, .7f), px - w / 2 + w * Mathf.Clamp01(chip) / 2, py, w * Mathf.Clamp01(chip), 4, 41);
            Draw(square, col, px - w / 2 + w * Mathf.Clamp01(frac) / 2, py, w * Mathf.Clamp01(frac), 4, 42);
        }

        void Update()
        {
            if (sim == null || cam == null) return;
            // Telefon dikeyken 360 genişlik sığsın
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            cam.orthographicSize = Mathf.Max(2.8f, 1.8f / Mathf.Max(.01f, aspect));

            if (sim.Live)
            {
                if (Input.GetMouseButton(0) && GUIUtility.hotControl == 0 && Input.mousePosition.y > Screen.height * .14f)
                {
                    var wp = cam.ScreenToWorldPoint(Input.mousePosition);
                    sim.tx = Mathf.Clamp(wp.x * 100f + 180f, 0, Field.W);
                }
                float k = (Input.GetKey(KeyCode.LeftArrow) ? -1 : 0) + (Input.GetKey(KeyCode.RightArrow) ? 1 : 0);
                if (k != 0) sim.tx = Mathf.Clamp(sim.x + k * 60, 0, Field.W);
                if (Input.GetKeyDown(KeyCode.Space)) sim.UseSkill();
#if UNITY_EDITOR
                // Test kısayolları (sadece Editor'de): K = yenil, PageUp/PageDown = bölüm değiştir
                if (Input.GetKeyDown(KeyCode.K)) sim.DamageSquad(sim.n + 999);
                if (Input.GetKeyDown(KeyCode.PageUp)) { StartLevel(Mathf.Min(Balance.MaxLevel, level + 1)); return; }
                if (Input.GetKeyDown(KeyCode.PageDown)) { StartLevel(Mathf.Max(1, level - 1)); return; }
#endif
                sim.Step(Mathf.Min(Time.deltaTime, .05f));
                if (!sim.Live) OnBattleEnd();
            }
            else sim.Step(Mathf.Min(Time.deltaTime, .05f)); // yazılar sönsün

            float dt = Mathf.Min(Time.deltaTime, .05f);
            foreach (var p in parts) { p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 260 * dt; p.life -= dt; }
            parts.RemoveAll(p => p.life <= 0);
            shake = Mathf.Max(0, shake - dt * 22); flash = Mathf.Max(0, flash - dt * 2.5f);
            cam.transform.position = new Vector3((Random.value - .5f) * shake / 100f, (Random.value - .5f) * shake / 100f, -10);

            Render();
        }

        void Render()
        {
            used = 0;
            int m = sim.map;
            var M = BattleData.Maps[m];
            // Zemin: prototipteki harita dokusu kayarak tekrar eder
            var bg = Art.Background(m);
            float off = sim.scroll % 480f;
            for (float y0 = off - 480f; y0 < Field.H; y0 += 480f)
            {
                if (bg != null) DrawSprite(bg, 180, y0 + 240, 1, 0);
                else Draw(square, C(M.ground), 180, y0 + 240, 360, 480, 0);
            }
            foreach (var d in decals) { float sy = d.wy + sim.scroll; if (sy > -20 && sy < Field.H + 20) Draw(circle, d.c, d.x, sy, d.r * 2, d.r * 1.1f, 2); }
            // Ağaçlar
            var tree = Art.Tree(m);
            int i0 = Mathf.CeilToInt((sim.scroll - Field.H - 70) / 110f), i1 = Mathf.FloorToInt((sim.scroll + 70) / 110f);
            for (int i = i0; i <= i1; i++)
            {
                float ty = -i * 110 + sim.scroll + 20;
                if (Hh(i) > .2f) DrawSprite(tree, 14 + Hh(i + 5) * 10, ty, .85f + Hh(i + 9) * .4f, 3);
                if (Hh(i + 50) > .2f) DrawSprite(tree, Field.W - 14 - Hh(i + 15) * 10, ty + 40, .85f + Hh(i + 19) * .4f, 3);
            }

            int frame = Mathf.FloorToInt(Time.time * 4) & 1;
            foreach (var o in sim.objs)
            {
                if (o.done) continue;
                float sy = sim.Osy(o);
                if (sy < -90 || sy > Field.H + 60) continue;
                int ord = 10 + Mathf.Clamp((int)(sy / 4), 0, 200);
                Color? tint = o.flash > 0 ? new Color(1.6f, 1.6f, 1.6f) : (Color?)null;
                switch (o.kind)
                {
                    case ObjKind.Barrel:
                        DrawSprite(Art.Prop(0), o.x, sy, 1, ord, tint);
                        Bar(o.x, sy - 38, 54, o.hp / o.max, o.chip / o.max, C("#8be39d"));
                        break;
                    case ObjKind.Power:
                        DrawSprite(Art.Prop(o.pk == Power.Double ? 1 : o.pk == Power.Rapid ? 2 : 3), o.x, sy + Mathf.Sin(Time.time * 4 + o.x) * 2, 1, ord, tint);
                        Bar(o.x, sy - 38, 54, o.hp / o.max, o.chip / o.max, C("#8be39d"));
                        break;
                    case ObjKind.Board:
                        DrawSprite(Art.Prop(4), o.x, sy, 1, ord);
                        break;
                    default:
                        var T = o.type; float sc = T.sc * sim.mapScale;
                        var es = Art.Enemy(m, T.id, (frame + o.seed) & 1);
                        if (es != null) DrawSprite(es, o.x, sy, 1, ord, tint ?? (T.ghost ? new Color(1, 1, 1, .75f) : (Color?)null));
                        else Draw(circle, C(T.skin ?? M.enemySkin), o.x, sy - 10, 30, 30, ord);
                        float top = T.big ? sy - 21 * sc - 8 : sy - 17 * sc * 1.4f - 12;
                        Bar(o.x, top, Mathf.Min(84, 30 + 14 * sc * (T.big ? 1 : 1.4f)), o.hp / o.max, o.chip / o.max, C("#ff5a4d"));
                        break;
                }
            }

            if (sim.boss != null && sim.state != BattleState.Won)
            {
                var B = sim.boss;
                Color? bt = B.flash > 0 ? new Color(1.6f, 1.6f, 1.6f) : (Color?)null;
                if (B.isBoss) DrawSprite(Art.Boss(B.bossIndex, frame), B.x, B.y, B.sc / 3.9f, 260, bt);
                else DrawSprite(Art.Elite(B.typeId, frame), B.x, B.y, B.sc / 2.6f, 260, bt);
            }

            // Sürü: askerler + kahraman
            if (sim.state != BattleState.Lost)
            {
                int cnt = Mathf.Min(40, Mathf.CeilToInt(sim.Soldiers));
                float rad = sim.Radius;
                for (int i = cnt - 1; i >= 0; i--)
                {
                    float a = i * 2.39996f, rr = rad * Mathf.Sqrt((i + .5f) / Mathf.Max(1, cnt));
                    float sx = sim.x + Mathf.Cos(a) * rr, sy2 = Field.SQY + 4 + Mathf.Sin(a) * rr * .6f;
                    DrawSprite(Art.Soldier((int)(Time.time * 8 + i)), sx, sy2, 1, 280 + (int)(sy2 - Field.SQY + 40));
                }
                DrawSprite(Art.Hero(heroIndex, frame), sim.x, Field.SQY + 2, 1, 340);
            }

            foreach (var b in sim.bullets)
            {
                var bc = sim.buffBlast > 0 ? C("#ff9a3a") : C(sim.mods.weaponColor);
                Draw(circle, new Color(bc.r, bc.g, bc.b, .35f), b.x, b.y, 12, 16, 345);
                Draw(circle, bc, b.x, b.y, 5, 9, 346);
            }
            foreach (var e in sim.eb)
            {
                var ec = C(e.color);
                Draw(circle, new Color(ec.r, ec.g, ec.b, .35f), e.x, e.y, 18, 18, 347);
                Draw(circle, ec, e.x, e.y, 9, 9, 348);
            }
            foreach (var p in parts)
            {
                var pc = p.c; pc.a = Mathf.Clamp01(p.life / p.max);
                Draw(circle, pc, p.x, p.y, p.size, p.size, 350);
            }
            if (flash > 0) Draw(square, new Color(.8f, .72f, 1f, flash * .35f), 180, 280, 400, 700, 360);

            for (int i = used; i < pool.Count; i++) if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        void OnBattleEnd()
        {
            if (sim.state == BattleState.Won)
            {
                Fails[level] = 0;
                completedThisSession++;
                if (GameServices.Save != null) { Daily.Add(GameServices.Save.Data, "clear"); if (sim.stars == 3) Daily.Add(GameServices.Save.Data, "stars3"); }
                GrantRewards();
            }
            else
            {
                Fails.TryGetValue(level, out int f); Fails[level] = f + 1;
            }
            GameServices.Analytics?.LogEvent(AnalyticsEvent.LevelEnd, new Dictionary<string, object>
                { { "level", level }, { "win", sim.state == BattleState.Won }, { "stars", sim.stars }, { "revived", sim.revived } });
        }

        int rwGold, rwIron, rwFood, rwGems;
        void GrantRewards()
        {
            if (rewardGiven || GameServices.Save == null) return;
            rewardGiven = true;
            var d = GameServices.Save.Data;
            int idx = level - 1;
            bool first = d.stars[idx] == 0;
            float mu = (first ? 1f : .4f) * (.7f + .15f * sim.stars);
            var ev = Daily.CurrentEvent();
            rwGold = Mathf.RoundToInt((30 + 6 * level) * mu * sim.mods.goldMul * ev.all * ev.gold);
            rwIron = Mathf.RoundToInt((14 + 3 * level) * mu * sim.mods.marketMul * ev.all * ev.iron);
            rwFood = Mathf.RoundToInt((18 + 4 * level) * mu * sim.mods.marketMul * ev.all);
            rwGems = first ? Mathf.RoundToInt((sim.stars + (level % 10 == 0 ? 5 : 0)) * ev.gem) : 0;
            d.stars[idx] = Mathf.Max(d.stars[idx], sim.stars);
            if (level < Balance.MaxLevel) d.unlocked = Mathf.Max(d.unlocked, level + 1);
            var w = GameServices.Wallet;
            w.Add(Currency.Gold, rwGold, "level"); w.Add(Currency.Iron, rwIron, "level"); w.Add(Currency.Food, rwFood, "level");
            if (rwGems > 0) w.Add(Currency.Diamonds, rwGems, "level");
            GameServices.Save.Save();
            rewardText = $"+{rwGold} altın  +{rwIron} demir  +{rwFood} yiyecek" + (rwGems > 0 ? $"  +{rwGems} elmas" : "") + (first ? "" : "  (tekrar)");
        }

        void DoubleRewards()
        {
            Shop.WatchAd(AdPlacement.DoubleReward, () =>
            {
                doubled = true;
                var w = GameServices.Wallet;
                w.Add(Currency.Gold, rwGold, "ad_double"); w.Add(Currency.Iron, rwIron, "ad_double"); w.Add(Currency.Food, rwFood, "ad_double");
                rewardText = $"+{rwGold * 2} altın  +{rwIron * 2} demir  +{rwFood * 2} yiyecek" + (rwGems > 0 ? $"  +{rwGems} elmas" : "") + "  (2 kat)";
            });
        }

        // ---------- Arayüz (geçici: OnGUI ile; sonra Unity UI ile değişecek) ----------
        GUIStyle label, big, btn;
        float gs;

        Vector2 ToGui(float px, float py)
        {
            var sp = cam.WorldToScreenPoint(W(px, py));
            return new Vector2(sp.x / gs, (Screen.height - sp.y) / gs);
        }

        void Text(Vector2 p, string s, Color c, GUIStyle st, float w = 160)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, c.a * .8f); GUI.Label(new Rect(p.x - w / 2 + 1.5f, p.y - 12 + 1.5f, w, 26), s, st);
            GUI.color = c; GUI.Label(new Rect(p.x - w / 2, p.y - 12, w, 26), s, st);
            GUI.color = old;
        }

        void OnGUI()
        {
            if (sim == null || cam == null) return;
            gs = Screen.height / 800f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(gs, gs, 1));
            float VW = Screen.width / gs;
            if (label == null)
            {
                label = new GUIStyle(UiSkin.Label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16, wordWrap = false };
                big = new GUIStyle(UiSkin.Big);
                btn = UiSkin.Button;
            }

            // Nesne yazıları
            foreach (var o in sim.objs)
            {
                if (o.done) continue;
                float sy = sim.Osy(o); if (sy < -40 || sy > Field.H) continue;
                if (o.kind == ObjKind.Barrel) Text(ToGui(o.x, sy + 7), "+" + Mathf.CeilToInt(o.v - o.rel), Color.white, big);
                else if (o.kind == ObjKind.Power) Text(ToGui(o.x, sy + 15), o.pk == Power.Double ? "ÇİFT" : o.pk == Power.Rapid ? "HIZ" : "PATLA", Color.white, label);
                else if (o.kind == ObjKind.Board) Text(ToGui(o.x, sy - 4), "−" + Mathf.CeilToInt(o.v), Color.white, big);
            }
            foreach (var f in sim.floats) { var c = C(f.color); c.a = Mathf.Clamp01(f.life * 1.5f); Text(ToGui(f.x, f.y), f.text, c, label); }
            if (sim.state != BattleState.Lost) Text(ToGui(sim.x, Field.SQY + 34), BattleSim.Fmt(sim.n), Color.white, big);

            // Üst bilgi
            var M = BattleData.Maps[sim.map];
            Text(new Vector2(VW / 2, 22), $"Bölüm {level} · {M.nameTr}", Color.white, label, 400);
            float hpF = sim.hhp / Mathf.Max(1, sim.HeroMax);
            GUI.DrawTexture(new Rect(VW / 2 - 100, 40, 200, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .6f), 0, 0);
            GUI.DrawTexture(new Rect(VW / 2 - 100, 40, 200 * hpF, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, C("#8be39d"), 0, 0);
            if (GameServices.Wallet != null)
                Text(new Vector2(VW - 90, 22), "Elmas: " + GameServices.Wallet.Get(Currency.Diamonds), C("#9fe8ff"), label, 170);
            if (sim.boss != null && sim.Live)
            {
                var B = sim.boss;
                Text(new Vector2(VW / 2, 66), (B.isBoss ? "PATRON: " : "") + B.name, C("#ffd23f"), label, 400);
                GUI.DrawTexture(new Rect(VW / 2 - 140, 82, 280, 12), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .6f), 0, 0);
                GUI.DrawTexture(new Rect(VW / 2 - 140, 82, 280 * Mathf.Clamp01(B.hp / B.max), 12), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, C("#ff5a4d"), 0, 0);
            }
            if (sim.t < 2.6f && sim.Live)
                Text(new Vector2(VW / 2, 300), $"BÖLÜM {level}", Color.white, big, 400);

            if (sim.Live && GUI.Button(new Rect(8, 8, 70, 36), "Üs", btn)) { GoMenu(); return; }

            // Yetenek butonu
            if (sim.Live)
            {
                GUI.enabled = sim.SkillReady;
                string sk = sim.SkillReady ? "YETENEK" : sim.cd.ToString("0.0") + " sn";
                if (GUI.Button(new Rect(VW / 2 - 90, 800 - 90, 180, 64), sk, btn)) sim.UseSkill();
                GUI.enabled = true;
            }
            else DrawResult(VW);
        }

        void DrawResult(float VW)
        {
            float pw = Mathf.Min(360, VW - 20), px = VW / 2 - pw / 2, py = 230;
            GUI.Box(new Rect(px, py, pw, 330), "", UiSkin.Card);
            bool won = sim.state == BattleState.Won;
            Text(new Vector2(VW / 2, py + 34), won ? $"Bölüm {level} tamam" : "Yenildin", won ? C("#8be39d") : C("#ff8a80"), big, pw);
            if (won)
            {
                Text(new Vector2(VW / 2, py + 76), new string('★', sim.stars) + new string('☆', 3 - sim.stars), C("#ffd23f"), big, pw);
                Text(new Vector2(VW / 2, py + 112), rewardText, Color.white, label, pw);
                float by = py + 140;
                if (!doubled && GUI.Button(new Rect(px + 20, by, pw - 40, 50), "▶ Ödülü 2 katına çıkar (reklam)", UiSkin.ButtonGold)) DoubleRewards();
                by += 60;
                if (level < Balance.MaxLevel && GUI.Button(new Rect(px + 20, by, pw - 40, 50), "Sonraki bölüm", btn))
                {
                    if (completedThisSession % 3 == 0) Shop.MaybeShowInterstitial(AdPlacement.LevelEnd);
                    StartLevel(level + 1);
                }
                by += 60;
                if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Tekrar oyna", btn)) StartLevel(level);
                if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Üsse dön", btn)) GoMenu();
            }
            else
            {
                Text(new Vector2(VW / 2, py + 80), "Kahramanın düştü.", Color.white, label, pw);
                float by = py + 120;
                if (sim.CanRevive && GUI.Button(new Rect(px + 20, by, pw - 40, 56), "▶ İkinci şans (reklam izle)", UiSkin.ButtonGold))
                    Shop.WatchAd(AdPlacement.SecondChance, () => sim.Revive());
                by += 70;
                if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Tekrar dene", btn)) StartLevel(level);
                if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Üsse dön", btn)) GoMenu();
            }
        }
    }
}
