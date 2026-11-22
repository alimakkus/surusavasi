using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Yetenek ağacı ekranı: 3 dal × (2 geliştirme + zirve). Düğüme dokun → ayrıntı ve "Geliştir".</summary>
    public static class TalentScreen
    {
        public static int hero;
        static int sel = 0;
        static float pop;      // geliştirme anında düğüm büyüyüp küçülür
        static int popNode = -1;
        static SaveData D => GameServices.Save.Data;

        public static void Open(GameFlow flow, int h) { hero = h; sel = 0; flow.Show(GameScreen.Talents); }

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var d = D; var H = Progress.Heroes[hero]; var T = Talents.Trees[hero];
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Heroes); return; }
            int free = Talents.Free(d, hero), total = Talents.Total(d, hero);
            GUI.Label(new Rect(x0 + 100, 44, w - 100, 26), $"<b>{H.name} · Yetenek Ağacı</b>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(x0 + 100, 68, w - 100, 22), $"Boş puan: <b><color={(free > 0 ? "#8be39d" : "#c9d3e0")}>{free}</color></b> / {total}   <size=11>(kahraman seviyesi başına 2)</size>", new GUIStyle(UiSkin.Small) { richText = true });

            // Dallar
            float colW = (w - 16) / 3, top = 98;
            GUI.Box(new Rect(x0, top, w, 360), "", UiSkin.CardDark);
            pop = Mathf.Max(0, pop - Time.deltaTime * 3);
            for (int b = 0; b < 3; b++)
            {
                float cx = x0 + 8 + colW * b + colW / 2;
                var bc = ModeUi.C(T.colors[b]);
                var rib = new Rect(cx - colW / 2 + 4, top + 8, colW - 8, 30);
                GUI.Box(rib, "", UiSkin.Ribbon);
                GUI.Label(rib, $"<b><size=13>{T.branches[b]}</size></b>", ModeUi.Center);
                int spent = Talents.BranchSpent(d, hero, b);
                float[] ys = { top + 92, top + 188, top + 298 };
                // Bağlantı çizgileri (dolu: geçilmiş)
                for (int k = 0; k < 2; k++)
                {
                    bool on = k == 0 ? Talents.Rank(d, hero, b * 3) > 0 : spent >= 3;
                    GUI.DrawTexture(new Rect(cx - 3, ys[k] + 30, 6, ys[k + 1] - ys[k] - 60), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, on ? bc : new Color(1, 1, 1, .15f), 0, 3);
                }
                for (int k = 0; k < 3; k++)
                {
                    int node = b * 3 + k; var n = T.nodes[node];
                    int r = Talents.Rank(d, hero, node);
                    bool cap = Talents.IsCap(node), can = Talents.CanUp(d, hero, node, out _);
                    float size = (cap ? 70 : 58) * (node == popNode ? 1 + pop * .35f : 1);
                    var nr = new Rect(cx - size / 2, ys[k] - size / 2, size, size);
                    // Halka: seçiliyse beyaz, geliştirilebilirse nabız
                    if (sel == node) GUI.DrawTexture(new Rect(nr.x - 5, nr.y - 5, nr.width + 10, nr.height + 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Color.white, 0, (size + 10) / 2);
                    else if (can) { float pu = .4f + .3f * Mathf.Sin(Time.time * 5 + node); GUI.DrawTexture(new Rect(nr.x - 4, nr.y - 4, nr.width + 8, nr.height + 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(bc.r, bc.g, bc.b, pu), 0, (size + 8) / 2); }
                    var fill = r > 0 ? bc : new Color(.16f, .18f, .24f, 1);
                    GUI.DrawTexture(nr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.05f, .06f, .09f, 1), 0, size / 2);
                    GUI.DrawTexture(new Rect(nr.x + 4, nr.y + 4, nr.width - 8, nr.height - 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, fill, 0, (size - 8) / 2);
                    GUI.Label(nr, $"<b><size={(cap ? 30 : 24)}>{n.icon}</size></b>", new GUIStyle(ModeUi.Center) { normal = { textColor = r > 0 ? new Color(.1f, .08f, .05f) : new Color(.75f, .8f, .9f) } });
                    GUI.Label(new Rect(cx - 50, nr.yMax + 1, 100, 18), $"<size=12><b>{r}/{n.max}</b></size>", ModeUi.Small);
                    if (GUI.Button(nr, "", GUIStyle.none)) { sel = node; SoundFx.Play("click"); }
                }
            }

            // Ayrıntı paneli
            var sn = T.nodes[sel]; int sr = Talents.Rank(d, hero, sel);
            var dp = new Rect(x0, top + 368, w, 190);
            GUI.Box(dp, "", UiSkin.Card);
            GUI.Label(new Rect(dp.x + 16, dp.y + 10, dp.width - 32, 28), $"<b><color={T.colors[sel / 3]}>{sn.name}</color></b>" + (Talents.IsCap(sel) ? "  <size=12><color=#ffd23f>ZİRVE</color></size>" : ""), new GUIStyle(UiSkin.Label) { richText = true, fontSize = 19 });
            GUI.Label(new Rect(dp.x + 16, dp.y + 40, dp.width - 32, 40), sn.desc + $"\n<size=12>Kademe: {sr}/{sn.max}</size>", new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true, fontSize = 14 });
            bool ok = Talents.CanUp(d, hero, sel, out string why);
            GUI.enabled = ok;
            if (GUI.Button(new Rect(dp.x + 16, dp.y + 100, dp.width - 32, 70), ok ? "GELİŞTİR  <size=13>(1 puan)</size>" : why, ok ? UiSkin.ButtonGreen : UiSkin.ButtonGray))
            {
                if (Talents.Up(d, hero, sel)) { GameServices.Save.Save(); SoundFx.Play("levelup"); pop = 1; popNode = sel; }
            }
            GUI.enabled = true;

            // Sıfırla
            bool rf = Talents.ResetFree(d); int spentAll = Talents.Spent(d, hero);
            GUI.enabled = spentAll > 0;
            if (GUI.Button(new Rect(x0 + w / 2 - 110, top + 568, 220, 44), rf ? "Puanları sıfırla (ücretsiz)" : $"Puanları sıfırla ({Talents.ResetGems} elmas)", new GUIStyle(UiSkin.ButtonGray) { fontSize = 13 }))
            {
                if (rf || GameServices.Wallet.Spend(Currency.Diamonds, Talents.ResetGems, "talent_reset"))
                { Talents.Reset(d, hero); GameServices.Save.Save(); SoundFx.Play("whoosh"); flow.Toast("Puanlar geri verildi"); }
                else flow.Toast("Yeterli elmas yok");
            }
            GUI.enabled = true;
        }
    }
}
