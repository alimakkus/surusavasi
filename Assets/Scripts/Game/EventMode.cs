using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Kar Fırtınası (hafta sonu etkinliği): kampanya benzeri koşu; fırtınada düşmanlar yavaş ama dayanıklı,
    /// iyi kapılar güçlü. Ekranda kar yağar. Zafer kaynak, elmas ve sezon puanı verir.</summary>
    public class EventMode : IBattleMode
    {
        static SaveData D => GameServices.Save.Data;
        public string Id => "event";
        public string Title => "Kar Fırtınası";
        public string Intro => "FIRTINA BAŞLIYOR!";
        public int Level => SnowEvent.Level(D);
        public float HardMul => 1;
        public float TimeLimit => 0;
        public void Configure(BattleMods m) { m.gateBoost += SnowEvent.GateBoost; }
        public void Setup(BattleSim s)
        {
            s.enemySlow = SnowEvent.Slow;
            foreach (var o in s.objs) if (o.kind == ObjKind.Enemy) { o.hp *= SnowEvent.HpMul; o.max *= SnowEvent.HpMul; }
            s.L.fin.hp *= SnowEvent.HpMul;
            s.AddFloat(Field.W / 2, Field.SQY - 100, "Fırtına: düşmanlar yavaş ama dayanıklı", "#bff4ff");
        }
        public string Hud(BattleSim s) => "<color=#bff4ff>Kar Fırtınası</color>";
        public void Tick(BattleSim s, float dt) { }
        public bool Paused => false;

        // Kar taneleri: sabit tohumlu, zamanla düşen ve rüzgârla kayan noktalar
        public bool DrawOverlay(BattleView v, float VW)
        {
            float t = Time.time;
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.75f, .9f, 1f, .07f), 0, 0);
            for (int i = 0; i < 70; i++)
            {
                float sp = 40 + (i * 37 % 60), size = 2 + (i % 3);
                float y = Mathf.Repeat(i * 53f + t * sp, 820) - 10;
                float x = Mathf.Repeat(i * 97f + t * (25 + i % 5 * 6) + Mathf.Sin(t * 1.3f + i) * 12, VW + 20) - 10;
                GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, .55f + (i % 4) * .1f), 0, size / 2);
            }
            return false;
        }

        bool won; string text = "";
        public void OnEnd(BattleSim s)
        {
            var d = D;
            won = s.state == BattleState.Won;
            if (won)
            {
                var rw = SnowEvent.Reward(d, s.stars, s.mods);
                ModeUi.Grant(rw, "event");
                int sp = Season.Open(d) ? Season.AddPts(d, SnowEvent.WinSP, false) : 0;
                d.eventWins++; d.eventBest = Mathf.Max(d.eventBest, s.stars);
                League.Add(d, League.PtsTowerBase);
                Daily.Add(d, "clear");
                text = ModeUi.RewardLine(rw) + (sp > 0 ? $"\n<color=#9fe4ff><b>+{sp} sezon puanı</b></color>" : "");
            }
            else text = "Fırtına bu sefer ağır bastı. Hakkın varsa tekrar dene!";
            GameServices.Save.Save();
        }

        public void DrawResult(BattleView v, float VW)
        {
            var d = D;
            float pw = Mathf.Min(380, VW - 20), px = VW / 2 - pw / 2, py = 200;
            GUI.Box(new Rect(px, py, pw, 330), "", won ? UiSkin.CardGold : UiSkin.Card);
            GUI.Label(new Rect(px, py + 12, pw, 40), won ? "<color=#9fe4ff>Fırtınayı yendin!</color>" : "Fırtınada kaybettin", ModeUi.CenterBig);
            GUI.Label(new Rect(px + 12, py + 58, pw - 24, 90), text, ModeUi.Small);
            GUI.Label(new Rect(px, py + 150, pw, 24), $"<size=12>Bu sezon fırtına zaferi: <b>{d.eventWins}</b></size>", ModeUi.Center);
            float by = py + 182;
            var now = SnowEvent.Now;
            int left = SnowEvent.TriesLeft(d, now);
            if (SnowEvent.Open(d, now) && left > 0)
            {
                if (GUI.Button(new Rect(px + 20, by, pw - 40, 54), $"Tekrar  <size=13>({left} hak)</size>", UiSkin.ButtonGreen))
                { SnowEvent.UseTry(d, now); GameServices.Save.Save(); v.StartMode(new EventMode()); return; }
            }
            else GUI.Label(new Rect(px + 20, by, pw - 40, 54), "Bugünkü fırtına hakların bitti.", ModeUi.Center);
            by += 66;
            if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Modlar", UiSkin.Button)) ModeUi.ToModes(v);
            if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Üsse dön", UiSkin.Button)) v.GoMenu();
        }
    }
}
