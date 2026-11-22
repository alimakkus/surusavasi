using System.Collections.Generic;
using SuruSavasi.Core;
using SuruSavasi.Core.Battle;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Hayatta Kalma: dalga dalga gelen zombiler. Öldürülen her zombi para verir; dalga aralarında
    /// mağazadan asker, hasar, yol kenarına savunma kulesi ve şifa alınır. Ordu tükenince koşu biter.</summary>
    public class SurvivalMode : IBattleMode
    {
        static SaveData D => GameServices.Save.Data;
        readonly int lv;
        int wave, lastKills, coins, granted;
        bool shop, waveOn;
        float gap = 1.6f;
        readonly int[] bought = new int[4];
        readonly System.Random rnd = new System.Random();
        BattleSim sim;
        string endText = ""; bool reviving;

        public SurvivalMode() { lv = Modes.SurvLevel(D); }

        public string Id => "survival";
        public string Title => "Hayatta Kalma";
        public string Intro => "DALGALAR GELİYOR!";
        public int Level => lv;
        public float HardMul => 1;
        public float TimeLimit => 0;
        public bool Paused => shop;
        public void Configure(BattleMods m) { }

        public void Setup(BattleSim s)
        {
            sim = s;
            foreach (var o in s.objs) o.done = true;
            s.objs.Clear();
            s.endScroll = float.MaxValue;   // patron yok, dalgalar sonsuz
            s.turretL = s.mods.startTurret;  // Kaya'nın "Kule Ustası" zirvesi
            s.turretDmg = Balance.StartSoldiers(lv) * Balance.DmgPer(lv) * .3f;
        }

        public string Hud(BattleSim s) => $"Dalga <b>{Mathf.Max(1, wave)}</b>   <color=#ffd23f>Para {coins}</color>" + (s.turretL + s.turretR > 0 ? $"   Kule {s.turretL + s.turretR}" : "");

        public void Tick(BattleSim s, float dt)
        {
            if (shop) return;
            if (s.kills > lastKills) { coins += (s.kills - lastKills) * Modes.KillCoins(Mathf.Max(1, wave)); lastKills = s.kills; }
            if (!waveOn)
            {
                gap -= dt;
                if (gap <= 0) SpawnWave(s, ++wave);
                return;
            }
            bool alive = false;
            foreach (var o in s.objs) if (!o.done && o.kind == ObjKind.Enemy) { alive = true; break; }
            if (!alive)
            {
                waveOn = false;
                int bonus = Modes.WaveBonus(wave);
                coins += bonus;
                s.AddFloat(Field.W / 2, Field.SQY - 120, $"Dalga {wave} temizlendi! +{bonus} para", "#ffd23f");
                // Toplanmamış fıçılar kaybolur, mağaza açılır
                foreach (var o in s.objs) if (o.kind == ObjKind.Barrel) o.done = true;
                shop = true; SoundFx.Play("coin");
            }
        }

        void SpawnWave(BattleSim s, int w)
        {
            waveOn = true;
            float H = Modes.WaveHard(lv, w);
            int row = Modes.WaveRow(lv, w), cnt = Modes.WaveCount(w);
            var types = LevelGen.TypesFor(lv);
            float baseY = -s.scroll - 70;
            int r = 0;
            for (int k = 0; k < cnt; r++)
            {
                // Her sırada 1-3 düşman, şeritler karışık
                int inRow = Mathf.Min(cnt - k, 1 + rnd.Next(3));
                var lanes = new List<int> { 0, 1, 2 };
                for (int j = 0; j < inRow; j++)
                {
                    int li = rnd.Next(lanes.Count); int lane = lanes[li]; lanes.RemoveAt(li);
                    string tid = types[rnd.Next(types.Count)];
                    s.objs.Add(LevelGen.MakeEnemy(lv, row, tid, H, Field.Lanes[lane], baseY - r * 115, rnd.NextDouble()));
                    k++;
                }
                // Arada bir fıçı: ordu büyüyebilsin
                if (r % 2 == 1) s.objs.Add(LevelGen.MakeBarrel(lv, row, Field.Lanes[rnd.Next(3)], baseY - r * 115 - 57, rnd.NextDouble()));
            }
            if (w % 5 == 0)
            {
                string big = types.Contains("giant") ? "giant" : types.Contains("brute") ? "brute" : types.Contains("armored") ? "armored" : "walker";
                s.objs.Add(LevelGen.MakeEnemy(lv, row, big, H, Field.W / 2, baseY - (r + 1) * 115, rnd.NextDouble(), 5f));
            }
            s.turretDmg = Balance.StartSoldiers(lv) * Balance.DmgPer(lv) * .3f * Mathf.Pow(1.06f, w - 1);
            SoundFx.Play("wave");
            BattleView.Current?.Banner($"DALGA {w}", w % 5 == 0 ? "Dev zombi geliyor!" : $"{cnt} zombi yaklaşıyor", w % 5 == 0 ? "#ff8a80" : "#ffffff", 1.6f);
        }

        // ---------------- Dalga arası mağaza ----------------
        public bool DrawOverlay(BattleView v, float VW)
        {
            if (!shop || !v.Sim.Live) return false;
            var s = v.Sim;
            float pw = Mathf.Min(380, VW - 20), px = VW / 2 - pw / 2, py = 170;
            GUI.Box(new Rect(px, py, pw, 440), "", UiSkin.CardGold);
            GUI.Label(new Rect(px, py + 10, pw, 40), $"<color=#8be39d>Dalga {wave} temizlendi!</color>", ModeUi.CenterBig);
            GUI.Label(new Rect(px, py + 48, pw, 26), $"Para: <b><color=#ffd23f>{coins}</color></b>    Ordu: <b>{BattleSim.Fmt(s.n)}</b>    Can: %{s.hhp / Mathf.Max(1, s.HeroMax) * 100:0}", ModeUi.Center);
            float y = py + 82;
            for (int i = 0; i < 4; i++)
            {
                int price = Modes.ShopPrice(i, bought[i]);
                bool maxed = i == 2 && s.turretL + s.turretR >= 8;
                bool can = coins >= price && !maxed && !(i == 3 && s.hhp >= s.HeroMax);
                string desc = i == 0 ? $"+{Modes.ArmyGain(lv, wave)} asker" : i == 1 ? "+%15 mermi hasarı" : i == 2 ? (maxed ? "En üst seviye" : (s.turretL + s.turretR == 0 ? "Yol kenarına kule kur" : "Kuleyi güçlendir")) : "Kahraman canı tamamen dolar";
                var r = new Rect(px + 16, y, pw - 32, 60);
                GUI.enabled = can;
                if (GUI.Button(r, "", can ? UiSkin.ButtonGray : UiSkin.ButtonGray)) Buy(s, i, price);
                GUI.enabled = true;
                GUI.Label(new Rect(r.x + 14, r.y + 6, r.width - 110, 26), $"<b>{Modes.ShopNames[i]}</b>" + (bought[i] > 0 ? $"  <size=12>(x{bought[i]})</size>" : ""), new GUIStyle(UiSkin.Label) { richText = true });
                GUI.Label(new Rect(r.x + 14, r.y + 30, r.width - 110, 24), desc, new GUIStyle(UiSkin.Small) { richText = true });
                GUI.Label(new Rect(r.xMax - 96, r.y + 6, 86, 46), maxed ? "—" : $"<b><color={(coins >= price ? "#ffd23f" : "#ff8a80")}>{price}</color></b>\n<size=11>para</size>", ModeUi.Center);
                y += 68;
            }
            if (GUI.Button(new Rect(px + 16, py + 360, pw - 32, 62), $"▶ DALGA {wave + 1}", UiSkin.ButtonGreen))
            { SoundFx.Play("click"); shop = false; gap = .6f; }
            return true;
        }

        void Buy(BattleSim s, int i, int price)
        {
            coins -= price; bought[i]++;
            SoundFx.Play(i == 3 ? "power" : "coin");
            switch (i)
            {
                case 0: s.n += Modes.ArmyGain(lv, wave); if (s.n > s.peak) s.peak = s.n; break;
                case 1: s.mods.dmgMul *= 1.15f; break;
                case 2: if (s.turretL <= s.turretR) s.turretL++; else s.turretR++; break;
                case 3: s.hhp = s.HeroMax; break;
            }
        }

        // ---------------- Sonuç ----------------
        public void OnEnd(BattleSim s)
        {
            var d = D;
            int waves = Mathf.Max(0, wave - 1);
            if (waves > granted)
            {
                // İkinci şanstan sonra tekrar bitince sadece yeni dalgaların ödülü verilir
                int prevBest = d.survBest;
                var all = Modes.SurvReward(d, waves, s.mods);
                var prev = granted > 0 ? Modes.SurvReward(new SaveData { survBest = 999, unlocked = d.unlocked }, granted, s.mods) : new LevelReward();
                var rw = new LevelReward { gold = all.gold - prev.gold, iron = all.iron - prev.iron, food = all.food - prev.food, gems = all.gems };
                ModeUi.Grant(rw, "survival");
                string shard = all.gems > 0 ? HeroShards.GiveText(d, 5) : "";
                League.Add(d, (waves - granted) * League.PtsSurvWave);
                Daily.Add(d, "surv_wave", waves - granted);
                endText = $"Ulaşılan dalga: <b>{waves}</b>   Rekor: <b>{d.survBest}</b>" + (waves > prevBest ? "  <color=#ffd23f>Yeni rekor!</color>" : "") + "\n" + ModeUi.RewardLine(rw) + shard;
                granted = waves;
                Daily.Add(d, "clear");
            }
            else if (string.IsNullOrEmpty(endText)) endText = "İlk dalgayı geçemedin. Asker toplamak için fıçıları vur!";
            GameServices.Save.Save();
        }

        public void DrawResult(BattleView v, float VW)
        {
            var d = D;
            float pw = Mathf.Min(380, VW - 20), px = VW / 2 - pw / 2, py = 210;
            GUI.Box(new Rect(px, py, pw, 330), "", UiSkin.Card);
            GUI.Label(new Rect(px, py + 12, pw, 40), "<color=#ff8a80>Ordun düştü</color>", ModeUi.CenterBig);
            GUI.Label(new Rect(px + 12, py + 56, pw - 24, 70), endText, ModeUi.Small);
            float by = py + 132;
            var s = v.Sim;
            if (!reviving && s.CanRevive && GUI.Button(new Rect(px + 20, by, pw - 40, 54), "▶ İkinci şans (reklam)", UiSkin.ButtonGold))
            {
                reviving = true;
                Shop.WatchAd(AdPlacement.SecondChance, () => { reviving = false; s.Revive(); }, () => reviving = false);
            }
            by += 64;
            int free = Modes.SurvFreeLeft(d);
            if (free > 0 && GUI.Button(new Rect(px + 20, by, pw - 40, 50), $"Yeni koşu  <size=13>({free} ücretsiz)</size>", UiSkin.ButtonGreen))
            { Modes.SurvStart(d, "free"); GameServices.Save.Save(); v.StartMode(new SurvivalMode()); return; }
            by += 60;
            if (GUI.Button(new Rect(px + 20, by, (pw - 50) / 2, 50), "Modlar", UiSkin.Button)) ModeUi.ToModes(v);
            if (GUI.Button(new Rect(px + 30 + (pw - 50) / 2, by, (pw - 50) / 2, 50), "Üsse dön", UiSkin.Button)) v.GoMenu();
        }
    }
}
