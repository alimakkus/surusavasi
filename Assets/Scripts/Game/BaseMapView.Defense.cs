using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Üs savunma modu: kapının önündeki yoldan zombi dalgaları gelir, oyuncu boş yerlere kule kurar.</summary>
    public partial class BaseMapView
    {
        DefenseSim def;
        int defPick = -1;
        bool defShowTiers, defRewardGiven;
        LevelReward defRw;
        const float DefSpawn = 330;

        float GateY => Pos[BuildingKind.Wall].y;
        static readonly float[] SlotDX = { -78, 78, -118, 118, -78, 78 }, SlotDY = { 70, 70, 165, 165, 255, 255 };

        public bool InDefense => def != null;

        void StartDefense(int tier)
        {
            if (DefenseSim.ReadyIn(D) > 0) return;
            var sx = new float[6]; var sy = new float[6];
            for (int i = 0; i < 6; i++) { sx[i] = CX + SlotDX[i]; sy[i] = GateY + SlotDY[i]; }
            def = new DefenseSim(D, tier, CX, GateY, GateY + DefSpawn, sx, sy);
            def.Fx += DefFx;
            D.defenseReadyAt = BaseLogic.Now + DefenseSim.CooldownMinutes * 60000L; GameServices.Save.Save();
            sel = null; popup = Popup.None; defPick = -1; defRewardGiven = false;
            visW = 480; camX = CX; camY = GateY + 120;
            events.Clear();
            GameServices.Analytics?.LogEvent("defense_start", new System.Collections.Generic.Dictionary<string, object> { { "tier", tier } });
        }

        void DefFx(string k, float x, float y)
        {
            if (k.StartsWith("+")) { AddFloat(x, y, k, Gold); return; }
            switch (k)
            {
                case "kill": Burst(x, y - 12, 12, new[] { Hex("#8dbd80"), Hex("#ff5a4d"), Color.white }, 90, 30); SoundFx.Play("kill"); break;
                case "bigkill": Burst(x, y - 20, 30, new[] { Hex("#8dbd80"), Hex("#ff5a4d"), Gold }, 130, 40); SoundFx.Play("boom"); break;
                case "boom": Burst(x, y - 10, 12, new[] { Hex("#ff9a3a"), Gold, Color.white }, 100, 40); SoundFx.Play("boom"); break;
                case "bossin": AddFloat(x, y - 60, "BOSS!", Hex("#ff5a4d")); SoundFx.Play("boss"); break;
                case "hurt": Burst(x, y - 14, 2, new[] { Hex("#c9b08a"), Color.white }, 40, 20); SoundFx.Play("hurt"); break;
                default: SoundFx.Play(k); break;
            }
        }

        void DefenseTick(float dt)
        {
            if (def == null) return;
            def.Step(dt);
            if (def.state != DefenseSim.St.Run && !defRewardGiven)
            {
                defRewardGiven = true; defRw = def.Reward(D);
                Wl.Add(Currency.Gold, defRw.gold, "defense"); Wl.Add(Currency.Iron, defRw.iron, "defense"); Wl.Add(Currency.Food, defRw.food, "defense");
                if (defRw.gems > 0) Wl.Add(Currency.Diamonds, defRw.gems, "defense");
                if (def.state == DefenseSim.St.Won) Quests.Add(D, "defense");
                GameServices.Save.Save();
                GameServices.Analytics?.LogEvent("defense_end", new System.Collections.Generic.Dictionary<string, object> { { "tier", def.tier }, { "win", def.state == DefenseSim.St.Won }, { "wave", def.wave } });
            }
        }

        void DefenseDraw()
        {
            if (def == null) return;
            // Kule yerleri
            for (int i = 0; i < def.slotX.Length; i++)
            {
                float x = def.slotX[i], y = def.slotY[i];
                var q = def.TowerAt(i);
                if (q == null)
                {
                    bool ok = i < def.slots; float a = ok ? .45f + .2f * Mathf.Sin(t * 4 + i) : .2f;
                    Draw(square, new Color(1, 1, 1, a), x, y, 30, 30, 6, 45);
                    Draw(square, new Color(.1f, .14f, .1f, a * .6f), x, y, 24, 24, 7, 45);
                    if (defPick == i) Draw(circle, new Color(1, .9f, .3f, .5f + .2f * Mathf.Sin(t * 8)), x, y, 64, 34, 5);
                    continue;
                }
                var w = DefenseSim.Towers[q.type]; int ord = 100 + (int)y;
                if (defPick == i) Draw(circle, new Color(1, .9f, .3f, .5f + .2f * Mathf.Sin(t * 8)), x, y + 2, 64, 34, 5);
                Spr(Art.WallTower(Mathf.Clamp(q.lv + 1, 1, 5)), x, y, ord, .85f);
                Draw(circle, Color.white, x, y - 58, 17, 17, ord + 1); Draw(circle, Hex(w.color), x, y - 58, 14, 14, ord + 2);
                for (int s = 0; s < q.lv; s++) Draw(circle, Gold, x - 8 + s * 8, y - 72, 6, 6, ord + 2);
            }
            // Kapıdaki kahraman
            Spr(Art.Hero(Progress.HeroIndex(D.hero), Mathf.FloorToInt(t * 3) & 1), CX, GateY - 14, 100 + (int)GateY + 30, 1f);
            // Zombiler
            foreach (var z in def.zs)
            {
                int ord = 100 + (int)z.y; Color? tint = z.flash > 0 ? new Color(1.6f, 1.6f, 1.6f) : z.slow > 0 ? new Color(.6f, .85f, 1.3f) : (Color?)null;
                int fr = Mathf.FloorToInt(z.ph * .7f);
                if (z.boss) Spr(Art.Boss(Mathf.Clamp(D.unlocked / 10, 0, 9), fr), z.x, z.y, ord, .55f, false, tint);
                else Spr(Art.Enemy(0, z.big ? "brute" : z.run ? "runner" : "walker", fr), z.x, z.y, ord, z.sc * .8f, false, tint);
                float bw = z.boss ? 60 : z.big ? 36 : 26, hy = z.y - (z.boss ? 120 : 46 * z.sc);
                Draw(square, new Color(.03f, .05f, .07f, .85f), z.x, hy, bw, 6, 2050);
                float f = Mathf.Clamp01(z.hp / z.max);
                Draw(square, z.boss ? Hex("#ff9a3a") : Hex("#ff5a4d"), z.x - (bw - 2) / 2 * (1 - f), hy, (bw - 2) * f, 4, 2051);
            }
            // Atışlar
            foreach (var sh in def.shots)
            {
                float dx = sh.x2 - sh.x1, dy = sh.y2 - sh.y1, len = Mathf.Sqrt(dx * dx + dy * dy);
                var c = Hex(sh.color); c.a = Mathf.Min(1, sh.t * 8);
                Draw(square, c, (sh.x1 + sh.x2) / 2, (sh.y1 + sh.y2) / 2, len, 2.6f, 2060, -Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
            }
            if (def.flash > 0) Draw(square, new Color(1, .25f, .15f, def.flash * .3f), CX, GateY - 10, 200, 70, 2040);
        }

        bool DefenseTap(float lx, float ly)
        {
            if (def == null) return false;
            if (def.state != DefenseSim.St.Run) return true;
            for (int i = 0; i < def.slotX.Length; i++)
                if (Mathf.Abs(lx - def.slotX[i]) < 34 && ly > def.slotY[i] - 70 && ly < def.slotY[i] + 22)
                {
                    if (i >= def.slots) { AddFloat(def.slotX[i], def.slotY[i] - 40, "Gözetleme Kulesi seviyesi yetersiz", Hex("#ff8a80")); SoundFx.Play("hit"); return true; }
                    defPick = defPick == i ? -1 : i; SoundFx.Play("board"); return true;
                }
            defPick = -1;
            return true;
        }

        void DefenseGui()
        {
            float w = Mathf.Min(440, VW - 12), x0 = VW / 2 - w / 2;
            GUI.Box(new Rect(x0, 6, w, 52), "", card);
            GUI.Label(new Rect(x0 + 12, 6, 120, 52), $"<b>Dalga {Mathf.Min(def.wave, def.T.waves)}/{def.T.waves}</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, fontSize = 16 });
            float gx = x0 + 136, gw = w - 260, f = Mathf.Clamp01(def.gate / def.gmax);
            GUI.Label(new Rect(gx, 8, gw, 20), "Kapı", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            GUI.DrawTexture(new Rect(gx, 30, gw, 14), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.05f, .07f, .1f, .9f), 0, 7);
            GUI.DrawTexture(new Rect(gx + 2, 32, Mathf.Max(6, (gw - 4) * f), 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, f < .35f ? Hex("#ff5a4d") : Hex("#4fc16a"), 0, 5);
            GUI.Label(new Rect(x0 + w - 120, 6, 108, 52), $"<color=#ffd23f><b>{Mathf.FloorToInt(def.gold)} altın</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontSize = 16 });

            if (def.banner > 0 && def.state == DefenseSim.St.Run)
            {
                var c = def.boss ? Hex("#ff6a4a") : Hex("#ffe45a"); c.a = Mathf.Min(1, def.banner);
                Outlined(new Vector2(VW / 2, 230), def.boss ? "BOSS DALGASI" : $"DALGA {def.wave}", c, new GUIStyle(center) { fontSize = 32 }, 400);
                if (def.wave == 1) Outlined(new Vector2(VW / 2, 270), "Boş yere dokun: kule kur", new Color(1, 1, 1, c.a), center, 400);
            }

            if (def.state == DefenseSim.St.Run)
            {
                if (defPick >= 0) DefensePickMenu(defPick);
                if (GUI.Button(new Rect(x0, 800 - 62, 110, 52), "Pes et", new GUIStyle(btn) { fontSize = 15 })) { def.gate = 0; SoundFx.Play("click"); }
            }
            else DefenseResult();
        }

        void DefensePickMenu(int i)
        {
            var g = ToGui(def.slotX[i], def.slotY[i] - 80);
            var q = def.TowerAt(i); float hs = 62;
            bool Hx(float cx, string glyph, string text, GUIStyle st, bool enabled)
            {
                GUI.enabled = enabled;
                bool hit = GUI.Button(new Rect(cx - hs / 2, g.y - hs / 2, hs, hs), glyph, st);
                GUI.enabled = true;
                Outlined(new Vector2(cx, g.y + hs / 2 + 8), text, enabled ? Color.white : new Color(1, 1, 1, .5f), tiny, 120);
                return hit && enabled;
            }
            if (q == null)
            {
                for (int k = 0; k < DefenseSim.Towers.Length; k++)
                {
                    var w = DefenseSim.Towers[k]; bool req = def.TowerLevel >= w.req;
                    string txt = req ? $"{w.name} {w.cost}" : $"Kule Sv.{w.req}";
                    var st = k == 0 ? hexOrange : k == 1 ? hexGreen : hexBlue;
                    if (Hx(g.x + (k - 1) * 74, k == 0 ? "➶" : k == 1 ? "●" : "❄", txt, st, req && def.gold >= w.cost)) { def.Build(i, k); defPick = -1; }
                }
            }
            else
            {
                int uc = DefenseSim.UpgradeCost(q);
                if (Hx(g.x - 40, "▲", q.lv >= 3 ? "Tam" : $"Yükselt {uc}", hexGreen, q.lv < 3 && def.gold >= uc)) { def.Upgrade(i); defPick = -1; }
                if (Hx(g.x + 40, "$", $"Sat {DefenseSim.SellValue(q)}", hexOrange, true)) { def.Sell(i); defPick = -1; }
            }
        }

        void DefenseResult()
        {
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .5f), 0, 0);
            float w = Mathf.Min(380, VW - 24), x = VW / 2 - w / 2, h = 330, y = 400 - h / 2;
            GUI.Box(new Rect(x, y, w, h), "", card);
            bool won = def.state == DefenseSim.St.Won;
            GUI.Label(new Rect(x, y + 14, w, 40), won ? "<color=#8be39d><b>Savunma başarılı!</b></color>" : "<color=#ff8a80><b>Kapı düştü</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 24 });
            GUI.Label(new Rect(x, y + 56, w, 28), $"{def.T.name} · Dalga {Mathf.Min(def.wave, def.T.waves)}/{def.T.waves} · {def.kills} zombi", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
            string rw = $"<color=#ffd23f>+{defRw.gold} altın</color>";
            if (defRw.iron > 0) rw += $"\n<color=#cfd8e0>+{defRw.iron} demir</color>";
            if (defRw.food > 0) rw += $"\n<color=#8be39d>+{defRw.food} yiyecek</color>";
            if (defRw.gems > 0) rw += $"\n<color=#9fe8ff>+{defRw.gems} elmas</color>";
            GUI.Label(new Rect(x, y + 92, w, 140), rw, new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });
            if (GUI.Button(new Rect(x + 20, y + h - 70, w - 40, 54), "Tamam", UiSkin.ButtonGold)) { def = null; defPick = -1; visW = 540; camX = CX; camY = CY + 30; SoundFx.Play("click"); }
        }

        // ---------- Zorluk seçimi (Popup.Defense) ----------
        void DefenseTierPopup(float x, float y, float w, float h, GUIStyle head)
        {
            GUI.Label(new Rect(x + 16, y + 10, w - 70, 36), "Üs Savunması", head);
            GUI.Label(new Rect(x + 16, y + 46, w - 32, 44), "Zombi dalgaları kapına geliyor. Kule kur, kapıyı koru, ödülü kap!", small);
            long rem = DefenseSim.ReadyIn(D);
            if (rem > 0)
            {
                GUI.Label(new Rect(x, y + 120, w, 40), $"<b>Yeni saldırıya: {Fmt(rem / 1000f)}</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 });
                if (GUI.Button(new Rect(x + 20, y + 180, w - 40, 54), "▶ Reklam izle: hemen hazır", UiSkin.ButtonGold))
                    Shop.WatchAd(AdPlacement.DefenseRefresh, () => { D.defenseReadyAt = 0; GameServices.Save.Save(); }, () => flow.ShowToast("Reklam hazır değil"));
                return;
            }
            int st = Progress.TotalStars(D);
            for (int i = 0; i < DefenseSim.Tiers.Length; i++)
            {
                var T = DefenseSim.Tiers[i]; bool ok = st >= T.req; float ry = y + 100 + i * 74;
                GUI.Box(new Rect(x + 16, ry, w - 32, 64), "", card);
                GUI.Label(new Rect(x + 30, ry + 4, w - 190, 30), $"<b>{T.name}</b>", label);
                GUI.Label(new Rect(x + 30, ry + 32, w - 190, 26), ok ? $"{T.waves} dalga · ödül ×{T.rw:0.#}" : $"★ {T.req} gerekli", small);
                GUI.enabled = ok;
                if (GUI.Button(new Rect(x + w - 146, ry + 8, 120, 48), "Başla", i == 0 ? btn : UiSkin.ButtonGold)) { SoundFx.Play("click"); StartDefense(i); }
                GUI.enabled = true;
            }
        }
    }
}
