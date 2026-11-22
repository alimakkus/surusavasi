using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Modlar ekranı: kampanya dışındaki oyun stilleri + haftalık lig girişi.
    /// Her kart: görsel, açıklama, günlük haklar ve başlat düğmesi.</summary>
    public static class ModesScreen
    {
        static SaveData D => GameServices.Save.Data;
        static Wallet Wl => GameServices.Wallet;
        const float CH = 194, ART_W = 96, ART_H = 128;

        public static void Draw(GameFlow flow, float x0, float w)
        {
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 250, 40), "<b>Oyun Modları</b>", UiSkin.Label);
            // Lig düğmesi: lig rengi + sonuç bekliyorsa kırmızı nokta
            var d = D; League.Check(d);
            var lr = new Rect(x0 + w - 146, 46, 146, 44);
            if (GUI.Button(lr, $"Lig: {League.TierNames[d.leagueTier]}\n<size=11>{d.leaguePts} puan</size>", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14, richText = true }))
            { SoundFx.Play("click"); flow.Show(GameScreen.League); return; }
            if (d.leagueResPending) Dot(lr);
            float y = 96;
            Tower(flow, x0, y, w); y += CH + 8;
            Hunt(flow, x0, y, w); y += CH + 8;
            Survival(flow, x0, y, w);
        }

        static void Dot(Rect r)
        {
            float pu = 1 + .15f * Mathf.Sin(Time.time * 6);
            GUI.DrawTexture(new Rect(r.xMax - 10 - 6 * pu, r.y - 4 - 6 * pu, 14 * pu, 14 * pu), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.88f, .27f, .23f), 0, 7 * pu);
        }

        static void Header(Rect r, string title, string sub)
        {
            var rib = new Rect(r.x + ART_W + 20, r.y + 8, r.width - ART_W - 30, 32);
            GUI.Box(rib, "", UiSkin.Ribbon);
            GUI.Label(rib, $"<b>{title}</b>", ModeUi.Center);
            GUI.Label(new Rect(rib.x + 2, r.y + 42, rib.width - 4, 52), sub, new GUIStyle(UiSkin.Small) { wordWrap = true, richText = true, fontSize = 12 });
        }

        static void Locked(Rect r, int need)
        {
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 14);
            GUI.Label(r, $"<b>KİLİTLİ</b>\n<size=13>{need - 1}. bölümü geç</size>", ModeUi.CenterBig);
        }

        static Rect ArtBox(Rect r, string bg)
        {
            var art = new Rect(r.x + 10, r.y + 10, ART_W, ART_H);
            GUI.DrawTexture(art, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(bg), 0, 10);
            return art;
        }

        static void Caption(Rect art, string top, string main) =>
            GUI.Label(new Rect(art.x - 6, art.yMax + 2, art.width + 12, 44), $"<size=11>{top}</size>\n<b>{main}</b>", ModeUi.Small);

        static GUIStyle info;
        static GUIStyle Info => info ??= new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true, fontSize = 12 };

        // ---------------- Kule Tırmanışı ----------------
        static void Tower(GameFlow flow, float x0, float y, float w)
        {
            var d = D; var r = new Rect(x0, y, w, CH);
            GUI.Box(r, "", UiSkin.CardDark);
            var art = ArtBox(r, "#3b2f6b");
            float t = Time.time;
            for (int i = 0; i < 5; i++)
            {
                float fw = 62 - i * 7, fy = art.yMax - 20 - i * 21;
                bool lit = i < Mathf.Min(5, d.towerBest / 3 + 1);
                GUI.DrawTexture(new Rect(art.center.x - fw / 2, fy, fw, 18), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, lit ? ModeUi.C("#8f7bd9") : ModeUi.C("#55497f"), 0, 3);
                GUI.DrawTexture(new Rect(art.center.x - 4, fy + 5, 8, 9), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, lit ? new Color(1, .85f, .4f, .7f + .3f * Mathf.Sin(t * 3 + i)) : new Color(.15f, .12f, .25f), 0, 2);
            }
            GUI.DrawTexture(new Rect(art.center.x - 2, art.y + 6, 3, 14), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Color.white, 0, 0);
            GUI.DrawTexture(new Rect(art.center.x + 1, art.y + 6 + Mathf.Sin(t * 4), 15, 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#ff5a4d"), 0, 2);
            Caption(art, "Rekor", $"Kat {d.towerBest}");

            Header(r, "KULE TIRMANIŞI", "Tek can, sonsuz kat. Her kattan sonra 3 güçlendirmeden birini seç. 5, 10, 20. katlarda ekipman sandığı!");
            if (!Modes.TowerOpen(d)) { Locked(r, Modes.TowerUnlock); return; }

            float bx = r.x + ART_W + 20, bw = r.width - ART_W - 30, by = r.y + 96;
            if (Modes.TowerInRun(d))
            {
                GUI.Label(new Rect(bx, by, bw, 22), $"Devam eden koşu: <b>Kat {d.towerFloor}</b> · {d.towerBoons.Count} güçlendirme", Info);
                if (GUI.Button(new Rect(bx, by + 26, bw - 72, 58), $"DEVAM ET  <size=13>Kat {d.towerFloor}</size>", UiSkin.ButtonGreen))
                { SoundFx.Play("click"); flow.PlayMode(new TowerMode(d.towerFloor)); }
                if (GUI.Button(new Rect(bx + bw - 66, by + 26, 66, 58), "<size=12>Bırak</size>", UiSkin.ButtonGray)) { Modes.TowerEnd(d); GameServices.Save.Save(); }
                return;
            }
            int free = Modes.TowerFreeLeft(d);
            GUI.Label(new Rect(bx, by, bw, 22), $"Bugünkü ücretsiz koşu: <b>{free}/{Modes.TowerFreePerDay}</b>", Info);
            if (free > 0)
            {
                if (GUI.Button(new Rect(bx, by + 26, bw, 58), "TIRMANIŞA BAŞLA", UiSkin.ButtonGold))
                { SoundFx.Play("click"); Modes.TowerStart(d, "free"); GameServices.Save.Save(); flow.PlayMode(new TowerMode(1)); }
            }
            else
            {
                float hw = (bw - 8) / 2;
                GUI.enabled = Modes.TowerAdLeft(d) > 0;
                if (GUI.Button(new Rect(bx, by + 26, hw, 58), "▶ Reklamla\nyeni koşu", new GUIStyle(UiSkin.ButtonGold) { fontSize = 13 }))
                    Shop.WatchAd(AdPlacement.PowerBoost, () => { Modes.TowerStart(d, "ad"); GameServices.Save.Save(); flow.PlayMode(new TowerMode(1)); });
                GUI.enabled = true;
                if (GUI.Button(new Rect(bx + hw + 8, by + 26, hw, 58), $"{Modes.TowerGemCost} elmas\nyeni koşu", new GUIStyle(UiSkin.Button) { fontSize = 13 }))
                {
                    if (Wl.Spend(Currency.Diamonds, Modes.TowerGemCost, "tower_run")) { Modes.TowerStart(d, "gem"); GameServices.Save.Save(); flow.PlayMode(new TowerMode(1)); }
                    else flow.Toast("Yeterli elmas yok");
                }
            }
        }

        // ---------------- Patron Avı ----------------
        static void Hunt(GameFlow flow, float x0, float y, float w)
        {
            var d = D; var r = new Rect(x0, y, w, CH);
            GUI.Box(r, "", UiSkin.CardDark);
            int bi = Modes.HuntBoss(d);
            var art = ArtBox(r, "#5a1f24");
            var sp = Art.Boss(bi, (int)(Time.time * 2.5f));
            if (sp != null) DialogBox.DrawSprite(new Rect(art.x - 36, art.y - 18, art.width + 72, art.height + 14), sp);
            Caption(art, "Bugünün patronu", BattleData.BossNames[bi]);

            Header(r, "PATRON AVI", $"Koşu yok, ordun hazır! {Modes.HuntTime:0} saniyede patrona en çok hasarı ver. Canı 10 kat, kademe ödülleri her gün yenilenir.");
            if (!Modes.HuntOpen(d)) { Locked(r, Modes.HuntUnlock); return; }

            float bx = r.x + ART_W + 20, bw = r.width - ART_W - 30, by = r.y + 92;
            HuntMode.TierBar(new Rect(bx + 10, by - 4, bw - 20, 34), d.huntBest / 1000f, d.huntTiers);
            int left = Modes.HuntTriesLeft(d);
            if (left > 0)
            {
                if (GUI.Button(new Rect(bx, by + 36, bw, 54), $"AVA ÇIK  <size=13>({left} hak · en iyi %{d.huntBest / 10f:0})</size>", UiSkin.ButtonRed))
                { SoundFx.Play("click"); Modes.HuntUseTry(d); GameServices.Save.Save(); flow.PlayMode(new HuntMode()); }
            }
            else
            {
                GUI.enabled = Modes.HuntAdLeft(d) > 0;
                if (GUI.Button(new Rect(bx, by + 36, bw, 54), GUI.enabled ? "▶ Reklam izle: +1 deneme" : "Haklar bitti, yarın yeni patron", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14 }))
                    Shop.WatchAd(AdPlacement.SecondChance, () => { Modes.HuntAddAdTry(d); GameServices.Save.Save(); });
                GUI.enabled = true;
            }
        }

        // ---------------- Hayatta Kalma ----------------
        static void Survival(GameFlow flow, float x0, float y, float w)
        {
            var d = D; var r = new Rect(x0, y, w, CH);
            GUI.Box(r, "", UiSkin.CardDark);
            var art = ArtBox(r, "#24452e");
            var tw = Art.WallTower(4);
            if (tw != null) DialogBox.DrawSprite(new Rect(art.x + 8, art.y + 6, art.width - 16, art.height - 10), tw);
            // Kulenin önünde yürüyen zombiler
            var z = Art.Enemy(0, "walker", (int)(Time.time * 4));
            if (z != null)
                for (int i = 0; i < 2; i++)
                {
                    float zx = art.x + Mathf.Repeat(Time.time * 14 + i * 48, art.width - 20);
                    DialogBox.DrawSprite(new Rect(zx - 8, art.yMax - 46, 36, 44), z);
                }
            Caption(art, "Rekor", $"Dalga {d.survBest}");

            Header(r, "HAYATTA KALMA", "Dalga dalga zombi! Her öldürme para verir. Dalga arasında asker, hasar, şifa ve yol kenarına savunma kulesi al.");
            if (!Modes.SurvOpen(d)) { Locked(r, Modes.SurvUnlock); return; }

            float bx = r.x + ART_W + 20, bw = r.width - ART_W - 30, by = r.y + 96;
            int free = Modes.SurvFreeLeft(d);
            GUI.Label(new Rect(bx, by, bw, 22), $"Bugünkü ücretsiz koşu: <b>{free}/{Modes.SurvFreePerDay}</b>   Lig: +{League.PtsSurvWave}/dalga", Info);
            if (free > 0)
            {
                if (GUI.Button(new Rect(bx, by + 26, bw, 58), "SAVUNMAYA GEÇ", UiSkin.ButtonGreen))
                { SoundFx.Play("click"); Modes.SurvStart(d, "free"); GameServices.Save.Save(); flow.PlayMode(new SurvivalMode()); }
            }
            else
            {
                GUI.enabled = Modes.SurvAdLeft(d) > 0;
                if (GUI.Button(new Rect(bx, by + 26, bw, 58), GUI.enabled ? "▶ Reklam izle: yeni koşu" : "Haklar bitti, yarın tekrar", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14 }))
                    Shop.WatchAd(AdPlacement.PowerBoost, () => { Modes.SurvStart(d, "ad"); GameServices.Save.Save(); flow.PlayMode(new SurvivalMode()); });
                GUI.enabled = true;
            }
        }
    }
}
