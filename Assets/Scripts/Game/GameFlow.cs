using System.Collections.Generic;
using SuruSavasi.Cloud;
using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    public enum GameScreen { Splash, Base, Levels, Heroes, Weapons, Battle, Shop, Daily, Gear, Modes, League, Talents, Skins, Achievements, Account, Season, Alliance }

    /// <summary>Oyunun ekran akışı: açılış (yükleme) → üs. Üsten savaş, bölümler, kahramanlar ve silahlara gidilir.
    /// Geçici arayüz (OnGUI); ileride Unity UI'ya taşınacak.</summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow I { get; private set; }
        public GameScreen screen = GameScreen.Splash;
        BattleView battle;
        BaseMapView baseMap;
        Vector2 scroll;
        string toast; float toastT;
        GUIStyle title, label, small, btn, card;
        float gs, VW;

        // Açılış
        readonly Texture2D[] frames = new Texture2D[16];
        Texture2D logo;
        List<string> toLoad; int loaded; float splashT;

        SaveData D => GameServices.Save.Data;
        Wallet Wl => GameServices.Wallet;

        void Awake()
        {
            I = this;
            Application.targetFrameRate = 60;
            UnityEngine.Screen.orientation = ScreenOrientation.Portrait;
            var go = new GameObject("Battle"); go.transform.SetParent(transform);
            battle = go.AddComponent<BattleView>();
            battle.onMenu = () => Show(GameScreen.Base);
            go.SetActive(false);
            var bg = new GameObject("Base"); bg.transform.SetParent(transform);
            baseMap = bg.AddComponent<BaseMapView>(); baseMap.flow = this;
            bg.SetActive(false);
            SoundFx.Ensure();
            var cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cam = cg.AddComponent<Camera>(); cg.tag = "MainCamera"; }
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.05f, .05f, .09f);
            cam.transform.position = new Vector3(0, 0, -10);

            for (int i = 0; i < frames.Length; i++) frames[i] = Resources.Load<Texture2D>($"Art/title_{i:00}");
            logo = Resources.Load<Texture2D>("Art/logo");
            toLoad = Art.AllNames();
        }

        public void Show(GameScreen s)
        {
            screen = s; scroll = Vector2.zero;
            if (s == GameScreen.Levels) LevelMapScreen.Focus();
            if (s == GameScreen.Season) SeasonScreen.Focus();
            if (s == GameScreen.Base) baseMap.Open(); else baseMap.Close();
        }

        public void Play(int lv)
        {
            baseMap.Close();
            screen = GameScreen.Battle;
            battle.Begin(Mathf.Clamp(lv, 1, Balance.MaxLevel));
        }

        /// <summary>Kule / Patron Avı gibi bir oyun modunu başlatır.</summary>
        public void PlayMode(IBattleMode m)
        {
            baseMap.Close();
            screen = GameScreen.Battle;
            battle.BeginMode(m);
        }

        public void Toast(string s) { toast = s; toastT = 2.2f; }

        // ---------- Bildirimler: arka plana geçince planla, geri gelince sil ----------
        void OnApplicationPause(bool paused) { if (paused) PlanNotifications(); else GameServices.Notifications?.CancelAll(); }
        void OnApplicationQuit() => PlanNotifications();
        void Start() => GameServices.Notifications?.CancelAll();

        void PlanNotifications()
        {
            var n = GameServices.Notifications;
            if (n == null || GameServices.Save == null || !GameServices.Ready) return;
            n.CancelAll();
            if (!NotificationsFactory.Enabled) return;
            foreach (var p in NotificationPlan.Make(D, System.DateTime.Now, NotificationsFactory.CatOn)) n.Schedule(p.id, p.title, p.body, p.when);
            GameServices.Save.Save();
        }

#if UNITY_EDITOR
        // Editor testi: B tuşu bildirim planını konsola yazar
        void LateUpdate() { if (Input.GetKeyDown(KeyCode.B)) PlanNotifications(); }
