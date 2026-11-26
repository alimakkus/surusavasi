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
        public static int shopTab = 4; // 0 elmas, 1 kaynak, 2 tema, 3 görünüm, 4 teklif

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
        /// <summary>Mağaza "Teklif" sekmesi: haftalık takvim şeridi + etkin teklifler (ilk alım, hafta sonu, kahraman paketi, hız paketi, başlangıç).</summary>
        static float OfferTab(GameFlow flow, float y, float w)
        {
            var now = System.DateTime.Now;
            // Takvim şeridi: Pazartesi-Pazar, bugün çerçeveli, hafta sonu yeşil
            string[] dn = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };
            float dw = (w - 36) / 7;
            int today = ((int)now.DayOfWeek + 6) % 7;
            for (int i = 0; i < 7; i++)
            {
                var r = new Rect(i * (dw + 6), y, dw, 62);
                bool we = i >= 5, td = i == today;
                GUI.Box(r, "", td ? UiSkin.CardGold : UiSkin.Card);
                GUI.Label(new Rect(r.x, r.y + 4, r.width, 20), $"<b>{dn[i]}</b>", new GUIStyle(UiSkin.Small) { alignment = TextAnchor.MiddleCenter, richText = true });
                string tag = i == 0 ? "<color=#ffb02e>Yeni\npaket</color>" : we ? "<color=#8be39d>+%50\nelmas</color>" : "<color=#7d8696>Hız\npaketi</color>";
                GUI.Label(new Rect(r.x, r.y + 24, r.width, 34), $"<size=10>{tag}</size>", new GUIStyle(UiSkin.Small) { alignment = TextAnchor.MiddleCenter, richText = true });
            }
            y += 72;

            // İlk alım 2 kat
            if (Packs.FirstDouble(D))
            {
                var r = new Rect(0, y, w, 74); GUI.Box(r, "", UiSkin.CardGold);
                GUI.Label(new Rect(14, y + 8, w - 170, 28), "<b><color=#ff6f91>İLK ALIMA 2 KAT ELMAS</color></b>", new GUIStyle(UiSkin.Label) { richText = true });
                GUI.Label(new Rect(14, y + 36, w - 170, 32), "<size=12>İlk elmas paketinde iki katı elmas alırsın. Tek seferlik.</size>", new GUIStyle(UiSkin.Small) { richText = true });
                if (GUI.Button(new Rect(w - 150, y + 12, 136, 50), "Paketlere bak", UiSkin.ButtonGold)) shopTab = 0;
                y += 84;
            }
            // Hafta sonu
            {
                bool we = Packs.Weekend(now); var left = Packs.WeekendLeft(now);
                var r = new Rect(0, y, w, 74); GUI.Box(r, "", we ? UiSkin.CardGold : UiSkin.Card);
                GUI.Label(new Rect(14, y + 8, w - 170, 28), we ? "<b><color=#8be39d>HAFTA SONU: +%50 ELMAS</color></b>" : "<b>Hafta sonu bonusu</b>", new GUIStyle(UiSkin.Label) { richText = true });
                GUI.Label(new Rect(14, y + 36, w - 170, 32), we ? $"<size=12>Tüm elmas paketlerinde. Bitişe {(int)left.TotalHours}sa {left.Minutes}dk</size>" : $"<size=12>Cumartesi başlıyor: {(int)left.TotalDays}g {left.Hours}sa</size>", new GUIStyle(UiSkin.Small) { richText = true });
                if (we && GUI.Button(new Rect(w - 150, y + 12, 136, 50), "Paketler", UiSkin.ButtonGold)) shopTab = 0;
                y += 84;
            }
            // Haftalık kahraman paketi
            {
                int t = Packs.HeroPackTarget(D);
                var r = new Rect(0, y, w, 150); GUI.Box(r, "", UiSkin.CardGold);
                var (sh, ss) = Packs.HeroPackSkin(D);
                var port = t >= 0 ? Art.HeroPortrait(t, Mathf.FloorToInt(Time.time * 1.6f), 0) : sh >= 0 ? Art.HeroPortrait(sh, Mathf.FloorToInt(Time.time * 1.6f), ss) : null;
                GUI.DrawTexture(new Rect(10, y + 10, 120, 130), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.25f, .18f, .06f, .9f), 0, 10);
                if (port != null) GUI.DrawTexture(new Rect(0, y + 6, 140, 140), port, ScaleMode.ScaleToFit);
                var left = League.Left(now);
                GUI.Label(new Rect(144, y + 8, w - 154, 26), "<b><color=#ffb02e>HAFTALIK KAHRAMAN PAKETİ</color></b>", new GUIStyle(UiSkin.Label) { richText = true, fontSize = 15 });
                GUI.Label(new Rect(144, y + 36, w - 154, 44), $"<size=13>{Packs.HeroPackDesc(D)}</size>", new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true });
                GUI.Label(new Rect(144, y + 78, w - 154, 20), $"<size=11>Haftada 1 · yenilenmesine {(int)left.TotalDays}g {left.Hours}sa</size>", new GUIStyle(UiSkin.Small) { richText = true });
                bool bought = Packs.HeroPackBought(D);
                var def = Catalog.Find(Catalog.HeroPack); var store = GameServices.Store; bool ready = store != null && store.IsReady;
                GUI.enabled = !bought && ready;
                if (GUI.Button(new Rect(144, y + 98, w - 158, 44), bought ? "Bu hafta alındı" : (def?.localizedPrice ?? "—"), UiSkin.ButtonGold))
                    Services.Shop.Buy(Catalog.HeroPack, ok => { if (ok) { SoundFx.Play("chest"); flow.ShowToast(Services.Shop.LastPackText + $" + {Packs.HeroPackGems} elmas"); } else flow.ShowToast("Satın alma tamamlanmadı"); });
                GUI.enabled = true;
                y += 160;
            }
            // Hız paketi
            {
                var r = new Rect(0, y, w, 92); GUI.Box(r, "", UiSkin.Card);
                GUI.Label(new Rect(14, y + 8, w - 170, 28), "<b>Hız Paketi</b>  <size=12><color=#9fe8ff>elinde " + D.speedTokens + " hızlandırıcı</color></size>", new GUIStyle(UiSkin.Label) { richText = true });
                GUI.Label(new Rect(14, y + 36, w - 170, 50), $"<size=12>{Packs.SpeedPackTokens} hızlandırıcı (her biri inşaatı 1 saat kısaltır) + {Packs.SpeedPackGems} elmas · günde 1</size>", new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true });
                bool bought = Packs.SpeedPackBought(D);
                var def = Catalog.Find(Catalog.SpeedPack); var store = GameServices.Store; bool ready = store != null && store.IsReady;
                GUI.enabled = !bought && ready;
                if (GUI.Button(new Rect(w - 150, y + 20, 136, 52), bought ? "Yarın" : (def?.localizedPrice ?? "—"), UiSkin.Button))
                    Services.Shop.Buy(Catalog.SpeedPack, ok => { if (ok) { SoundFx.Play("coin"); flow.ShowToast(Services.Shop.LastPackText); } else flow.ShowToast("Satın alma tamamlanmadı"); });
                GUI.enabled = true;
                y += 102;
            }
            if (!D.starterPackBought) y = Product(flow, Catalog.StarterPack, "Başlangıç paketi", "500 elmas, reklamsız oyun · tek seferlik", "shop_starter", y, w, true);
            return y;
        }

        /// <summary>Mağaza "Görünüm" sekmesi: haftanın görünümü vitrini (%40 indirim) + kahraman başına görünüm durumu.</summary>
        static float SkinTab(GameFlow flow, float y, float w)
        {
            var (dh, ds) = Skins.WeeklyDeal();
            var H = Progress.Heroes[dh];
            bool open = Progress.HeroOpen(D, dh), owned = Skins.Owned(D, dh, ds);
            var r = new Rect(0, y, w, 320);
            GUI.Box(r, "", UiSkin.CardGold);
            var rib = new Rect(16, y + 10, w - 32, 32); GUI.Box(rib, "", UiSkin.Ribbon);
            GUI.Label(rib, "<b>HAFTANIN GÖRÜNÜMÜ  ·  %40 İNDİRİM</b>", new GUIStyle(UiSkin.Label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 15 });
            var left = League.Left(System.DateTime.Now);
            string[] bg = { "#2a3550", "#5a4512", "#101836", "#5a1420" };
            ColorUtility.TryParseHtmlString(bg[ds], out var bc);
            GUI.DrawTexture(new Rect(16, y + 50, 180, 200), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, bc, 0, 12);
            var port = Art.HeroPortrait(dh, Mathf.FloorToInt(Time.time * 1.6f), ds);
            if (port != null) GUI.DrawTexture(new Rect(6, y + 40 + Mathf.Sin(Time.time * 2.2f) * 3, 200, 200), port, ScaleMode.ScaleToFit);
            float tx = 210, tw2 = w - 224;
            GUI.Label(new Rect(tx, y + 56, tw2, 30), $"<b><size=20>{H.name} · {Skins.Names[ds]}</size></b>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(tx, y + 88, tw2, 60), $"<size=13>{Skins.Descs[ds]}</size>", new GUIStyle(UiSkin.Small) { richText = true, wordWrap = true });
            GUI.Label(new Rect(tx, y + 150, tw2, 30), $"<color=#ff8a80><size=14>{Skins.Price[ds]} yerine</size></color>  <color=#9fe8ff><b><size=22>{Skins.PriceFor(dh, ds)}</size> elmas</b></color>", new GUIStyle(UiSkin.Label) { richText = true });
            GUI.Label(new Rect(tx, y + 182, tw2, 22), $"<size=12>Bitişe {(int)left.TotalDays}g {left.Hours}sa</size>", new GUIStyle(UiSkin.Small) { richText = true });
            var br = new Rect(16, y + 260, w - 32, 50);
            if (owned) { GUI.enabled = false; GUI.Button(br, "Bu görünüme sahipsin", UiSkin.ButtonGray); GUI.enabled = true; }
            else if (!open) { GUI.enabled = false; GUI.Button(br, $"Önce {H.name} kahramanını aç", UiSkin.ButtonGray); GUI.enabled = true; }
            else if (GUI.Button(br, $"SATIN AL VE KUŞAN  <size=14>({Skins.PriceFor(dh, ds)} elmas)</size>", UiSkin.ButtonGold))
            {
                if (Wl.Spend(Currency.Diamonds, Skins.PriceFor(dh, ds), $"skin_deal:{H.id}_{Skins.Ids[ds]}"))
                { Skins.Give(D, dh, ds); Skins.Equip(D, dh, ds); GameServices.Save.Save(); SoundFx.Play("chest"); flow.ShowToast($"{H.name}: {Skins.Names[ds]} görünümü!"); }
                else flow.ShowToast("Yeterli elmas yok");
            }
            y += 330;
            // Kahraman başına görünümler
            for (int i = 0; i < Progress.Heroes.Length; i++)
            {
                var hr = new Rect(0, y, w, 84);
                GUI.Box(hr, "", UiSkin.Card);
                bool ho = Progress.HeroOpen(D, i);
                for (int s = 0; s < Skins.Ids.Length; s++)
                {
                    var p2 = Art.HeroPortrait(i, 0, s);
                    var old = GUI.color; if (!Skins.Owned(D, i, s)) GUI.color = new Color(1, 1, 1, .35f);
                    if (p2 != null) GUI.DrawTexture(new Rect(4 + s * 46, y + 10, 64, 64), p2, ScaleMode.ScaleToFit);
                    GUI.color = old;
                }
                GUI.Label(new Rect(196, y + 10, w - 330, 26), $"<b>{Progress.Heroes[i].name}</b>", new GUIStyle(UiSkin.Label) { richText = true });
                GUI.Label(new Rect(196, y + 38, w - 330, 30), $"<size=12>{Skins.OwnedCount(D, i)}/{Skins.Ids.Length} görünüm</size>", new GUIStyle(UiSkin.Small) { richText = true });
                GUI.enabled = ho;
                if (GUI.Button(new Rect(w - 130, y + 18, 116, 48), ho ? "Görünümler" : "Kilitli", new GUIStyle(UiSkin.Button) { fontSize = 14 })) SkinScreen.Open(flow, i);
                GUI.enabled = true;
                y += 92;
            }
            return y;
        }

        public static void Shop(GameFlow flow, float x0, float w)
        {
            var label = UiSkin.Label; var small = UiSkin.Small; var btn = UiSkin.Button; var card = UiSkin.Card;
            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", btn)) { flow.Show(GameScreen.Base); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), "<b>Mağaza</b>", label);

            float tw = (w - 24) / 5;
            int[] order = { 4, 0, 1, 2, 3 }; string[] names = { "Teklif", "Elmas", "Kaynak", "Tema", "Görünüm" };
            for (int ti = 0; ti < 5; ti++)
            {
                int t = order[ti];
                var tr = new Rect(x0 + ti * (tw + 6), 96, tw, 40);
                if (GUI.Button(tr, names[ti], new GUIStyle(shopTab == t ? UiSkin.ButtonGold : btn) { fontSize = 13 })) { shopTab = t; SoundFx.Play("click"); }
                if (t == 4 && Packs.AnyNew(D) && shopTab != 4) { float pu = 1 + .15f * Mathf.Sin(Time.time * 6); GUI.DrawTexture(new Rect(tr.xMax - 10 - 5 * pu, tr.y - 4 - 5 * pu, 12 * pu, 12 * pu), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.88f, .27f, .23f), 0, 6 * pu); }
            }
            if (shopTab == 4) Packs.MarkSeen(D);
            GUI.Label(new Rect(x0 + w - 150, 48, 150, 40), $"<color=#9fe8ff><b>Elmas {Wl.Get(Currency.Diamonds)}</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleRight });

            var store = GameServices.Store;
            float y0 = 146, ch = 86;
            shopScroll = GUI.BeginScrollView(new Rect(x0, y0, w + 16, 800 - y0 - 10), shopScroll, new Rect(0, 0, w, shopTab == 2 ? 40 + Themes.All.Length * 142 : shopTab == 3 ? 340 + Progress.Heroes.Length * 92 : shopTab == 4 ? 900 : 1100));
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

                if (Offers.FounderActive(D, BaseLogic.Now))
                {
                    int fl = Mathf.CeilToInt(Offers.FounderSecondsLeft(D, BaseLogic.Now));
                    string inside = (D.builders < 2 ? "İkinci inşaatçı" : $"+{Offers.FounderBuilderGems} elmas") + $" + {Offers.FounderGems} elmas + Epik ekipman + {Offers.FounderGold(D)} altın";
                    y = Product(flow, Catalog.FounderPack, $"Kurucu Paketi  <color=#ff8a80><size=13>{fl / 3600}:{fl / 60 % 60:00}:{fl % 60:00}</size></color>", inside, "shop_founder", y, w, true);
                }
                // Kumbara
                {
                    float kh = 130; GUI.Box(new Rect(0, y, w, kh), "", card);
                    GUI.DrawTexture(new Rect(4, y + 4, 6, kh - 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .69f, .18f), 0, 3);
                    GUI.Label(new Rect(18, y + 6, w - 180, 30), $"<b>Kumbara</b>  <color=#9fe8ff>{D.piggy}/{Offers.PiggyCap} elmas</color>", label);
                    GUI.Label(new Rect(18, y + 36, w - 180, 40), "Her zaferde elmas birikir. Kır, hepsini al!", small);
                    float pf = D.piggy / (float)Offers.PiggyCap;
                    GUI.DrawTexture(new Rect(18, y + 76, w - 200, 10), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.05f, .07f, .1f, .9f), 0, 5);
                    GUI.DrawTexture(new Rect(19, y + 77, Mathf.Max(4, (w - 202) * pf), 8), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .69f, .18f), 0, 4);
                    var pdef = Catalog.Find(Catalog.PiggyBank); bool pready = store != null && store.IsReady && D.piggy >= Offers.PiggyMinOpen;
                    GUI.enabled = pready;
                    if (GUI.Button(new Rect(w - 150, y + 10, 136, 50), D.piggy >= Offers.PiggyMinOpen ? (pdef?.localizedPrice ?? "—") : $"En az {Offers.PiggyMinOpen}", UiSkin.ButtonGold))
                        Services.Shop.Buy(Catalog.PiggyBank, ok => flow.ShowToast(ok ? "Kumbara kırıldı!" : "Satın alma tamamlanmadı"));
                    GUI.enabled = D.piggy > 0 && Offers.AdLeft(D, "piggy") > 0;
                    if (GUI.Button(new Rect(w - 150, y + 68, 136, 50), $"▶ Reklam: {Mathf.Min(Offers.PiggyAdTake, D.piggy)} al", new GUIStyle(btn) { fontSize = 14 }))
                        Services.Shop.WatchAd(AdPlacement.PiggyPeek, () => { Offers.AdUsed(D, "piggy"); int n = Offers.PiggyTake(D, Offers.PiggyAdTake); Grant(0, 0, 0, n, "piggy_ad"); flow.ShowToast($"+{n} elmas"); }, () => flow.ShowToast("Reklam hazır değil"));
                    GUI.enabled = true;
                    y += kh + 10;
                }
                if (!D.starterPackBought) { y = Product(flow, Catalog.StarterPack, "Başlangıç paketi", "500 elmas, reklamsız oyun · tek seferlik", "shop_starter", y, w, true); }
                y = Product(flow, Catalog.MonthlyCard, "Aylık kart",
                    Daily.MonthlyActive(D) ? $"Aktif · {Daily.MonthlyDaysLeft(D)} gün kaldı" : $"Hemen 300 elmas + 30 gün her gün {Daily.MonthlyDailyGems} elmas", "shop_monthly", y, w, true);
                string[] ids = { Catalog.Diamonds100, Catalog.Diamonds550, Catalog.Diamonds1200, Catalog.Diamonds2600, Catalog.Diamonds7000 };
                string[] extra = { "", "+%10 bonus", "+%20 bonus", "+%30 bonus", "En çok tercih edilen" };
                for (int i = 0; i < ids.Length; i++)
                {
                    var def = Catalog.Find(ids[i]);
                    int got = Packs.DiamondsFor(D, def.diamonds, System.DateTime.Now);
                    string badge = Packs.FirstDouble(D) ? "  <color=#ff6f91><b>İLK ALIM 2 KAT!</b></color>" : Packs.Weekend(System.DateTime.Now) ? "  <color=#8be39d><b>Hafta sonu +%50</b></color>" : "";
                    y = Product(flow, ids[i], got > def.diamonds ? $"<color=#ff8a80>{def.diamonds}</color> → {got} elmas" : $"{def.diamonds} elmas", extra[i] + badge, $"shop_gems_{i + 1}", y, w, got > def.diamonds);
                }
                if (!D.noAds) y = Product(flow, Catalog.NoAds, "Reklamsız", "Bölüm aralarındaki reklamlar kalkar. Ödüllü reklamlar isteğe bağlı kalır.", "shop_noads", y, w, false);

                if (GUI.Button(new Rect(0, y, w, 44), "Satın alımları geri yükle", new GUIStyle(btn) { fontSize = 14 }))
                    store?.RestorePurchases(ok => flow.ShowToast(ok ? "Satın alımlar kontrol edildi" : "Mağazaya bağlanılamadı"));
                y += 54;
                GUI.Label(new Rect(0, y, w, 60), $"<size=12>Mağaza: {store?.Name ?? "yok"}. Fiyatlar mağazadan gelir; Editor'de satın alma sahtedir, para çekilmez.</size>", small);
            }
            else if (shopTab == 2) y = ThemeTab(flow, y, w);
            else if (shopTab == 3) y = SkinTab(flow, y, w);
            else if (shopTab == 4) y = OfferTab(flow, y, w);
            else
            {
                // İkinci inşaatçı (kalıcı)
                if (BaseLogic.Slots(D) < BaseLogic.MaxBuilders)
                {
                    GUI.Box(new Rect(0, y, w, 84), "", card);
                    GUI.DrawTexture(new Rect(4, y + 4, 6, 76), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(1, .82f, .25f), 0, 3);
                    GUI.Label(new Rect(18, y + 6, w - 180, 30), "<b>İkinci İnşaatçı</b>", label);
                    GUI.Label(new Rect(18, y + 36, w - 180, 44), "Aynı anda iki bina yükselt. Kalıcı.", small);
                    if (GUI.Button(new Rect(w - 150, y + 16, 136, 52), $"{BaseLogic.SecondBuilderGems} elmas", UiSkin.ButtonGold))
                    {
                        if (Wl.Spend(Currency.Diamonds, BaseLogic.SecondBuilderGems, "second_builder")) { D.builders = 2; GameServices.Save.Save(); SoundFx.Play("power"); flow.ShowToast("Yeni inşaatçı geldi!"); }
                        else flow.ShowToast("Yeterli elmas yok");
                    }
                    y += 94;
                }
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
                var r = new Rect(w - 144, y + 70, 132, 50);
                if (!active)
                {   // göz: temayı üste canlı uygular
                    var er = new Rect(w - 144, y + 12, 132, 50);
                    if (GUI.Button(er, "", UiSkin.Button)) { SoundFx.Play("click"); BaseMapView.StartPreview(th.id); flow.Show(GameScreen.Base); return y; }
                    if (UiSkin.Eye != null) GUI.DrawTexture(new Rect(er.x + 12, er.y + 9, 32, 32), UiSkin.Eye, ScaleMode.ScaleToFit);
                    GUI.Label(new Rect(er.x + 46, er.y, er.width - 50, er.height), "<b>Önizle</b>", new GUIStyle(label) { alignment = TextAnchor.MiddleLeft, fontSize = 15 });
                }
                if (active) GUI.Label(new Rect(w - 144, y + 40, 132, 52), "<color=#8be39d><b>Kullanılıyor</b></color>", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter });
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
            GUI.Label(new Rect(x0 + 100, 48, w - 260, 40), "<b>Günlük</b>", label);
            var abr = new Rect(x0 + w - 150, 46, 150, 44);
            if (GUI.Button(abr, $"Başarımlar\n<size=11>{Achievements.Score(D)}/{Achievements.All.Length * 3} rozet</size>", new GUIStyle(UiSkin.ButtonGold) { fontSize = 14, richText = true }))
            { SoundFx.Play("click"); flow.Show(GameScreen.Achievements); return; }
            if (Achievements.AnyClaim(D)) { float pu = 1 + .15f * Mathf.Sin(Time.time * 6); GUI.DrawTexture(new Rect(abr.xMax - 10 - 6 * pu, abr.y - 4 - 6 * pu, 14 * pu, 14 * pu), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, new Color(.88f, .27f, .23f), 0, 7 * pu); }

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
                int k = Dd.streak - 1; Grant(Daily.Login[k, 0], Daily.Login[k, 1], Daily.Login[k, 2], Daily.Login[k, 3], "login"); Season.OnEvent(D, "login");
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
                { q.c = true; Season.OnEvent(D, "quest"); Grant(qg, 0, 0, qe, "quest:" + q.id); }
                GUI.enabled = true;
                y += 94;
            }
            bool all = Daily.AllClaimed(D);
            GUI.enabled = all && !Dd.bonus;
            if (GUI.Button(new Rect(0, y, w, 50), Dd.bonus ? "Bonus alındı" : $"Tüm görevler bonusu: {Daily.BonusGems} elmas, {Daily.BonusFood} yiyecek", all && !Dd.bonus ? UiSkin.ButtonGold : btn))
            { Dd.bonus = true; Season.OnEvent(D, "quest_bonus"); Grant(0, 0, Daily.BonusFood, Daily.BonusGems, "quest_bonus"); }
            GUI.enabled = true;
            GUI.EndScrollView();
        }
    }
}
