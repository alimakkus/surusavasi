using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Modlar ekranı: kampanya dışındaki oyun stilleri. Her kart: görsel, açıklama, günlük haklar ve başlat düğmesi.</summary>
    public static class ModesScreen
    {
        static SaveData D => GameServices.Save.Data;
        static Wallet Wl => GameServices.Wallet;

        public static void Draw(GameFlow flow, float x0, float w)
        {
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), "<b>Oyun Modları</b>", UiSkin.Label);
            float y = 98;
            Tower(flow, x0, y, w); y += 262;
            Hunt(flow, x0, y, w);
        }

        static void Header(Rect r, string title, string sub, string col)
        {
            var rib = new Rect(r.x + 120, r.y + 10, r.width - 132, 34);
            GUI.Box(rib, "", UiSkin.Ribbon);
            GUI.Label(rib, $"<b><color={col}>{title}</color></b>", ModeUi.Center);
            GUI.Label(new Rect(r.x + 124, r.y + 46, r.width - 136, 54), sub, new GUIStyle(UiSkin.Small) { wordWrap = true, richText = true });
        }

        static void Locked(Rect r, int need)
        {
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 14);
            GUI.Label(r, $"<b>KİLİTLİ</b>\n<size=13>{need - 1}. bölümü geç</size>", ModeUi.CenterBig);
        }

        // ---------------- Kule Tırmanışı ----------------
        static void Tower(GameFlow flow, float x0, float y, float w)
        {
            var d = D; var r = new Rect(x0, y, w, 250);
            GUI.Box(r, "", UiSkin.CardDark);
            // Görsel: kule silüeti + katlar
            var art = new Rect(r.x + 12, r.y + 12, 100, 150);
            GUI.DrawTexture(art, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#3b2f6b"), 0, 10);
            float t = Time.time;
            for (int i = 0; i < 6; i++)
            {
                float fw = 64 - i * 6, fy = art.yMax - 18 - i * 21;
                bool lit = i < Mathf.Min(6, d.towerBest / 3 + 1);
                GUI.DrawTexture(new Rect(art.center.x - fw / 2, fy, fw, 18), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, lit ? ModeUi.C("#8f7bd9") : ModeUi.C("#55497f"), 0, 3);
                GUI.DrawTexture(new Rect(art.center.x - 4, fy + 5, 8, 9), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, lit ? new Color(1, .85f, .4f, .7f + .3f * Mathf.Sin(t * 3 + i)) : new Color(.15f, .12f, .25f), 0, 2);
            }
            GUI.DrawTexture(new Rect(art.center.x - 3, art.y + 4, 3, 16), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, Color.white, 0, 0);
            GUI.DrawTexture(new Rect(art.center.x, art.y + 4 + Mathf.Sin(t * 4), 16, 9), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#ff5a4d"), 0, 2);
            GUI.Label(new Rect(art.x, art.yMax + 4, art.width, 40), $"<size=12>Rekor</size>\n<b>Kat {d.towerBest}</b>", ModeUi.Small);

            Header(r, "KULE TIRMANIŞI", "Tek can, sonsuz kat. Her kattan sonra 3 güçlendirmeden birini seç, gücünü katla. 5, 10, 20. katlarda ekipman sandığı!", "#c9b8ff");
            if (!Modes.TowerOpen(d)) { Locked(r, Modes.TowerUnlock); return; }

            float bx = r.x + 124, bw = r.width - 136, by = r.y + 104;
            if (Modes.TowerInRun(d))
            {
                GUI.Label(new Rect(bx, by, bw, 40), $"Devam eden koşu: <b>Kat {d.towerFloor}</b>  ·  {d.towerBoons.Count} güçlendirme", new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true });
                // Etkin güçlendirmeler: renkli noktalar
                for (int i = 0; i < d.towerBoons.Count && i < 16; i++)
                {
                    var b = Modes.FindBoon(d.towerBoons[i]); if (b == null) continue;
                    GUI.DrawTexture(new Rect(bx + i * 14, by + 40, 10, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(b.color), 0, 5);
                }
                if (GUI.Button(new Rect(bx, by + 60, bw, 54), $"DEVAM ET  <size=13>Kat {d.towerFloor}</size>", UiSkin.ButtonGreen))
                { SoundFx.Play("click"); flow.PlayMode(new TowerMode(d.towerFloor)); }
                if (GUI.Button(new Rect(bx, by + 118, bw, 26), "<size=12>Koşuyu bırak</size>", UiSkin.ButtonGray)) { Modes.TowerEnd(d); GameServices.Save.Save(); }
                return;
            }
            int free = Modes.TowerFreeLeft(d);
            GUI.Label(new Rect(bx, by, bw, 24), $"Bugünkü ücretsiz koşu: <b>{free}/{Modes.TowerFreePerDay}</b>", new GUIStyle(UiSkin.Small) { richText = true });
            if (free > 0)
            {
                if (GUI.Button(new Rect(bx, by + 30, bw, 60), "TIRMANIŞA BAŞLA", UiSkin.ButtonGold))
                { SoundFx.Play("click"); Modes.TowerStart(d, "free"); GameServices.Save.Save(); flow.PlayMode(new TowerMode(1)); }
            }
            else
            {
                float hw = (bw - 8) / 2;
                GUI.enabled = Modes.TowerAdLeft(d) > 0;
                if (GUI.Button(new Rect(bx, by + 30, hw, 60), "▶ Reklamla\nyeni koşu", new GUIStyle(UiSkin.ButtonGold) { fontSize = 13 }))
                    Shop.WatchAd(AdPlacement.PowerBoost, () => { Modes.TowerStart(d, "ad"); GameServices.Save.Save(); flow.PlayMode(new TowerMode(1)); });
                GUI.enabled = true;
                if (GUI.Button(new Rect(bx + hw + 8, by + 30, hw, 60), $"{Modes.TowerGemCost} elmas\nyeni koşu", new GUIStyle(UiSkin.Button) { fontSize = 13 }))
                {
                    if (Wl.Spend(Currency.Diamonds, Modes.TowerGemCost, "tower_run")) { Modes.TowerStart(d, "gem"); GameServices.Save.Save(); flow.PlayMode(new TowerMode(1)); }
                    else flow.Toast("Yeterli elmas yok");
                }
            }
        }

        // ---------------- Patron Avı ----------------
        static void Hunt(GameFlow flow, float x0, float y, float w)
        {
            var d = D; var r = new Rect(x0, y, w, 250);
            GUI.Box(r, "", UiSkin.CardDark);
            int bi = Modes.HuntBoss(d);
            var art = new Rect(r.x + 12, r.y + 12, 100, 150);
            GUI.DrawTexture(art, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#5a1f24"), 0, 10);
            var sp = Art.Boss(bi, (int)(Time.time * 2.5f));
            if (sp != null) DialogBox.DrawSprite(new Rect(art.x - 6, art.y + 6, art.width + 12, art.height - 4), sp);
            GUI.Label(new Rect(art.x - 4, art.yMax + 4, art.width + 8, 40), $"<size=12>Bugünün patronu</size>\n<b>{BattleData.BossNames[bi]}</b>", ModeUi.Small);

            Header(r, "PATRON AVI", $"Koşu yok, ordun hazır! {Modes.HuntTime:0} saniyede patrona en çok hasarı ver. Canı 3 kat. Kademe ödülleri her gün yenilenir.", "#ff8a80");
            if (!Modes.HuntOpen(d)) { Locked(r, Modes.HuntUnlock); return; }

            float bx = r.x + 124, bw = r.width - 136, by = r.y + 104;
            GUI.Label(new Rect(bx, by - 2, bw, 22), $"Bugünkü en iyi: <b>%{d.huntBest / 10f:0}</b>   Rekor: %{d.huntBestEver / 10f:0}", new GUIStyle(UiSkin.Small) { richText = true });
            HuntMode.TierBar(new Rect(bx + 10, by + 18, bw - 20, 34), d.huntBest / 1000f, d.huntTiers);
            int left = Modes.HuntTriesLeft(d);
            if (left > 0)
            {
                if (GUI.Button(new Rect(bx, by + 60, bw, 60), $"AVA ÇIK  <size=13>({left} hak)</size>", UiSkin.ButtonRed))
                { SoundFx.Play("click"); Modes.HuntUseTry(d); GameServices.Save.Save(); flow.PlayMode(new HuntMode()); }
            }
            else
            {
                GUI.enabled = Modes.HuntAdLeft(d) > 0;
                if (GUI.Button(new Rect(bx, by + 60, bw, 60), GUI.enabled ? "▶ Reklam izle: +1 deneme" : "Haklar bitti, yarın yeni patron", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14 }))
                    Shop.WatchAd(AdPlacement.SecondChance, () => { Modes.HuntAddAdTry(d); GameServices.Save.Save(); });
                GUI.enabled = true;
            }
        }
    }
}
