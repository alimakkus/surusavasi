using System;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Elmas mağazası ve Günlük (giriş ödülü, görevler, etkinlik, aylık kart) ekranları.</summary>
    public static class MetaScreens
    {
        static Vector2 shopScroll, dailyScroll;
        public static int shopTab; // 0 elmas, 1 kaynak, 2 tema

        static SaveData D => GameServices.Save.Data;
        static Wallet Wl => GameServices.Wallet;

        static string Rew(int g, int i, int f, int e)
        {
            string s = "";
            if (g > 0) s += $"<color=#ffd23f>{g} altın</color> ";
            if (i > 0) s += $"<color=#cfd8e0>{i} demir</color> ";
            if (f > 0) s += $"<color=#8be39d>{f} yiyecek</color> ";
            if (e > 0) s += $"<color=#9fe8ff>{e} elmas</color>";
            return s.Trim();
        }

        static void Grant(int g, int i, int f, int e, string why)
        {
            Wl.Add(Currency.Gold, g, why); Wl.Add(Currency.Iron, i, why); Wl.Add(Currency.Food, f, why); Wl.Add(Currency.Diamonds, e, why);
            GameServices.Save.Save(); SoundFx.Play("pickup");
        }

        static Texture2D Icon(string name) => Resources.Load<Texture2D>("Art/" + name); // görseller gelince otomatik görünür

        // ---------------- Mağaza ----------------
        public static void Shop(GameFlow flow, float x0, float w)
        {
            var label = UiSkin.Label; var small = UiSkin.Small; var btn = UiSkin.Button; var card = UiSkin.Card;
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", btn)) { flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), "<b>Mağaza</b>", label);

            float tw = (w - 20) / 3;
            if (GUI.Button(new Rect(x0, 96, tw, 40), "Elmas", shopTab == 0 ? UiSkin.ButtonGold : btn)) shopTab = 0;
            if (GUI.Button(new Rect(x0 + tw + 10, 96, tw, 40), "Kaynak", shopTab == 1 ? UiSkin.ButtonGold : btn)) shopTab = 1;
            if (GUI.Button(new Rect(x0 + 2 * tw + 20, 96, tw, 40), "Tema", shopTab == 2 ? UiSkin.ButtonGold : btn)) shopTab = 2;
            GUI.Label(new Rect(x0 + w - 150, 48, 150, 40), $"<color=#9fe8ff><b>Elmas {Wl.Get(Currency.Diamonds)}</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleRight });

            var store = GameServices.Store;
            float y0 = 146, ch = 86;
            shopScroll = GUI.BeginScrollView(new Rect(x0, y0, w + 16, 800 - y0 - 10), shopScroll, new Rect(0, 0, w, 1100));
            float y = 0;

            if (shopTab == 0)
            {
                // Bedava elmas (ödüllü reklam)
                var Dd = Daily.Ensure(D); int left = Daily.FreeAdsPerDay - Dd.adsToday;
                GUI.Box(new Rect(0, y, w, ch), "", card);
                GUI.Label(new Rect(14, y + 8, w - 170, 30), $"<b>Bedava elmas</b>", label);
                GUI.Label(new Rect(14, y + 40, w - 170, 30), $"Reklam izle, <color=#9fe8ff>{Daily.FreeAdGems} elmas</color> al · bugün {left} hak", small);
                GUI.enabled = left > 0;
                if (GUI.Button(new Rect(w - 150, y + 18, 136, 50), "▶ İzle", UiSkin.ButtonGold))
                    Services.Shop.WatchAd(AdPlacement.FreeDiamonds, () => { Dd.adsToday++; Grant(0, 0, 0, Daily.FreeAdGems, "ad_free_gems"); flow.ShowToast($"+{Daily.FreeAdGems} elmas"); }, () => flow.ShowToast("Reklam hazır değil"));
                GUI.enabled = true;
                y += ch + 10;

                if (!D.starterPackBought) { y = Product(flow, Catalog.StarterPack, "Başlangıç paketi", "500 elmas, reklamsız oyun · tek seferlik", "shop_starter", y, w, true); }
                y = Product(flow, Catalog.MonthlyCard, "Aylık kart",
                    Daily.MonthlyActive(D) ? $"Aktif · {Daily.MonthlyDaysLeft(D)} gün kaldı" : $"Hemen 300 elmas + 30 gün her gün {Daily.MonthlyDailyGems} elmas", "shop_monthly", y, w, true);
                string[] ids = { Catalog.Diamonds100, Catalog.Diamonds550, Catalog.Diamonds1200, Catalog.Diamonds2600, Catalog.Diamonds7000 };
                string[] extra = { "", "+%10 bonus", "+%20 bonus", "+%30 bonus", "En çok tercih edilen" };
                for (int i = 0; i < ids.Length; i++)
                {
                    var def = Catalog.Find(ids[i]);
                    y = Product(flow, ids[i], $"{def.diamonds} elmas", extra[i], $"shop_gems_{i + 1}", y, w, false);
                }
                if (!D.noAds) y = Product(flow, Catalog.NoAds, "Reklamsız", "Bölüm aralarındaki reklamlar kalkar. Ödüllü reklamlar isteğe bağlı kalır.", "shop_noads", y, w, false);

                if (GUI.Button(new Rect(0, y, w, 44), "Satın alımları geri yükle", new GUIStyle(btn) { fontSize = 14 }))
                    store?.RestorePurchases(ok => flow.ShowToast(ok ? "Satın alımlar kontrol edildi" : "Mağazaya bağlanılamadı"));
                y += 54;
                GUI.Label(new Rect(0, y, w, 60), $"<size=12>Mağaza: {store?.Name ?? "yok"}. Fiyatlar mağazadan gelir; Editor'de satın alma sahtedir, para çekilmez.</size>", small);
            }
            else if (shopTab == 2) y = ThemeTab(flow, y, w);
            else
            {
                int lv = Mathf.Clamp(D.unlocked, 1, 100);
                (string name, Currency c, int amount, int cost)[] packs =
                {
                    ("Altın", Currency.Gold, 300 + 30 * lv, 20), ("Altın (büyük)", Currency.Gold, (300 + 30 * lv) * 6, 100),
                    ("Demir", Currency.Iron, 200 + 20 * lv, 20), ("Demir (büyük)", Currency.Iron, (200 + 20 * lv) * 6, 100),
                    ("Yiyecek", Currency.Food, 250 + 25 * lv, 20), ("Yiyecek (büyük)", Currency.Food, (250 + 25 * lv) * 6, 100),
                };
                foreach (var p in packs)
                {
                    GUI.Box(new Rect(0, y, w, 74), "", card);
                    string col = p.c == Currency.Gold ? "#ffd23f" : p.c == Currency.Iron ? "#cfd8e0" : "#8be39d";
                    GUI.Label(new Rect(14, y + 6, w - 170, 30), $"<b>{p.name}</b>", label);
                    GUI.Label(new Rect(14, y + 36, w - 170, 30), $"<color={col}>+{p.amount}</color>", label);
                    if (GUI.Button(new Rect(w - 150, y + 12, 136, 50), $"{p.cost} elmas", btn))
                    {
                        if (Wl.Spend(Currency.Diamonds, p.cost, "buy_resource")) { Wl.Add(p.c, p.amount, "gem_shop"); GameServices.Save.Save(); SoundFx.Play("pickup"); flow.ShowToast($"+{p.amount} {p.name.ToLower()}"); }
                        else flow.ShowToast("Yeterli elmas yok");
                    }
                    y += 84;
                }
                GUI.Label(new Rect(0, y, w, 40), "<size=13>Paket miktarı açtığın bölümle büyür.</size>", small);
            }
            GUI.EndScrollView();
        }

        // Üs temaları: elmasla satın al, seç
        static float ThemeTab(GameFlow flow, float y, float w)
        {
            var label = UiSkin.Label; var small = UiSkin.Small; float h = 132;
            GUI.Label(new Rect(0, y, w, 30), "<size=13>Üssünün çevresini değiştir. Tema sadece görünümü değiştirir.</size>", small);
            y += 34;
            foreach (var th in Themes.All)
            {
                bool owned = Themes.Owned(D, th.id), active = D.theme == th.id;
                GUI.Box(new Rect(0, y, w, h), "", UiSkin.Card);
                if (active) GUI.DrawTexture(new Rect(4, y + 4, 6, h - 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f), 0, 3);
                // Önizleme: zeminin köy kısmı
                var tex = Resources.Load<Texture2D>("Art/" + th.ground);
                if (tex != null) GUI.DrawTextureWithTexCoords(new Rect(16, y + 10, 112, 112), tex, new Rect(.175f, .3f, .65f, .4333f));
                GUI.Label(new Rect(140, y + 10, w - 290, 30), $"<b>{th.name}</b>", label);
                GUI.Label(new Rect(140, y + 42, w - 290, 80), th.desc, small);
                var r = new Rect(w - 144, y + 40, 132, 52);
                if (active) GUI.Label(r, "<color=#8be39d><b>Kullanılıyor</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
                else if (owned)
                {
                    if (GUI.Button(r, "Seç", UiSkin.Button)) { D.theme = th.id; GameServices.Save.Save(); SoundFx.Play("click"); flow.ShowToast($"{th.name} seçildi"); }
                }
                else if (GUI.Button(r, $"{th.price} elmas", UiSkin.ButtonGold))
                {
                    if (Wl.Spend(Currency.Diamonds, th.price, "buy_theme_" + th.id))
                    {
                        if (!D.ownedThemes.Contains(th.id)) D.ownedThemes.Add(th.id);
                        D.theme = th.id; GameServices.Save.Save(); SoundFx.Play("power"); flow.ShowToast($"{th.name} açıldı!");
                        GameServices.Analytics?.LogEvent("theme_buy", new System.Collections.Generic.Dictionary<string, object> { { "theme", th.id }, { "price", th.price } });
                    }
                    else flow.ShowToast("Yeterli elmas yok");
                }
                y += h + 10;
            }
            return y;
        }

        static float Product(GameFlow flow, string id, string title, string desc, string icon, float y, float w, bool highlight)
        {
            var store = GameServices.Store; var def = Catalog.Find(id);
            float h = 92;
            GUI.Box(new Rect(0, y, w, h), "", UiSkin.Card);
            if (highlight) GUI.DrawTexture(new Rect(4, y + 4, 6, h - 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f), 0, 3);
            var tex = Icon(icon); float tx = 14;
            if (tex != null) { GUI.DrawTexture(new Rect(14, y + 10, 72, 72), tex, ScaleMode.ScaleToFit); tx = 94; }
            GUI.Label(new Rect(tx, y + 8, w - tx - 150, 30), $"<b>{title}</b>", UiSkin.Label);
            GUI.Label(new Rect(tx, y + 38, w - tx - 150, 48), desc, UiSkin.Small);
            string price = def != null && !string.IsNullOrEmpty(def.localizedPrice) ? def.localizedPrice : "—";
            bool ready = store != null && store.IsReady;
            GUI.enabled = ready;
            if (GUI.Button(new Rect(w - 150, y + 20, 136, 52), ready ? price : "Hazır değil", highlight ? UiSkin.ButtonGold : UiSkin.Button))
                Services.Shop.Buy(id, ok =>
                {
                    if (ok) { SoundFx.Play("power"); flow.ShowToast($"{title} alındı!"); }
                    else flow.ShowToast("Satın alma tamamlanmadı");
                });
            GUI.enabled = true;
            return y + h + 10;
        }

        // ---------------- Günlük ----------------
        public static void DailyScreen(GameFlow flow, float x0, float w)
        {
            var label = UiSkin.Label; var small = UiSkin.Small; var btn = UiSkin.Button; var card = UiSkin.Card;
            var Dd = Daily.Ensure(D);
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", btn)) { flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), "<b>Günlük</b>", label);

            float y0 = 96;
            dailyScroll = GUI.BeginScrollView(new Rect(x0, y0, w + 16, 800 - y0 - 10), dailyScroll, new Rect(0, 0, w, 1000));
            float y = 0;

            // Etkinlik
            var ev = Daily.CurrentEvent(); var left = Daily.EventLeft();
            GUI.Box(new Rect(0, y, w, 70), "", card);
            GUI.Label(new Rect(14, y + 6, w - 28, 30), $"<color=#ffd23f><b>Etkinlik: {ev.name}</b></color>", label);
            GUI.Label(new Rect(14, y + 36, w - 28, 28), $"{ev.desc} · bitmesine {(left.Days > 0 ? left.Days + "g " : "")}{left.Hours}s {left.Minutes}dk", small);
            y += 80;

            // Giriş ödülü
            GUI.Label(new Rect(0, y, w, 30), "<b>Giriş ödülü</b>", label); y += 32;
            bool can = Daily.CanLogin(D); int now = Daily.LoginIndex(D), done = can ? now : Dd.streak;
            float bw = (w - 6 * 6) / 7;
            for (int i = 0; i < 7; i++)
            {
                var r = new Rect(i * (bw + 6), y, bw, 84);
                GUI.Box(r, "", card);
                if (can && i == now) GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f, .35f), 0, 8);
                string mark = i < done ? "<color=#8be39d>✓</color>" : $"{i + 1}";
                GUI.Label(new Rect(r.x, r.y + 4, r.width, 24), mark, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
                string rs = Daily.Login[i, 0] > 0 ? $"<color=#ffd23f>{Daily.Login[i, 0]}\naltın</color>" : Daily.Login[i, 1] > 0 ? $"<color=#cfd8e0>{Daily.Login[i, 1]}\ndemir</color>" :
                            Daily.Login[i, 2] > 0 ? $"<color=#8be39d>{Daily.Login[i, 2]}\nyiyecek</color>" : $"<color=#9fe8ff>{Daily.Login[i, 3]}\nelmas</color>";
                GUI.Label(new Rect(r.x, r.y + 28, r.width, 52), rs, new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontSize = 12 });
            }
            y += 92;
            GUI.enabled = can;
            if (GUI.Button(new Rect(0, y, w, 48), can ? "Bugünkü ödülü al" : "Bugünkü ödül alındı, yarın gel", can ? UiSkin.ButtonGold : btn))
            {
                Dd.streak = Dd.last == Daily.Yesterday ? Dd.streak % 7 + 1 : 1; Dd.last = Daily.Today;
                int k = Dd.streak - 1; Grant(Daily.Login[k, 0], Daily.Login[k, 1], Daily.Login[k, 2], Daily.Login[k, 3], "login");
                flow.ShowToast($"{Dd.streak}. gün ödülü alındı");
            }
            GUI.enabled = true;
            y += 60;

            // Aylık kart
            if (Daily.MonthlyActive(D))
            {
                bool mc = Daily.CanClaimMonthly(D);
                GUI.Box(new Rect(0, y, w, 64), "", card);
                GUI.Label(new Rect(14, y + 4, w - 170, 56), $"<b>Aylık kart</b>\n<size=13>{Daily.MonthlyDaysLeft(D)} gün kaldı</size>", label);
                GUI.enabled = mc;
                if (GUI.Button(new Rect(w - 150, y + 8, 136, 48), mc ? $"{Daily.MonthlyDailyGems} elmas al" : "Alındı", mc ? UiSkin.ButtonGold : btn))
                { Dd.monthlyClaim = Daily.Today; Grant(0, 0, 0, Daily.MonthlyDailyGems, "monthly_card"); }
                GUI.enabled = true;
                y += 74;
            }

            // Görevler
            GUI.Label(new Rect(0, y, w, 30), "<b>Günlük görevler</b>", label); y += 32;
            var (qg, qe) = Daily.QuestReward(D);
            foreach (var q in Dd.quests)
            {
                var def = Daily.Def(q.id); bool fin = Daily.QuestDone(q);
                GUI.Box(new Rect(0, y, w, 84), "", card);
                GUI.Label(new Rect(14, y + 6, w - 170, 28), string.Format(def.text, def.n), label);
                float pw = w - 180, pr = Mathf.Clamp01(q.p / (float)def.n);
                GUI.DrawTexture(new Rect(14, y + 40, pw, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(0, 0, 0, .5f), 0, 5);
                GUI.DrawTexture(new Rect(14, y + 40, Mathf.Max(10, pw * pr), 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, fin ? new Color(.55f, .89f, .62f) : new Color(1, .82f, .25f), 0, 5);
                GUI.Label(new Rect(14, y + 54, w - 170, 26), $"{q.p}/{def.n} · {Rew(qg, 0, 0, qe)}", new GUIStyle(small) { fontSize = 13 });
                GUI.enabled = fin && !q.c;
                if (GUI.Button(new Rect(w - 150, y + 16, 136, 50), q.c ? "Alındı" : fin ? "Al" : "Devam", fin && !q.c ? UiSkin.ButtonGold : btn))
                { q.c = true; Grant(qg, 0, 0, qe, "quest:" + q.id); }
                GUI.enabled = true;
                y += 94;
            }
            bool all = Daily.AllClaimed(D);
            GUI.enabled = all && !Dd.bonus;
            if (GUI.Button(new Rect(0, y, w, 50), Dd.bonus ? "Bonus alındı" : $"Üç görev bonusu: {Daily.BonusGems} elmas, {Daily.BonusFood} yiyecek", all && !Dd.bonus ? UiSkin.ButtonGold : btn))
            { Dd.bonus = true; Grant(0, 0, Daily.BonusFood, Daily.BonusGems, "quest_bonus"); }
            GUI.enabled = true;
            GUI.EndScrollView();
        }
    }
}
