using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>İttifak patronu: 60 saniyede haftanın ortak patronuna vur. Bu savaşta indirdiğin can, ittifak patronunun
    /// en çok %5'ine sayılır; hafta boyunca üyelerle birlikte %25/50/75/100 sandıkları açılır.</summary>
    public class AllianceBossMode : IBattleMode
    {
        static SaveData D => GameServices.Save.Data;
        public string Id => "alliance";
        public string Title => "İttifak Patronu";
        public string Intro => "İTTİFAK İÇİN!";
        public int Level => Modes.HuntLevel(D);
        public float HardMul => 1;
        public float TimeLimit => Alliance.BossTime;
        public void Configure(BattleMods m) { }
        public void Setup(BattleSim s)
        {
            int bi = Alliance.BossIndex(D);
            var F = s.L.fin;
            if (!F.boss) F.hp *= Balance.BossHp * Balance.BossEase(s.lv);
            F.boss = true; F.bossIndex = bi; F.name = BattleData.BossNames[bi]; F.color = BattleData.BossColors[bi];
            F.sc = 3.6f + s.lv / 100f * .8f; F.typeId = null;
            F.hp = Mathf.Ceil(F.hp * Modes.HuntHpMul);
            s.SkipToFinal(1f);
            s.noFatigue = true;
        }
        public void Tick(BattleSim s, float dt) { }
        public bool Paused => false;
        public bool DrawOverlay(BattleView v, float VW) => false;

        public string Hud(BattleSim s)
        {
            float left = Mathf.Max(0, TimeLimit - s.t), f = Modes.HuntFrac(s);
            return $"<color={(left < 10 ? "#ff8a80" : "#ffffff")}>⏱ {left:0} sn</color>   İttifak payı: %{f * Alliance.BossRunPct:0.0}";
        }

        float pct; string text = "";
        public void OnEnd(BattleSim s)
        {
            var d = D;
            float frac = s.state == BattleState.Won ? 1 : Modes.HuntFrac(s);
            pct = Alliance.BossHit(d, frac);
            League.Add(d, Mathf.RoundToInt(frac * League.PtsHuntMax * .5f));
            Daily.Add(d, "ally_boss");
            text = $"İttifak patronuna <b>%{pct:0.0}</b> hasar ekledin.\nPatronun toplam canı: <b>%{Alliance.TotalPct(d):0}</b> indirildi.\n<color=#ffd23f>+{Mathf.RoundToInt(frac * 20)} katkı puanı</color>";
            GameServices.Save.Save();
        }

        public void DrawResult(BattleView v, float VW)
        {
            var d = D;
            float pw = Mathf.Min(380, VW - 20), px = VW / 2 - pw / 2, py = 200;
            GUI.Box(new Rect(px, py, pw, 340), "", pct >= 3 ? UiSkin.CardGold : UiSkin.Card);
            GUI.Label(new Rect(px, py + 12, pw, 40), "İttifak için savaştın!", ModeUi.CenterBig);
            GUI.Label(new Rect(px + 12, py + 56, pw - 24, 90), text, ModeUi.Small);
            var bar = new Rect(px + 20, py + 152, pw - 40, 16);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 8);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Alliance.TotalPct(d) / 100f, bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#ff5a4d"), 0, 8);
            float by = py + 190;
            int left = Alliance.BossTriesLeft(d);
            if (left > 0 && Alliance.TotalPct(d) < 100)
            {
                if (GUI.Button(new Rect(px + 20, by, pw - 40, 54), $"Tekrar saldır  <size=13>({left} hak)</size>", UiSkin.ButtonRed))
                { Alliance.BossUseTry(d); GameServices.Save.Save(); v.StartMode(new AllianceBossMode()); return; }
            }
            else GUI.Label(new Rect(px + 20, by, pw - 40, 54), Alliance.TotalPct(d) >= 100 ? "Patron devrildi! Sandıkları al." : "Bugünkü hakların bitti.", ModeUi.Center);
            by += 66;
            if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "İttifak", UiSkin.Button)) { v.GoMenu(); GameFlow.I?.Show(GameScreen.Alliance); }
            if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Üsse dön", UiSkin.Button)) v.GoMenu();
        }
    }
}