#endif

        void Update()
        {
            if (toastT > 0) toastT -= Time.deltaTime;
            // Menü/üs müziği: gece saatlerinde (gece görünümü açıksa) sakin minör parça
            if (screen != GameScreen.Battle && screen != GameScreen.Splash)
            {
                int h = System.DateTime.Now.Hour;
                SoundFx.Music(BaseMapView.NightEnabled && (h >= 20 || h < 6) ? "base_night" : "base_day");
            }
            TickStartWait();
            if (screen != GameScreen.Splash) return;
            splashT += Time.unscaledDeltaTime;
            // Her karede birkaç görsel yüklenir: ilerleme çubuğu gerçek yüklemeyi gösterir
            for (int n = 0; n < 2 && loaded < toLoad.Count; n++) { Resources.Load<Texture2D>("Art/" + toLoad[loaded]); loaded++; }
            if (loaded >= toLoad.Count && splashT > 3.2f && GameServices.Ready)
            {
                for (int i = 0; i < frames.Length; i++) frames[i] = null;
                Resources.UnloadUnusedAssets();
                Show(GameScreen.Base);
                // Bulut kaydı: arka planda giriş + karşılaştırma (yeniden kurulumda kayıt geri gelir)
                CloudSaveManager.Begin();
                // İlk açılış: önce gizlilik onayı (üs ekranında), sonra günlük ödül
                if (ConsentScreen.Needed) LocalConsent.Answered += AfterConsent;
                else StartAfterLoad();
            }
        }

        void AfterConsent()
        {
            LocalConsent.Answered -= AfterConsent;
            if (screen != GameScreen.Base) return;
            StartAfterLoad();
        }

        // Yeni oyuncu doğrudan ilk savaşa (hikâye + rehber) gider; ama önce bulutta eski kaydı var mı diye en çok 4 sn beklenir
        float startWait = -1;
        void StartAfterLoad()
        {
            if (NewPlayer && CloudSync.Supported) { startWait = 4f; return; }
            if (NewPlayer) Play(1);
            else if (Daily.CanLogin(D)) Show(GameScreen.Daily);
        }
        void TickStartWait()
        {
            if (startWait < 0) return;
            startWait -= Time.unscaledDeltaTime;
            if (CloudSaveManager.Busy && startWait > 0) return;
            startWait = -1;
            if (screen != GameScreen.Base || CloudSaveManager.Conflict != null) return;
            if (NewPlayer) Play(1);
            else if (Daily.CanLogin(D)) Show(GameScreen.Daily);
        }
        public bool InBattle => screen == GameScreen.Battle;

        /// <summary>Hiç bölüm geçmemiş oyuncu.</summary>
        bool NewPlayer => D.unlocked <= 1 && Progress.TotalStars(D) == 0;

        float Progress01 => Mathf.Min(toLoad == null || toLoad.Count == 0 ? 1 : loaded / (float)toLoad.Count, splashT / 3.2f);

        void Styles()
        {
            if (title != null) return;
            title = UiSkin.Title; label = UiSkin.Label; small = UiSkin.Small; btn = UiSkin.Button; card = UiSkin.Card;
        }

        void OnGUI()
        {
            if (screen == GameScreen.Battle) return;
            GUI.depth = -5;   // rehber pencereleri üs arayüzünün üstünde kalsın ve tıklamayı önce alsın
            Styles();
            gs = UnityEngine.Screen.height / 800f;
            VW = UnityEngine.Screen.width / gs;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(gs, gs, 1));
            if (screen == GameScreen.Splash) { SplashScreen(); return; }
            if (GameServices.Save == null) return;

            if (screen != GameScreen.Base)
            {
                float w = Mathf.Min(440, VW - 20), x0 = VW / 2 - w / 2;
                var bg = Art.Background(BattleData.MapOf(Mathf.Clamp(D.unlocked, 1, Balance.MaxLevel)));
                if (bg != null)
                {
                    float th = 800, tw = th * 360f / 480f;
                    for (float bx = VW / 2 - tw / 2 - tw * Mathf.Ceil((VW / 2) / tw); bx < VW; bx += tw) GUI.DrawTexture(new Rect(bx, 0, tw, th), bg.texture);
                    GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.03f, .04f, .08f, .62f), 0, 0);
                }
                ResourceBar(x0, w);
                bool intro = Tutorial.HasIntro(screen);
                if (intro) GUI.enabled = false;
                switch (screen)
                {
                    case GameScreen.Levels: LevelsScreen(x0, w); break;
                    case GameScreen.Heroes: HeroesScreen(x0, w); break;
                    case GameScreen.Weapons: WeaponsScreen(x0, w); break;
                    case GameScreen.Shop: MetaScreens.Shop(this, x0, w); break;
                    case GameScreen.Daily: MetaScreens.DailyScreen(this, x0, w); break;
                    case GameScreen.Gear: GearScreen.Draw(this, x0, w); break;
                    case GameScreen.Modes: ModesScreen.Draw(this, x0, w); break;
                    case GameScreen.League: LeagueScreen.Draw(this, x0, w); break;
                    case GameScreen.Talents: TalentScreen.Draw(this, x0, w); break;
                    case GameScreen.Skins: SkinScreen.Draw(this, x0, w); break;
                    case GameScreen.Achievements: AchievementsScreen.Draw(this, x0, w); break;
                    case GameScreen.Account: AccountScreen.Draw(this, x0, w); break;
                    case GameScreen.Season: SeasonScreen.Draw(this, x0, w); break;
                    case GameScreen.Alliance: AllianceScreen.Draw(this, x0, w); break;
                }
                GUI.enabled = true;
                if (intro) Tutorial.DrawIntro(screen, VW);
            }
            if (CloudSaveManager.DrawConflict(VW)) { }
            else if (screen == GameScreen.Base) Tutorial.DrawUnlocks(this, VW);
            if (toastT > 0 && !string.IsNullOrEmpty(toast))
            {
                GUI.Box(new Rect(VW / 2 - 170, 640, 340, 44), "", card);
                GUI.Label(new Rect(VW / 2 - 160, 640, 320, 44), toast, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            }
        }

        /// <summary>Açılış ekranı: hareketli oyun görseli, yükleme göstergesi, altta sürüm. Buton yoktur.</summary>
        void SplashScreen()
        {
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.03f, .03f, .08f), 0, 0);
            // Görsel ekranı kaplar (yükseklik tam, genişlik ortalanır); kareler arası yumuşak geçiş
            float iw = Mathf.Max(VW, 800 * 360f / 560f), ih = iw * 560f / 360f, ix = VW / 2 - iw / 2, iy = Mathf.Min(0, 800 - ih);
            float ft = splashT * 8f; int a = Mathf.FloorToInt(ft) % frames.Length, b = (a + 1) % frames.Length; float k = ft - Mathf.Floor(ft);
            if (frames[a] != null) GUI.DrawTexture(new Rect(ix, iy, iw, ih), frames[a]);
            if (frames[b] != null) { GUI.color = new Color(1, 1, 1, k); GUI.DrawTexture(new Rect(ix, iy, iw, ih), frames[b]); GUI.color = Color.white; }

            if (logo != null)
            {
                float lw = Mathf.Min(VW - 30, 400), lh = lw * logo.height / logo.width, pop = 1 + .03f * Mathf.Sin(splashT * 2.2f);
                GUI.DrawTexture(new Rect(VW / 2 - lw * pop / 2, 46 - (lh * pop - lh) / 2, lw * pop, lh * pop), logo);
            }
            var c = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(0, 232 + Mathf.Min(VW - 30, 400) * .1f, VW, 24), "<color=#e8dcff>100 BÖLÜM · ZOMBİ SÜRÜSÜNE KARŞI</color>", c);

            // Yükleme göstergesi
            float bw = Mathf.Min(320, VW - 60), bx0 = VW / 2 - bw / 2, by = 706, p = Mathf.Clamp01(Progress01);
            GUI.DrawTexture(new Rect(bx0 - 3, by - 3, bw + 6, 20), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .65f), 0, 10);
            GUI.DrawTexture(new Rect(bx0, by, Mathf.Max(14, bw * p), 14), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1f, .78f, .25f), 0, 7);
            string dots = new string('.', 1 + Mathf.FloorToInt(splashT * 3) % 3);
            GUI.Label(new Rect(0, by + 20, VW, 24), $"Yükleniyor{dots}  %{Mathf.RoundToInt(p * 100)}", c);
            GUI.Label(new Rect(0, 770, VW, 22), $"<color=#ffffff99>Sürüm {Application.version}</color>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
        }

        public void ShowToast(string s) => Toast(s);

        void ResourceBar(float x0, float w)
        {
            UiSkin.ResourceBar(x0, 4, w, Wl.Get(Currency.Gold), Wl.Get(Currency.Iron), Wl.Get(Currency.Food), Wl.Get(Currency.Diamonds));
        }

        bool Back(float x0)
        {
            return GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", btn);
        }

        void LevelsScreen(float x0, float w) => LevelMapScreen.Draw(this, x0, w);

        Vector2 heroScroll;
        void HeroesScreen(float x0, float w)
        {
            if (Back(x0)) { Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 300, 40), $"<b>Kahramanlar</b>\n<size=12>★ {Progress.TotalStars(D)}</size>", label);
            if (GUI.Button(new Rect(x0 + w - 196, 48, 76, 40), "Silah", UiSkin.Button)) { SoundFx.Play("click"); Show(GameScreen.Weapons); return; }
            if (GUI.Button(new Rect(x0 + w - 116, 48, 116, 40), "Ekipman", UiSkin.ButtonGold)) { SoundFx.Play("click"); Show(GameScreen.Gear); return; }
            string[] tint = { "#7a5cff", "#ff6a3a", "#2ad6b0", "#ffb02e", "#3fd16a" };
            // 5 kahraman: kaydırılabilir liste
            float sx0 = x0; x0 = 0;
            heroScroll = GUI.BeginScrollView(new Rect(sx0, 96, w + 16, 700), heroScroll, new Rect(0, 0, w, Progress.Heroes.Length * 224 + 8));
            float y = 4;
            for (int i = 0; i < Progress.Heroes.Length; i++)
            {
                var H = Progress.Heroes[i]; int L = Progress.HeroLevel(D, i); bool open = Progress.HeroOpen(D, i), sel = D.hero == H.id;
                float ch = 214;
                GUI.Box(new Rect(x0, y, w, ch), "", sel ? UiSkin.CardGold : card);
                // Portre: renkli arka plan, nefes alan kahraman, seviye rozeti
                var pr = new Rect(x0 + 10, y + 10, 132, ch - 20);
                ColorUtility.TryParseHtmlString(tint[i], out var tc);
                GUI.DrawTexture(pr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(tc.r * .35f, tc.g * .35f, tc.b * .35f, .95f), 0, 10);
                GUI.DrawTexture(new Rect(pr.x + 4, pr.y + pr.height * .45f, pr.width - 8, pr.height * .55f - 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(tc.r, tc.g, tc.b, .25f), 0, 8);
                var port = Art.HeroPortrait(i, Mathf.FloorToInt(Time.time * 1.6f + i));
                if (port != null)
                {
                    float bob = Mathf.Sin(Time.time * 2.2f + i) * 3;
                    var old = GUI.color; if (!open) GUI.color = new Color(0, 0, 0, .85f);
                    GUI.DrawTexture(new Rect(pr.x - 14, pr.y - 6 + bob, pr.width + 28, pr.width + 28), port, ScaleMode.ScaleToFit);
                    GUI.color = old;
                }
                if (!open) GUI.Label(new Rect(pr.x, pr.y + pr.height / 2 - 20, pr.width, 40), "<b>KİLİTLİ</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
                GUI.Box(new Rect(pr.x + 6, pr.yMax - 34, pr.width - 12, 28), $"Sv. {L}/{Progress.HeroMax}", new GUIStyle(UiSkin.Ribbon) { fontSize = 14 });
                if (open && GUI.Button(new Rect(pr.x + 6, pr.y + 6, pr.width - 12, 26), $"Görünüm ›  <size=10>{Skins.OwnedCount(D, i)}/{Skins.Ids.Length}</size>", new GUIStyle(UiSkin.ButtonGray) { fontSize = 12, richText = true }))
                { SoundFx.Play("click"); SkinScreen.Open(this, i); }

                float tx = x0 + 152, tw = w - 164;
                GUI.Label(new Rect(tx, y + 8, tw, 28), $"<b><size=20>{H.name}</size></b>  <color=#c9d3e0>{H.role}</color>", label);
                GUI.Label(new Rect(tx, y + 36, tw, 48), $"<color=#ffd23f><b>{H.skill}:</b></color> {H.skillDesc}", new GUIStyle(small) { fontSize = 13 });
                GUI.Label(new Rect(tx, y + 84, tw, 40), $"<color=#8be39d>●</color> {H.perk}\n<color=#9fe8ff>●</color> 5. seviye: {H.lv5}", new GUIStyle(small) { fontSize = 12 });
                float by = y + ch - 62, bw = (tw - 8) / 2;
                if (!open && HeroShards.IsShardHero(i))
                {
                    // Parçayla açılır: ilerleme çubuğu + aç / elmasla parça
                    int sh = D.heroShards[i];
                    var bar = new Rect(tx, y + 126, tw, 22);
                    GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 11);
                    GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * sh / (float)HeroShards.Need, bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(tc.r, tc.g, tc.b, .9f), 0, 11);
                    GUI.Label(bar, $"<b>Parça {sh}/{HeroShards.Need}</b>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
                    if (HeroShards.CanUnlock(D, i))
                    {
                        float pu = 1 + .04f * Mathf.Sin(Time.time * 6);
                        if (GUI.Button(new Rect(tx - (pu - 1) * tw / 2, by, tw * pu, 50), "KAHRAMANI AÇ", UiSkin.ButtonGold))
                        { HeroShards.Unlock(D, i); GameServices.Save.Save(); SoundFx.Play("promote"); Toast($"{H.name} katıldı!"); }
                    }
                    else
                    {
                        GUI.Label(new Rect(tx, by - 4, bw, 54), "<size=11>Parçalar: Kule sandıkları, Patron Avı, Hayatta Kalma, lig ve bölüm sandıkları</size>", new GUIStyle(small) { wordWrap = true });
                        if (GUI.Button(new Rect(tx + bw + 8, by, bw, 50), $"+{HeroShards.PackShards} parça\n<size=11>{HeroShards.PackGems} elmas</size>", new GUIStyle(UiSkin.ButtonGold) { fontSize = 15 }))
                        {
                            if (Wl.Spend(Currency.Diamonds, HeroShards.PackGems, "hero_shards:" + H.id)) { HeroShards.Give(D, HeroShards.PackShards, i); GameServices.Save.Save(); SoundFx.Play("coin"); }
                            else Toast("Yeterli elmas yok");
                        }
                    }
                }
                else if (!open)
                {
                    GUI.enabled = false; GUI.Button(new Rect(tx, by, tw, 50), $"{H.reqStars} yıldızla açılır", UiSkin.ButtonGray); GUI.enabled = true;
                }
                else
                {
                    // Yetenek ağacı: boş puan varsa nabız atan işaret
                    int tfree = Talents.Free(D, i);
                    var trr = new Rect(tx, y + 124, tw, 26);
                    if (GUI.Button(trr, tfree > 0 ? $"<b>Yetenek Ağacı · {tfree} boş puan</b>" : "Yetenek Ağacı", new GUIStyle(tfree > 0 ? UiSkin.ButtonGreen : UiSkin.Button) { fontSize = 13, richText = true }))
                    { SoundFx.Play("click"); TalentScreen.Open(this, i); }
                    if (tfree > 0) { float pu = 1 + .15f * Mathf.Sin(Time.time * 6); GUI.DrawTexture(new Rect(trr.xMax - 8 - 5 * pu, trr.y - 4 - 5 * pu, 12 * pu, 12 * pu), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.88f, .27f, .23f), 0, 6 * pu); }
                    GUI.enabled = !sel;
                    if (GUI.Button(new Rect(tx, by, bw, 50), sel ? "Seçili" : "Seç", sel ? UiSkin.ButtonGray : UiSkin.ButtonGreen)) { D.hero = H.id; GameServices.Save.Save(); SoundFx.Play("power"); }
                    GUI.enabled = L < Progress.HeroMax;
                    int cg = Progress.HeroCostGold(L), ce = Progress.HeroCostGems(L);
                    if (GUI.Button(new Rect(tx + bw + 8, by, bw, 50), L < Progress.HeroMax ? $"Yükselt\n<size=11>{UiSkin.Short(cg)} altın + {ce} elmas</size>" : "En üst", new GUIStyle(UiSkin.ButtonGold) { fontSize = 16 }))
                    {
                        if (Wl.CanAfford(Currency.Gold, cg) && Wl.CanAfford(Currency.Diamonds, ce))
                        {
                            Wl.Spend(Currency.Gold, cg, "hero_up"); Wl.Spend(Currency.Diamonds, ce, "hero_up:" + H.id);
                            D.heroLevels[i] = L + 1; GameServices.Save.Save(); Toast($"{H.name} seviye {L + 1}! +{(L + 1 == 5 || L + 1 == 10 ? 2 : 1)} yetenek puanı"); SoundFx.Play("levelup");
                        }
                        else Toast("Yeterli kaynak yok");
                    }
                    GUI.enabled = true;
                }
                y += 224;
            }
            GUI.EndScrollView();
        }

        void WeaponsScreen(float x0, float w)
        {
            if (Back(x0)) { Show(GameScreen.Heroes); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), $"<b>Silahlar</b>  (Silahhane Sv. {Progress.Level(D, BuildingKind.Armory)})", label);
            float y = 100;
            for (int i = 0; i < Progress.Weapons.Length; i++)
            {
                var Wp = Progress.Weapons[i]; int L = Progress.WeaponLevel(D, i); bool open = Progress.WeaponOpen(D, i), sel = D.weapon == Wp.id;
                GUI.Box(new Rect(x0, y, w, 150), "", card);
                GUI.Label(new Rect(x0 + 12, y + 6, w - 24, 28), $"<b>{Wp.name}</b>   <color=#ffd23f>Sv. {L}/{Progress.WeaponMax}</color>{(sel ? "  <color=#8be39d>(seçili)</color>" : "")}", label);
                GUI.Label(new Rect(x0 + 12, y + 34, w - 24, 26), Wp.desc, small);
                GUI.Label(new Rect(x0 + 12, y + 58, w - 24, 26), $"Hasar x{Wp.dmg:0.##} · Ateş aralığı x{Wp.rate:0.##} · Her seviye hasar +%8", small);
                float by = y + 96, bw = (w - 36) / 2;
                if (!open)
                {
                    GUI.enabled = false; GUI.Button(new Rect(x0 + 12, by, w - 24, 42), $"Kilitli: Silahhane seviye {Wp.reqArmory}", btn); GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = !sel;
                    if (GUI.Button(new Rect(x0 + 12, by, bw, 42), sel ? "Seçili" : "Seç", btn)) { D.weapon = Wp.id; GameServices.Save.Save(); }
                    GUI.enabled = L < Progress.WeaponMax;
                    int cg = Progress.WeaponCostGold(L), ci = Progress.WeaponCostIron(L);
                    if (GUI.Button(new Rect(x0 + 24 + bw, by, bw, 42), L < Progress.WeaponMax ? $"Yükselt: {cg} altın + {ci} demir" : "En üst seviye", new GUIStyle(btn) { fontSize = 14 }))
                    {
                        if (Wl.CanAfford(Currency.Gold, cg) && Wl.CanAfford(Currency.Iron, ci))
                        {
                            Wl.Spend(Currency.Gold, cg, "weapon_up"); Wl.Spend(Currency.Iron, ci, "weapon_up");
                            D.weaponLevels[i] = L + 1; GameServices.Save.Save(); Toast($"{Wp.name} seviye {L + 1}!");
                        }
                        else Toast("Yeterli kaynak yok");
                    }
                    GUI.enabled = true;
                }
                y += 160;
            }
        }
    }

    /// <summary>Oyun başlarken akışı kurar: boş sahnede Play'e basınca başlık ekranı açılır.</summary>
    public static class GameBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Object.FindObjectOfType<GameFlow>() != null) return;
            new GameObject("Game").AddComponent<GameFlow>();
        }
    }
}
