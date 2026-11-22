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
        const float SP = 92, HEAD = 128, BLOCK = 10 * SP + HEAD, BOTTOM = 70;
        static Vector2 scroll;
        static bool focus = true;
        static SaveData D => GameServices.Save.Data;
        static GUIStyle num, numBig, tag;

        public static void Focus() => focus = true;

        static float TotalH => 10 * BLOCK + BOTTOM;
        static float BlockTop(int c) => TotalH - BOTTOM - (c + 1) * BLOCK;
        static float NodeY(int lv) { int c = (lv - 1) / 10, k = (lv - 1) % 10; return BlockTop(c) + HEAD + (9 - k) * SP + SP / 2; }
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
        }

        static void Backdrop(int c, float bt, float w)
        {
            // Alt yarı: ilk 5 bölümün haritası, üst yarı: sonraki 5'in
            float mid = BlockTop(c) + HEAD + 5 * SP;
            for (int half = 0; half < 2; half++)
            {
                int map = BattleData.MapOf(c * 10 + 1 + half * 5);
                var r = half == 0 ? new Rect(0, mid, w, BlockTop(c) + BLOCK - mid) : new Rect(0, bt + HEAD - 30, w, mid - (bt + HEAD - 30));
                var bg = Art.Background(map);
                if (bg != null) GUI.DrawTexture(r, bg.texture, ScaleMode.ScaleAndCrop);
                else GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(BattleData.Maps[map].ground), 0, 0);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.02f, .03f, .06f, .38f), 0, 0);
                // Bölge etiketi
                var tr = new Rect(8, r.y + (half == 0 ? r.height - 30 : 8), 150, 22);
                GUI.DrawTexture(tr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .5f), 0, 11);
                GUI.Label(new Rect(tr.x + 10, tr.y, tr.width - 12, tr.height), $"<color=#9fe8ff>◆</color> {BattleData.Maps[map].nameTr}", tag);
            }
            // Bölüm başlığı: kurdele + patron
            var d = D;
            bool open = c * 10 + 1 <= d.unlocked;
            var hr = new Rect(0, bt, w, HEAD - 30);
            GUI.DrawTexture(hr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.05f, .04f, .08f, .92f), 0, 0);
            GUI.DrawTexture(new Rect(0, hr.yMax - 3, w, 3), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(BattleData.BossColors[c]), 0, 0);
            var bs = Art.Boss(c, (int)(Time.time * 2f) + c);
            if (bs != null)
            {
                var old = GUI.color; if (!open) GUI.color = new Color(0, 0, 0, .85f);
                DialogBox.DrawSprite(new Rect(w - 120, bt - 18, 116, hr.height + 16), bs);
                GUI.color = old;
            }
            int st = 0; for (int i = 0; i < 10; i++) st += d.stars[c * 10 + i];
            GUI.Label(new Rect(14, bt + 8, w - 140, 26), $"<size=13><color=#ffd23f>BÖLÜM {c + 1}</color></size>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(14, bt + 30, w - 140, 30), $"<b><size=21>{(open ? Story.ChapterNames[c] : "???")}</size></b>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(14, bt + 62, w - 140, 22), open ? $"Patron: {BattleData.BossNames[c]}   ·   ★ {st}/30" : $"{c * 10}. seviyeyi geç", new GUIStyle(UiSkin.Small) { richText = true });
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
            if (open && GUI.Button(r, "", GUIStyle.none)) { SoundFx.Play("click"); flow.Play(lv); }
        }
    }
}
