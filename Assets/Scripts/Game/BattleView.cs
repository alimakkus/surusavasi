using System.Collections.Generic;
using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Savaşı çizer, dokunmayı okur, sonuç ekranını ve ödüllü reklamı yönetir.
    /// Şimdilik basit şekillerle çizer; çizimler sonra gerçek sprite'larla değişecek.</summary>
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

            Render();
        }

        void Render()
        {
            used = 0;
            var M = BattleData.Maps[sim.map];
            // Zemin ve yol
            Draw(square, C(M.ground), 180, 280, 360, 1400, 0);
            Draw(square, C(M.road), 180, 280, 250, 1400, 1);
            for (int i = -1; i < 12; i++)
            {
                float y = i * 60 + (sim.scroll % 60);
                Draw(square, C(M.road2), 123, y, 4, 26, 2);
                Draw(square, C(M.road2), 237, y, 4, 26, 2);
            }

            foreach (var o in sim.objs)
            {
                if (o.done) continue;
                float sy = sim.Osy(o);
                if (sy < -80 || sy > Field.H + 60) continue;
                bool fl = o.flash > 0;
                switch (o.kind)
                {
                    case ObjKind.Barrel:
                        Draw(square, fl ? Color.white : C("#8a5a2a"), o.x, sy - 8, 44, 38, 10);
                        Draw(square, C("#c98d49"), o.x, sy - 8, 44, 6, 11);
                        Bar(o.x, sy + 18, 40, o.hp / o.max, o.chip / o.max, C("#ffd23f"));
                        break;
                    case ObjKind.Power:
                        var pc = o.pk == Power.Double ? "#d8463e" : o.pk == Power.Rapid ? "#2fb6c9" : "#e07a2a";
                        Draw(square, fl ? Color.white : C(pc), o.x, sy - 8, 46, 40, 10);
                        Bar(o.x, sy + 18, 40, o.hp / o.max, o.chip / o.max, Color.white);
                        break;
                    case ObjKind.Board:
                        Draw(square, C("#6c4a2e"), o.x - 28, sy + 6, 5, 22, 9);
                        Draw(square, C("#6c4a2e"), o.x + 28, sy + 6, 5, 22, 9);
                        Draw(square, C("#b8322b"), o.x, sy - 8, 76, 22, 10);
                        break;
                    default:
                        float sz = o.type.hw * sim.mapScale * 1.5f * (o.type.big ? 1.6f : 1f);
                        var ec = fl ? Color.white : C(o.type.skin ?? M.enemySkin);
                        if (o.type.ghost) ec.a = .7f;
                        Draw(circle, C(M.enemyShirt), o.x, sy, sz * .9f, sz * .7f, 12);
                        Draw(circle, ec, o.x, sy - sz * .45f, sz * .6f, sz * .6f, 13);
                        Draw(circle, C(M.eye), o.x - sz * .1f, sy - sz * .5f, 4, 4, 14);
                        Draw(circle, C(M.eye), o.x + sz * .1f, sy - sz * .5f, 4, 4, 14);
                        Bar(o.x, sy - sz * .95f, 40, o.hp / o.max, o.chip / o.max, C("#ff5a4d"));
                        break;
                }
            }

            if (sim.boss != null && sim.state != BattleState.Won)
            {
                var B = sim.boss; float bs = 26 * B.sc;
                Draw(circle, B.flash > 0 ? Color.white : C("#3c3c44"), B.x, B.y, bs * 1.2f, bs, 20);
                Draw(circle, B.flash > 0 ? Color.white : C(B.color), B.x, B.y - bs * .6f, bs * .8f, bs * .8f, 21);
                Draw(circle, C("#ffd23f"), B.x - bs * .15f, B.y - bs * .65f, 7, 7, 22);
                Draw(circle, C("#ffd23f"), B.x + bs * .15f, B.y - bs * .65f, 7, 7, 22);
            }

            // Sürü: kahraman + askerler
            if (sim.state != BattleState.Lost)
            {
                int cnt = Mathf.Min(40, Mathf.CeilToInt(sim.Soldiers));
                float rad = sim.Radius;
                for (int i = 0; i < cnt; i++)
                {
                    float a = i * 2.39996f, rr = rad * Mathf.Sqrt((i + .5f) / Mathf.Max(1, cnt));
                    Draw(circle, C("#2f6fb3"), sim.x + Mathf.Cos(a) * rr, Field.SQY + Mathf.Sin(a) * rr * .6f, 11, 11, 30);
                    Draw(circle, C("#cfe3f7"), sim.x + Mathf.Cos(a) * rr, Field.SQY + Mathf.Sin(a) * rr * .6f - 5, 6, 6, 31);
                }
                Draw(circle, new Color(.6f, .53f, 1f, .35f), sim.x, Field.SQY, 46, 30, 32);
                Draw(circle, C("#7c3dc4"), sim.x, Field.SQY, 24, 24, 33);
                Draw(circle, C("#e9edf5"), sim.x, Field.SQY - 12, 12, 12, 34);
            }

            foreach (var b in sim.bullets) Draw(circle, sim.buffBlast > 0 ? C("#ff9a3a") : C(sim.mods.weaponColor), b.x, b.y, 6, 9, 35);
            foreach (var e in sim.eb) Draw(circle, C(e.color), e.x, e.y, 10, 10, 36);

            for (int i = used; i < pool.Count; i++) if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }

        void OnBattleEnd()
        {
            if (sim.state == BattleState.Won)
            {
                Fails[level] = 0;
                completedThisSession++;
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
            rwGold = Mathf.RoundToInt((30 + 6 * level) * mu * sim.mods.goldMul);
            rwIron = Mathf.RoundToInt((14 + 3 * level) * mu * sim.mods.marketMul);
            rwFood = Mathf.RoundToInt((18 + 4 * level) * mu * sim.mods.marketMul);
            rwGems = first ? sim.stars + (level % 10 == 0 ? 5 : 0) : 0;
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
                label = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 16 };
                big = new GUIStyle(label) { fontSize = 30 };
                btn = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            }

            // Nesne yazıları
            foreach (var o in sim.objs)
            {
                if (o.done) continue;
                float sy = sim.Osy(o); if (sy < -40 || sy > Field.H) continue;
                if (o.kind == ObjKind.Barrel) Text(ToGui(o.x, sy - 8), "+" + Mathf.CeilToInt(o.v - o.rel), Color.white, label);
                else if (o.kind == ObjKind.Power) Text(ToGui(o.x, sy - 8), BattleSim.PowerName(o.pk), Color.white, label);
                else if (o.kind == ObjKind.Board) Text(ToGui(o.x, sy - 8), "−" + Mathf.CeilToInt(o.v), Color.white, label);
            }
            foreach (var f in sim.floats) { var c = C(f.color); c.a = Mathf.Clamp01(f.life * 1.5f); Text(ToGui(f.x, f.y), f.text, c, label); }
            if (sim.state != BattleState.Lost) Text(ToGui(sim.x, Field.SQY + 34), BattleSim.Fmt(sim.n), Color.white, big);

            // Üst bilgi
            var M = BattleData.Maps[sim.map];
            Text(new Vector2(VW / 2, 22), $"Bölüm {level} · {M.nameTr}", Color.white, label, 400);
            Text(new Vector2(VW / 2, 106), Progress.Heroes[Progress.HeroIndex(sim.mods.heroId)].name + " · " + Progress.Weapons[Progress.WeaponIndex(sim.mods.weaponId)].name, new Color(1, 1, 1, .6f), label, 400);
            float hpF = sim.hhp / Mathf.Max(1, sim.HeroMax);
            GUI.DrawTexture(new Rect(VW / 2 - 100, 40, 200, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .6f), 0, 0);
            GUI.DrawTexture(new Rect(VW / 2 - 100, 40, 200 * hpF, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, C("#8be39d"), 0, 0);
            if (GameServices.Wallet != null)
                Text(new Vector2(VW - 90, 22), "Elmas: " + GameServices.Wallet.Get(Currency.Diamonds), C("#9fe8ff"), label, 170);
            if (sim.boss != null)
            {
                var B = sim.boss;
                Text(new Vector2(VW / 2, 66), (B.isBoss ? "PATRON: " : "") + B.name, C("#ffd23f"), label, 400);
                GUI.DrawTexture(new Rect(VW / 2 - 140, 82, 280, 12), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .6f), 0, 0);
                GUI.DrawTexture(new Rect(VW / 2 - 140, 82, 280 * Mathf.Clamp01(B.hp / B.max), 12), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, C("#ff5a4d"), 0, 0);
            }
            if (sim.t < 2.6f && sim.Live)
                Text(new Vector2(VW / 2, 300), $"BÖLÜM {level}", Color.white, big, 400);

            if (sim.Live && GUI.Button(new Rect(8, 8, 70, 36), "Menü", btn)) { GoMenu(); return; }

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
            GUI.DrawTexture(new Rect(px, py, pw, 330), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .78f), 0, 12);
            bool won = sim.state == BattleState.Won;
            Text(new Vector2(VW / 2, py + 34), won ? $"Bölüm {level} tamam" : "Yenildin", won ? C("#8be39d") : C("#ff8a80"), big, pw);
            if (won)
            {
                Text(new Vector2(VW / 2, py + 76), new string('★', sim.stars) + new string('☆', 3 - sim.stars), C("#ffd23f"), big, pw);
                Text(new Vector2(VW / 2, py + 112), rewardText, Color.white, label, pw);
                float by = py + 140;
                if (!doubled && GUI.Button(new Rect(px + 20, by, pw - 40, 50), "Ödülü 2 katına çıkar (reklam izle)", btn)) DoubleRewards();
                by += 60;
                if (level < Balance.MaxLevel && GUI.Button(new Rect(px + 20, by, pw - 40, 50), "Sonraki bölüm", btn))
                {
                    if (completedThisSession % 3 == 0) Shop.MaybeShowInterstitial(AdPlacement.LevelEnd);
                    StartLevel(level + 1);
                }
                by += 60;
                if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Tekrar oyna", btn)) StartLevel(level);
                if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Menü", btn)) GoMenu();
            }
            else
            {
                Text(new Vector2(VW / 2, py + 80), "Kahramanın düştü.", Color.white, label, pw);
                float by = py + 120;
                if (sim.CanRevive && GUI.Button(new Rect(px + 20, by, pw - 40, 56), "İkinci şans: reklam izle", btn))
                    Shop.WatchAd(AdPlacement.SecondChance, () => sim.Revive());
                by += 70;
                if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Tekrar dene", btn)) StartLevel(level);
                if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Menü", btn)) GoMenu();
            }
        }
    }
}
