using System.Collections.Generic;
using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Haritalı üs: surla çevrili alan, seviyeye göre değişen binalar, çalışan inşaatçılar,
    /// devriye atan askerler, toplanacak üretim balonları, dokununca açılan altıgen menü ve yükseltme penceresi.
    /// Yuvarlak köy: ortada Karargâh, çevresinde 8 bina, en dışta oval sur ve kapı. Çevre seçilen temaya göre değişir.
    /// Koordinatlar oyun pikseli (100 = 1 dünya birimi), y aşağı doğru. Dünya 800 x 1200, köy merkezi (400, 600).</summary>
    public partial class BaseMapView : MonoBehaviour
    {
        public const float WW = 800, WH = 1200, CX = 400, CY = 600;
        public const float WallRX = 250, WallRY = 215, RingRX = 175, RingRY = 150; // Tools/artgen/gen_ground.py ile aynı
        static readonly Dictionary<BuildingKind, Vector2> Pos = MakeLayout();

        static Dictionary<BuildingKind, Vector2> MakeLayout()
        {
            var d = new Dictionary<BuildingKind, Vector2>();
            void At(BuildingKind k, float deg) { float r = deg * Mathf.Deg2Rad; d[k] = new Vector2(CX + RingRX * Mathf.Cos(r), CY + RingRY * Mathf.Sin(r)); }
            At(BuildingKind.Farm, 247.5f); At(BuildingKind.Mine, -67.5f);
            At(BuildingKind.Armory, 202.5f); At(BuildingKind.Quarry, -22.5f);
            d[BuildingKind.Headquarters] = new Vector2(CX, CY + 6);
            At(BuildingKind.Tower, 157.5f); At(BuildingKind.Market, 22.5f);
            At(BuildingKind.Barracks, 112.5f); At(BuildingKind.Hospital, 67.5f);
            d[BuildingKind.Wall] = new Vector2(CX, CY + WallRY + 2);
            return d;
        }

        /// <summary>Binanın haritadaki çizim ölçeği: Karargâh büyük, kapı biraz küçük.</summary>
        static float BScale(BuildingKind k) => k == BuildingKind.Headquarters ? 1.3f : k == BuildingKind.Wall ? .8f : .92f;

        public GameFlow flow;
        Camera cam;
        Sprite square, circle, glow;
        Material mat;
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        int used;
        float camX = CX, camY = CY + 30, visW = 540, visH = 820, t;
        bool pinching; float pinchDist;
        readonly List<Vector2> wallSegs = new List<Vector2>(), wallTowers = new List<Vector2>();
        readonly List<Vector3> trees = new List<Vector3>(); // x, y, ölçek
        bool inited;

        SaveData D => GameServices.Save.Data;
        Wallet Wl => GameServices.Wallet;

        // Canlı üs
        class Worker { public float x = CX, y = CY + WallRY - 10, tx = CX, ty = CY + 100, wait, ph, dir = 1; public bool carry, builder; public int kind = -1; }
        class Ev { public bool chest, dead; public float x, y, life, t, flash; public int hp; }
        class Float { public float x, y, life; public string text; public Color c; }
        class Part { public float x, y, vx, vy, life, max, size; public Color c; }
        readonly List<Worker> workers = new List<Worker>();
        readonly List<Ev> events = new List<Ev>();
        readonly List<Float> floats = new List<Float>();
        readonly List<Part> parts = new List<Part>();
        readonly Dictionary<BuildingKind, float> pop = new Dictionary<BuildingKind, float>();
        float evTimer = 8, nextTick;

        // Seçim ve pencereler
        enum Popup { None, Upgrade, Info, Trade, Settings }
        BuildingKind? sel;
        Popup popup;
        Rect popupRect;
        BuildingKind popKind;

        void Init()
        {
            if (inited) return;
            inited = true;
            cam = Camera.main;
            var sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ?? Shader.Find("Sprites/Default");
            mat = sh != null ? new Material(sh) : null;
            var tex = new Texture2D(4, 4); var px = new Color[16]; for (int i = 0; i < 16; i++) px[i] = Color.white; tex.SetPixels(px); tex.Apply();
            square = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
            int s = 64; var ct = new Texture2D(s, s); var cp = new Color[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) { float d = Mathf.Sqrt((x + .5f - 32) * (x + .5f - 32) + (y + .5f - 32) * (y + .5f - 32)); cp[y * s + x] = new Color(1, 1, 1, Mathf.Clamp01(32 - d)); }
            ct.SetPixels(cp); ct.Apply();
            circle = Sprite.Create(ct, new Rect(0, 0, s, s), new Vector2(.5f, .5f), s);
            // Yumuşak ışık hâlesi (gece ışıkları için)
            var gt = new Texture2D(s, s); var gp = new Color[s * s];
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) { float d = Mathf.Sqrt((x + .5f - 32) * (x + .5f - 32) + (y + .5f - 32) * (y + .5f - 32)) / 32f; float a = Mathf.Clamp01(1 - d); gp[y * s + x] = new Color(1, 1, 1, a * a * (3 - 2 * a)); }
            gt.SetPixels(gp); gt.Apply();
            glow = Sprite.Create(gt, new Rect(0, 0, s, s), new Vector2(.5f, .5f), s);
            BuildScenery();
        }

        /// <summary>Sur parçalarını elips boyunca eşit aralıkla dizer, kuleleri ve dıştaki ağaçları yerleştirir.</summary>
        void BuildScenery()
        {
            const int N = 720; const float step = 21f; float acc = step;
            Vector2 prev = new Vector2(CX + WallRX, CY);
            for (int i = 1; i <= N; i++)
            {
                float a = i * Mathf.PI * 2 / N;
                var p = new Vector2(CX + WallRX * Mathf.Cos(a), CY + WallRY * Mathf.Sin(a));
                acc += Vector2.Distance(prev, p); prev = p;
                if (acc < step) continue;
                acc = 0;
                if (Mathf.Abs(p.x - CX) < 50 && p.y > CY) continue; // kapı boşluğu
                wallSegs.Add(p);
            }
            foreach (float deg in new[] { 0f, 45f, 135f, 180f, 225f, 270f, 315f })
            {
                float r = deg * Mathf.Deg2Rad;
                wallTowers.Add(new Vector2(CX + WallRX * Mathf.Cos(r), CY + WallRY * Mathf.Sin(r)));
            }
            var rng = new System.Random(7);
            for (int tries = 0; tries < 900 && trees.Count < 120; tries++)
            {
                float x = (float)rng.NextDouble() * WW, y = (float)rng.NextDouble() * (WH + 60);
                float e = ((x - CX) / WallRX) * ((x - CX) / WallRX) + ((y - CY) / WallRY) * ((y - CY) / WallRY);
                if (e < 1.38f || (Mathf.Abs(x - CX) < 40 && y > CY)) continue; // surun dibi ve kapı yolu boş kalsın
                bool close = false;
                foreach (var tr in trees) if (Mathf.Abs(tr.x - x) < 34 && Mathf.Abs(tr.y - y) < 22) { close = true; break; }
                if (close) continue;
                trees.Add(new Vector3(x, y, .8f + (float)rng.NextDouble() * .4f));
            }
        }

        public void Open()
        {
            Init();
            gameObject.SetActive(true);
            sel = null; popup = Popup.None;
            if (previewTheme != null) { visW = 9999; camX = CX; camY = CY; previewNight = false; }
            if (cam != null) { cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; }
            nextTick = 0;
        }

        public void Close()
        {
            foreach (var r in pool) if (r != null) r.gameObject.SetActive(false);
            gameObject.SetActive(false);
            SoundFx.Ambient(null);
            if (previewTheme != null) previewTheme = null;
        }

        // ---------- Çizim yardımcıları ----------
        Vector3 W(float px, float py) => new Vector3((px - camX) / 100f, (camY - py) / 100f, 0);

        void Draw(Sprite s, Color c, float px, float py, float w, float h, int order, float rot = 0)
        {
            if (s == null) return;
            SpriteRenderer r;
            if (used < pool.Count) r = pool[used];
            else
            {
                var go = new GameObject("b"); go.transform.SetParent(transform);
                r = go.AddComponent<SpriteRenderer>(); if (mat != null) r.sharedMaterial = mat; pool.Add(r);
            }
            used++;
            if (!r.gameObject.activeSelf) r.gameObject.SetActive(true);
            r.sprite = s; r.color = c; r.sortingOrder = order;
            r.transform.position = W(px, py);
            r.transform.localScale = new Vector3(w / 100f, h / 100f, 1);
            r.transform.rotation = rot == 0 ? Quaternion.identity : Quaternion.Euler(0, 0, rot);
        }

        void Spr(Sprite s, float px, float py, int order, float scale = 1, bool flip = false, Color? tint = null, float rot = 0) =>
            Draw(s, tint ?? Color.white, px, py, (flip ? -100f : 100f) * scale, 100f * scale, order, rot);

        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static readonly Color Gold = Hex("#ffd23f"), Iron = Hex("#cfd8e0"), Food = Hex("#8be39d"), Gem = Hex("#9fe8ff");

        void AddFloat(float x, float y, string text, Color c) => floats.Add(new Float { x = x, y = y, text = text, c = c, life = 1.4f });

        void Burst(float x, float y, int n, Color[] cols, float speed, float up)
        {
            for (int i = 0; i < n && parts.Count < 300; i++)
            {
                float a = Random.value * 6.283f, sp = speed * (.3f + Random.value * .7f), life = .4f + Random.value * .5f;
                parts.Add(new Part { x = x, y = y, vx = Mathf.Cos(a) * sp, vy = Mathf.Sin(a) * sp - up, life = life, max = life, size = 3 + Random.value * 3, c = cols[Random.Range(0, cols.Length)] });
            }
        }

        // ---------- Canlı üs mantığı ----------
        int SumLevels() { int n = 0; foreach (var l in D.buildings) n += l; return n; }

        void Retarget(Worker w)
        {
            w.builder = false;
            var built = new List<BuildingKind>();
            foreach (var kv in Pos) if (kv.Key != BuildingKind.Wall && Progress.Level(D, kv.Key) > 0) built.Add(kv.Key);
            if (built.Count > 0 && Random.value < .75f)
            {
                var k = built[Random.Range(0, built.Count)];
                w.tx = Pos[k].x + (Random.value - .5f) * 30; w.ty = Pos[k].y + 26; w.kind = (int)k;
            }
            else { var q = RandomInside(); w.tx = q.x; w.ty = q.y; w.kind = -1; }
        }

        /// <summary>Sur içinde, meydanın dışında rastgele bir nokta.</summary>
        static Vector2 RandomInside()
        {
            float a = Random.value * Mathf.PI * 2, r = Random.Range(.5f, .82f);
            return new Vector2(CX + (WallRX - 22) * r * Mathf.Cos(a), CY + (WallRY - 22) * r * Mathf.Sin(a));
        }

        void LiveTick(float dt)
        {
            int want = Mathf.Min(6, 2 + SumLevels() / 8);
            while (workers.Count < want) { var w = new Worker { ph = Random.value * 6 }; Retarget(w); workers.Add(w); }

            // İnşaat varsa ilk iki işçi oraya gider ve bitene kadar çalışır
            bool building = BaseLogic.Building(D);
            for (int i = 0; i < workers.Count; i++)
            {
                var w = workers[i];
                bool shouldBuild = building && i < 2;
                if (shouldBuild && (!w.builder || w.kind != D.buildKind))
                {
                    var p = Pos[(BuildingKind)D.buildKind];
                    w.builder = true; w.kind = D.buildKind; w.wait = 0; w.carry = false;
                    w.tx = p.x + (i == 0 ? -30 : 30); w.ty = p.y + 22;
                }
                else if (!shouldBuild && w.builder) { w.wait = 0; Retarget(w); }

                if (w.wait > 0)
                {
                    if (w.builder) continue; // çekiç sallamaya devam
                    w.wait -= dt;
                    if (w.wait <= 0) { w.carry = w.kind >= 0 && Random.value < .6f; Retarget(w); }
                    continue;
                }
                float dx = w.tx - w.x, dy = w.ty - w.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < 3) { w.wait = 1.2f + Random.value * 2.5f; continue; }
                float sp = w.builder ? 60 : 38;
                w.x += dx / d * sp * dt; w.y += dy / d * sp * dt; w.ph += dt * 9; if (Mathf.Abs(dx) > .5f) w.dir = Mathf.Sign(dx);
            }

            evTimer -= dt;
            if (evTimer <= 0)
            {
                evTimer = 14 + Random.value * 14;
                if (events.Count < 2)
                {
                    if (Random.value < .6f) { var q = RandomInside(); events.Add(new Ev { chest = true, x = q.x, y = q.y, life = 28 }); SoundFx.Play("power"); }
                    else { float zy = CY + WallRY + 130; events.Add(new Ev { x = CX, y = zy, hp = 3, life = 30 }); SoundFx.Play("hurt"); AddFloat(CX, zy - 70, "Kaçak zombi!", Hex("#ff8a80")); }
                }
            }
            foreach (var e in events)
            {
                e.t += dt; e.life -= dt; e.flash = Mathf.Max(0, e.flash - dt * 4);
                if (e.chest) continue;
                e.y -= 22 * dt; e.x += Mathf.Sin(e.t * 1.5f) * 8 * dt;
                if (e.y < CY + 120)
                {   // askerler yakaladı
                    e.dead = true; int q = 8 + Mathf.Min(40, Progress.TotalStars(D) / 3);
                    Wl.Add(Currency.Gold, q, "base_event"); AddFloat(e.x, e.y - 30, $"+{q} altın", Gold);
                    Burst(e.x, e.y - 10, 16, new[] { Hex("#8dbd80"), Hex("#ff5a4d"), Color.white }, 100, 40); SoundFx.Play("kill");
                }
            }
            events.RemoveAll(e => e.dead || e.life <= 0);

            foreach (var f in floats) { f.y -= 26 * dt; f.life -= dt; }
            floats.RemoveAll(f => f.life <= 0);
            foreach (var p in parts) { p.x += p.vx * dt; p.y += p.vy * dt; p.vy += 260 * dt; p.life -= dt; }
            parts.RemoveAll(p => p.life <= 0);
            var keys = new List<BuildingKind>(pop.Keys);
            foreach (var k in keys) pop[k] = Mathf.Max(0, pop[k] - dt * 1.6f);
        }

        bool EventTap(float px, float py)
        {
            foreach (var e in events)
            {
                if (e.chest && Mathf.Abs(px - e.x) < 28 && Mathf.Abs(py - e.y) < 28)
                {
                    int r = Random.Range(0, 3), q = 15 + Random.Range(0, 20) + Mathf.Min(60, Progress.TotalStars(D) / 2);
                    var cur = r == 0 ? Currency.Gold : r == 1 ? Currency.Iron : Currency.Food;
                    string nm = r == 0 ? "altın" : r == 1 ? "demir" : "yiyecek"; var col = r == 0 ? Gold : r == 1 ? Iron : Food;
                    Wl.Add(cur, q, "base_chest"); string msg = $"+{q} {nm}";
                    if (Random.value < .12f) { Wl.Add(Currency.Diamonds, 1, "base_chest"); msg += " +1 elmas"; }
                    SoundFx.Play("pickup"); AddFloat(e.x, e.y - 20, msg, col); Burst(e.x, e.y, 18, new[] { col, Gold, Color.white }, 100, 50);
                    e.dead = true; return true;
                }
                if (!e.chest && Mathf.Abs(px - e.x) < 30 && py > e.y - 46 && py < e.y + 14)
                {
                    e.hp--; e.flash = 1; SoundFx.Play("hit"); Burst(e.x, e.y - 20, 8, new[] { Hex("#8dbd80"), Color.white }, 80, 30);
                    if (e.hp <= 0)
                    {
                        int q2 = 20 + Mathf.Min(60, Progress.TotalStars(D) / 2);
                        Wl.Add(Currency.Gold, q2, "base_event"); AddFloat(e.x, e.y - 40, $"+{q2} altın", Gold); SoundFx.Play("kill"); e.dead = true;
                    }
                    return true;
                }
            }
            return false;
        }

        static BuildingKind? ProdOf(BuildingKind k) => k == BuildingKind.Mine || k == BuildingKind.Quarry || k == BuildingKind.Farm ? k : (BuildingKind?)null;
        float Pending(BuildingKind k) => k == BuildingKind.Mine ? D.pendGold : k == BuildingKind.Quarry ? D.pendIron : D.pendFood;

        void CollectOne(BuildingKind k)
        {
            BaseLogic.TickProduction(D);
            int q; Currency cur; string nm; Color col;
            if (k == BuildingKind.Mine) { q = Mathf.FloorToInt(D.pendGold); D.pendGold -= q; cur = Currency.Gold; nm = "altın"; col = Gold; }
            else if (k == BuildingKind.Quarry) { q = Mathf.FloorToInt(D.pendIron); D.pendIron -= q; cur = Currency.Iron; nm = "demir"; col = Iron; }
            else { q = Mathf.FloorToInt(D.pendFood); D.pendFood -= q; cur = Currency.Food; nm = "yiyecek"; col = Food; }
            var p = Pos[k];
            if (q < 1) { AddFloat(p.x, p.y - 50, "Henüz birikmedi", Color.white); return; }
            Wl.Add(cur, q, "production"); Daily.Add(D, "gather"); GameServices.Save.Save();
            SoundFx.Play("pickup"); AddFloat(p.x, p.y - 60, $"+{q} {nm}", col); Burst(p.x, p.y - 50, 14, new[] { col, Color.white }, 90, 50);
        }

        // Devriye yolu: surun üstünde tur (elips, yaklaşık 1460 piksel)
        const float PathLen = 1460f;
        static Vector2 OnPath(float dist, out float dirX)
        {
            float a = Mathf.Repeat(dist, PathLen) / PathLen * Mathf.PI * 2;
            dirX = -Mathf.Sin(a);
            return new Vector2(CX + WallRX * Mathf.Cos(a), CY + WallRY * Mathf.Sin(a));
        }

        // ---------- Güncelleme ve çizim ----------
        void Update()
        {
            if (!inited || cam == null || GameServices.Save == null) return;
            float dt = Mathf.Min(Time.deltaTime, .05f); t += dt;
            CameraInput();
            ApplyCamera();

            if (Time.unscaledTime > nextTick)
            {
                nextTick = Time.unscaledTime + .5f;
                BaseLogic.TickProduction(D);
                var done = BaseLogic.Complete(D);
                if (done.HasValue)
                {
                    GameServices.Save.Save(); var p = Pos[done.Value]; pop[done.Value] = 1;
                    SoundFx.Play("power"); AddFloat(p.x, p.y - 70, $"{BaseLogic.Def(done.Value).name} Sv. {Progress.Level(D, done.Value)}!", Gold);
                    Burst(p.x, p.y - 20, 30, new[] { Gold, Color.white, Hex("#8be39d") }, 140, 70);
                }
            }
            LiveTick(dt);
            AmbientTick(dt, ThemeId);
            NightTick(dt);
            PreviewTick();
#if UNITY_EDITOR
            // Test kısayolları (sadece Editor): seçili binanın seviyesi PageUp/PageDown veya + / - ile değişir
            if (sel.HasValue)
            {
                if (Input.GetKeyDown(KeyCode.PageUp) || Input.GetKeyDown(KeyCode.KeypadPlus) || Input.GetKeyDown(KeyCode.Equals)) DebugSetLevel(sel.Value, +1);
                if (Input.GetKeyDown(KeyCode.PageDown) || Input.GetKeyDown(KeyCode.KeypadMinus) || Input.GetKeyDown(KeyCode.Minus)) DebugSetLevel(sel.Value, -1);
            }
#endif
            Render();
        }

        // ---------- Kamera: sürükle kaydır, tekerlek / iki parmak yakınlaştır ----------
        void ApplyCamera()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            visW = Mathf.Clamp(visW, 300, Mathf.Min(WW, WH * aspect));
            visH = visW / aspect;
            cam.orthographicSize = visH / 200f;
            camX = Mathf.Clamp(camX, visW / 2, WW - visW / 2);
            camY = Mathf.Clamp(camY, visH / 2, WH - visH / 2);
        }

        Vector2 ScreenToGame(Vector2 sp) =>
            new Vector2(camX + (sp.x / Screen.width - .5f) * visW, camY - (sp.y / Screen.height - .5f) * visH);

        /// <summary>Ekrandaki bir noktanın altındaki yer sabit kalacak şekilde yakınlaştırır.</summary>
        void ZoomAt(Vector2 screenPos, float newW)
        {
            var g = ScreenToGame(screenPos);
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            visW = Mathf.Clamp(newW, 300, Mathf.Min(WW, WH * aspect)); visH = visW / aspect;
            camX = g.x - (screenPos.x / Screen.width - .5f) * visW;
            camY = g.y + (screenPos.y / Screen.height - .5f) * visH;
        }

        void CameraInput()
        {
            if (popup != Popup.None) { pinching = false; return; }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > .01f) ZoomAt(Input.mousePosition, visW * (1 - .12f * Mathf.Sign(wheel)));
            if (Input.touchCount >= 2)
            {
                Vector2 a = Input.GetTouch(0).position, b = Input.GetTouch(1).position; float d = Vector2.Distance(a, b);
                if (!pinching) { pinching = true; pinchDist = d; dragging = true; }
                else if (d > 10) { ZoomAt((a + b) / 2, visW * pinchDist / d); pinchDist = d; }
            }
            else pinching = false;
        }

        void Render()
        {
            used = 0;
            var theme = Themes.Get(ThemeId);
            Spr(Art.ThemeGround(theme.id), WW / 2, WH / 2, 0);
            int wallL = Progress.Level(D, BuildingKind.Wall), tier = Mathf.CeilToInt(wallL / 4f);

            // Çevredeki ağaçlar (sadece ekrandakiler)
            for (int i = 0; i < trees.Count; i++)
            {
                var tr = trees[i];
                if (Mathf.Abs(tr.x - camX) < visW / 2 + 70 && Mathf.Abs(tr.y - camY) < visH / 2 + 140)
                    Spr(Art.Tree(theme.trees[(i * 7 + 3) % theme.trees.Length]), tr.x, tr.y, 100 + (int)tr.y, tr.z);
            }

            // Sur: parçalar, kuleler
            var seg = Art.WallSeg(tier); var tow = Art.WallTower(tier);
            foreach (var p in wallSegs) Spr(seg, p.x, p.y, 100 + (int)p.y);
            foreach (var p in wallTowers) Spr(tow, p.x, p.y + 1, 101 + (int)p.y);

            // Karargâh önünde kahraman ve talim yapan askerler
            int frame = Mathf.FloorToInt(t * 3) & 1;
            for (int i = 0; i < 4; i++)
            {
                float wx = CX + Mathf.Sin(t * .45f + i * 1.7f) * 44, wy = CY + 96 + (i % 2) * 18 + Mathf.Cos(t * .6f + i) * 4;
                Spr(Art.Soldier((int)(t * 6) + i), wx, wy + 8, 100 + (int)wy, .8f);
            }
            Spr(Art.Hero(Progress.HeroIndex(D.hero), frame), CX - 70, CY + 104, 100 + (int)(CY + 104), 1f);

            // Devriye: iki manga surun üstünde tur atar, iki nöbetçi kapıda bekler
            for (int sq = 0; sq < 2; sq++)
                for (int i = 0; i < 3; i++)
                {
                    var p = OnPath(t * 22 + sq * PathLen / 2 - i * 16, out float dx);
                    Spr(Art.Soldier((int)(t * 8) + i), p.x, p.y - 18, 102 + (int)p.y, .7f, dx < 0);
                }
            float gy = Pos[BuildingKind.Wall].y + 22;
            Spr(Art.Soldier(0), CX - 62, gy, 100 + (int)gy, .85f); Spr(Art.Soldier(2), CX + 62, gy, 100 + (int)gy, .85f, true);

            // İşçiler
            foreach (var w in workers)
            {
                bool working = w.wait > 0 && w.kind >= 0;
                float bob = w.wait > 0 ? (working ? Mathf.Sin(t * 10 + w.ph) * 1.5f : 0) : Mathf.Abs(Mathf.Sin(w.ph)) * 1.5f;
                int ord = 100 + (int)w.y;
                Spr(Art.BaseMisc(2), w.x, w.y - bob, ord, 1, w.dir < 0);
                if (w.carry && w.wait <= 0) Spr(Art.BaseMisc(3), w.x, w.y - 23 - bob, ord + 1);
                if (working) Spr(Art.BaseMisc(4), w.x + 7 * w.dir, w.y - 12, ord + 1, 1, false, null, Mathf.Sin(t * 10 + w.ph) * 35);
            }

            // Olaylar
            foreach (var e in events)
            {
                if (e.chest)
                {
                    float bobc = Mathf.Sin(e.t * 4) * 2;
                    Draw(circle, new Color(1, .82f, .25f, .25f + .1f * Mathf.Sin(e.t * 5)), e.x, e.y, 60, 36, 99 + (int)e.y);
                    Spr(Art.BaseMisc(6), e.x, e.y + 6 - bobc, 100 + (int)e.y);
                }
                else Spr(Art.Enemy(0, "walker", Mathf.FloorToInt(e.t * 4)), e.x, e.y, 100 + (int)e.y, .8f, false, e.flash > 0 ? new Color(1.6f, 1.6f, 1.6f) : (Color?)null);
            }

            // Binalar
            foreach (var kv in Pos)
            {
                var k = kv.Key; var p = kv.Value; int L = Progress.Level(D, k);
                float bs = BScale(k);
                pop.TryGetValue(k, out float pp); float sc = (1 + pp * .16f) * bs; int ord = 100 + (int)p.y;
                bool busy = BaseLogic.Building(D) && D.buildKind == (int)k;
                if (sel == k) Draw(circle, new Color(1, .82f, .25f, .45f + .2f * Mathf.Sin(t * 6)), p.x, p.y + 10, 112 * bs, 34 * bs, 5);
                if (k == BuildingKind.Wall) Spr(Art.WallGate(tier), p.x, p.y, ord, sc);
                else if (L == 0) Spr(Art.BaseMisc(busy ? 1 : 0), p.x, p.y, ord, bs);
                else if (Art.HasCustomBuilding(k, L)) Spr(Art.Building(k, L), p.x, p.y + 4, ord, sc * Art.LevelScale(k, L));
                else Spr(Art.Building(k, L), p.x, p.y + 8, ord, sc);

                if (busy)
                {
                    float pr = Mathf.Clamp01(1 - BaseLogic.Remaining(D) / Mathf.Max(1, BaseLogic.Duration(D.buildFrom)));
                    Draw(square, new Color(.03f, .05f, .07f, .85f), p.x, p.y - 48, 64, 9, 2050);
                    Draw(square, Hex("#ffb02e"), p.x - 31 + 31 * pr, p.y - 48, 62 * pr, 7, 2051);
                    Spr(Art.BaseMisc(4), p.x + 34, p.y - 22, 2052, 1.6f, false, null, Mathf.Sin(t * 8) * 30);
                    if (Random.value < Time.deltaTime * 6) Burst(p.x + (Random.value - .5f) * 50, p.y + 6, 1, new[] { new Color(.8f, .75f, .65f, .8f) }, 20, 30);
                }
                if (L > 0)
                {
                    float bx = p.x + 40 * bs, by = p.y - 30 * bs;
                    Draw(circle, Color.white, bx, by, 23, 23, 2053); Draw(circle, Hex("#4b3aa8"), bx, by, 20, 20, 2054);
                }
                var pk = ProdOf(k);
                if (pk.HasValue && L > 0 && Pending(k) >= 1 && !busy)
                {
                    float by2 = p.y - 62 + Mathf.Sin(t * 3 + p.x) * 3; var rc = k == BuildingKind.Mine ? Gold : k == BuildingKind.Quarry ? Iron : Food;
                    Draw(circle, rc, p.x, by2, 33, 33, 2055); Draw(circle, Color.white, p.x, by2, 28, 28, 2056); Draw(circle, rc, p.x, by2, 14, 14, 2057);
                }
            }

            NightDraw();
            AmbientDraw();
            foreach (var p in parts) { var c = p.c; c.a *= Mathf.Clamp01(p.life / p.max); Draw(circle, c, p.x, p.y, p.size, p.size, 2100); }
            // Bulut gölgeleri
            for (int cl = 0; cl < 4; cl++) Draw(circle, new Color(.04f, .08f, .12f, .07f), ((t * 9 + cl * 290) % 1100) - 150, CY - 420 + cl * 280, 180, 70, 2110);

            for (int i = used; i < pool.Count; i++) if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        // ---------- Arayüz ----------
        float gs, VW;
        GUIStyle label, small, big, btn, card, hexBlue, hexGreen, hexOrange, center, tiny;
        Vector2 downPos; bool dragging, pressed;

        void Styles()
        {
            if (label != null) return;
            label = UiSkin.Label; small = UiSkin.Small; big = UiSkin.Big; btn = UiSkin.Button; card = UiSkin.Card;
            center = new GUIStyle(UiSkin.Label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 15, wordWrap = false };
            tiny = new GUIStyle(center) { fontSize = 13 };
            GUIStyle HexStyle(string a, string b, string c)
            {
                var n = UiSkin.Hex(Hex(a), Hex(b), Hex(c));
                var s = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold };
                s.normal.background = n; s.normal.textColor = Color.white; s.hover = s.normal; s.active = s.normal;
                return s;
            }
            hexBlue = HexStyle("#3f7fd0", "#7fb6f0", "#ffffff"); hexGreen = HexStyle("#2f9a3f", "#6fd96a", "#ffffff"); hexOrange = HexStyle("#d9801e", "#ffb85a", "#ffffff");
        }

        Vector2 ToGui(float px, float py) { var sp = cam.WorldToScreenPoint(W(px, py)); return new Vector2(sp.x / gs, (Screen.height - sp.y) / gs); }

        void Outlined(Vector2 p, string s, Color c, GUIStyle st, float w = 200)
        {
            var old = GUI.color;
            GUI.color = new Color(0, 0, 0, c.a * .85f);
            GUI.Label(new Rect(p.x - w / 2 + 1.5f, p.y - 13 + 1.5f, w, 28), s, st); GUI.Label(new Rect(p.x - w / 2 - 1, p.y - 13 + 1, w, 28), s, st);
            GUI.color = c; GUI.Label(new Rect(p.x - w / 2, p.y - 13, w, 28), s, st);
            GUI.color = old;
        }

        static string Fmt(float s)
        {
            int v = Mathf.CeilToInt(s), h = v / 3600, m = v % 3600 / 60, sec = v % 60;
            return h > 0 ? $"{h}:{m:00}:{sec:00}" : $"{m}:{sec:00}";
        }

        bool Spend(string res, int amount)
        {
            if (amount <= 0) return true;
            return Wl.Spend(res == "gold" ? Currency.Gold : res == "iron" ? Currency.Iron : Currency.Food, amount, "building");
        }

        void OnGUI()
        {
            if (!inited || cam == null || GameServices.Save == null) return;
            Styles();
            gs = Screen.height / 800f; VW = Screen.width / gs;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(gs, gs, 1));

            // Bina yazıları
            foreach (var kv in Pos)
            {
                var k = kv.Key; var p = kv.Value; int L = Progress.Level(D, k);
                bool busy = BaseLogic.Building(D) && D.buildKind == (int)k;
                float bs = BScale(k);
                if (sel != k) Outlined(ToGui(p.x, p.y + 32 * bs), BaseLogic.Def(k).name, Color.white, tiny);
                if (L > 0) Outlined(ToGui(p.x + 40 * bs, p.y - 30 * bs), L.ToString(), Color.white, tiny, 40);
                if (busy) Outlined(ToGui(p.x, p.y - 62), Fmt(BaseLogic.Remaining(D)), Color.white, center);
                else if (sel != k && BaseLogic.Check(D, k) == BaseLogic.UpResult.Ok) Outlined(ToGui(p.x - 42, p.y - 8 + Mathf.Sin(t * 5 + p.x) * 3), "▲", Hex("#5be06a"), big, 40);
                if (ProdOf(k).HasValue && L > 0 && Pending(k) >= 1 && !busy)
                    Outlined(ToGui(p.x + 34, p.y - 62 + Mathf.Sin(t * 3 + p.x) * 3), Mathf.FloorToInt(Pending(k)).ToString(), Color.white, tiny, 60);
            }
            foreach (var e in events) if (e.chest) Outlined(ToGui(e.x, e.y - 24), "Dokun!", Gold, tiny);
            foreach (var f in floats) { var c = f.c; c.a = Mathf.Clamp01(f.life * 1.5f); Outlined(ToGui(f.x, f.y), f.text, c, center, 260); }

            if (previewTheme != null) { PreviewGui(); WorldInput(false); return; }
