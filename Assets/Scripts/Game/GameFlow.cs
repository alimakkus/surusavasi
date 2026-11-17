using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    public enum GameScreen { Title, Levels, Heroes, Weapons, Base, Battle }

    /// <summary>Oyunun ekran akışı: başlık, bölüm seçimi, kahramanlar, silahlar, üs, savaş.
    /// Geçici arayüz (OnGUI); görsel adımda Unity UI'ya taşınacak.</summary>
    public class GameFlow : MonoBehaviour
    {
        public static GameFlow I { get; private set; }
        public GameScreen screen = GameScreen.Title;
        BattleView battle;
        Vector2 scroll;
        string toast; float toastT;
        GUIStyle title, label, small, btn, card;
        float gs, VW;

        SaveData D => GameServices.Save.Data;
        Wallet Wl => GameServices.Wallet;

        void Awake()
        {
            I = this;
            var go = new GameObject("Battle");
            go.transform.SetParent(transform);
            battle = go.AddComponent<BattleView>();
            battle.onMenu = () => Show(GameScreen.Title);
            go.SetActive(false);
            SoundFx.Ensure();
            var cam = Camera.main;
            if (cam != null) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.1f, .11f, .14f); }
        }

        public void Show(GameScreen s) { screen = s; scroll = Vector2.zero; }

        public void Play(int lv)
        {
            screen = GameScreen.Battle;
            battle.Begin(Mathf.Clamp(lv, 1, Balance.MaxLevel));
        }

        void Toast(string s) { toast = s; toastT = 2.2f; }

        void Update() { if (toastT > 0) toastT -= Time.deltaTime; }

        void Styles()
        {
            if (title != null) return;
            title = UiSkin.Title; label = UiSkin.Label; small = UiSkin.Small; btn = UiSkin.Button; card = UiSkin.Card;
        }

        void OnGUI()
        {
            if (screen == GameScreen.Battle || GameServices.Save == null) return;
            Styles();
            gs = UnityEngine.Screen.height / 800f;
            VW = UnityEngine.Screen.width / gs;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(gs, gs, 1));
            float w = Mathf.Min(440, VW - 20), x0 = VW / 2 - w / 2;

            // Arka plan: oyuncunun bulunduğu haritanın dokusu, üstüne koyu perde
            var bg = Art.Background(BattleData.MapOf(Mathf.Clamp(D.unlocked, 1, Balance.MaxLevel)));
            if (bg != null)
            {
                float th = 800, tw = th * 360f / 480f;
                for (float bx = VW / 2 - tw / 2 - tw * Mathf.Ceil((VW / 2) / tw); bx < VW; bx += tw) GUI.DrawTexture(new Rect(bx, 0, tw, th), bg.texture);
                GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.03f, .04f, .08f, .62f), 0, 0);
            }

            ResourceBar(x0, w);
            switch (screen)
            {
                case GameScreen.Title: TitleScreen(x0, w); break;
                case GameScreen.Levels: LevelsScreen(x0, w); break;
                case GameScreen.Heroes: HeroesScreen(x0, w); break;
                case GameScreen.Weapons: WeaponsScreen(x0, w); break;
                case GameScreen.Base: BaseScreen.Draw(this, x0, w, label, small, btn, card); break;
            }
            if (toastT > 0 && !string.IsNullOrEmpty(toast))
            {
                GUI.Box(new Rect(VW / 2 - 170, 720, 340, 44), "");
                GUI.Label(new Rect(VW / 2 - 160, 720, 320, 44), toast, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
            }
        }

        public void ShowToast(string s) => Toast(s);

        void ResourceBar(float x0, float w)
        {
            string s = $"<color=#ffd23f>Altın {Wl.Get(Currency.Gold)}</color>   <color=#cfd8e0>Demir {Wl.Get(Currency.Iron)}</color>   " +
                       $"<color=#8be39d>Yiyecek {Wl.Get(Currency.Food)}</color>   <color=#9fe8ff>Elmas {Wl.Get(Currency.Diamonds)}</color>";
            GUI.Box(new Rect(x0, 6, w, 34), "");
            GUI.Label(new Rect(x0 + 10, 6, w - 20, 34), s, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
        }

        bool Back(float x0)
        {
            return GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", btn);
        }

        void TitleScreen(float x0, float w)
        {
            GUI.Label(new Rect(x0 + 2, 92, w, 64), "<color=#00000099>SÜRÜ SAVAŞI</color>", title);
            GUI.Label(new Rect(x0, 90, w, 64), "SÜRÜ SAVAŞI", title);
            int lv = Mathf.Clamp(D.unlocked, 1, Balance.MaxLevel);
            GUI.Label(new Rect(x0, 150, w, 30), $"Toplam yıldız: {Progress.TotalStars(D)}  ·  Harita: {BattleData.Maps[BattleData.MapOf(lv)].nameTr}", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            float y = 340, bh = 58;
            var heroSp = Art.Hero(Progress.HeroIndex(D.hero), Mathf.FloorToInt(Time.time * 3) & 1);
            if (heroSp != null) { var r = heroSp.textureRect; var tx = heroSp.texture; GUI.DrawTextureWithTexCoords(new Rect(VW / 2 - 60, y - 128, 120, 120), tx, new Rect(r.x / tx.width, r.y / tx.height, r.width / tx.width, r.height / tx.height)); }
            if (GUI.Button(new Rect(x0 + 30, y, w - 60, bh + 10), $"OYNA  (Bölüm {lv})", UiSkin.ButtonGold)) { SoundFx.Play("click"); Play(lv); }
            y += bh + 26;
            if (GUI.Button(new Rect(x0 + 30, y, w - 60, bh), "Bölümler", btn)) Show(GameScreen.Levels); y += bh + 12;
            if (GUI.Button(new Rect(x0 + 30, y, w - 60, bh), "Üs", btn)) Show(GameScreen.Base); y += bh + 12;
            if (GUI.Button(new Rect(x0 + 30, y, w - 60, bh), "Kahramanlar", btn)) Show(GameScreen.Heroes); y += bh + 12;
            if (GUI.Button(new Rect(x0 + 30, y, w - 60, bh), "Silahlar", btn)) Show(GameScreen.Weapons); y += bh + 12;
            var H = Progress.Heroes[Progress.HeroIndex(D.hero)]; var Wp = Progress.Weapons[Progress.WeaponIndex(D.weapon)];
            GUI.Label(new Rect(x0, y - 4, w, 26), $"Seçili: {H.name} · {Wp.name}", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            float hw = (w - 70) / 2;
            if (GUI.Button(new Rect(x0 + 30, y + 24, hw, 40), SoundFx.Muted ? "Ses: Kapalı" : "Ses: Açık", new GUIStyle(btn) { fontSize = 15 })) SoundFx.Muted = !SoundFx.Muted;
            if (GUI.Button(new Rect(x0 + 40 + hw, y + 24, hw, 40), SoundFx.Vibrate ? "Titreşim: Açık" : "Titreşim: Kapalı", new GUIStyle(btn) { fontSize = 15 })) SoundFx.Vibrate = !SoundFx.Vibrate;
        }

        void LevelsScreen(float x0, float w)
        {
            if (Back(x0)) { Show(GameScreen.Title); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), "<b>Bölümler</b>", label);
            int cols = 5; float cw = (w - 10) / cols, ch = 64;
            int rows = Mathf.CeilToInt(Balance.MaxLevel / (float)cols);
            float headerEvery = ch * 1 + 26; // her 5 bölüm (1 satır) bir harita
            scroll = GUI.BeginScrollView(new Rect(x0, 96, w + 16, 700), scroll, new Rect(0, 0, w, rows * headerEvery + 10));
            for (int r = 0; r < rows; r++)
            {
                float ry = r * headerEvery;
                GUI.Label(new Rect(4, ry, w, 24), $"<color=#9fe8ff>{BattleData.Maps[BattleData.MapOf(r * cols + 1)].nameTr}</color>", small);
                for (int c = 0; c < cols; c++)
                {
                    int lv = r * cols + c + 1; if (lv > Balance.MaxLevel) break;
                    bool open = lv <= D.unlocked;
                    int st = D.stars[lv - 1];
                    GUI.enabled = open;
                    string txt = open ? $"{lv}{(lv % 10 == 0 ? " ☠" : "")}\n<color=#ffd23f>{new string('★', st)}{new string('☆', 3 - st)}</color>" : $"{lv}\nKilitli";
                    if (GUI.Button(new Rect(c * cw + 2, ry + 24, cw - 4, ch - 4), txt, btn)) Play(lv);
                    GUI.enabled = true;
                }
            }
            GUI.EndScrollView();
        }

        void HeroesScreen(float x0, float w)
        {
            if (Back(x0)) { Show(GameScreen.Title); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), $"<b>Kahramanlar</b>  (yıldız: {Progress.TotalStars(D)})", label);
            float y = 100;
            for (int i = 0; i < Progress.Heroes.Length; i++)
            {
                var H = Progress.Heroes[i]; int L = Progress.HeroLevel(D, i); bool open = Progress.HeroOpen(D, i), sel = D.hero == H.id;
                GUI.Box(new Rect(x0, y, w, 200), "", card);
                GUI.Label(new Rect(x0 + 12, y + 6, w - 24, 28), $"<b>{H.name}</b> — {H.role}   <color=#ffd23f>Sv. {L}/{Progress.HeroMax}</color>{(sel ? "  <color=#8be39d>(seçili)</color>" : "")}", label);
                GUI.Label(new Rect(x0 + 12, y + 34, w - 24, 44), $"<b>{H.skill}:</b> {H.skillDesc}", small);
                GUI.Label(new Rect(x0 + 12, y + 78, w - 24, 24), $"Özellik: {H.perk}", small);
                GUI.Label(new Rect(x0 + 12, y + 100, w - 24, 24), $"5. seviye: {H.lv5}", small);
                GUI.Label(new Rect(x0 + 12, y + 122, w - 24, 24), $"Her seviye: hasar +%3", small);
                float by = y + 148, bw = (w - 36) / 2;
                if (!open)
                {
                    GUI.enabled = false; GUI.Button(new Rect(x0 + 12, by, w - 24, 42), $"Kilitli: {H.reqStars} yıldız gerekli", btn); GUI.enabled = true;
                }
                else
                {
                    GUI.enabled = !sel;
                    if (GUI.Button(new Rect(x0 + 12, by, bw, 42), sel ? "Seçili" : "Seç", btn)) { D.hero = H.id; GameServices.Save.Save(); }
                    GUI.enabled = L < Progress.HeroMax;
                    int cg = Progress.HeroCostGold(L), ce = Progress.HeroCostGems(L);
                    if (GUI.Button(new Rect(x0 + 24 + bw, by, bw, 42), L < Progress.HeroMax ? $"Yükselt: {cg} altın + {ce} elmas" : "En üst seviye", new GUIStyle(btn) { fontSize = 14 }))
                    {
                        if (Wl.CanAfford(Currency.Gold, cg) && Wl.CanAfford(Currency.Diamonds, ce))
                        {
                            Wl.Spend(Currency.Gold, cg, "hero_up"); Wl.Spend(Currency.Diamonds, ce, "hero_up:" + H.id);
                            D.heroLevels[i] = L + 1; GameServices.Save.Save(); Toast($"{H.name} seviye {L + 1}!");
                        }
                        else Toast("Yeterli kaynak yok");
                    }
                    GUI.enabled = true;
                }
                y += 210;
            }
        }

        void WeaponsScreen(float x0, float w)
        {
            if (Back(x0)) { Show(GameScreen.Title); return; }
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
