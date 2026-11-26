using System;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Sezon bileti ekranı: sezon kartı (kademe, SP çubuğu, kalan süre), Sezon Bileti satın alma şeridi,
    /// 30 kademelik iki yollu ödül listesi ve haftalık sezon görevleri.</summary>
    public static class SeasonScreen
    {
        static SaveData D => GameServices.Save.Data;
        static Vector2 scroll; static bool tasks; static bool focus = true;
        static string popText; static float popT;
        static readonly System.Random rnd = new System.Random();

        public static void Focus() { focus = true; tasks = false; }
        public static void ShowTasks(bool on) { tasks = on; }

        static void Grant(string kind, int n)
        {
            var w = GameServices.Wallet; if (w == null || n <= 0) return;
            var c = kind == "gold" ? Currency.Gold : kind == "iron" ? Currency.Iron : kind == "food" ? Currency.Food : Currency.Diamonds;
            w.Add(c, n, c == Currency.Diamonds ? "season:" + D.seasonId : "season");
        }

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var d = D; Season.Ensure(d);
            if (!d.seasonSeen) { d.seasonSeen = true; GameServices.Save.Save(); }
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 220, 40), "<b>Sezon Bileti</b>", UiSkin.Label);
            if (GUI.Button(new Rect(x0 + w - 110, 48, 110, 40), tasks ? "Ödüller" : "Görevler", UiSkin.Button)) { tasks = !tasks; SoundFx.Play("click"); }
            for (int i = 0; i < 3 && !tasks; i++) if (Season.WeeklyReady(d, i)) { Dot(new Rect(x0 + w - 14, 42, 14, 14)); break; }

            TopCard(new Rect(x0, 96, w, 118), d);
            float y = 222;
            y = PassStrip(flow, new Rect(x0, y, w, 64), d);

            var area = new Rect(x0, y, w, 800 - y - 8);
            if (tasks) Tasks(area, d); else Track(flow, area, d);

            if (popText != null)
            {
                popT += Time.unscaledDeltaTime;
                float a = Mathf.Clamp01(3 - popT);
                var r = new Rect(x0 + 20, 330, w - 40, 90);
                GUI.color = new Color(1, 1, 1, a);
                GUI.Box(r, "", UiSkin.CardGold);
                GUI.Label(r, $"<b>Ödül alındı!</b>\n{popText}", new GUIStyle(ModeUi.Center) { fontSize = 16 });
                GUI.color = Color.white;
                if (popT > 3) popText = null;
            }
        }

        static void Dot(Rect r)
        {
            float pu = 1 + .15f * Mathf.Sin(Time.time * 6);
            GUI.DrawTexture(new Rect(r.center.x - 7 * pu, r.center.y - 7 * pu, 14 * pu, 14 * pu), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.88f, .27f, .23f), 0, 7 * pu);
        }

        static void TopCard(Rect r, SaveData d)
        {
            var sc = ModeUi.C(Season.Color(d.seasonId));
            GUI.Box(r, "", UiSkin.CardGold);
            // Kademe rozeti
            int tier = Season.Tier(d);
            var b = new Rect(r.x + 12, r.y + 14, 88, 88);
            float pu = 1 + .03f * Mathf.Sin(Time.time * 3);
            var bb = new Rect(b.center.x - 44 * pu, b.center.y - 44 * pu, 88 * pu, 88 * pu);
            GUI.DrawTexture(bb, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, sc * new Color(.4f, .4f, .4f, 1), 0, 44 * pu);
            var bi = new Rect(bb.x + 6, bb.y + 6, bb.width - 12, bb.height - 12);
            GUI.DrawTexture(bi, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, sc, 0, bi.width / 2);
            GUI.Label(bi, $"<size=11>KADEME</size>\n<b><size=30>{tier}</size></b>", new GUIStyle(ModeUi.Center) { normal = { textColor = new Color(.08f, .1f, .16f) } });

            float tx = r.x + 112, tw = r.width - 124;
            var left = Season.Left;
            GUI.Label(new Rect(tx, r.y + 8, tw, 28), $"<b><color={Season.Color(d.seasonId)}>Sezon {d.seasonId}: {Season.Name(d.seasonId)}</color></b>", new GUIStyle(UiSkin.Big) { richText = true, fontSize = 20 });
            GUI.Label(new Rect(tx, r.y + 36, tw, 20), $"Bitişe {(int)left.TotalDays}g {left.Hours}sa  ·  bugün {d.seasonDayPts}/{Season.DailyCap} SP", new GUIStyle(UiSkin.Small) { richText = true, fontSize = 13 });
            bool past = d.seasonPts >= Season.Tiers * Season.TierPts;
            int prog = Season.TierProgress(d), need = past ? Season.BonusPts : Season.TierPts;
            var bar = new Rect(tx, r.y + 62, tw, 18);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 9);
            GUI.DrawTexture(new Rect(bar.x + 2, bar.y + 2, Mathf.Max(14, (bar.width - 4) * prog / (float)need), bar.height - 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, sc, 0, 7);
            GUI.Label(bar, $"<b>{prog}/{need} SP</b>", new GUIStyle(ModeUi.Small) { fontSize = 12 });
            string next = past ? "Sonraki: bonus sandık" : $"Sonraki kademe: {tier + 1}";
            GUI.Label(new Rect(tx, r.y + 84, tw, 26), $"<size=12>{next}</size>" + (Achievements.Title(d) is string tt ? $"   <size=11><color=#ffd23f>« {tt} »</color></size>" : ""), new GUIStyle(UiSkin.Small) { richText = true });
            string key = "season" + d.seasonId;
            if (d.seasonTitles.Contains(key) && d.title != key)
                if (GUI.Button(new Rect(r.xMax - 108, r.y + 82, 96, 30), "Unvan yap", new GUIStyle(UiSkin.ButtonGreen) { fontSize = 12 })) { d.title = key; GameServices.Save.Save(); SoundFx.Play("click"); }
        }

        static float PassStrip(GameFlow flow, Rect r, SaveData d)
        {
            if (d.seasonPremium)
            {
                GUI.Label(new Rect(r.x, r.y, r.width, 26), "<color=#ffd23f><b>♛ Sezon Bileti etkin</b></color>  <size=12>· iki yolun ödülleri de senin</size>", new GUIStyle(UiSkin.Small) { richText = true, fontSize = 14 });
                return r.y + 30;
            }
            float pu = .5f + .5f * Mathf.Sin(Time.time * 3);
            GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f, .2f + .2f * pu), 0, 12);
            GUI.Box(r, "", UiSkin.Card);
            int waiting = 0; for (int t = 1; t <= Season.Tier(d); t++) waiting++;
            GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 170, r.height - 8),
                $"<b><color=#ffd23f>♛ SEZON BİLETİ</color></b>\n<size=12>Görünüm, epik ekipman, parça, 350+ elmas" + (waiting > 0 ? $" · <color=#8be39d>{waiting} ödül hemen açılır</color>" : "") + "</size>",
                new GUIStyle(UiSkin.Label) { richText = true, wordWrap = true, fontSize = 15 });
            var store = GameServices.Store; var def = Catalog.Find(Catalog.SeasonPass);
            bool ready = store != null && store.IsReady;
            string price = def != null && !string.IsNullOrEmpty(def.localizedPrice) ? def.localizedPrice : "—";
            GUI.enabled = ready;
            if (GUI.Button(new Rect(r.xMax - 150, r.y + 8, 140, r.height - 16), ready ? $"Aç\n<size=12>{price}</size>" : "Hazır değil", new GUIStyle(UiSkin.ButtonGold) { richText = true, fontSize = 16 }))
                Shop.Buy(Catalog.SeasonPass, ok =>
                {
                    if (ok) { SoundFx.Play("promote"); flow.ShowToast("Sezon Bileti açıldı! Ücretli yolun ödülleri hazır."); }
                    else flow.ShowToast("Satın alma tamamlanmadı");
                });
            GUI.enabled = true;
            return r.yMax + 8;
        }

        // ---------------- Ödül yolu ----------------
        static void Track(GameFlow flow, Rect area, SaveData d)
        {
            int tier = Season.Tier(d);
            float head = 34;
            // Üst sıra: tümünü al / kademe atla
            int cl = 0; for (int t = 1; t <= tier; t++) { if (Season.CanFree(d, t)) cl++; if (Season.CanPrem(d, t)) cl++; }
            float bx = area.x;
            if (cl > 0 && GUI.Button(new Rect(bx, area.y, 160, 30), $"Tümünü al ({cl})", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14 }))
            {
                string all = ""; int n = 0;
                for (int t = 1; t <= tier; t++)
                {
                    var a = Season.ClaimFree(d, t, Grant, rnd); if (a != null) { n++; if (n <= 4) all += a + "\n"; }
                    var p = Season.ClaimPrem(d, t, Grant, rnd); if (p != null) { n++; if (n <= 4) all += p + "\n"; }
                }
                if (n > 4) all += $"… ve {n - 4} ödül daha";
                GameServices.Save.Save(); SoundFx.Play("chest"); Pop(all.Trim());
            }
            if (cl > 0) bx += 168;
            if (Season.CanSkip(d))
            {
                var wl = GameServices.Wallet;
                if (GUI.Button(new Rect(bx, area.y, 190, 30), $"1 kademe atla: {Season.SkipGems} elmas", new GUIStyle(UiSkin.Button) { fontSize = 13 }))
                {
                    if (wl.Spend(Currency.Diamonds, Season.SkipGems, "season_skip")) { Season.Skip(d); GameServices.Save.Save(); SoundFx.Play("levelup"); }
                    else flow.ShowToast("Yeterli elmas yok");
                }
            }
            if (cl == 0 && !Season.CanSkip(d))
                GUI.Label(new Rect(area.x, area.y, area.width, 30), "<size=13>Oynadıkça sezon puanı (SP) toplarsın · <color=#7d8696>Görevler</color> sekmesinde haftalık büyük ödüller</size>", new GUIStyle(UiSkin.Small) { richText = true });

            // Sütun başlıkları
            var hr = new Rect(area.x, area.y + head, area.width, 22);
            float colW = (hr.width - 18 - 70) / 2;
            GUI.Label(new Rect(hr.x + 70, hr.y, colW, 22), "<b>ÜCRETSİZ</b>", new GUIStyle(ModeUi.Small) { fontSize = 12 });
            GUI.Label(new Rect(hr.x + 70 + colW, hr.y, colW, 22), "<b><color=#ffd23f>♛ SEZON BİLETİ</color></b>", new GUIStyle(ModeUi.Small) { fontSize = 12 });

            float rowH = 74;
            var inner = new Rect(area.x, hr.yMax + 2, area.width, area.height - head - 24);
            int rows = Season.Tiers + 1;
            if (focus) { scroll.y = Mathf.Max(0, (Mathf.Min(tier, Season.Tiers) - 1) * rowH - 40); focus = false; }
            scroll = GUI.BeginScrollView(inner, scroll, new Rect(0, 0, inner.width - 18, rows * rowH));
            float rw = inner.width - 18;
            for (int t = 1; t <= Season.Tiers; t++)
            {
                float y = (t - 1) * rowH;
                if (y + rowH < scroll.y - 10 || y > scroll.y + inner.height + 10) continue;
                bool reached = tier >= t;
                var row = new Rect(0, y, rw, rowH - 6);
                GUI.DrawTexture(row, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, reached ? new Color(1, .82f, .25f, .10f) : new Color(1, 1, 1, .04f), 0, 10);
                // Kademe numarası ve yol çizgisi
                var sc = reached ? ModeUi.C(Season.Color(d.seasonId)) : new Color(.3f, .33f, .4f);
                GUI.DrawTexture(new Rect(31, y - 3, 6, rowH), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, sc * new Color(1, 1, 1, .6f), 0, 0);
                var nr = new Rect(12, y + 12, 44, 44);
                GUI.DrawTexture(nr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, sc, 0, 22);
                GUI.Label(nr, $"<b>{t}</b>", new GUIStyle(ModeUi.Center) { fontSize = 18, normal = { textColor = reached ? new Color(.08f, .1f, .16f) : Color.white } });
                Cell(new Rect(66, y + 4, colW - 8, rowH - 14), Season.Free(t), reached, Season.FreeClaimed(d, t), true, t == 30 || t % 5 == 0, d,
                    () => { var s = Season.ClaimFree(d, t, Grant, rnd); if (s != null) { GameServices.Save.Save(); SoundFx.Play(t % 5 == 0 ? "chest" : "pickup"); Pop(s); } });
                Cell(new Rect(66 + colW, y + 4, colW - 8, rowH - 14), Season.Premium(t), reached, Season.PremClaimed(d, t), d.seasonPremium, true, d,
                    () => { var s = Season.ClaimPrem(d, t, Grant, rnd); if (s != null) { GameServices.Save.Save(); SoundFx.Play("chest"); Pop(s); } });
            }
            // Bonus sandık satırı
            {
                float y = Season.Tiers * rowH; var row = new Rect(0, y, rw, rowH - 6);
                GUI.Box(row, "", UiSkin.CardDark);
                int earned = Season.BonusEarned(d), left = earned - d.seasonBonus;
                GUI.Label(new Rect(14, y + 4, rw - 170, rowH - 14), $"<b>Bonus sandıklar</b>\n<size=12>Her {Season.BonusPts} SP bir sandık · açılan {d.seasonBonus}</size>", new GUIStyle(UiSkin.Label) { richText = true, wordWrap = true, fontSize = 15 });
                GUI.enabled = left > 0;
                if (GUI.Button(new Rect(rw - 150, y + 10, 136, rowH - 26), left > 0 ? $"AÇ ({left})" : "Bekliyor", left > 0 ? UiSkin.ButtonGold : UiSkin.Button))
                { var s = Season.ClaimBonus(d, Grant); if (s != null) { GameServices.Save.Save(); SoundFx.Play("chest"); Pop(s); } }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        static void Cell(Rect r, Season.Reward rw, bool reached, bool claimed, bool unlocked, bool big, SaveData d, Action claim)
        {
            bool can = reached && unlocked && !claimed;
            Color bg = claimed ? new Color(.2f, .55f, .3f, .35f) : can ? new Color(1, .82f, .25f, .30f) : new Color(0, 0, 0, .35f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, bg, 0, 9);
            if (big) GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f, .8f), 0, 2);
            string txt = $"<size=20>{Season.RewardIcon(rw)}</size> <b>{Season.RewardText(rw, d)}</b>";
            string sub = claimed ? "<color=#8be39d>Alındı</color>" : can ? "<color=#ffe27a><b>AL</b></color>" : !unlocked ? "<color=#7d8696>Bilet gerekli</color>" : "<color=#7d8696>Kilitli</color>";
            GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 16, r.height - 22), txt, new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true, fontSize = 13, alignment = TextAnchor.MiddleLeft });
            GUI.Label(new Rect(r.x + 8, r.yMax - 22, r.width - 16, 20), $"<size=11>{sub}</size>", new GUIStyle(UiSkin.Small) { richText = true });
            if (can && GUI.Button(r, "", GUIStyle.none)) claim();
            if (can) Dot(new Rect(r.xMax - 12, r.y - 2, 14, 14));
        }

        static void Pop(string s) { if (string.IsNullOrEmpty(s)) return; popText = s; popT = 0; }

        // ---------------- Haftalık görevler ----------------
        static void Tasks(Rect area, SaveData d)
        {
            var wl = Season.WeekLeft();
            GUI.Label(new Rect(area.x, area.y, area.width, 26), $"<b>Hafta {Mathf.Max(0, d.seasonWeek) + 1}/4 sezon görevleri</b>  <size=12>· yenilenmesine {(int)wl.TotalDays}g {wl.Hours}sa</size>", new GUIStyle(UiSkin.Label) { richText = true });
            float y = area.y + 32;
            for (int i = 0; i < 3; i++)
            {
                var t = Season.WeeklyDef(d, i);
                bool done = (d.seasonWeekDone & (1 << i)) != 0, ready = Season.WeeklyReady(d, i);
                var r = new Rect(area.x, y, area.width, 84);
                GUI.Box(r, "", ready ? UiSkin.CardGold : UiSkin.Card);
                GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 170, 28), $"<b>{t.text}</b>", new GUIStyle(UiSkin.Label) { richText = true });
                int p = Mathf.Min(t.n, d.seasonWeekProg[i]);
                float pw = r.width - 184;
                GUI.DrawTexture(new Rect(r.x + 14, r.y + 42, pw, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .5f), 0, 5);
                GUI.DrawTexture(new Rect(r.x + 14, r.y + 42, Mathf.Max(10, pw * p / (float)t.n), 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, done || ready ? new Color(.55f, .89f, .62f) : new Color(1, .82f, .25f), 0, 5);
                GUI.Label(new Rect(r.x + 14, r.y + 56, r.width - 170, 24), $"<size=13>{p}/{t.n} · +{Season.WeeklyPts} SP</size>", new GUIStyle(UiSkin.Small) { richText = true });
                GUI.enabled = ready;
                if (GUI.Button(new Rect(r.xMax - 150, r.y + 16, 136, 52), done ? "Alındı" : ready ? $"+{Season.WeeklyPts} SP" : "Devam", ready ? UiSkin.ButtonGold : UiSkin.Button))
                { if (Season.ClaimWeekly(d, i)) { GameServices.Save.Save(); SoundFx.Play("power"); Pop($"+{Season.WeeklyPts} sezon puanı"); } }
                GUI.enabled = true;
                y += 94;
            }
            y += 6;
            var hr = new Rect(area.x, y, area.width, area.yMax - y);
            GUI.Box(hr, "", UiSkin.CardDark);
            GUI.Label(new Rect(hr.x + 14, hr.y + 8, hr.width - 28, hr.height - 16),
                "<b>Sezon puanı (SP) nasıl kazanılır?</b>\n" +
                "<size=13>• Savaş kazan: 10 SP  ·  3 yıldız: +3\n" +
                "• Patron/elit yen: 5  ·  Bina geliştir: 8  ·  Asker eğit: 3\n" +
                "• Günlük görev: 15  ·  Tüm görevler bonusu: 20  ·  Giriş: 15\n" +
                "• Kule katı: 3  ·  Av kademesi: 5  ·  Hayatta kalma dalgası: 1\n" +
                $"• Haftalık sezon görevi: {Season.WeeklyPts} (günlük sınıra sayılmaz)\n" +
                $"Günde en çok {Season.DailyCap} SP · her {Season.TierPts} SP bir kademe</size>",
                new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true });
        }
    }
}
