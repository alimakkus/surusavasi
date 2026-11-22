using System;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Haftalık lig ekranı: lig rozeti, kalan süre, 30 kişilik sıralama (yükselme/düşme bölgeleri),
    /// puan kaynakları ve geçen haftanın sonucu (sandık) penceresi.</summary>
    public static class LeagueScreen
    {
        static SaveData D => GameServices.Save.Data;
        static Vector2 scroll;
        static bool help;

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var d = D; League.Check(d);
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Modes); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 200, 40), "<b>Haftalık Lig</b>", UiSkin.Label);
            if (GUI.Button(new Rect(x0 + w - 96, 48, 96, 40), help ? "Sıralama" : "Puanlar", UiSkin.Button)) help = !help;

            var now = DateTime.Now;
            string tc = League.TierColors[d.leagueTier];
            // Üst kart: rozet + lig adı + kalan süre + oyuncunun sırası
            var top = new Rect(x0, 96, w, 96);
            GUI.Box(top, "", UiSkin.CardGold);
            Badge(new Rect(top.x + 12, top.y + 10, 76, 76), d.leagueTier);
            var left = League.Left(now);
            var table = League.Table(d.leagueWeek, d.leagueTier, d.leaguePts, League.WeekFrac(now));
            int rank = League.RankOf(table);
            GUI.Label(new Rect(top.x + 100, top.y + 8, w - 110, 30), $"<b><color={tc}>{League.TierNames[d.leagueTier]} Ligi</color></b>", new GUIStyle(UiSkin.Big) { richText = true, fontSize = 22 });
            GUI.Label(new Rect(top.x + 100, top.y + 40, w - 110, 22), $"Sıran: <b>{rank}.</b>   Puanın: <b>{d.leaguePts}</b>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(top.x + 100, top.y + 64, w - 110, 22), $"Bitişe: {(int)left.TotalDays}g {left.Hours}sa  ·  İlk {League.Promote} yükselir, son {League.Demote} düşer", new GUIStyle(UiSkin.Small) { richText = true, fontSize = 12 });

            var area = new Rect(x0, 200, w, 590);
            if (help) Help(area, d.leagueTier);
            else List(area, table);

            if (d.leagueResPending) ResultPopup(x0, w);
        }

        static void Badge(Rect r, int tier)
        {
            var c = ModeUi.C(League.TierColors[tier]);
            float pu = 1 + .04f * Mathf.Sin(Time.time * 3);
            var rr = new Rect(r.center.x - r.width / 2 * pu, r.center.y - r.height / 2 * pu, r.width * pu, r.height * pu);
            GUI.DrawTexture(rr, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c * new Color(.45f, .45f, .45f, 1), 0, rr.width / 2);
            var inner = new Rect(rr.x + 6, rr.y + 6, rr.width - 12, rr.height - 12);
            GUI.DrawTexture(inner, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, 0, inner.width / 2);
            // Yıldızlar: lig basamağı
            GUI.Label(inner, $"<b><size=22>{new string('★', tier + 1)}</size></b>", new GUIStyle(ModeUi.CenterBig) { normal = { textColor = new Color(.15f, .1f, .05f) }, fontSize = 13 });
        }

        static void List(Rect area, System.Collections.Generic.List<League.Entry> t)
        {
            float rowH = 40;
            GUI.Box(area, "", UiSkin.CardDark);
            var inner = new Rect(area.x + 6, area.y + 6, area.width - 12, area.height - 12);
            scroll = GUI.BeginScrollView(inner, scroll, new Rect(0, 0, inner.width - 18, t.Count * rowH));
            for (int i = 0; i < t.Count; i++)
            {
                var e = t[i]; var r = new Rect(0, i * rowH, inner.width - 18, rowH - 3);
                Color bg = e.me ? new Color(1f, .82f, .25f, .35f) : i < League.Promote ? new Color(.2f, .75f, .35f, .22f) : i >= League.Size - League.Demote ? new Color(.9f, .25f, .25f, .2f) : new Color(1, 1, 1, .05f);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, bg, 0, 8);
                string medal = i == 0 ? "#ffd23f" : i == 1 ? "#cfd8dc" : i == 2 ? "#d08a4a" : null;
                if (medal != null) GUI.DrawTexture(new Rect(r.x + 8, r.y + 7, 24, 24), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(medal), 0, 12);
                GUI.Label(new Rect(r.x + 8, r.y, 24, rowH - 3), $"<b>{i + 1}</b>", new GUIStyle(ModeUi.Center) { normal = { textColor = medal != null ? new Color(.15f, .1f, .05f) : Color.white } });
                GUI.Label(new Rect(r.x + 44, r.y, r.width - 140, rowH - 3), e.me ? "<b><color=#ffe27a>Sen</color></b>" + (Achievements.Title(D) is string tt ? $"  <size=11><color=#ffd23f>« {tt} »</color></size>" : "") : e.name, new GUIStyle(UiSkin.Label) { richText = true, alignment = TextAnchor.MiddleLeft });
                GUI.Label(new Rect(r.xMax - 96, r.y, 88, rowH - 3), $"<b>{e.score}</b>", new GUIStyle(UiSkin.Label) { richText = true, alignment = TextAnchor.MiddleRight });
                if (i == League.Promote - 1 || i == League.Size - League.Demote - 1)
                    GUI.DrawTexture(new Rect(r.x, r.yMax + 1, r.width, 2), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, i == League.Promote - 1 ? ModeUi.C("#8be39d") : ModeUi.C("#ff8a80"), 0, 0);
            }
            GUI.EndScrollView();
        }

        static void Help(Rect area, int tier)
        {
            GUI.Box(area, "", UiSkin.CardDark);
            string s =
                "<b>Puan nasıl kazanılır?</b>\n" +
                $"• Kampanya bölümü: ilk geçiş +{League.PtsLevelFirst}, tekrar +{League.PtsLevelRepeat}\n" +
                $"• Kule Tırmanışı: her kat +{League.PtsTowerBase} + kat no\n" +
                $"• Patron Avı: her deneme hasar oranı kadar (en çok +{League.PtsHuntMax})\n" +
                $"• Hayatta Kalma: her dalga +{League.PtsSurvWave}\n\n" +
                "<b>Hafta sonu ödülleri</b> (" + League.TierNames[tier] + " ligi)\n";
            foreach (var (lab, rk) in new[] { ("1.", 1), ("2-3.", 2), ("4-10.", 4), ("11-20.", 11), ("21-30.", 21) })
            {
                var rw = League.RankReward(rk, tier);
                s += $"• {lab}  <color=#9fe8ff>{rw.gems} elmas</color> + {UiSkin.Short(rw.gold)} altın" + (League.RankGear(rk) ? "  <color=#c07bff>+ Epik ekipman</color>" : "") + "\n";
            }
            s += $"\nİlk {League.Promote} oyuncu bir üst lige çıkar, son {League.Demote} oyuncu düşer. Hiç puan almayan da düşer. Üst liglerde ödüller büyür.";
            GUI.Label(new Rect(area.x + 16, area.y + 12, area.width - 32, area.height - 24), s, new GUIStyle(UiSkin.Label) { richText = true, wordWrap = true, fontSize = 15 });
        }

        static void ResultPopup(float x0, float w)
        {
            var d = D;
            GUI.DrawTexture(new Rect(-200, 0, 2000, 800), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .7f), 0, 0);
            var r = new Rect(x0 + 10, 200, w - 20, 380);
            GUI.Box(r, "", UiSkin.CardGold);
            int rank = d.leagueResRank, ot = d.leagueResTier, nt = d.leagueResNew;
            GUI.Label(new Rect(r.x, r.y + 14, r.width, 36), "<b>Geçen Haftanın Sonucu</b>", ModeUi.CenterBig);
            Badge(new Rect(r.center.x - 40, r.y + 56, 80, 80), nt);
            string move = nt > ot ? $"<color=#8be39d>{League.TierNames[nt]} Ligine yükseldin!</color>" : nt < ot ? $"<color=#ff8a80>{League.TierNames[nt]} Ligine düştün</color>" : $"{League.TierNames[nt]} Liginde kaldın";
            GUI.Label(new Rect(r.x + 10, r.y + 144, r.width - 20, 60), $"{League.TierNames[ot]} Ligi'nde <b>{rank}.</b> oldun ({d.leagueResPts} puan)\n{move}", ModeUi.Center);
            var rw = League.RankReward(rank, ot);
            GUI.Label(new Rect(r.x + 10, r.y + 208, r.width - 20, 50), ModeUi.RewardLine(rw) + $"\n<color=#ffb02e>+{(rank <= 3 ? 15 : rank <= 10 ? 10 : 5)} kahraman parçası</color>" + (League.RankGear(rank) ? "\n<color=#c07bff>+ Epik ekipman</color>" : ""), ModeUi.Center);
            if (d.leagueResSkin >= 0) GUI.Label(new Rect(r.x + 10, r.y + 256, r.width - 20, 22), $"<color=#ff6f91><b>Ödül: {Progress.Heroes[d.leagueResSkin].name} Bayram görünümü!</b></color>", ModeUi.Center);
            if (GUI.Button(new Rect(r.x + 30, r.y + 280, r.width - 60, 64), "SANDIĞI AÇ", UiSkin.ButtonGold))
            {
                SoundFx.Play(nt > ot ? "promote" : "chest");
                ModeUi.Grant(rw, "league");
                HeroShards.Give(d, rank <= 3 ? 15 : rank <= 10 ? 10 : 5);
                if (League.RankGear(rank)) Gear.AddToBag(d, new GearItem { id = ++d.gearSeq, slot = UnityEngine.Random.Range(0, 3), rarity = 2 });
                d.leagueResPending = false;
                GameServices.Save.Save();
            }
        }
    }
}
