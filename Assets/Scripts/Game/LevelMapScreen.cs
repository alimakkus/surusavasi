using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Bölüm haritası: aşağıdan yukarı kıvrılan yol. Her 10 bölüm bir hikâye bölümü (başlık + patron),
    /// her 5 bölüm kendi harita dokusunda. Tamamlanan düğümlerde yıldızlar, sıradaki düğümde kahraman durur.
    /// Ekran açılınca sıradaki bölüme kaydırılır.</summary>
    public static class LevelMapScreen
    {
        const float SP = 92, HEAD = 160, BLOCK = 10 * SP + HEAD, BOTTOM = 70;
        static Vector2 scroll;
        static bool focus = true;
        static SaveData D => GameServices.Save.Data;
        static GUIStyle num, numBig, tag;

        public static void Focus() => focus = true;

        static float TotalH => 10 * BLOCK + BOTTOM;
        static float BlockTop(int c) => TotalH - BOTTOM - (c + 1) * BLOCK;
        static float NodeY(int lv) { int c = (lv - 1) / 10, k = (lv - 1) % 10; return BlockTop(c) + (9 - k) * SP + SP / 2; }
        static float NodeX(int lv, float w) => w / 2 + Mathf.Sin((lv - 1) * .95f) * w * .3f;

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var d = D;
            num ??= new GUIStyle(UiSkin.Label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 18, richText = true };
            numBig ??= new GUIStyle(num) { fontSize = 22 };
            tag ??= new GUIStyle(UiSkin.Small) { alignment = TextAnchor.MiddleLeft, richText = true, fontSize = 12 };

            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Base); return; }
            int cur = Mathf.Clamp(d.unlocked, 1, Balance.MaxLevel), cc = Story.Chapter(cur);
            GUI.Label(new Rect(x0 + 100, 44, w - 100, 26), $"<b>Bölüm {cc + 1}: {Story.ChapterNames[cc]}</b>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(x0 + 100, 68, w - 100, 22), $"Toplam ★ {Progress.TotalStars(d)} / {Balance.MaxLevel * 3}   ·   Sıradaki: {cur}. seviye", new GUIStyle(UiSkin.Small) { richText = true });

            var view = new Rect(x0, 96, w + 16, 700);
            if (focus) { focus = false; scroll.y = Mathf.Clamp(NodeY(cur) - view.height * .6f, 0, TotalH - view.height); }
            bool blocked = popupCh >= 0 || revealT >= 0 || DialogBox.Active;   // üstte pencere varken harita tıklanmasın
            if (blocked) GUI.enabled = false;
            scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, w, TotalH));
            float vy0 = scroll.y - 150, vy1 = scroll.y + view.height + 150;

            for (int c = 9; c >= 0; c--)
            {
                float bt = BlockTop(c);
                if (bt > vy1 || bt + BLOCK < vy0) continue;
                Backdrop(c, bt, w);
            }
            // Yol: düğümler arası noktalar
            for (int lv = 1; lv < Balance.MaxLevel; lv++)
            {
                float y1 = NodeY(lv), y2 = NodeY(lv + 1);
                if (Mathf.Max(y1, y2) < vy0 || Mathf.Min(y1, y2) > vy1) continue;
                Vector2 a = new Vector2(NodeX(lv, w), y1), b = new Vector2(NodeX(lv + 1, w), y2);
                bool done = lv < cur;
                int dots = Mathf.CeilToInt(Vector2.Distance(a, b) / 16);
                for (int i = 1; i < dots; i++)
                {
                    var p = Vector2.Lerp(a, b, i / (float)dots);
                    GUI.DrawTexture(new Rect(p.x - 4, p.y - 4, 8, 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, done ? new Color(1f, .84f, .3f, .95f) : new Color(1, 1, 1, .35f), 0, 4);
                }
            }
            // Düğümler
            for (int lv = 1; lv <= Balance.MaxLevel; lv++)
            {
                float y = NodeY(lv); if (y < vy0 || y > vy1) continue;
                Node(flow, lv, NodeX(lv, w), y, cur);
            }
            GUI.EndScrollView();
            GUI.enabled = true;

            float VW = Screen.width / (Screen.height / 800f);
            if (popupCh >= 0) BossPopup(flow, x0, w, VW);
            Reveal(d, VW);
            DialogBox.Draw(VW);
        }

        static void Backdrop(int c, float bt, float w)
        {
            // Alt yarı: ilk 5 bölümün haritası, üst yarı: sonraki 5'in
            // Blok (yukarıdan aşağı): 6-10. seviyeler, 1-5. seviyeler, en altta bölüm başlığı (yol yukarı doğru girer)
            float mid = bt + 5 * SP;
            for (int half = 0; half < 2; half++)
            {
                int map = BattleData.MapOf(c * 10 + 1 + half * 5);
                var r = half == 0 ? new Rect(0, mid, w, 5 * SP + 15) : new Rect(0, bt - 15, w, 5 * SP + 15);
                var bg = Art.Background(map);
                if (bg != null) GUI.DrawTexture(r, bg.texture, ScaleMode.ScaleAndCrop);
                else GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(BattleData.Maps[map].ground), 0, 0);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.02f, .03f, .06f, .38f), 0, 0);
                // Bölge etiketi
                var tr = new Rect(8, r.y + r.height - 34, 150, 22);
                GUI.DrawTexture(tr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .5f), 0, 11);
                GUI.Label(new Rect(tr.x + 10, tr.y, tr.width - 12, tr.height), $"<color=#9fe8ff>◆</color> {BattleData.Maps[map].nameTr}", tag);
            }
            // Bölüm başlığı: kurdele + patron
            var d = D;
            bool open = c * 10 + 1 <= d.unlocked;
            var hr = new Rect(0, bt + 10 * SP + 15, w, HEAD - 30);
            float hy = hr.y;
            GUI.DrawTexture(hr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.05f, .04f, .08f, .92f), 0, 0);
            GUI.DrawTexture(new Rect(0, hr.y, w, 3), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(BattleData.BossColors[c]), 0, 0);
            GUI.DrawTexture(new Rect(0, hr.yMax - 3, w, 3), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(BattleData.BossColors[c]), 0, 0);
            var bs = Art.Boss(c, (int)(Time.time * 2f) + c);
            if (bs != null)
            {
                var old = GUI.color; if (!open) GUI.color = new Color(0, 0, 0, .85f);
                DialogBox.DrawSprite(new Rect(w - 120, hy - 18, 116, hr.height + 16), bs);
                GUI.color = old;
            }
            int st = 0; for (int i = 0; i < 10; i++) st += d.stars[c * 10 + i];
            GUI.Label(new Rect(14, hy + 8, w - 140, 26), $"<size=13><color=#ffd23f>BÖLÜM {c + 1}</color></size>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(14, hy + 30, w - 140, 30), $"<b><size=21>{(open ? Story.ChapterNames[c] : "???")}</size></b>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(14, hy + 62, w - 140, 22), open ? $"Patron: {BattleData.BossNames[c]}   ·   ★ {st}/30" : $"{c * 10}. seviyeyi geç", new GUIStyle(UiSkin.Small) { richText = true });
            if (open) ChestRow(c, new Rect(14, hy + 92, w - 150, 30), st);
        }

        // ---------- Bölüm yıldız sandıkları ----------
        static void ChestRow(int c, Rect r, int st)
        {
            var d = D; float t = Time.time;
            GUI.DrawTexture(new Rect(r.x, r.y + 11, r.width, 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .6f), 0, 4);
            GUI.DrawTexture(new Rect(r.x, r.y + 11, r.width * Mathf.Clamp01(st / 30f), 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#ffd23f"), 0, 4);
            for (int k = 0; k < 2; k++)
            {
                float cx = r.x + r.width * ChapterChests.Need[k] / 30f;
                bool claimed = ChapterChests.Claimed(d, c, k), ready = ChapterChests.Ready(d, c, k);
                float s = ready ? 30 * (1 + .1f * Mathf.Sin(t * 6 + k)) : 26;
                var cr = new Rect(cx - s / 2 - (k == 1 ? s / 2 : 0), r.y + 15 - s / 2, s, s);
                if (ready) GUI.DrawTexture(new Rect(cr.x - 5, cr.y - 5, s + 10, s + 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .85f, .3f, .45f + .3f * Mathf.Sin(t * 5)), 0, (s + 10) / 2);
                // Sandık: gövde + kapak + kilit
                Color body = claimed ? new Color(.35f, .3f, .25f) : ModeUi.C(k == 0 ? "#b06a2a" : "#7a4bc2");
                GUI.DrawTexture(new Rect(cr.x, cr.y + s * .38f, s, s * .62f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, body, 0, 4);
                GUI.DrawTexture(new Rect(cr.x - 1, cr.y + (claimed ? -s * .05f : s * .12f), s + 2, s * .3f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, body * 1.25f, 0, 5);
                GUI.DrawTexture(new Rect(cr.center.x - 3, cr.y + s * .4f, 6, 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#ffd23f"), 0, 2);
                if (claimed) GUI.Label(cr, "<b><color=#8be39d>✓</color></b>", new GUIStyle(num) { fontSize = 16 });
                GUI.Label(new Rect(cx - 30 - (k == 1 ? 14 : 0), r.y + 26, 60, 16), $"<size=10>{ChapterChests.Need[k]}★</size>", new GUIStyle(num) { fontSize = 10 });
                if (ready && GUI.Button(cr, "", GUIStyle.none)) Claim(c, k);
            }
        }

        static void Claim(int c, int k)
        {
            var d = D;
            var rw = ChapterChests.Reward(c, k);
            var g = ChapterChests.Claim(d, c, k, new System.Random());
            ModeUi.Grant(rw, "chapter_chest");
            GameServices.Save.Save();
            SoundFx.Play("chest");
            chestMsg = $"<b>Bölüm {c + 1} · {ChapterChests.Need[k]}★ sandığı</b>\n{ModeUi.RewardLine(rw)}" + (g != null ? $"\n<color={Gear.RarityColors[g.rarity]}><b>{Gear.Name(g)} ({Gear.RarityNames[g.rarity]})</b></color>" : "");
            chestT = 3.2f;
        }

        // ---------- Patron menüsü: savaş veya hikâyeyi yeniden izle ----------
        static int popupCh = -1;
        static string chestMsg; static float chestT;

        static void BossPopup(GameFlow flow, float x0, float w, float VW)
        {
            var d = D; int c = popupCh, lv = (c + 1) * 10;
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .65f), 0, 0);
            var r = new Rect(x0 + 10, 220, w - 20, 380);
            GUI.Box(r, "", UiSkin.CardGold);
            var bs = Art.Boss(c, (int)(Time.time * 2.5f));
            if (bs != null) DialogBox.DrawSprite(new Rect(r.center.x - 80, r.y - 60, 160, 150), bs);
            GUI.Label(new Rect(r.x, r.y + 92, r.width, 30), $"<b>{BattleData.BossNames[c]}</b>", ModeUi.CenterBig);
            GUI.Label(new Rect(r.x, r.y + 124, r.width, 24), $"Bölüm {c + 1}: {Story.ChapterNames[c]}  ·  Seviye {lv}", ModeUi.Center);
            bool open = lv <= d.unlocked;
            GUI.enabled = open;
            if (GUI.Button(new Rect(r.x + 24, r.y + 160, r.width - 48, 60), d.stars[lv - 1] > 0 ? "TEKRAR SAVAŞ" : "PATRONA SALDIR", UiSkin.ButtonRed)) { popupCh = -1; flow.Play(lv); return; }
            // Hikâye: bu bölümde görülen sahneler sırayla
            var ids = new System.Collections.Generic.List<string>();
            // Görülen ya da ilerlemeyle açılmış sahneler (hikâye sonradan eklendiyse eski oyuncular da izleyebilsin)
            bool[] avail = { c * 10 + 1 <= d.unlocked, lv <= d.unlocked, d.stars[lv - 1] > 0 };
            string[] sc = { $"intro{c}", $"boss{c}", $"win{c}" };
            for (int i = 0; i < 3; i++) if (Story.Get(sc[i]) != null && (avail[i] || Story.Seen(d, sc[i]))) ids.Add(sc[i]);
            GUI.enabled = ids.Count > 0;
            if (GUI.Button(new Rect(r.x + 24, r.y + 230, r.width - 48, 54), ids.Count > 0 ? $"▶ Hikâyeyi yeniden izle  <size=12>({ids.Count} sahne)</size>" : "Hikâye henüz görülmedi", UiSkin.Button))
            { popupCh = -1; PlayChain(ids, 0, lv); return; }
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.x + 24, r.y + 300, r.width - 48, 50), "Kapat", UiSkin.ButtonGray)) popupCh = -1;
        }

        static void PlayChain(System.Collections.Generic.List<string> ids, int i, int lv)
        {
            if (i >= ids.Count) return;
            DialogBox.Play(Story.Get(ids[i]), lv, () => PlayChain(ids, i + 1, lv));
        }

        // ---------- "Yeni bölge açıldı" gösterisi ve sandık bildirimi ----------
        static float revealT = -1; static int revealCh;

        static void Reveal(SaveData d, float VW)
        {
            int ch = Story.Chapter(Mathf.Clamp(d.unlocked, 1, Balance.MaxLevel));
            if (revealT < 0 && ch > d.chapterRevealed && !DialogBox.Active)
            {
                // İlk açılışta (eski kayıt) mevcut bölümü sessizce işaretle; sonrakilerde gösteri yap
                if (d.chapterRevealed < 0 && ch > 0 && d.unlocked % 10 != 1) { d.chapterRevealed = ch; GameServices.Save.Save(); }
                else { revealCh = ch; revealT = 0; d.chapterRevealed = ch; GameServices.Save.Save(); SoundFx.Play("promote"); Focus(); }
            }
            if (chestT > 0)
            {
                chestT -= Time.deltaTime;
                var cr = new Rect(VW / 2 - 170, 600, 340, 96);
                GUI.Box(cr, "", UiSkin.CardGold);
                GUI.Label(new Rect(cr.x + 10, cr.y + 6, cr.width - 20, cr.height - 12), chestMsg, ModeUi.Small);
            }
            if (revealT < 0) return;
            revealT += Time.deltaTime;
            float t = revealT, a = Mathf.Clamp01(t * 3) * Mathf.Clamp01((3.6f - t) * 2);
            if (t > 3.6f) { revealT = -1; return; }
            GUI.DrawTexture(new Rect(0, 0, VW, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .72f * a), 0, 0);
            // Işınlar
            var bc = ModeUi.C(BattleData.BossColors[revealCh]);
            for (int i = 0; i < 12; i++)
            {
                float ang = t * 20 + i * 30;
                var m = GUI.matrix; GUIUtility.RotateAroundPivot(ang, new Vector2(VW / 2, 330));
                GUI.DrawTexture(new Rect(VW / 2 - 6, 330 - 260, 12, 260), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(bc.r, bc.g, bc.b, .12f * a), 0, 6);
                GUI.matrix = m;
            }
            // Patron silüeti siyahtan renge döner
            var bs = Art.Boss(revealCh, (int)(t * 2.5f));
            float k = Mathf.Clamp01((t - .6f) / 1.2f), sc = .8f + .2f * Mathf.Min(1, t * 2);
            var old = GUI.color; GUI.color = new Color(k, k, k, a);
            if (bs != null) DialogBox.DrawSprite(new Rect(VW / 2 - 110 * sc, 330 - 150 * sc, 220 * sc, 220 * sc), bs);
            GUI.color = new Color(1, 1, 1, a);
            GUI.Label(new Rect(0, 420, VW, 30), "<color=#ffd23f><b>YENİ BÖLGE AÇILDI</b></color>", ModeUi.Center);
            GUI.Label(new Rect(0, 450, VW, 46), $"<b><size=30>Bölüm {revealCh + 1}: {Story.ChapterNames[revealCh]}</size></b>", ModeUi.CenterBig);
            GUI.Label(new Rect(0, 500, VW, 26), $"Seni bekleyen patron: <b>{BattleData.BossNames[revealCh]}</b>", ModeUi.Center);
            GUI.color = old;
            if (t > .8f && GUI.Button(new Rect(0, 0, VW, 800), "", GUIStyle.none)) revealT = 3.3f;   // dokun: geç
        }

        static void Node(GameFlow flow, int lv, float x, float y, int cur)
        {
            var d = D;
            bool boss = lv % 10 == 0, open = lv <= cur, here = lv == cur, done = d.stars[lv - 1] > 0;
            float t = Time.time, sz = boss ? 78 : 56;
            if (here) sz *= 1 + .06f * Mathf.Sin(t * 4);
            var r = new Rect(x - sz / 2, y - sz / 2, sz, sz);
            // Gölge
            GUI.DrawTexture(new Rect(r.x + 3, r.y + 6, sz, sz), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .45f), 0, sz / 2);
            // Halka + dolgu
            Color ring = here ? ModeUi.C("#ffd23f") : boss ? ModeUi.C("#ff5a4d") : done ? (d.stars[lv - 1] == 3 ? ModeUi.C("#ffd23f") : ModeUi.C("#e8e8f0")) : new Color(.35f, .38f, .45f);
            Color fill = !open ? new Color(.16f, .18f, .23f) : boss ? ModeUi.C("#7a1f1f") : here ? ModeUi.C("#2f6fb3") : ModeUi.C("#2f8a4a");
            if (here) { float pu = .35f + .25f * Mathf.Sin(t * 5); GUI.DrawTexture(new Rect(r.x - 10, r.y - 10, sz + 20, sz + 20), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .84f, .25f, pu), 0, (sz + 20) / 2); }
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ring, 0, sz / 2);
            GUI.DrawTexture(new Rect(r.x + 5, r.y + 5, sz - 10, sz - 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, fill, 0, (sz - 10) / 2);
            // Üst parlama
            GUI.DrawTexture(new Rect(r.x + sz * .22f, r.y + sz * .14f, sz * .56f, sz * .22f), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, 1, 1, open ? .18f : .06f), 0, sz * .11f);

            if (boss)
            {
                var bs = Art.Boss(lv / 10 - 1, (int)(t * 2.5f));
                if (bs != null)
                {
                    var old = GUI.color; if (!open) GUI.color = new Color(0, 0, 0, .8f);
                    DialogBox.DrawSprite(new Rect(r.x - 6, r.y - 14, sz + 12, sz + 8), bs);
                    GUI.color = old;
                }
                GUI.Label(new Rect(x - 40, r.yMax - 4, 80, 22), $"<b>{lv}</b>", new GUIStyle(num) { fontSize = 15 });
            }
            else GUI.Label(r, open ? $"{lv}" : $"<color=#7d8696>{lv}</color>", num);

            // Yıldızlar
            if (done)
            {
                int s = d.stars[lv - 1];
                for (int i = 0; i < 3; i++)
                {
                    float sx = x - 22 + i * 22, sy = r.y - 14 - (i == 1 ? 5 : 0);
                    GUI.Label(new Rect(sx - 12, sy - 12, 24, 24), i < s ? "<color=#ffd23f>★</color>" : "<color=#00000088>★</color>", new GUIStyle(num) { fontSize = 20 });
                }
            }
            // Kahraman: sıradaki bölümün üstünde zıplar
            if (here)
            {
                int hi = Progress.HeroIndex(d.hero);
                var port = Art.HeroPortrait(hi, Mathf.FloorToInt(t * 1.6f));
                float bob = Mathf.Abs(Mathf.Sin(t * 3)) * 8;
                if (port != null) GUI.DrawTexture(new Rect(x - 34, r.y - 74 - bob, 68, 68), port, ScaleMode.ScaleToFit);
                var pr = new Rect(x > 210 ? x - sz / 2 - 78 : x + sz / 2 + 4, y - 14, 74, 28);
                GUI.Box(pr, "", UiSkin.ButtonGold);
                GUI.Label(pr, "<b><size=13>OYNA ›</size></b>", new GUIStyle(num) { normal = { textColor = new Color(.25f, .14f, .02f) } });
                if (GUI.Button(pr, "", GUIStyle.none)) { SoundFx.Play("click"); flow.Play(lv); }
            }
            if ((open || boss) && GUI.Button(r, "", GUIStyle.none))
            {
                SoundFx.Play("click");
                if (boss) popupCh = lv / 10 - 1; else if (open) flow.Play(lv);   // patron: önce menü (savaş / hikâye)
            }
        }
    }
}