#if UNITY_EDITOR
            if (nightDebug > 0) Outlined(new Vector2(VW / 2, 100), $"TEST: {NightDebugNames[nightDebug]} (N)", Hex("#ff9a3a"), tiny, 260);
#endif
            bool modal = popup != Popup.None;
            if (!modal && sel.HasValue) SelectionMenu(sel.Value);
#if UNITY_EDITOR
            if (!modal && sel.HasValue) DebugButtons(sel.Value);
#endif
            if (!modal) Hud();
            if (modal) DrawPopup();
            WorldInput(modal);
        }

        void Hud()
        {
            float w = Mathf.Min(440, VW - 12), x0 = VW / 2 - w / 2;
            GUI.Box(new Rect(x0, 6, w, 36), "", card);
            GUI.Label(new Rect(x0, 6, w, 36),
                $"<color=#ffd23f>Altın {Wl.Get(Currency.Gold)}</color>   <color=#cfd8e0>Demir {Wl.Get(Currency.Iron)}</color>   <color=#8be39d>Yiyecek {Wl.Get(Currency.Food)}</color>   <color=#9fe8ff>Elmas {Wl.Get(Currency.Diamonds)}</color>",
                new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });

            // İnşaatçı durumu
            string bs = BaseLogic.Building(D) ? $"İnşaat: {BaseLogic.Def((BuildingKind)D.buildKind).name} {Fmt(BaseLogic.Remaining(D))}" : "İnşaatçı boşta";
            if (GUI.Button(new Rect(x0, 48, 210, 34), bs, new GUIStyle(btn) { fontSize = 13 }) && BaseLogic.Building(D))
            { popKind = (BuildingKind)D.buildKind; popup = Popup.Upgrade; SoundFx.Play("click"); }
            GUI.Label(new Rect(x0 + 216, 48, 60, 34), $"<color=#ffd23f>★ {Progress.TotalStars(D)}</color>", new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold, fontSize = 15 });
            var tb = new GUIStyle(btn) { fontSize = 13, padding = new RectOffset(2, 2, 2, 2) };
            bool ready = Daily.Ready(D);
            if (GUI.Button(new Rect(x0 + w - 164, 48, 78, 34), "Günlük", tb)) { SoundFx.Play("click"); flow.Show(GameScreen.Daily); }
            if (ready) { float pu = 1 + .15f * Mathf.Sin(t * 6); GUI.DrawTexture(new Rect(x0 + w - 96 - 6 * pu, 42 - 6 * pu, 14 * pu, 14 * pu), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.88f, .27f, .23f), 0, 7 * pu); }
            if (GUI.Button(new Rect(x0 + w - 80, 48, 80, 34), "Mağaza", UiSkin.ButtonGold)) { SoundFx.Play("click"); flow.Show(GameScreen.Shop); }

            // Alt menü
            float bh = 64, by = 800 - bh - 8, mid = 124, side = (w - mid - 32) / 4;
            GUI.Box(new Rect(x0 - 4, by - 8, w + 8, bh + 16), "", card);
            var sb = new GUIStyle(btn) { fontSize = 12, padding = new RectOffset(2, 2, 2, 2) };
            if (GUI.Button(new Rect(x0 + 4, by, side, bh), "Bölümler", sb)) { SoundFx.Play("click"); flow.Show(GameScreen.Levels); }
            if (GUI.Button(new Rect(x0 + 10 + side, by, side, bh), "Kahraman", sb)) { SoundFx.Play("click"); flow.Show(GameScreen.Heroes); }
            int lv = Mathf.Clamp(D.unlocked, 1, Balance.MaxLevel);
            if (GUI.Button(new Rect(x0 + 16 + side * 2, by - 6, mid, bh + 12), $"SAVAŞ\n<size=13>Bölüm {lv}</size>", UiSkin.ButtonGold)) { SoundFx.Play("click"); flow.Play(lv); }
            if (GUI.Button(new Rect(x0 + 22 + mid + side * 2, by, side, bh), "Silah", sb)) { SoundFx.Play("click"); flow.Show(GameScreen.Weapons); }
            if (GUI.Button(new Rect(x0 + 28 + mid + side * 3, by, side, bh), "Ayarlar", sb)) { SoundFx.Play("click"); popup = Popup.Settings; }
        }

