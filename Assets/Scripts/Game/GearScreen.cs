using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Ekipman ekranı: kuşanılan 3 yuva, çanta, seçili eşyanın yükseltme/parçalama işlemleri ve elmasla sandık.</summary>
    public static class GearScreen
    {
        static int selId;
        static Vector2 scroll;
        static SaveData D => GameServices.Save.Data;
        static Wallet Wl => GameServices.Wallet;
        static Color C(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var label = UiSkin.Label; var small = UiSkin.Small; var btn = UiSkin.Button; var card = UiSkin.Card;
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", btn)) { flow.Show(GameScreen.Heroes); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), $"<b>Ekipman</b>   <color=#c07bff>Parça {D.shards}</color>", label);

            // Kuşanılan yuvalar
            float sw = (w - 16) / 3, y = 96;
            for (int s = 0; s < 3; s++)
            {
                var g = Gear.Equipped(D, s); var r = new Rect(x0 + s * (sw + 8), y, sw, 110);
                GUI.Box(r, "", card);
                if (g != null) GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, 5), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, C(Gear.RarityColors[g.rarity]), 0, 2);
                GUI.Label(new Rect(r.x + 8, r.y + 10, r.width - 16, 22), $"<b>{Gear.SlotNames[s]}</b>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter });
                GUI.Label(new Rect(r.x + 6, r.y + 32, r.width - 12, 46), g != null ? $"<color={Gear.RarityColors[g.rarity]}><b>{Gear.Name(g)}</b></color>\nSv. {g.lvl}" : "<color=#8b97a0>Boş</color>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
                if (g != null && GUI.Button(r, "", GUIStyle.none)) selId = g.id;
                if (g != null) GUI.Label(new Rect(r.x + 4, r.y + 78, r.width - 8, 30), Gear.Describe(g), new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 10 });
            }
            y += 120;

            // Seçili eşya
            var sel = Gear.Find(D, selId);
            GUI.Box(new Rect(x0, y, w, 132), "", card);
            if (sel == null) GUI.Label(new Rect(x0 + 14, y + 10, w - 28, 110), "Bir eşyaya dokun. Eşyalar savaş ve savunma zaferlerinden düşer; patron bölümleri kesin eşya verir.", small);
            else
            {
                bool eq = Gear.IsEquipped(D, sel);
                GUI.Label(new Rect(x0 + 14, y + 6, w - 28, 26), $"<color={Gear.RarityColors[sel.rarity]}><b>{Gear.Name(sel)}</b></color>  {Gear.RarityNames[sel.rarity]} {Gear.SlotNames[sel.slot]} · Sv. {sel.lvl}/{Gear.MaxLevel}{(eq ? "  <color=#8be39d>(kuşanılı)</color>" : "")}", label);
                GUI.Label(new Rect(x0 + 14, y + 32, w - 28, 24), Gear.Describe(sel), small);
                float bw = (w - 40) / 3, by = y + 64;
                GUI.enabled = !eq;
                if (GUI.Button(new Rect(x0 + 12, by, bw, 54), "Kuşan", btn)) { D.equipped[sel.slot] = sel.id; GameServices.Save.Save(); SoundFx.Play("power"); }
                int sh = Gear.UpgradeShards(sel), gg = Gear.UpgradeGold(sel);
                GUI.enabled = sel.lvl < Gear.MaxLevel && D.shards >= sh && Wl.CanAfford(Currency.Gold, gg);
                if (GUI.Button(new Rect(x0 + 20 + bw, by, bw, 54), sel.lvl < Gear.MaxLevel ? $"Yükselt\n<size=11>{sh} parça + {gg} altın</size>" : "En üst", UiSkin.ButtonGold))
                {
                    if (Wl.Spend(Currency.Gold, gg, "gear_up")) { D.shards -= sh; sel.lvl++; GameServices.Save.Save(); SoundFx.Play("power"); flow.ShowToast($"{Gear.Name(sel)} Sv. {sel.lvl}!"); }
                }
                GUI.enabled = !eq;
                if (GUI.Button(new Rect(x0 + 28 + bw * 2, by, bw, 54), $"Parçala\n<size=11>+{Gear.DismantleShards(sel)} parça</size>", btn)) { Gear.Dismantle(D, sel); selId = 0; GameServices.Save.Save(); SoundFx.Play("board"); }
                GUI.enabled = true;
            }
            y += 142;

            // Sandık
            if (GUI.Button(new Rect(x0, y, w, 46), $"Ekipman sandığı aç: {Gear.ChestGems} elmas  (Nadir ve üstü şansı yüksek)", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14 }))
            {
                if (D.gear.Count >= Gear.BagSize && D.gear.TrueForAll(x => Gear.IsEquipped(D, x))) flow.ShowToast("Çanta dolu");
                else if (Wl.Spend(Currency.Diamonds, Gear.ChestGems, "gear_chest"))
                {
                    var g = Gear.Roll(D, new System.Random(), 2); Gear.AddToBag(D, g); selId = g.id; GameServices.Save.Save(); SoundFx.Play("power");
                    flow.ShowToast($"{Gear.RarityNames[g.rarity]}: {Gear.Name(g)}!");
                    GameServices.Analytics?.LogEvent("gear_chest", new System.Collections.Generic.Dictionary<string, object> { { "rarity", g.rarity } });
                }
                else flow.ShowToast("Yeterli elmas yok");
            }
            y += 56;

            // Çanta
            GUI.Label(new Rect(x0, y, w, 24), $"<b>Çanta</b>  {D.gear.Count}/{Gear.BagSize}", small);
            y += 26;
            int cols = 4; float cw = (w - (cols - 1) * 6) / cols, ch = 58;
            int rows = (D.gear.Count + cols - 1) / cols;
            scroll = GUI.BeginScrollView(new Rect(x0, y, w + 16, 800 - y - 8), scroll, new Rect(0, 0, w, Mathf.Max(1, rows * (ch + 6))));
            for (int i = 0; i < D.gear.Count; i++)
            {
                var g = D.gear[i]; var r = new Rect((i % cols) * (cw + 6), (i / cols) * (ch + 6), cw, ch);
                GUI.Box(r, "", card);
                GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, 5, r.height - 6), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, C(Gear.RarityColors[g.rarity]), 0, 2);
                if (g.id == selId) GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .9f, .3f, .18f), 0, 8);
                GUI.Label(new Rect(r.x + 10, r.y + 2, r.width - 12, r.height - 4), $"<color={Gear.RarityColors[g.rarity]}><b>{Gear.SlotNames[g.slot]}</b></color>\n<size=11>Sv. {g.lvl}{(Gear.IsEquipped(D, g) ? " ✓" : "")}</size>", new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 13 });
                if (GUI.Button(r, "", GUIStyle.none)) { selId = g.id; SoundFx.Play("click"); }
            }
            GUI.EndScrollView();
        }
    }
}
