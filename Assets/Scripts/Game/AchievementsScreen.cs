using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Başarımlar ekranı: rozet, ilerleme, 3 kademe, ödül alma ve unvan seçimi.</summary>
    public static class AchievementsScreen
    {
        static Vector2 scroll;
        static SaveData D => GameServices.Save.Data;
        static float pop; static string popId;

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var d = D;
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Daily); return; }
            int score = Achievements.Score(d), max = Achievements.All.Length * 3;
            string ttl = Achievements.Title(d);
            GUI.Label(new Rect(x0 + 100, 44, w - 100, 26), $"<b>Başarımlar</b>   <color=#ffd23f>{score}/{max} rozet</color>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(x0 + 100, 68, w - 100, 22), ttl != null ? $"Unvanın: <color=#ffd23f><b>{ttl}</b></color>" : "<size=11>Bir başarımın Altın kademesini al, adını unvan yap</size>", new GUIStyle(UiSkin.Small) { richText = true });

            pop = Mathf.Max(0, pop - Time.deltaTime * 2.5f);
            float rowH = 92;
            var list = Achievements.All;
            scroll = GUI.BeginScrollView(new Rect(x0, 96, w + 16, 700), scroll, new Rect(0, 0, w, list.Length * (rowH + 6)));
            for (int i = 0; i < list.Length; i++)
            {
                var a = list[i]; var r = new Rect(0, i * (rowH + 6), w, rowH);
                int reached = Achievements.Reached(d, a), claimed = Achievements.ClaimedTiers(d, a);
                bool can = Achievements.CanClaim(d, a);
                long v = a.value(d);
                GUI.Box(r, "", can ? UiSkin.CardGold : UiSkin.Card);

                // Rozet: alınan en yüksek kademenin renginde
                float bs = 64 * (popId == a.id ? 1 + pop * .3f : 1);
                var br = new Rect(r.x + 12 + 32 - bs / 2, r.y + 14 + 32 - bs / 2, bs, bs);
                Color rc = claimed > 0 ? ModeUi.C(Achievements.TierColors[claimed - 1]) : new Color(.3f, .33f, .4f);
                GUI.DrawTexture(br, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, rc, 0, bs / 2);
                GUI.DrawTexture(new Rect(br.x + 5, br.y + 5, bs - 10, bs - 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.08f, .09f, .14f, 1), 0, (bs - 10) / 2);
                GUI.Label(br, $"<b><size=26><color={(claimed > 0 ? Achievements.TierColors[claimed - 1] : "#7d8696")}>{a.icon}</color></size></b>", ModeUi.Center);

                float tx = r.x + 88, tw = r.width - 88 - 120;
                GUI.Label(new Rect(tx, r.y + 8, tw, 24), $"<b>{a.name}</b>", new GUIStyle(UiSkin.Label) { richText = true });
                int next = Mathf.Min(2, reached);
                long goal = a.tiers[next];
                GUI.Label(new Rect(tx, r.y + 32, tw, 20), reached >= 3 ? $"<size=12>Tamamlandı · {v} {a.unit}</size>" : $"<size=12>{v} / {goal} {a.unit}</size>", new GUIStyle(UiSkin.Small) { richText = true });
                // İlerleme ve 3 kademe noktası
                var pb = new Rect(tx, r.y + 56, tw, 10);
                GUI.DrawTexture(pb, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .5f), 0, 5);
                float f = reached >= 3 ? 1 : Mathf.Clamp01(v / (float)goal);
                GUI.DrawTexture(new Rect(pb.x, pb.y, Mathf.Max(8, pb.width * f), pb.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(Achievements.TierColors[next]), 0, 5);
                for (int t = 0; t < 3; t++)
                {
                    var c = ModeUi.C(Achievements.TierColors[t]); if (t >= claimed) c *= new Color(.4f, .4f, .4f, 1);
                    GUI.DrawTexture(new Rect(tx + t * 22, r.y + 70, 16, 16), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, 0, 8);
                }

                // Sağ: ödül al / unvan
                var ab = new Rect(r.xMax - 112, r.y + 18, 100, 56);
                if (can)
                {
                    if (GUI.Button(ab, $"AL\n<size=12>+{Achievements.TierGems[claimed]} elmas</size>", new GUIStyle(UiSkin.ButtonGold) { fontSize = 16, richText = true }))
                    {
                        int g = Achievements.Claim(d, a);
                        GameServices.Wallet.Add(Currency.Diamonds, g, "achievement:" + a.id);
                        GameServices.Save.Save(); SoundFx.Play(claimed + 1 >= 3 ? "promote" : "chest"); pop = 1; popId = a.id;
                        flow.Toast($"{a.name}: {Achievements.TierNames[claimed]} rozet! +{g} elmas");
                    }
                }
                else if (claimed >= 3)
                {
                    bool on = d.title == a.id;
                    if (GUI.Button(ab, on ? "Unvan\n<size=11>seçili</size>" : $"Unvan yap\n<size=10>{a.title}</size>", new GUIStyle(on ? UiSkin.ButtonGreen : UiSkin.Button) { fontSize = 13, richText = true }))
                    { d.title = on ? "" : a.id; GameServices.Save.Save(); SoundFx.Play("click"); }
                }
                else GUI.Label(ab, $"<size=11>Sıradaki:\n{Achievements.TierNames[next]}  +{Achievements.TierGems[next]} elmas</size>", ModeUi.Small);
            }
            GUI.EndScrollView();
        }
    }
}
