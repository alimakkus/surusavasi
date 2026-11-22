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

        // ---------- His paketi: başlık bandı, sayı zıplaması, vuruş yavaşlatma ----------
        public static BattleView Current { get; private set; }
        string bannerText, bannerSub, bannerColor = "#ffffff"; float bannerT, bannerDur;
        float countPop, countHurt, lastN, slowT, gateFlash; Color gateFlashCol;
        bool bossBannerShown; int lastTick = -1;

        /// <summary>Ekranın ortasından kayarak geçen büyük başlık (bölüm, dalga, kat, patron).</summary>
        public void Banner(string text, string sub = null, string color = "#ffffff", float dur = 1.9f)
        { bannerText = text; bannerSub = sub; bannerColor = color; bannerT = 0; bannerDur = dur; }

        void ResetJuice()
        {
            bannerText = null; countPop = countHurt = slowT = gateFlash = 0; lastN = sim != null ? sim.n : 0;
            bossBannerShown = false; lastTick = -1;
            SoundFx.Ambient("battle");
        }

        void Init()
        {
            Current = this;
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
            mode = null;
            StartLevel(lv);
        }

        // ---------- Oyun modları (Kule, Patron Avı): aynı savaş motoru, farklı kurallar ----------
        IBattleMode mode;
        public BattleSim Sim => sim;
        public int Level => level;

        public void BeginMode(IBattleMode m)
        {
            Init();
            gameObject.SetActive(true);
            StartMode(m);
        }

        public void StartMode(IBattleMode m)
        {
            mode = m;
            var mods = GameServices.Save != null ? Progress.BuildMods(GameServices.Save.Data) : new BattleMods();
            m.Configure(mods);
            level = m.Level;
            sim = new BattleSim(level, mods, 0, 0, m.HardMul);
            sim.Fx += OnFx;
            m.Setup(sim);
            parts.Clear(); decals.Clear(); shake = 0; flash = 0;
            heroIndex = Progress.HeroIndex(mods.heroId);
            rewardText = ""; rewardGiven = false; doubled = false;
            GameServices.Analytics?.LogEvent(AnalyticsEvent.LevelStart, new Dictionary<string, object> { { "level", level }, { "mode", m.Id } });
            ResetJuice();
            Banner(m.Intro, m.Title, "#ffd23f");
        }

        public void Stop()
        {
            sim = null;
            foreach (var r in pool) if (r != null) r.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        public void GoMenu() { Stop(); mode = null; SoundFx.Ambient(null); onMenu?.Invoke(); }

        void StartLevel(int lv)
        {
            mode = null;
            level = lv;
            Fails.TryGetValue(lv, out int f);
            var mods = GameServices.Save != null ? Progress.BuildMods(GameServices.Save.Data) : new BattleMods();
            int trained = 0; bool boosted = false;
            if (GameServices.Save != null) boosted = Offers.ApplyBoost(GameServices.Save.Data, mods);
            if (GameServices.Save != null) { Troops.Tick(GameServices.Save.Data, BaseLogic.Now); trained = Troops.TakeForBattle(GameServices.Save.Data); mods.startBonus += trained; GameServices.Save.Save(); }
            sim = new BattleSim(lv, mods, f);
            sim.Fx += OnFx;
            if (trained > 0) sim.AddFloat(Field.W / 2, Field.SQY - 90, $"+{trained} eğitimli asker", "#8be39d");
            if (boosted) sim.AddFloat(Field.W / 2, Field.SQY - 130, "Güç takviyesi!", "#ffd23f");
            parts.Clear(); decals.Clear(); shake = 0; flash = 0;
            heroIndex = Progress.HeroIndex(mods.heroId);
            rewardText = ""; rewardGiven = false; doubled = false;
            GameServices.Analytics?.LogEvent(AnalyticsEvent.LevelStart, new Dictionary<string, object> { { "level", lv } });
            ResetJuice();
            Banner($"BÖLÜM {lv}", BattleData.Maps[sim.map].nameTr + (Story.Chapter(lv) >= 0 ? " · " + Story.ChapterNames[Mathf.Clamp(Story.Chapter(lv), 0, Story.ChapterNames.Length - 1)] : ""), lv % 10 == 0 ? "#ff8a80" : "#ffffff");
            // Hikâye: bölüm açılışı / patron öncesi sahne
            if (GameServices.Save != null) DialogBox.Play(Story.Before(GameServices.Save.Data, lv), lv, null);
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
                case "gate_good":
                    SoundFx.Play("gate_good"); gateFlash = 1; gateFlashCol = C("#5ad1ff");
                    Burst(x, y - 20, 36, new[] { "#5ad1ff", "#ffffff", "#8be39d", "#ffd23f" }, 170, 70); break;
                case "gate_bad":
                    SoundFx.Play("gate_bad"); gateFlash = 1; gateFlashCol = C("#ff3b30"); shake = Mathf.Max(shake, 8);
                    Burst(x, y - 20, 30, new[] { "#ff5a4d", "#b8322b", "#2b0f0f" }, 140, 40); break;
                case "bigkill": slowT = Mathf.Max(slowT, .12f); SoundFx.Play("boom"); shake = Mathf.Max(shake, 6); Burst(x, y - 20, 30, new[] { "#ffd23f", "#ffffff", "#ff7a3a" }, 150, 40); Splat(x, y); break;
                case "boom": SoundFx.Play("boom"); shake = Mathf.Max(shake, 3); Burst(x, y, 16, new[] { "#ff9a3a", "#ffd23f", "#ffffff" }, 110, 30); break;
                case "spit": SoundFx.Play("spit"); break;
                case "hurt": SoundFx.Play("hurt"); shake = Mathf.Max(shake, 6); Burst(x, y, 22, new[] { "#ff5a4d", "#b8322b", "#8dbd80" }, 110, 30); break;
                case "board": SoundFx.Play("board"); Burst(x, y, 18, new[] { "#b8322b", "#ffb3ab", "#6c4a2e" }, 90, 30); break;
                case "block": Burst(x, y, 8, new[] { "#ffffff", "#b6ff5a" }, 60, 10); break;
                case "skill": SoundFx.Play("skill"); shake = Mathf.Max(shake, 5); flash = 1; break;
                case "bossin":
                    if (!bossBannerShown && sim.boss != null)
                    {
                        bossBannerShown = true; shake = Mathf.Max(shake, sim.boss.isBoss ? 12 : 6);
                        SoundFx.Play(sim.boss.isBoss ? "roar" : "boss");
                        if (sim.boss.isBoss) { Banner("PATRON", sim.boss.name, "#ff5a4d", 2.2f); SoundFx.Ambient("boss_fight"); }
                        else Banner("ELİT", sim.boss.name, "#ffb02e", 1.6f);
                    }
                    else { SoundFx.Play("roar"); shake = Mathf.Max(shake, 10); Banner("ÖFKELENDİ!", "Zırhı çatladı: şimdi vur!", "#ffb02e", 1.5f); }
                    break;
                case "bossdead": slowT = .45f; shake = 9; SoundFx.Play("boom"); Burst(x, y - 20, 60, new[] { "#ffd23f", "#ffffff", "#8be39d", "#cbb8ff" }, 160, 60); break;
                case "win":
                    SoundFx.Play("win"); SoundFx.Ambient(null);
                    // Konfeti: üstten renkli yağmur
                    for (int cf = 0; cf < 6; cf++) Burst(40 + cf * 56, 40, 14, new[] { "#ffd23f", "#8be39d", "#5ad1ff", "#ff6f91", "#c9b8ff" }, 120, 10);
                    break;
                case "lose": SoundFx.Play("lose"); SoundFx.Ambient(null); shake = 8; Burst(x, Field.SQY - 6, 40, new[] { "#2f6fb3", "#cfe3f7", "#ff5a4d" }, 130, 40); break;
                case "revive": SoundFx.Play("power"); SoundFx.Ambient(sim.boss != null && sim.boss.isBoss ? "boss_fight" : "battle"); Burst(x, Field.SQY, 30, new[] { "#8be39d", "#ffffff" }, 120, 40); break;
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

            if (DialogBox.Active) return; // hikâye sahnesi oynarken savaş durur
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
                if (mode != null && mode.TimeLimit > 0 && sim.t >= mode.TimeLimit) sim.ForceEnd(false);
#if UNITY_EDITOR
                // Test kısayolları (sadece Editor'de): K = yenil, PageUp/PageDown = bölüm değiştir
                if (Input.GetKeyDown(KeyCode.K)) sim.DamageSquad(sim.n + 999);
                if (mode == null && Input.GetKeyDown(KeyCode.PageUp)) { StartLevel(Mathf.Min(Balance.MaxLevel, level + 1)); return; }
                if (mode == null && Input.GetKeyDown(KeyCode.PageDown)) { StartLevel(Mathf.Max(1, level - 1)); return; }
#endif
                if (mode != null && mode.Paused) { Render(); return; }
                // Vuruş yavaşlatma: dev düşman / patron ölünce an kısa süre ağırlaşır
                float sdt = Mathf.Min(Time.deltaTime, .05f) * (slowT > 0 ? .2f : 1);
                slowT = Mathf.Max(0, slowT - Time.unscaledDeltaTime);
                if (sim.Live) sim.Step(sdt);
                if (mode != null && sim.Live) mode.Tick(sim, sdt);
                // Son 10 saniye tik sesi (süreli modlar)
                if (mode != null && mode.TimeLimit > 0 && sim.Live)
                {
                    int left = Mathf.CeilToInt(mode.TimeLimit - sim.t);
                    if (left <= 10 && left != lastTick) { lastTick = left; SoundFx.Play("tick"); }
                }
                if (!sim.Live) OnBattleEnd();
            }
            else sim.Step(Mathf.Min(Time.deltaTime, .05f)); // yazılar sönsün

            float dt = Mathf.Min(Time.deltaTime, .05f);
            // Ordu sayısı değişince zıplasın: artınca yeşil, azalınca kırmızı
            if (sim.n > lastN + .5f) countPop = 1; else if (sim.n < lastN - .5f) countHurt = 1;
            lastN = sim.n;
            countPop = Mathf.Max(0, countPop - dt * 3); countHurt = Mathf.Max(0, countHurt - dt * 3);
            gateFlash = Mathf.Max(0, gateFlash - dt * 2.5f);
            bannerT += Time.unscaledDeltaTime;
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
                    case ObjKind.Gate:
                    {
                        // Yarı saydam enerji kapısı: ordunun bulunduğu taraf parlar
                        bool good = LevelGen.GateGood(o), mine = (o.x < Field.W / 2) == (sim.x < Field.W / 2);
                        var gc = good ? (o.gk == GateFx.Add || o.gk == GateFx.Mul ? C("#3aa0ff") : C("#2fd08a")) : C("#ff4d4d");
                        float pulse = .5f + .5f * Mathf.Sin(Time.time * 5 + o.x);
                        gc.a = mine ? .55f + .15f * pulse : .32f;
                        Draw(square, gc, o.x, sy, Field.W * .46f, 46, 9);
                        var edge = gc; edge.a = Mathf.Min(1, gc.a + .35f);
                        Draw(square, edge, o.x, sy - 22, Field.W * .46f, 4, 9);
                        Draw(square, edge, o.x, sy + 22, Field.W * .46f, 4, 9);
                        float px = o.x < Field.W / 2 ? o.x - Field.W * .23f : o.x + Field.W * .23f;
                        Draw(square, C("#e8e8f0"), px, sy, 8, 56, 9);
                        break;
                    }
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
            // Hayatta Kalma savunma kuleleri: yol kenarında, seviyeye göre büyür
            for (int side = 0; side < 2; side++)
            {
                int tl = side == 0 ? sim.turretL : sim.turretR; if (tl <= 0) continue;
                var tw = Art.WallTower(Mathf.Clamp(tl, 1, 5));
                if (tw != null) DrawSprite(tw, side == 0 ? 26 : Field.W - 26, Field.SQY + 6, .62f + .04f * tl, 10 + (int)(Field.SQY / 4) + 1);
            }
            if (flash > 0) Draw(square, new Color(.8f, .72f, 1f, flash * .35f), 180, 280, 400, 700, 360);

            for (int i = used; i < pool.Count; i++) if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        void OnBattleEnd()
        {
            if (mode != null)
            {
                mode.OnEnd(sim);
                GameServices.Analytics?.LogEvent(AnalyticsEvent.LevelEnd, new Dictionary<string, object>
                    { { "level", level }, { "mode", mode.Id }, { "win", sim.state == BattleState.Won } });
                return;
            }
            if (sim.state == BattleState.Won)
            {
                if (GameServices.Save != null) { var after = Story.After(GameServices.Save.Data, level); if (after != null) DialogBox.Play(after, level, null); }
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
        string dropText = "";
        void GrantRewards()
        {
            if (rewardGiven || GameServices.Save == null) return;
            rewardGiven = true;
            var d = GameServices.Save.Data;
            int idx = level - 1;
            bool first = d.stars[idx] == 0;
            var rw = Rewards.ForLevel(level, sim.stars, first, sim.mods, Daily.CurrentEvent());
            rwGold = rw.gold; rwIron = rw.iron; rwFood = rw.food; rwGems = rw.gems;
            dropText = "";
            if (sim.state == BattleState.Won)
            {
                Offers.PiggyAdd(d, first);
                League.Add(d, first ? League.PtsLevelFirst : League.PtsLevelRepeat);
                dropText += $"\n<size=13><color=#ffb02e>Kumbara: {d.piggy}/{Offers.PiggyCap} elmas</color></size>";
                var drop = Gear.BattleDrop(d, level, new System.Random());
                if (drop != null) dropText = $"\n<color={Gear.RarityColors[drop.rarity]}><b>Yeni ekipman: {Gear.Name(drop)} ({Gear.RarityNames[drop.rarity]})</b></color>";
                // Patron sandığı: patron bölümünde ek altın; öfkelenen patrondan ek Epik şansı; son patron ilk zaferde Efsanevi verir
                if (level % 10 == 0)
                {
                    var rnd = new System.Random();
                    int chestGold = (int)(rwGold * (first ? 1f : .3f));
                    rwGold += chestGold;
                    dropText += $"\n<color=#ffd23f><b>Patron sandığı: +{chestGold} altın</b></color>";
                    GearItem extra = null;
                    if (level >= Balance.MaxLevel && first) extra = new GearItem { id = ++d.gearSeq, slot = rnd.Next(3), rarity = 3 };
                    else if (sim.boss != null && sim.boss.phase > 0 && rnd.NextDouble() < (first ? .5 : .15)) extra = new GearItem { id = ++d.gearSeq, slot = rnd.Next(3), rarity = 2 };
                    if (extra != null)
                    {
                        Gear.AddToBag(d, extra);
                        dropText += $"\n<color={Gear.RarityColors[extra.rarity]}><b>Patron ganimeti: {Gear.Name(extra)} ({Gear.RarityNames[extra.rarity]})</b></color>";
                    }
                }
            }
            d.stars[idx] = Mathf.Max(d.stars[idx], sim.stars);
            if (level < Balance.MaxLevel) d.unlocked = Mathf.Max(d.unlocked, level + 1);
            var w = GameServices.Wallet;
            w.Add(Currency.Gold, rwGold, "level"); w.Add(Currency.Iron, rwIron, "level"); w.Add(Currency.Food, rwFood, "level");
            if (rwGems > 0) w.Add(Currency.Diamonds, rwGems, "level");
            GameServices.Save.Save();
            rewardText = $"+{rwGold} altın  +{rwIron} demir  +{rwFood} yiyecek" + (rwGems > 0 ? $"  +{rwGems} elmas" : "") + (first ? "" : "  (tekrar)") + dropText;
        }

        void DoubleRewards()
        {
            Shop.WatchAd(AdPlacement.DoubleReward, () =>
            {
                doubled = true;
                var w = GameServices.Wallet;
                w.Add(Currency.Gold, rwGold, "ad_double"); w.Add(Currency.Iron, rwIron, "ad_double"); w.Add(Currency.Food, rwFood, "ad_double");
                rewardText = $"+{rwGold * 2} altın  +{rwIron * 2} demir  +{rwFood * 2} yiyecek" + (rwGems > 0 ? $"  +{rwGems} elmas" : "") + "  (2 kat)" + dropText;
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

        void ScaledText(Vector2 p, string s, Color c, GUIStyle st, float scale, float w = 170)
        {
            if (Mathf.Abs(scale - 1) < .01f) { Text(p, s, c, st, w); return; }
            var m = GUI.matrix;
            GUI.matrix = m * Matrix4x4.TRS(new Vector3(p.x, p.y, 0), Quaternion.identity, new Vector3(scale, scale, 1)) * Matrix4x4.TRS(new Vector3(-p.x, -p.y, 0), Quaternion.identity, Vector3.one);
            Text(p, s, c, st, w);
            GUI.matrix = m;
        }

        GUIStyle bannerSt, bannerSubSt;
        void DrawBanner(float VW)
        {
            if (gateFlash > 0) GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(gateFlashCol.r, gateFlashCol.g, gateFlashCol.b, gateFlash * .22f), 0, 0);
            if (bannerText == null || bannerT > bannerDur) return;
            bannerSt ??= new GUIStyle(UiSkin.Big) { alignment = TextAnchor.MiddleCenter, fontSize = 40, richText = true };
            bannerSubSt ??= new GUIStyle(UiSkin.Label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, richText = true };
            // Giriş: soldan kayar, ortada durur, sağa kayıp çıkar
            float t = bannerT, inT = .28f, outT = bannerDur - .3f;
            float ox = t < inT ? Mathf.Lerp(-VW, 0, 1 - Mathf.Pow(1 - t / inT, 3)) : t > outT ? Mathf.Lerp(0, VW, Mathf.Pow((t - outT) / .3f, 2)) : 0;
            float a = t > outT ? 1 - (t - outT) / .3f : 1;
            float y = 270;
            GUI.DrawTexture(new Rect(ox - 20, y - 46, VW + 40, 92 + (bannerSub != null ? 22 : 0)), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f * a), 0, 0);
            var bc = C(bannerColor); bc.a = a;
            GUI.DrawTexture(new Rect(ox - 20, y - 46, VW + 40, 3), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, bc, 0, 0);
            GUI.DrawTexture(new Rect(ox - 20, y + 43 + (bannerSub != null ? 22 : 0), VW + 40, 3), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, bc, 0, 0);
            float pulse = t < inT + .15f ? 1 + .25f * Mathf.Max(0, 1 - (t - inT) / .15f) * (t > inT ? 1 : 0) : 1;
            var old = GUI.color;
            var m = GUI.matrix; var pv = new Vector3(VW / 2 + ox, y, 0);
            GUI.matrix = m * Matrix4x4.TRS(pv, Quaternion.identity, new Vector3(pulse, pulse, 1)) * Matrix4x4.TRS(-pv, Quaternion.identity, Vector3.one);
            GUI.color = new Color(0, 0, 0, .8f * a); GUI.Label(new Rect(ox + 2, y - 28 + 2, VW, 60), $"<b>{bannerText}</b>", bannerSt);
            GUI.color = bc; GUI.Label(new Rect(ox, y - 28, VW, 60), $"<b>{bannerText}</b>", bannerSt);
            GUI.matrix = m;
            if (bannerSub != null) { GUI.color = new Color(1, 1, 1, a); GUI.Label(new Rect(ox, y + 30, VW, 30), bannerSub, bannerSubSt); }
            GUI.color = old;
        }

        void OnGUI()
        {
            if (sim == null || cam == null) return;
            gs = Screen.height / 800f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(gs, gs, 1));
            float VW = Screen.width / gs;
            if (DialogBox.Draw(VW)) return;
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
                else if (o.kind == ObjKind.Gate)
                {
                    string gt = LevelGen.GateText(o);
                    Text(ToGui(o.x, sy), gt, LevelGen.GateGood(o) ? Color.white : C("#ffd0d0"), gt.Length <= 5 ? big : label, 170);
                }
            }
            foreach (var f in sim.floats)
            {
                // Yazılar büyüyerek belirir, sonra söner
                var c = C(f.color); c.a = Mathf.Clamp01(f.life * 1.5f);
                float age = 1 - f.life, sc = age < .12f ? Mathf.Lerp(1.7f, 1f, age / .12f) : 1;
                ScaledText(ToGui(f.x, f.y), f.text, c, label, sc);
            }
            if (sim.state != BattleState.Lost)
            {
                var nc = Color.Lerp(Color.Lerp(Color.white, C("#8be39d"), countPop), C("#ff6a5a"), countHurt);
                ScaledText(ToGui(sim.x, Field.SQY + 34), BattleSim.Fmt(sim.n), nc, big, 1 + .45f * countPop * countPop + .2f * countHurt);
            }

            // Üst bilgi
            var M = BattleData.Maps[sim.map];
            Text(new Vector2(VW / 2, 22), mode != null ? mode.Title : $"Bölüm {level} · {M.nameTr}", mode != null ? C("#ffd23f") : Color.white, label, 400);
            if (mode != null && sim.Live)
            {
                string hud = mode.Hud(sim);
                if (!string.IsNullOrEmpty(hud)) Text(new Vector2(VW / 2, 112), hud, Color.white, label, 400);
            }
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
            DrawBanner(VW);

            if (sim.Live && GUI.Button(new Rect(8, 8, 70, 36), mode != null ? "Pes et" : "Üs", btn))
            {
                // Modda çıkış = yenilgi (kat/deneme hile ile tekrarlanmasın); sonuç ekranı açılır
                if (mode != null) { sim.ForceEnd(false); OnBattleEnd(); } else GoMenu();
                return;
            }

            if (mode != null && sim.Live && mode.DrawOverlay(this, VW)) return;
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
            if (mode != null) { mode.DrawResult(this, VW); return; }
            float pw = Mathf.Min(360, VW - 20), px = VW / 2 - pw / 2, py = 230;
            bool won = sim.state == BattleState.Won;
            var sd = GameServices.Save?.Data;
            Fails.TryGetValue(level, out int nf);
            bool boostOffer = !won && sd != null && nf >= Offers.BoostOfferFails && !Offers.BoostActive(sd);
            GUI.Box(new Rect(px, py, pw, boostOffer ? 470 : 330), "", UiSkin.Card);
            Text(new Vector2(VW / 2, py + 34), won ? $"Bölüm {level} tamam" : "Yenildin", won ? C("#8be39d") : C("#ff8a80"), big, pw);
            if (won)
            {
                Text(new Vector2(VW / 2, py + 76), new string('★', sim.stars) + new string('☆', 3 - sim.stars), C("#ffd23f"), big, pw);
                GUI.Label(new Rect(px + 12, py + 96, pw - 24, 44), rewardText, new GUIStyle(label) { wordWrap = true, fontSize = 13, richText = true });
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
                if (boostOffer)
                {
                    // Takıldı: güç takviyesi öner (reklamla küçük, elmasla büyük)
                    by += 64;
                    Text(new Vector2(VW / 2, by + 8), "Zorlanıyor musun? Güç takviyesi al:", C("#ffd23f"), label, pw);
                    by += 26;
                    int adLeft = Offers.AdLeft(sd, "boost");
                    GUI.enabled = adLeft > 0;
                    if (GUI.Button(new Rect(px + 20, by, pw - 40, 50), adLeft > 0 ? $"▶ Reklam izle: +%{Offers.AdBoostDmg * 100:0} hasar, +{Offers.AdBoostSoldiers} asker (1 savaş)" : "Bugünkü reklam hakkı bitti", new GUIStyle(UiSkin.ButtonGold) { fontSize = 13 }))
                        Shop.WatchAd(AdPlacement.PowerBoost, () => { Offers.AdUsed(sd, "boost"); Offers.GiveBoost(sd, Offers.AdBoostDmg, Offers.AdBoostSoldiers, 1); GameServices.Save.Save(); StartLevel(level); });
                    GUI.enabled = true;
                    by += 58;
                    if (GUI.Button(new Rect(px + 20, by, pw - 40, 50), $"{Offers.BoostGems} elmas: +%{Offers.GemBoostDmg * 100:0} hasar, +{Offers.GemBoostSoldiers} asker ({Offers.GemBoostBattles} savaş)", new GUIStyle(btn) { fontSize = 13 }))
                    {
                        if (GameServices.Wallet.Spend(Currency.Diamonds, Offers.BoostGems, "power_boost"))
                        { Offers.GiveBoost(sd, Offers.GemBoostDmg, Offers.GemBoostSoldiers, Offers.GemBoostBattles); GameServices.Save.Save(); StartLevel(level); }
                        else sim.AddFloat(Field.W / 2, Field.SQY - 60, "Yeterli elmas yok", "#ff8a80");
                    }
                }
            }
        }
    }
}