#if UNITY_EDITOR
        /// <summary>Sadece Editor: binanın seviyesini anında değiştirir (görselleri denemek için). Kaynak harcamaz.</summary>
        void DebugSetLevel(BuildingKind k, int delta)
        {
            int L = Mathf.Clamp(Progress.Level(D, k) + delta, k == BuildingKind.Headquarters ? 1 : 0, Progress.MaxLevel(k));
            D.buildings[(int)k] = L;
            if (BaseLogic.Building(D) && D.buildKind == (int)k) D.buildKind = -1;
            GameServices.Save.Save();
            pop[k] = 1;
            var p = Pos[k];
            AddFloat(p.x, p.y - 70, $"TEST: Sv. {L} · kademe {Art.TierOf(k, Mathf.Max(1, L))}", Hex("#ff9a3a"));
        }

        void DebugButtons(BuildingKind k)
        {
            var p = Pos[k]; var g = ToGui(p.x, p.y - 112);
            var st = new GUIStyle(btn) { fontSize = 13, padding = new RectOffset(2, 2, 2, 2) };
            GUI.Label(new Rect(g.x - 90, g.y - 40, 180, 20), "<color=#ff9a3a><b>TEST (sadece Editor)</b></color>", new GUIStyle(tiny) { richText = true });
            if (GUI.Button(new Rect(g.x - 100, g.y - 18, 46, 32), "−4", st)) DebugSetLevel(k, -4);
            if (GUI.Button(new Rect(g.x - 50, g.y - 18, 46, 32), "−1", st)) DebugSetLevel(k, -1);
            if (GUI.Button(new Rect(g.x + 4, g.y - 18, 46, 32), "+1", st)) DebugSetLevel(k, +1);
            if (GUI.Button(new Rect(g.x + 54, g.y - 18, 46, 32), "+4", st)) DebugSetLevel(k, +4);
        }
