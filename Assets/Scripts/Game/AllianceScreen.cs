using System;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>İttifak ekranı. Katılmadıysan 3 öneri; katıldıysan ittifak kartı (seviye, avantajlar, katkı puanı) ve
    /// sekmeler: Akış (sohbet + hazır mesajlar), Bağış (bağış + başkalarına yardım), Patron (haftalık ortak patron),
    /// Üyeler, Mağaza (katkı puanıyla).</summary>
    public static class AllianceScreen
    {
        static SaveData D => GameServices.Save.Data;
        static Wallet Wl => GameServices.Wallet;
        static int tab; static Vector2 scroll;
        static readonly string[] Tabs = { "Akış", "Bağış", "Patron", "Üyeler", "Mağaza" };
        static readonly System.Random rnd = new System.Random();

        static GUIStyle sm, lb;
        static GUIStyle Sm => sm ??= new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true, fontSize = 13 };
        static GUIStyle Lb => lb ??= new GUIStyle(UiSkin.Label) { richText = true, wordWrap = true };

        public static void Open(GameFlow flow, int t = -1) { if (t >= 0) tab = t; scroll = Vector2.zero; flow.Show(GameScreen.Alliance); }

        static void Grant(string kind, int n)
        {
            if (n <= 0 || Wl == null) return;
            var c = kind == "gold" ? Currency.Gold : kind == "iron" ? Currency.Iron : kind == "food" ? Currency.Food : Currency.Diamonds;
            Wl.Add(c, n, "alliance");
        }

        static void Dot(Rect r)
        {
            float pu = 1 + .15f * Mathf.Sin(Time.time * 6);
            GUI.DrawTexture(new Rect(r.xMax - 10 - 6 * pu, r.y - 4 - 6 * pu, 14 * pu, 14 * pu), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.88f, .27f, .23f), 0, 7 * pu);
        }

        static void Emblem(Rect r, Alliance.Info info)
        {
            var c = ModeUi.C(info.color);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c * new Color(.45f, .45f, .45f, 1), 0, 14);
            var inn = new Rect(r.x + 5, r.y + 5, r.width - 10, r.height - 10);
            GUI.DrawTexture(inn, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, c, 0, 11);
            GUI.Label(inn, $"<b>{info.tag}</b>", new GUIStyle(ModeUi.Center) { fontSize = (int)(r.width * .3f), normal = { textColor = new Color(.08f, .1f, .16f) } });
        }

        public static void Draw(GameFlow flow, float x0, float w)
        {
            var d = D; Alliance.Tick(d);
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", UiSkin.Button)) { flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), "<b>İttifak</b>", UiSkin.Label);
            if (!Alliance.Joined(d)) { Choose(flow, x0, w, d); return; }

            var info = Alliance.Describe(d.allyId);
            int lv = Alliance.Level(d); var (cur, need) = Alliance.LevelProgress(d);
            var top = new Rect(x0, 96, w, 112);
            GUI.Box(top, "", UiSkin.CardGold);
            Emblem(new Rect(top.x + 12, top.y + 14, 84, 84), info);
            float tx = top.x + 108, tw = top.width - 120;
            GUI.Label(new Rect(tx, top.y + 6, tw, 28), $"<b><color={info.color}>[{info.tag}] {info.name}</color></b>", new GUIStyle(UiSkin.Big) { richText = true, fontSize = 19 });
            GUI.Label(new Rect(tx, top.y + 34, tw, 20), $"Seviye <b>{lv}</b> · {info.members + 1}/{Alliance.Members} üye · katkı puanın: <color=#ffd23f><b>{d.allyCoins}</b></color>", Sm);
            var bar = new Rect(tx, top.y + 58, tw, 12);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 6);
            GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(8, bar.width * cur / (float)need), bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C(info.color), 0, 6);
            GUI.Label(new Rect(tx, top.y + 74, tw, 34), $"<size=11>Yardım: {Alliance.HelpsPerBuild(d)}/inşaat · patron sandığı +%{(Alliance.BossBonus(d) - 1) * 100:0}" + (Alliance.ShopDiscount(d) > 0 ? $" · mağaza %{Alliance.ShopDiscount(d)} indirim" : "") + "</size>", Sm);

            // Sekmeler
            float tbw = (w - 4 * 6) / 5, ty = 216;
            for (int i = 0; i < Tabs.Length; i++)
            {
                var r = new Rect(x0 + i * (tbw + 6), ty, tbw, 38);
                if (GUI.Button(r, Tabs[i], new GUIStyle(i == tab ? UiSkin.ButtonGold : UiSkin.Button) { fontSize = 14 })) { tab = i; scroll = Vector2.zero; SoundFx.Play("click"); }
                bool todo = i == 1 ? Alliance.DonationsLeft(d) > 0 || Alliance.HelpsLeft(d) > 0 : i == 2 ? Alliance.BossTriesLeft(d) > 0 && Alliance.TotalPct(d) < 100 || MilestoneAny(d) : false;
                if (todo) Dot(r);
            }
            var area = new Rect(x0, ty + 46, w, 800 - ty - 54);
            switch (tab)
            {
                case 0: Feed(area, d); break;
                case 1: Donate(flow, area, d); break;
                case 2: Boss(flow, area, d); break;
                case 3: MembersTab(area, d); break;
                case 4: ShopTab(flow, area, d); break;
            }
        }

        static bool MilestoneAny(SaveData d) { for (int i = 0; i < Alliance.BossMilestones.Length; i++) if (Alliance.MilestoneReady(d, i)) return true; return false; }

        // ---------------- Katıl ----------------
        static void Choose(GameFlow flow, float x0, float w, SaveData d)
        {
            if (d.allySeed == 0) { d.allySeed = UnityEngine.Random.Range(1, 99999); GameServices.Save.Save(); }
            GUI.Label(new Rect(x0, 100, w, 60), "İttifaka katıl: üyeler inşaatına yardım eder, birlikte haftalık patronu devirir, bağışlarla ittifakı büyütürsünüz.", Lb);
            float y = 170;
            foreach (var info in Alliance.Suggestions(d))
            {
                var r = new Rect(x0, y, w, 150);
                GUI.Box(r, "", UiSkin.Card);
                Emblem(new Rect(r.x + 14, r.y + 16, 76, 76), info);
                GUI.Label(new Rect(r.x + 104, r.y + 10, r.width - 116, 28), $"<b><color={info.color}>[{info.tag}] {info.name}</color></b>", new GUIStyle(UiSkin.Big) { richText = true, fontSize = 18 });
                string act = info.activity > .78f ? "<color=#8be39d>Çok aktif</color>" : info.activity > .65f ? "<color=#ffd23f>Aktif</color>" : "<color=#c9d3e0>Sakin</color>";
                GUI.Label(new Rect(r.x + 104, r.y + 40, r.width - 116, 44), $"Seviye {info.baseLevel} · {info.members}/{Alliance.Members} üye · {act}\n<size=11>Dil: Türkçe · katılım serbest</size>", Sm);
                if (GUI.Button(new Rect(r.x + 104, r.y + 90, r.width - 118, 48), "KATIL", UiSkin.ButtonGold))
                {
                    Alliance.Join(d, info.id); GameServices.Save.Save(); SoundFx.Play("promote"); tab = 0;
                    flow.ShowToast($"[{info.tag}] {info.name} ittifakına katıldın!");
                }
                y += 160;
            }
        }

        // ---------------- Akış ----------------
        static void Feed(Rect area, SaveData d)
        {
            var feed = Alliance.Feed(d);
            float qh = 2 * 46 + 6;
            var box = new Rect(area.x, area.y, area.width, area.height - qh - 8);
            GUI.Box(box, "", UiSkin.CardDark);
            float rowH = 46;
            var inner = new Rect(box.x + 6, box.y + 6, box.width - 12, box.height - 12);
            var view = new Rect(0, 0, inner.width - 18, Mathf.Max(inner.height, feed.Count * rowH));
            if (scroll.y == 0 && view.height > inner.height) scroll.y = view.height; // en yeniler altta
            scroll = GUI.BeginScrollView(inner, scroll, view);
            for (int i = 0; i < feed.Count; i++)
            {
                var f = feed[i]; var r = new Rect(0, i * rowH, view.width, rowH - 4);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, f.me ? new Color(1, .82f, .25f, .15f) : new Color(1, 1, 1, .04f), 0, 8);
                string who = f.me ? "<color=#ffe27a>Sen</color>" : f.who == "Sistem" ? "<color=#8be39d>Sistem</color>" : $"<color=#9fd0ff>{f.who}</color>";
                string body = f.action ? $"<i>{f.text}</i>" : $"\"{f.text}\"";
                GUI.Label(new Rect(r.x + 8, r.y + 2, r.width - 60, r.height - 4), $"<b>{who}</b> {body}", Sm);
                GUI.Label(new Rect(r.xMax - 50, r.y + 2, 46, 20), $"<size=10><color=#7d8696>{f.time}</color></size>", Sm);
            }
            GUI.EndScrollView();
            float qw = (area.width - 12) / 3, qy = box.yMax + 8;
            for (int i = 0; i < Alliance.Quick.Length; i++)
            {
                var r = new Rect(area.x + (i % 3) * (qw + 6), qy + (i / 3) * 52, qw, 46);
                if (GUI.Button(r, Alliance.Quick[i], new GUIStyle(UiSkin.Button) { fontSize = 12, wordWrap = true }))
                { Alliance.Say(d, "Sen", Alliance.Quick[i]); GameServices.Save.Save(); SoundFx.Play("click"); scroll.y = 1e6f; }
            }
        }

        // ---------------- Bağış + yardım ----------------
        static void Donate(GameFlow flow, Rect area, SaveData d)
        {
            int left = Alliance.DonationsLeft(d);
            GUI.Label(new Rect(area.x, area.y, area.width, 24), $"<b>Bağış</b>  <size=12>bugün kalan {left}/{Alliance.DonatePerDay} · her bağış +{Alliance.DonateCoins} katkı, +{Alliance.DonateXp} ittifak XP</size>", Lb);
            float bw = (area.width - 12) / 3, by = area.y + 30;
            string[] nm = { "Altın", "Demir", "Yiyecek" }; Currency[] cur = { Currency.Gold, Currency.Iron, Currency.Food };
            for (int i = 0; i < 3; i++)
            {
                var res = (Alliance.Res)i; int cost = Alliance.DonateCost(d, res);
                GUI.enabled = left > 0;
                if (GUI.Button(new Rect(area.x + i * (bw + 6), by, bw, 60), $"{nm[i]}\n<size=12>{UiSkin.Short(cost)}</size>", new GUIStyle(UiSkin.ButtonGreen) { fontSize = 15, richText = true }))
                {
                    if (Wl.Spend(cur[i], cost, "alliance_donate")) { Alliance.Donated(d, res); Daily.Add(d, "donate"); GameServices.Save.Save(); SoundFx.Play("coin"); flow.ShowToast($"+{Alliance.DonateCoins} katkı puanı"); }
                    else flow.ShowToast($"Yeterli {nm[i].ToLowerInvariant()} yok");
                }
                GUI.enabled = true;
            }

            float y = by + 74;
            var reqs = Alliance.OthersRequests(d);
            int hl = Alliance.HelpsLeft(d);
            GUI.Label(new Rect(area.x, y, area.width - 170, 26), $"<b>Yardım bekleyenler</b>  <size=12>her yardım +{Alliance.HelpCoins} katkı</size>", Lb);
            GUI.enabled = hl > 0;
            if (GUI.Button(new Rect(area.xMax - 164, y - 4, 164, 34), $"Hepsine yardım ({hl})", new GUIStyle(UiSkin.ButtonGold) { fontSize = 13 }))
            {
                int c = 0; foreach (var q in reqs) c += Alliance.HelpOthers(d, q.idx);
                Daily.Add(d, "ally_help", hl); GameServices.Save.Save(); SoundFx.Play("power"); flow.ShowToast($"{hl} kişiye yardım ettin: +{c} katkı");
            }
            GUI.enabled = true;
            y += 36;
            var inner = new Rect(area.x, y, area.width, area.yMax - y);
            float rowH = 50;
            scroll = GUI.BeginScrollView(inner, scroll, new Rect(0, 0, inner.width - 18, reqs.Count * rowH));
            foreach (var q in reqs)
            {
                var r = new Rect(0, q.idx * rowH, inner.width - 18, rowH - 6);
                bool done = Alliance.Helped(d, q.idx);
                GUI.Box(r, "", UiSkin.Card);
                GUI.Label(new Rect(r.x + 12, r.y + 4, r.width - 140, r.height - 8), $"<b>{q.who}</b>  <size=12>{q.what}</size>", Sm);
                GUI.enabled = !done;
                if (GUI.Button(new Rect(r.xMax - 118, r.y + 5, 110, r.height - 10), done ? "Yardım edildi" : "Yardım et", new GUIStyle(done ? UiSkin.ButtonGray : UiSkin.Button) { fontSize = 13 }))
                { Alliance.HelpOthers(d, q.idx); Daily.Add(d, "ally_help"); GameServices.Save.Save(); SoundFx.Play("click"); }
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        // ---------------- Patron ----------------
        static void Boss(GameFlow flow, Rect area, SaveData d)
        {
            int bi = Alliance.BossIndex(d);
            var top = new Rect(area.x, area.y, area.width, 150);
            GUI.Box(top, "", UiSkin.CardDark);
            var art = new Rect(top.x + 8, top.y + 8, 110, 134);
            GUI.DrawTexture(art, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#3a1a24"), 0, 10);
            var sp = Art.Boss(bi, (int)(Time.time * 2.5f));
            if (sp != null) DialogBox.DrawSprite(new Rect(art.x - 30, art.y - 14, art.width + 60, art.height + 14), sp);
            float tx = top.x + 130, tw = top.width - 142;
            var lw = League.Left(DateTime.Now);
            GUI.Label(new Rect(tx, top.y + 6, tw, 26), $"<b>Haftanın patronu: {Core.Battle.BattleData.BossNames[bi]}</b>", Lb);
            GUI.Label(new Rect(tx, top.y + 32, tw, 20), $"<size=12>Bitişe {(int)lw.TotalDays}g {lw.Hours}sa · senin payın %{Alliance.MinePct(d):0.0}</size>", Sm);
            float total = Alliance.TotalPct(d);
            var bar = new Rect(tx, top.y + 60, tw, 20);
            GUI.DrawTexture(bar, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .55f), 0, 10);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * total / 100f, bar.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, ModeUi.C("#ff5a4d"), 0, 10);
            GUI.Label(bar, $"<b>%{total:0} indirildi</b>", new GUIStyle(ModeUi.Small) { fontSize = 12 });
            int left = Alliance.BossTriesLeft(d);
            GUI.enabled = left > 0 && total < 100;
            if (GUI.Button(new Rect(tx, top.y + 90, tw, 50), total >= 100 ? "Patron devrildi!" : left > 0 ? $"SALDIR  <size=13>({left}/{Alliance.BossTriesPerDay} hak)</size>" : "Bugünkü haklar bitti", new GUIStyle(UiSkin.ButtonRed) { richText = true }))
            { SoundFx.Play("click"); Alliance.BossUseTry(d); GameServices.Save.Save(); flow.PlayMode(new AllianceBossMode()); }
            GUI.enabled = true;

            // Sandıklar
            float y = top.yMax + 10, cw = (area.width - 18) / 4;
            for (int i = 0; i < Alliance.BossMilestones.Length; i++)
            {
                var r = new Rect(area.x + i * (cw + 6), y, cw, 84);
                bool ready = Alliance.MilestoneReady(d, i), got = Alliance.MilestoneClaimed(d, i);
                GUI.Box(r, "", ready ? UiSkin.CardGold : UiSkin.Card);
                var rw = Alliance.MilestoneReward(d, i);
                GUI.Label(new Rect(r.x, r.y + 4, r.width, 22), $"<b>%{Alliance.BossMilestones[i]}</b>", ModeUi.Center);
                GUI.Label(new Rect(r.x + 2, r.y + 26, r.width - 4, 30), $"<size=11><color=#9fe8ff>{rw.gems} elmas</color>\n{UiSkin.Short(rw.gold)} altın</size>", ModeUi.Small);
                if (got) GUI.Label(new Rect(r.x, r.yMax - 26, r.width, 22), "<size=12><color=#8be39d>Alındı</color></size>", ModeUi.Small);
                else if (ready && GUI.Button(new Rect(r.x + 6, r.yMax - 30, r.width - 12, 26), "AL", new GUIStyle(UiSkin.ButtonGold) { fontSize = 13 }))
                {
                    Alliance.ClaimMilestone(d, i); ModeUi.Grant(rw, "alliance"); GameServices.Save.Save(); SoundFx.Play("chest");
                    flow.ShowToast($"%{Alliance.BossMilestones[i]} sandığı: " + ModeUi.RewardLine(rw).Replace("<color=#9fe8ff>", "").Replace("</color>", ""));
                }
                else if (!ready) GUI.Label(new Rect(r.x, r.yMax - 26, r.width, 22), "<size=11><color=#7d8696>Kilitli</color></size>", ModeUi.Small);
                if (ready) Dot(r);
            }
            if (d.allyBossMine == 0) GUI.Label(new Rect(area.x, y + 88, area.width, 20), "<size=11><color=#ffb02e>Patrona hiç vurmazsan sandıkların yarısını alırsın.</color></size>", Sm);

            // Sıralama
            float ly = y + 112;
            GUI.Label(new Rect(area.x, ly, area.width, 24), "<b>Bu haftaki hasar sıralaması</b>", Lb);
            var rank = Alliance.BossRanking(d);
            var inner = new Rect(area.x, ly + 28, area.width, area.yMax - ly - 28);
            float rowH = 32;
            scroll = GUI.BeginScrollView(inner, scroll, new Rect(0, 0, inner.width - 18, rank.Count * rowH));
            for (int i = 0; i < rank.Count; i++)
            {
                var e = rank[i]; var r = new Rect(0, i * rowH, inner.width - 18, rowH - 3);
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, e.me ? new Color(1, .82f, .25f, .3f) : new Color(1, 1, 1, .05f), 0, 6);
                GUI.Label(new Rect(r.x + 8, r.y, r.width - 100, r.height), $"<b>{i + 1}.</b> {(e.me ? "<color=#ffe27a>Sen</color>" : e.name)}", Sm);
                GUI.Label(new Rect(r.xMax - 90, r.y, 84, r.height), $"<b>%{e.pct:0.0}</b>", new GUIStyle(Sm) { alignment = TextAnchor.MiddleRight });
            }
            GUI.EndScrollView();
        }

        // ---------------- Üyeler ----------------
        static void MembersTab(Rect area, SaveData d)
        {
            var mem = Alliance.MembersOf(d);
            float rowH = 50;
            scroll = GUI.BeginScrollView(area, scroll, new Rect(0, 0, area.width - 18, mem.Count * rowH));
            string[] roles = { "Üye", "Subay", "Lider" }; string[] rc = { "#c9d3e0", "#9fd0ff", "#ffd23f" };
            for (int i = 0; i < mem.Count; i++)
            {
                var m = mem[i]; var r = new Rect(0, i * rowH, area.width - 18, rowH - 6);
                GUI.Box(r, "", m.me ? UiSkin.CardGold : UiSkin.Card);
                string on = m.me ? "<color=#8be39d>çevrimiçi</color>" : m.activity > .8f ? "<color=#8be39d>çevrimiçi</color>" : m.activity > .5f ? $"<color=#7d8696>{1 + (int)((1 - m.activity) * 10)} sa önce</color>" : "<color=#7d8696>dün</color>";
                GUI.Label(new Rect(r.x + 12, r.y + 3, r.width - 150, r.height - 6), $"<b>{(m.me ? "Sen" : m.name)}</b>  <size=11><color={rc[m.role]}>{roles[m.role]}</color> · {on}</size>", Sm);
                GUI.Label(new Rect(r.xMax - 140, r.y + 3, 128, r.height - 6), $"<size=12>Güç</size> <b>{m.power:N0}</b>", new GUIStyle(Sm) { alignment = TextAnchor.MiddleRight });
            }
            GUI.EndScrollView();
        }

        // ---------------- Mağaza ----------------
        static void ShopTab(GameFlow flow, Rect area, SaveData d)
        {
            GUI.Label(new Rect(area.x, area.y, area.width, 24), $"<b>Katkı puanın: <color=#ffd23f>{d.allyCoins}</color></b>  <size=12>bağış, yardım ve patrondan kazanılır · haftalık sınırlar pazartesi yenilenir</size>", Lb);
            float y = area.y + 34;
            for (int i = 0; i < Alliance.Shop.Length; i++)
            {
                var it = Alliance.Shop[i]; var r = new Rect(area.x, y, area.width, 78);
                GUI.Box(r, "", UiSkin.Card);
                int price = Alliance.PriceOf(d, it), bought = Alliance.Bought(d, i);
                GUI.Label(new Rect(r.x + 14, r.y + 8, r.width - 170, 26), $"<b>{it.name}</b>", Lb);
                GUI.Label(new Rect(r.x + 14, r.y + 36, r.width - 170, 36), $"{it.desc} · bu hafta {bought}/{it.weekly}", Sm);
                GUI.enabled = Alliance.CanBuy(d, i);
                if (GUI.Button(new Rect(r.xMax - 150, r.y + 14, 136, 50), bought >= it.weekly ? "Tükendi" : $"{price} katkı", new GUIStyle(UiSkin.ButtonGold) { fontSize = 15 }))
                {
                    var s = Alliance.Buy(d, i, Grant, rnd);
                    if (s != null) { GameServices.Save.Save(); SoundFx.Play("chest"); flow.ShowToast(s); }
                }
                GUI.enabled = true;
                y += 86;
            }
        }
    }
}
