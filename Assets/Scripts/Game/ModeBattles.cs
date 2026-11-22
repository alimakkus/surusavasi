using System.Collections.Generic;
using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Kampanya dışı savaş modu. BattleView savaşı yürütür; mod kuralları, üst bilgi ve sonuç ekranını belirler.</summary>
    public interface IBattleMode
    {
        string Id { get; }
        string Title { get; }
        string Intro { get; }
        int Level { get; }
        float HardMul { get; }
        float TimeLimit { get; }
        void Configure(BattleMods m);
        void Setup(BattleSim s);
        string Hud(BattleSim s);
        void OnEnd(BattleSim s);
        void DrawResult(BattleView v, float VW);
        /// <summary>Savaş sürerken her kare (Hayatta Kalma dalgaları).</summary>
        void Tick(BattleSim s, float dt);
        /// <summary>true iken savaş durur (dalga arası mağaza).</summary>
        bool Paused { get; }
        /// <summary>Savaş sırasında üstte çizilen panel; true dönerse yetenek düğmesi gizlenir.</summary>
        bool DrawOverlay(BattleView v, float VW);
    }

    static class ModeUi
    {
        public static Color C(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static GUIStyle center, centerBig, small;
        public static GUIStyle Center => center ??= new GUIStyle(UiSkin.Label) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
        public static GUIStyle CenterBig => centerBig ??= new GUIStyle(UiSkin.Big) { alignment = TextAnchor.MiddleCenter, richText = true };
        public static GUIStyle Small => small ??= new GUIStyle(UiSkin.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };

        public static string RewardLine(LevelReward r) =>
            $"+{UiSkin.Short(r.gold)} altın  +{UiSkin.Short(r.iron)} demir  +{UiSkin.Short(r.food)} yiyecek" + (r.gems > 0 ? $"  <color=#9fe8ff>+{r.gems} elmas</color>" : "");

        public static void Grant(LevelReward r, string src)
        {
            var w = GameServices.Wallet; if (w == null) return;
            if (r.gold > 0) w.Add(Currency.Gold, r.gold, src);
            if (r.iron > 0) w.Add(Currency.Iron, r.iron, src);
            if (r.food > 0) w.Add(Currency.Food, r.food, src);
            if (r.gems > 0) w.Add(Currency.Diamonds, r.gems, src);
        }

        public static void ToModes(BattleView v) { v.GoMenu(); GameFlow.I?.Show(GameScreen.Modes); }
    }

    // =====================================================================
    /// <summary>Kule Tırmanışı: tek can, kat kat zorlaşır; her kattan sonra 3 güçlendirmeden biri seçilir.</summary>
    public class TowerMode : IBattleMode
    {
        readonly int floor;
        static SaveData D => GameServices.Save.Data;
        public TowerMode(int floor) { this.floor = floor; }

        public string Id => "tower";
        public string Title => $"Kule Tırmanışı · Kat {floor}";
        public string Intro => $"KAT {floor}";
        public int Level => Modes.TowerLevel(D, floor);
        public float HardMul => Modes.TowerHard(D, floor);
        public float TimeLimit => 0;
        public void Configure(BattleMods m) => Modes.ApplyBoons(D, m);
        public void Setup(BattleSim s) { if (D.towerBoons.Count > 0) s.AddFloat(Field.W / 2, Field.SQY - 100, $"{D.towerBoons.Count} güçlendirme etkin", "#c9b8ff"); }
        public string Hud(BattleSim s) => Level % 10 == 0 ? "<color=#ff8a80>Patron katı!</color>" : "";
        public void Tick(BattleSim s, float dt) { }
        public bool Paused => false;
        public bool DrawOverlay(BattleView v, float VW) => false;

        bool won;
        string text = "";
        List<Modes.Boon> choices;
        List<string> savedBoons; int savedFloor; bool reviving;

        public void OnEnd(BattleSim s)
        {
            var d = D;
            won = s.state == BattleState.Won;
            if (won)
            {
                var rw = Modes.FloorReward(d, floor, s.mods);
                ModeUi.Grant(rw, "tower");
                text = ModeUi.RewardLine(rw);
                var g = Modes.MilestoneGear(d, floor, new System.Random());
                if (g != null) { SoundFx.Play("chest"); text += HeroShards.GiveText(d, 6); }
                if (g != null) text += $"\n<color={Gear.RarityColors[g.rarity]}><b>Kat {floor} sandığı: {Gear.Name(g)} ({Gear.RarityNames[g.rarity]})</b></color>";
                League.Add(d, League.PtsTowerBase + floor);
                Daily.Add(d, "tower_floor");
                bool record = floor > d.towerBest;
                Modes.TowerFloorWon(d, floor);
                if (record) text += "\n<color=#ffd23f>Yeni rekor!</color>";
                choices = Modes.BoonChoices(d, floor);
                Daily.Add(d, "clear");
            }
            else
            {
                // Koşu hemen biter (çıkıp tekrar girerek katı yeniden deneme olmasın). İkinci şans kullanılırsa geri yüklenir.
                savedBoons = new List<string>(d.towerBoons); savedFloor = d.towerFloor;
                Modes.TowerEnd(d);
                text = $"Koşu bitti. Ulaştığın kat: <b>{floor - 1}</b>   Rekor: <b>{d.towerBest}</b>";
            }
            GameServices.Save.Save();
        }

        public void DrawResult(BattleView v, float VW)
        {
            var d = D;
            float pw = Mathf.Min(380, VW - 20), px = VW / 2 - pw / 2, py = 150;
            if (won)
            {
                GUI.Box(new Rect(px, py, pw, 470), "", UiSkin.CardGold);
                GUI.Label(new Rect(px, py + 12, pw, 40), $"<color=#8be39d>Kat {floor} geçildi!</color>", ModeUi.CenterBig);
                GUI.Label(new Rect(px + 12, py + 52, pw - 24, 70), text, ModeUi.Small);
                GUI.Label(new Rect(px, py + 122, pw, 26), "<b>Bir güçlendirme seç</b>", ModeUi.Center);
                float y = py + 152;
                foreach (var b in choices)
                {
                    var r = new Rect(px + 16, y, pw - 32, 70);
                    if (GUI.Button(r, "", UiSkin.ButtonGray))
                    {
                        SoundFx.Play("click");
                        Modes.TowerPickBoon(d, b.id); GameServices.Save.Save();
                        v.StartMode(new TowerMode(floor + 1));
                        return;
                    }
                    GUI.DrawTexture(new Rect(r.x + 10, r.y + 12, 8, r.height - 26), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(b.color), 0, 3);
                    int have = Modes.BoonCount(d, b.id);
                    GUI.Label(new Rect(r.x + 28, r.y + 6, r.width - 40, 28), $"<b>{b.name}</b>" + (have > 0 ? $"  <size=12>(x{have + 1})</size>" : ""), new GUIStyle(UiSkin.Label) { richText = true });
                    GUI.Label(new Rect(r.x + 28, r.y + 32, r.width - 40, 26), b.desc, new GUIStyle(UiSkin.Small) { richText = true });
                    y += 78;
                }
                if (choices.Count == 0 && GUI.Button(new Rect(px + 16, y, pw - 32, 54), "Devam et", UiSkin.ButtonGreen)) v.StartMode(new TowerMode(floor + 1));
                GUI.Label(new Rect(px, py + 400, pw, 22), $"Sıradaki: Kat {floor + 1} (Bölüm {Modes.TowerLevel(d, floor + 1)} gücünde)", ModeUi.Small);
                if (GUI.Button(new Rect(px + 16, py + 424, pw - 32, 38), "Seçip sonra devam edeceğim: Üsse dön", new GUIStyle(UiSkin.Button) { fontSize = 13 }))
                {
                    // Seçim yapılmadan çıkılırsa ilk seçenek verilir, koşu korunur
                    if (choices.Count > 0) { Modes.TowerPickBoon(d, choices[0].id); GameServices.Save.Save(); }
                    ModeUi.ToModes(v);
                }
                return;
            }

            GUI.Box(new Rect(px, py + 60, pw, 300), "", UiSkin.Card);
            GUI.Label(new Rect(px, py + 74, pw, 40), "<color=#ff8a80>Kulede düştün</color>", ModeUi.CenterBig);
            GUI.Label(new Rect(px + 12, py + 116, pw - 24, 50), text, ModeUi.Center);
            float by = py + 176;
            var sim = v.Sim;
            if (savedFloor > 0 && !reviving && sim.CanRevive && GUI.Button(new Rect(px + 20, by, pw - 40, 54), "▶ İkinci şans (reklam)", UiSkin.ButtonGold))
            {
                reviving = true;
                Shop.WatchAd(AdPlacement.SecondChance, () =>
                {
                    d.towerFloor = savedFloor; d.towerBoons.Clear(); d.towerBoons.AddRange(savedBoons); d.towerRevived = true;
                    GameServices.Save.Save();
                    savedFloor = 0; reviving = false;
                    sim.Revive();
                }, () => reviving = false);
            }
            by += 66;
            if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Modlar", UiSkin.Button)) ModeUi.ToModes(v);
            if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Üsse dön", UiSkin.Button)) v.GoMenu();
        }
    }

    // =====================================================================
    /// <summary>Patron Avı: koşu yok, ordu hazır; 75 saniyede patrona olabildiğince hasar ver.</summary>
    public class HuntMode : IBattleMode
    {
        static SaveData D => GameServices.Save.Data;
        public string Id => "hunt";
        public string Title => "Patron Avı";
        public string Intro => "AV BAŞLASIN!";
        public int Level => Modes.HuntLevel(D);
        public float HardMul => 1;
        public float TimeLimit => Modes.HuntTime;
        public void Configure(BattleMods m) { }
        public void Setup(BattleSim s) => Modes.HuntSetup(D, s);
        public void Tick(BattleSim s, float dt) { }
        public bool Paused => false;
        public bool DrawOverlay(BattleView v, float VW) => false;

        public string Hud(BattleSim s)
        {
            float left = Mathf.Max(0, TimeLimit - s.t), f = Modes.HuntFrac(s);
            int tier = Modes.TierOf(f);
            string tc = tier >= 0 ? $"  <color={Modes.TierColors[tier]}>{Modes.TierNames[tier]}</color>" : "";
            return $"<color={(left < 10 ? "#ff8a80" : "#ffffff")}>⏱ {left:0} sn</color>   Hasar: %{f * 100:0}{tc}";
        }

        float frac; int tier; string text = "";
        public void OnEnd(BattleSim s)
        {
            var d = D;
            frac = s.state == BattleState.Won ? 1 : Modes.HuntFrac(s);
            tier = Modes.TierOf(frac);
            var rw = Modes.HuntResult(d, frac, out var nt);
            League.Add(d, Mathf.RoundToInt(frac * League.PtsHuntMax));
            if (tier >= 1) Daily.Add(d, "hunt_tier");
            ModeUi.Grant(rw, "hunt");
            if (nt.Count > 0)
            {
                text = "Yeni kademe: ";
                for (int i = 0; i < nt.Count; i++) text += (i > 0 ? ", " : "") + $"<color={Modes.TierColors[nt[i]]}>{Modes.TierNames[nt[i]]}</color>";
                text += "\n" + ModeUi.RewardLine(rw);
                if (nt.Contains(2)) text += HeroShards.GiveText(d, 4);
                if (nt.Contains(3)) text += HeroShards.GiveText(d, 6);
                if (tier == 3)
                {
                    var g = new GearItem { id = ++d.gearSeq, slot = Random.Range(0, 3), rarity = 2 };
                    if (nt.Contains(3)) { Gear.AddToBag(d, g); text += $"\n<color={Gear.RarityColors[2]}><b>Av ganimeti: {Gear.Name(g)}</b></color>"; }
                }
            }
            else text = tier >= 0 ? "Bu kademenin ödülünü bugün zaten aldın.\nDaha yüksek kademe için tekrar dene!" : "Hiç kademe yok. Bronz için %25 hasar gerekli.";
            GameServices.Save.Save();
        }

        public void DrawResult(BattleView v, float VW)
        {
            var d = D;
            float pw = Mathf.Min(380, VW - 20), px = VW / 2 - pw / 2, py = 190;
            GUI.Box(new Rect(px, py, pw, 400), "", tier >= 2 ? UiSkin.CardGold : UiSkin.Card);
            GUI.Label(new Rect(px, py + 12, pw, 40), frac >= 1 ? "<color=#8be39d>Patron devrildi!</color>" : "Süre doldu", ModeUi.CenterBig);
            GUI.Label(new Rect(px, py + 54, pw, 30), $"Verilen hasar: <b>%{frac * 100:0}</b>   Bugünkü en iyi: %{d.huntBest / 10f:0}", ModeUi.Center);
            TierBar(new Rect(px + 20, py + 90, pw - 40, 34), frac, d.huntTiers);
            GUI.Label(new Rect(px + 12, py + 132, pw - 24, 76), text, ModeUi.Small);
            float by = py + 214;
            int left = Modes.HuntTriesLeft(d);
            if (left > 0)
            {
                if (GUI.Button(new Rect(px + 20, by, pw - 40, 54), $"Tekrar dene  <size=13>({left} hak)</size>", UiSkin.ButtonGreen))
                { Modes.HuntUseTry(d); GameServices.Save.Save(); v.StartMode(new HuntMode()); return; }
            }
            else if (Modes.HuntAdLeft(d) > 0)
            {
                if (GUI.Button(new Rect(px + 20, by, pw - 40, 54), "▶ Reklam izle: +1 deneme", UiSkin.ButtonGold))
                    Shop.WatchAd(AdPlacement.SecondChance, () => { Modes.HuntAddAdTry(d); GameServices.Save.Save(); });
            }
            else GUI.Label(new Rect(px + 20, by, pw - 40, 54), "Bugünkü haklar bitti. Yarın yeni patron!", ModeUi.Center);
            by += 66;
            if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Modlar", UiSkin.Button)) ModeUi.ToModes(v);
            if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Üsse dön", UiSkin.Button)) v.GoMenu();
        }

        /// <summary>Kademe çubuğu: dolan kısım + 4 işaret (alınanlar parlak).</summary>
        public static void TierBar(Rect r, float frac, int claimed)
        {
            GUI.DrawTexture(new Rect(r.x, r.y + 10, r.width, 12), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 6);
            GUI.DrawTexture(new Rect(r.x, r.y + 10, r.width * Mathf.Clamp01(frac), 12), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#ff5a4d"), 0, 6);
            for (int i = 0; i < 4; i++)
            {
                float x = r.x + r.width * Modes.TierAt[i];
                bool got = (claimed & (1 << i)) != 0;
                var c = ModeUi.C(Modes.TierColors[i]); if (!got) c *= new Color(.55f, .55f, .55f, 1);
                GUI.DrawTexture(new Rect(x - 9, r.y + 7, 18, 18), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, 0, 9);
                GUI.Label(new Rect(Mathf.Min(x - 40, r.xMax - 64), r.y + 22, 80, 16), $"<size=11>{Modes.TierNames[i]}</size>", ModeUi.Small);
            }
        }
    }
}