#endif

        void SelectionMenu(BuildingKind k)
        {
            var p = Pos[k]; var top = ToGui(p.x, p.y - 78); var bot = ToGui(p.x, p.y + 44);
            Outlined(top, BaseLogic.Def(k).name, Color.white, new GUIStyle(center) { fontSize = 19 }, 260);
            Outlined(new Vector2(top.x, top.y + 22), $"Sv. {Progress.Level(D, k)}", Gold, center);

            string third = null, glyph = null;
            if (ProdOf(k).HasValue && Progress.Level(D, k) > 0) { third = "Topla"; glyph = "+"; }
            else if (k == BuildingKind.Market && Progress.Level(D, k) > 0) { third = "Takas"; glyph = "$"; }
            else if (k == BuildingKind.Barracks) { third = "Kahraman"; glyph = "★"; }
            else if (k == BuildingKind.Armory) { third = "Silah"; glyph = "★"; }
            else if (k == BuildingKind.Headquarters) { third = "Tema"; glyph = "◆"; }

            float hs = 66; float gap = third != null ? 78 : 44;
            bool HexBtn(float cx, float cy, string g, string text, GUIStyle st)
            {
                bool hit = GUI.Button(new Rect(cx - hs / 2, cy - hs / 2, hs, hs), g, st);
                Outlined(new Vector2(cx, cy + hs / 2 + 8), text, Color.white, center, 120);
                return hit;
            }
            if (HexBtn(bot.x - gap, bot.y + 26, "i", "Bilgi", hexBlue)) { popKind = k; popup = Popup.Info; SoundFx.Play("click"); }
            if (third != null && HexBtn(bot.x, bot.y + 48, glyph, third, hexOrange))
            {
                SoundFx.Play("click");
                if (third == "Topla") CollectOne(k);
                else if (third == "Takas") { popKind = k; popup = Popup.Trade; }
                else if (third == "Kahraman") flow.Show(GameScreen.Heroes);
                else if (third == "Tema") { MetaScreens.shopTab = 2; flow.Show(GameScreen.Shop); }
                else flow.Show(GameScreen.Weapons);
            }
            if (HexBtn(bot.x + gap, bot.y + 26, "▲", "Yükselt", hexGreen)) { popKind = k; popup = Popup.Upgrade; SoundFx.Play("click"); }
        }

        void DrawPopup()
        {
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 0);
            float w = Mathf.Min(400, VW - 24), x = VW / 2 - w / 2, h = popup == Popup.Settings ? 310 : 420, y = 400 - h / 2;
            popupRect = new Rect(x, y, w, h);
            GUI.Box(popupRect, "", card);
            if (GUI.Button(new Rect(x + w - 48, y + 8, 40, 40), "✕", new GUIStyle(btn) { fontSize = 16 })) { popup = Popup.None; return; }
            var k = popKind; var def = BaseLogic.Def(k); int L = Progress.Level(D, k); float e = Progress.Eff(D, k);
            var head = new GUIStyle(label) { fontSize = 22, fontStyle = FontStyle.Bold };
            var mid = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter };

            if (popup == Popup.Settings)
            {
                GUI.Label(new Rect(x + 16, y + 10, w - 70, 36), "Ayarlar", head);
                if (GUI.Button(new Rect(x + 20, y + 64, w - 40, 50), SoundFx.Muted ? "Ses: Kapalı" : "Ses: Açık", btn)) SoundFx.Muted = !SoundFx.Muted;
                if (GUI.Button(new Rect(x + 20, y + 124, w - 40, 50), SoundFx.Vibrate ? "Titreşim: Açık" : "Titreşim: Kapalı", btn)) SoundFx.Vibrate = !SoundFx.Vibrate;
                if (GUI.Button(new Rect(x + 20, y + 184, w - 40, 50), NightEnabled ? "Gece görünümü: Saate göre" : "Gece görünümü: Kapalı", btn)) NightEnabled = !NightEnabled;
                GUI.Label(new Rect(x, y + 250, w, 30), $"Sürüm {Application.version}", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                return;
            }

            // Başlık: bina görseli + ad
            var sp = k == BuildingKind.Wall ? Art.WallGate(Mathf.CeilToInt(L / 4f)) : L > 0 ? Art.Building(k, L) : Art.BaseMisc(0);
            if (sp != null) { var r = sp.textureRect; var tx = sp.texture; GUI.DrawTextureWithTexCoords(new Rect(x + 8, y + 4, 110, 110), tx, new Rect(r.x / tx.width, r.y / tx.height, r.width / tx.width, r.height / tx.height)); }
            GUI.Label(new Rect(x + 122, y + 22, w - 180, 32), def.name, head);
            GUI.Label(new Rect(x + 122, y + 54, w - 140, 26), $"<color=#ffd23f>Seviye {L} / {Progress.MaxLevel(k)}</color>", label);

            if (popup == Popup.Info)
            {
                GUI.Label(new Rect(x + 20, y + 124, w - 40, 30), $"<b>Şu an:</b> {(L > 0 ? def.effect(e) : "Henüz kurulmadı")}", label);
                string ex = def.extra(1);
                if (!string.IsNullOrEmpty(ex)) GUI.Label(new Rect(x + 20, y + 160, w - 40, 50), $"<color=#c9b8ff><b>5. ve 10. seviye eki:</b> {ex}</color>", label);
                GUI.Label(new Rect(x + 20, y + 216, w - 40, 60), k == BuildingKind.Headquarters
                    ? "Karargâh köyün kalbidir. Her seviyesi diğer binaların 2 seviye daha yükselmesine izin verir. Her 2 seviyede görünümü değişir."
                    : $"Her 4 seviyede binanın görünümü değişir. 10. seviyeden sonra etki yarı hızla artar. Şu an en fazla Sv. {Progress.LevelCap(D, k)} (Karargâh sınırı).", small);
                if (GUI.Button(new Rect(x + 20, y + h - 70, w - 40, 50), "Yükseltme ekranı", btn)) popup = Popup.Upgrade;
                return;
            }

            if (popup == Popup.Trade)
            {
                float rt = BaseLogic.TradeRate(D);
                GUI.Label(new Rect(x + 20, y + 120, w - 40, 30), $"100 kaynak ver, {Mathf.FloorToInt(100 * rt)} al.", label);
                string[] names = { "Altın → Demir", "Altın → Yiyecek", "Demir → Altın", "Yiyecek → Altın" };
                Currency[,] pairs = { { Currency.Gold, Currency.Iron }, { Currency.Gold, Currency.Food }, { Currency.Iron, Currency.Gold }, { Currency.Food, Currency.Gold } };
                for (int i = 0; i < 4; i++)
                    if (GUI.Button(new Rect(x + 20, y + 160 + i * 58, w - 40, 50), names[i], btn))
                    {
                        if (Wl.Spend(pairs[i, 0], 100, "trade")) { Wl.Add(pairs[i, 1], Mathf.FloorToInt(100 * rt), "trade"); SoundFx.Play("pickup"); }
                        else flow.ShowToast("100 kaynak gerekli");
                    }
                return;
            }

            // Yükseltme
            bool busyThis = BaseLogic.Building(D) && D.buildKind == (int)k;
            if (busyThis)
            {
                float rem = BaseLogic.Remaining(D); int scost = BaseLogic.SpeedCost(D);
                GUI.Label(new Rect(x, y + 130, w, 30), $"Seviye {D.buildFrom + 1} inşa ediliyor", mid);
                GUI.Label(new Rect(x, y + 164, w, 44), $"<size=34><b><color=#ffd23f>{Fmt(rem)}</color></b></size>", mid);
                if (GUI.Button(new Rect(x + 20, y + 230, w - 40, 54), scost == 0 ? "Hemen bitir (bedava)" : $"Hemen bitir: {scost} elmas", btn))
                {
                    if (scost == 0 || Wl.Spend(Currency.Diamonds, scost, "speed_up")) { BaseLogic.FinishNow(D); nextTick = 0; popup = Popup.None; }
                    else flow.ShowToast("Yeterli elmas yok");
                }
                if (GUI.Button(new Rect(x + 20, y + 294, w - 40, 54), "▶ Reklam izle: 5 dakika kısalt", UiSkin.ButtonGold))
                    Shop.WatchAd(AdPlacement.SpeedUpBuild, () => { BaseLogic.Shorten(D, 300); nextTick = 0; GameServices.Save.Save(); }, () => flow.ShowToast("Reklam hazır değil"));
                return;
            }
            if (L >= Progress.MaxLevel(k)) { GUI.Label(new Rect(x, y + 180, w, 40), "<color=#8be39d><b>En üst seviye</b></color>", mid); return; }

            float ne = BuildingData.EffectiveLevel(L + 1);
            GUI.Label(new Rect(x + 20, y + 122, w - 40, 26), L > 0 ? def.effect(e) : "Henüz kurulmadı", label);
            GUI.Label(new Rect(x + 20, y + 148, w - 40, 26), $"<color=#8be39d>▲ Sv. {L + 1}: {def.effect(ne)}</color>", label);
            int cg = BaseLogic.CostGold(k, L), ci = BaseLogic.CostIron(k, L), cf = BaseLogic.CostFood(k, L), rs = BaseLogic.ReqStars(L);
            float ry = y + 188;
            void Row(string name, int need, int have, string col)
            {
                if (need <= 0) return;
                bool ok = have >= need;
                GUI.Label(new Rect(x + 20, ry, w - 40, 26), $"<color={col}>{name}</color>", label);
                GUI.Label(new Rect(x + 20, ry, w - 40, 26), $"<color={(ok ? "#ffffff" : "#ff7a6e")}>{have} / {need}</color>", new GUIStyle(label) { alignment = TextAnchor.MiddleRight });
                ry += 28;
            }
            Row("Altın", cg, D.gold, "#ffd23f"); Row("Demir", ci, D.iron, "#cfd8e0"); Row("Yiyecek", cf, D.food, "#8be39d");
            if (rs > 0) Row("Yıldız şartı", rs, Progress.TotalStars(D), "#ffd23f");
            GUI.Label(new Rect(x + 20, ry, w - 40, 26), "Süre", label);
            GUI.Label(new Rect(x + 20, ry, w - 40, 26), Fmt(BaseLogic.Duration(L)), new GUIStyle(label) { alignment = TextAnchor.MiddleRight });

            var chk = BaseLogic.Check(D, k);
            string bt = chk == BaseLogic.UpResult.Ok ? (L == 0 ? "KUR" : "YÜKSELT") : chk == BaseLogic.UpResult.Busy ? "İnşaatçı meşgul" : chk == BaseLogic.UpResult.Stars ? $"{rs} yıldız gerekli"
                : chk == BaseLogic.UpResult.NeedHq ? $"Karargâh Sv. {(L + 2) / 2} gerekli" : "Kaynak yetersiz";
            GUI.enabled = chk == BaseLogic.UpResult.Ok;
            if (GUI.Button(new Rect(x + 20, y + h - 72, w - 40, 56), bt, UiSkin.ButtonGold))
            {
                if (BaseLogic.Start(D, k, Spend))
                {
                    Daily.Add(D, "upgrade"); GameServices.Save.Save(); SoundFx.Play("board"); popup = Popup.None; sel = null; nextTick = 0;
                    GameServices.Analytics?.LogEvent(AnalyticsEvent.BuildingUpgrade, new Dictionary<string, object> { { "building", k.ToString() }, { "to", L + 1 } });
                }
            }
            GUI.enabled = true;
        }

        // Haritaya dokunma ve kaydırma. Butonlar olayı önce tüketir, kalan olaylar haritaya gider.
        void WorldInput(bool modal)
        {
            var ev = Event.current;
            if (ev.type == EventType.MouseDown && ev.button == 0)
            {
                if (modal) { if (!popupRect.Contains(ev.mousePosition)) popup = Popup.None; ev.Use(); return; }
                pressed = true; dragging = false; downPos = ev.mousePosition; ev.Use();
            }
            else if (ev.type == EventType.MouseDrag && pressed)
            {
                if (!dragging && (ev.mousePosition - downPos).magnitude > 8) dragging = true;
                if (dragging && !pinching && Input.touchCount < 2) { camX -= ev.delta.x * visH / 800f; camY -= ev.delta.y * visH / 800f; }
                ev.Use();
            }
            else if (ev.rawType == EventType.MouseUp && pressed)
            {
                pressed = false;
                if (dragging || modal) return;
                var wp = cam.ScreenToWorldPoint(new Vector3(ev.mousePosition.x * gs, Screen.height - ev.mousePosition.y * gs, 10));
                float lx = wp.x * 100f + camX, ly = camY - wp.y * 100f;
                Tap(lx, ly);
            }
        }

        void Tap(float lx, float ly)
        {
            if (previewTheme != null) return;
            if (EventTap(lx, ly)) return;
            // Üretim balonu
            foreach (var kv in Pos)
            {
                var k = kv.Key; var p = kv.Value;
                if (ProdOf(k).HasValue && Progress.Level(D, k) > 0 && Pending(k) >= 1 && Mathf.Abs(lx - p.x) < 24 && Mathf.Abs(ly - (p.y - 62)) < 24) { CollectOne(k); return; }
            }
            BuildingKind? best = null; float bd = 1e9f;
            foreach (var kv in Pos)
            {
                var p = kv.Value; float bs = BScale(kv.Key), hw = (kv.Key == BuildingKind.Wall ? 75 : 50) * bs;
                if (Mathf.Abs(lx - p.x) < hw && ly > p.y - 70 * bs && ly < p.y + 36 * bs)
                { float d = Mathf.Abs(lx - p.x) + Mathf.Abs(ly - p.y); if (d < bd) { bd = d; best = kv.Key; } }
            }
            if (best.HasValue && best != sel) SoundFx.Play("click");
            sel = best;
            if (best.HasValue)
            {   // seçilen bina ve menüsü ekranda kalsın
                var bp = Pos[best.Value];
                camY = Mathf.Clamp(camY, bp.y + 150 - visH / 2 + 90, bp.y - 110 + visH / 2 - 60);
                camX = Mathf.Clamp(camX, bp.x + 125 - visW / 2, bp.x - 125 + visW / 2);
            }
        }
    }
}
