using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Üs ekranı (geçici liste görünümü): binalar, inşaat, hızlandırma, üretim toplama, pazar takası.</summary>
    public static class BaseScreen
    {
        static Vector2 scroll;
        static float nextTick;

        static string Fmt(float s)
        {
            int t = Mathf.CeilToInt(s), h = t / 3600, m = t % 3600 / 60, sec = t % 60;
            return h > 0 ? $"{h}:{m:00}:{sec:00}" : $"{m}:{sec:00}";
        }

        static bool Spend(string res, int amount)
        {
            if (amount <= 0) return true;
            var c = res == "gold" ? Currency.Gold : res == "iron" ? Currency.Iron : Currency.Food;
            return GameServices.Wallet.Spend(c, amount, "building");
        }

        public static void Draw(GameFlow flow, float x0, float w, GUIStyle label, GUIStyle small, GUIStyle btn, GUIStyle card)
        {
            var D = GameServices.Save.Data;
            var Wl = GameServices.Wallet;
            if (Time.unscaledTime > nextTick)
            {
                nextTick = Time.unscaledTime + 1f;
                BaseLogic.TickProduction(D);
                var done = BaseLogic.Complete(D);
                if (done.HasValue) { GameServices.Save.Save(); flow.ShowToast($"{BaseLogic.Def(done.Value).name} seviye {Progress.Level(D, done.Value)} oldu!"); }
            }

            if (GUI.Button(new Rect(x0, 48, 90, 40), "‹ Geri", btn)) { flow.Show(GameScreen.Title); GameServices.Save.Save(); return; }
            GUI.Label(new Rect(x0 + 100, 48, w - 100, 40), $"<b>Üs</b>  (yıldız: {Progress.TotalStars(D)})", label);

            // İnşaat durumu
            float y = 96;
            GUI.Box(new Rect(x0, y, w, 86), "", card);
            if (BaseLogic.Building(D))
            {
                var k = (BuildingKind)D.buildKind; float rem = BaseLogic.Remaining(D); int sc = BaseLogic.SpeedCost(D);
                GUI.Label(new Rect(x0 + 10, y + 4, w - 20, 26), $"İnşaat: <b>{BaseLogic.Def(k).name}</b> → Sv. {D.buildFrom + 1} · <color=#ffd23f>{Fmt(rem)}</color>", label);
                float bw = (w - 30) / 2;
                if (GUI.Button(new Rect(x0 + 10, y + 36, bw, 42), sc == 0 ? "Bitir (bedava)" : $"Bitir: {sc} elmas", new GUIStyle(btn) { fontSize = 15 }))
                {
                    if (sc == 0 || Wl.Spend(Currency.Diamonds, sc, "speed_up")) { BaseLogic.FinishNow(D); nextTick = 0; }
                    else flow.ShowToast("Yeterli elmas yok");
                }
                if (GUI.Button(new Rect(x0 + 20 + bw, y + 36, bw, 42), "Reklam izle: −5 dk", new GUIStyle(btn) { fontSize = 15 }))
                    Shop.WatchAd(AdPlacement.SpeedUpBuild, () => { BaseLogic.Shorten(D, 300); nextTick = 0; GameServices.Save.Save(); }, () => flow.ShowToast("Reklam hazır değil"));
            }
            else GUI.Label(new Rect(x0 + 10, y + 4, w - 20, 78), "İnşaatçı boşta. Aşağıdan bir bina yükselt.\n<size=13>Aynı anda tek bina yükselir.</size>", label);

            // Üretim
            y += 94;
            GUI.Box(new Rect(x0, y, w, 76), "", card);
            GUI.Label(new Rect(x0 + 10, y + 4, w - 20, 24),
                $"Üretim (dk): <color=#ffd23f>{BaseLogic.RateGold(D):0.#} altın</color> · <color=#cfd8e0>{BaseLogic.RateIron(D):0.#} demir</color> · <color=#8be39d>{BaseLogic.RateFood(D):0.#} yiyecek</color>", small);
            GUI.Label(new Rect(x0 + 10, y + 28, w * .6f, 44),
                $"Biriken: {Mathf.FloorToInt(D.pendGold)} altın, {Mathf.FloorToInt(D.pendIron)} demir, {Mathf.FloorToInt(D.pendFood)} yiyecek\n<size=12>En çok 8 saatlik birikir</size>", small);
            if (GUI.Button(new Rect(x0 + w * .62f, y + 26, w * .36f, 42), "Topla", btn))
            {
                var (g, i, f) = BaseLogic.Collect(D);
                Wl.Add(Currency.Gold, g, "production"); Wl.Add(Currency.Iron, i, "production"); Wl.Add(Currency.Food, f, "production");
                flow.ShowToast(g + i + f > 0 ? $"+{g} altın, +{i} demir, +{f} yiyecek" : "Henüz birikmedi");
            }

            // Binalar
            y += 84;
            float ch = 132;
            scroll = GUI.BeginScrollView(new Rect(x0, y, w + 16, 800 - y - 6), scroll, new Rect(0, 0, w, BaseLogic.Defs.Length * (ch + 8) + 120));
            float cy = 0;
            foreach (var def in BaseLogic.Defs)
            {
                var k = def.kind; int L = Progress.Level(D, k); float e = Progress.Eff(D, k);
                GUI.Box(new Rect(0, cy, w, ch), "", card);
                GUI.Label(new Rect(10, cy + 4, w - 20, 26), $"<b>{def.name}</b>  <color=#ffd23f>Sv. {L}/{Progress.BuildingMax}</color>", label);
                string next = L < Progress.BuildingMax ? $"  →  {def.effect(BuildingData.EffectiveLevel(L + 1))}" : "";
                GUI.Label(new Rect(10, cy + 30, w - 20, 24), $"{def.effect(e)}{next}", small);
                string ex = def.extra(L >= 10 ? 2 : L >= 5 ? 1 : 0);
                if (!string.IsNullOrEmpty(ex)) GUI.Label(new Rect(10, cy + 52, w - 20, 22), $"<color=#c9b8ff>5. ve 10. seviye eki: {def.extra(1)}</color>", new GUIStyle(small) { fontSize = 12 });

                if (L < Progress.BuildingMax)
                {
                    int cg = BaseLogic.CostGold(k, L), ci = BaseLogic.CostIron(k, L), cf = BaseLogic.CostFood(k, L), rs = BaseLogic.ReqStars(L);
                    string cost = (cg > 0 ? $"{cg} altın " : "") + (ci > 0 ? $"{ci} demir " : "") + (cf > 0 ? $"{cf} yiyecek " : "") + $"· {Fmt(BaseLogic.Duration(L))}" + (rs > 0 ? $" · {rs}★" : "");
                    GUI.Label(new Rect(10, cy + 74, w - 20, 22), cost, new GUIStyle(small) { fontSize = 13 });
                    var chk = BaseLogic.Check(D, k);
                    GUI.enabled = chk == BaseLogic.UpResult.Ok;
                    string bt = chk == BaseLogic.UpResult.Ok ? "Yükselt" : chk == BaseLogic.UpResult.Busy ? "İnşaatçı meşgul" : chk == BaseLogic.UpResult.Stars ? $"{rs} yıldız gerekli" : "Kaynak yetersiz";
                    if (GUI.Button(new Rect(10, cy + 96, w - 20, 32), bt, new GUIStyle(btn) { fontSize = 15 }))
                    {
                        if (BaseLogic.Start(D, k, Spend))
                        {
                            GameServices.Save.Save(); flow.ShowToast($"{def.name} yükseltiliyor");
                            GameServices.Analytics?.LogEvent(Services.AnalyticsEvent.BuildingUpgrade, new System.Collections.Generic.Dictionary<string, object> { { "building", k.ToString() }, { "to", L + 1 } });
                        }
                    }
                    GUI.enabled = true;
                }
                else GUI.Label(new Rect(10, cy + 96, w - 20, 30), "<color=#8be39d>En üst seviye</color>", label);

                cy += ch + 8;
            }

            // Pazar takası
            if (Progress.Level(D, BuildingKind.Market) > 0)
            {
                float rt = BaseLogic.TradeRate(D);
                GUI.Box(new Rect(0, cy, w, 110), "", card);
                GUI.Label(new Rect(10, cy + 4, w - 20, 26), $"<b>Pazar takası</b>  (100 → {Mathf.FloorToInt(100 * rt)})", label);
                string[] names = { "Altın→Demir", "Altın→Yiyecek", "Demir→Altın", "Yiyecek→Altın" };
                Currency[,] pairs = { { Currency.Gold, Currency.Iron }, { Currency.Gold, Currency.Food }, { Currency.Iron, Currency.Gold }, { Currency.Food, Currency.Gold } };
                float bw = (w - 30) / 2;
                for (int i = 0; i < 4; i++)
                {
                    var r = new Rect(10 + (i % 2) * (bw + 10), cy + 32 + (i / 2) * 38, bw, 34);
                    if (GUI.Button(r, names[i], new GUIStyle(btn) { fontSize = 14 }))
                    {
                        if (Wl.Spend(pairs[i, 0], 100, "trade")) { Wl.Add(pairs[i, 1], Mathf.FloorToInt(100 * rt), "trade"); }
                        else flow.ShowToast("100 kaynak gerekli");
                    }
                }
            }
            GUI.EndScrollView();
        }
    }
}
