using System;
using SuruSavasi.Core.Battle;

namespace SuruSavasi.Core.Meta
{
    /// <summary>Kahraman yetenek ağacı: her kahramanın 3 dalı, her dalda 2 geliştirme (3'er kademe) + 1 zirve yeteneği.
    /// Puan: kahraman seviyesi başına 2 (en çok 20). Toplam 21 kademe olduğu için hepsi alınamaz: oyuncu tarz seçer.
    /// Zirve yeteneği için o dala en az 3 puan harcanmış olmalı. Sıfırlama ilk seferde ücretsiz, sonra elmasla.</summary>
    public static class Talents
    {
        public const int Nodes = 9, ResetGems = 50;

        public class Node
        {
            public string name, desc, icon; public int max; public float per;
            public Action<BattleMods, float> apply;   // (mods, toplam etki = kademe × per)
        }

        public class Tree { public string[] branches; public string[] colors; public Node[] nodes; }

        static Node N(string name, string icon, int max, float per, string desc, Action<BattleMods, float> a) =>
            new Node { name = name, icon = icon, max = max, per = per, desc = desc, apply = a };

        // Düğüm sırası: dal0 (0,1,2=zirve), dal1 (3,4,5=zirve), dal2 (6,7,8=zirve)
        public static readonly Tree[] Trees =
        {
            // Ayaz: saldırı ve komuta
            new Tree
            {
                branches = new[] { "Ateş Gücü", "Komuta", "Dayanıklılık" }, colors = new[] { "#ff8a5a", "#ffd23f", "#8be39d" },
                nodes = new[]
                {
                    N("Keskin Nişan", "◎", 3, .06f, "Kademe başına +%6 mermi hasarı", (m, v) => m.dmgMul *= 1 + v),
                    N("Seri Atış", "»", 3, .05f, "Kademe başına %5 daha hızlı ateş", (m, v) => m.fireMul *= 1 - v),
                    N("Mermi Yağmuru", "✦", 1, 1, "Zirve: mermiler +1 düşmanı deler", (m, v) => m.pierce += (int)v),
                    N("Toplanma", "▲", 3, .06f, "Kademe başına +%6 başlangıç askeri", (m, v) => m.startMul += v),
                    N("Fıçı Ustası", "◆", 3, .08f, "Kademe başına fıçılardan +%8 asker", (m, v) => m.barrelMul += v),
                    N("Savaş Narası", "!", 1, 4, "Zirve: yetenek 4 sn hızlı ateş de verir", (m, v) => m.rapidOnSkill += v),
                    N("Kalın Zırh", "■", 3, .05f, "Kademe başına %5 daha az hasar", (m, v) => m.guard *= 1 - v),
                    N("İlk Yardım", "+", 3, .08f, "Kademe başına +%8 kahraman canı", (m, v) => m.heroHpMul += v),
                    N("Son Direniş", "♥", 1, .01f, "Zirve: kahraman saniyede %1 can yeniler", (m, v) => m.regen += v),
                }
            },
            // Kaya: kale ve istihkâm
            new Tree
            {
                branches = new[] { "Kale", "İstihkâm", "Diriliş" }, colors = new[] { "#9fe8ff", "#ffb02e", "#8be39d" },
                nodes = new[]
                {
                    N("Kaya Kalkan", "■", 3, .06f, "Kademe başına %6 daha az hasar", (m, v) => m.guard *= 1 - v),
                    N("Dev Gövde", "+", 3, .1f, "Kademe başına +%10 kahraman canı", (m, v) => m.heroHpMul += v),
                    N("Taş Deri", "◈", 1, .12f, "Zirve: alınan hasar %12 daha azalır", (m, v) => m.guard *= 1 - v),
                    N("Kule Mühendisi", "♜", 3, .15f, "Kademe başına savunma kulesi hasarı +%15", (m, v) => m.turretMul += v),
                    N("Barikat", "▬", 3, .1f, "Kademe başına tabelalar %10 daha az asker götürür", (m, v) => m.boardMul *= 1 - v),
                    N("Kule Ustası", "♖", 1, 1, "Zirve: Hayatta Kalma'ya hazır kuleyle başla", (m, v) => m.startTurret += (int)v),
                    N("Toparlanma", "↺", 3, .03f, "Kademe başına yetenekte +%3 asker geri gelir", (m, v) => m.heal += v),
                    N("Soğukkanlılık", "◇", 3, .05f, "Kademe başına yetenek %5 daha sık", (m, v) => m.skillCd *= 1 - v),
                    N("Ölümsüz", "♥", 1, .012f, "Zirve: kahraman saniyede %1,2 can yeniler", (m, v) => m.regen += v),
                }
            },
            // Mira: büyü ve simya
            new Tree
            {
                branches = new[] { "Büyü", "Simya", "Koruma" }, colors = new[] { "#c9b8ff", "#ffe27a", "#7fe8ff" },
                nodes = new[]
                {
                    N("Güç Akışı", "✧", 3, .15f, "Kademe başına yetenek hasarı +%15", (m, v) => m.skillMul *= 1 + v),
                    N("Hızlı Büyü", "◇", 3, .06f, "Kademe başına yetenek %6 daha sık", (m, v) => m.skillCd *= 1 - v),
                    N("Fırtına", "ϟ", 1, 3, "Zirve: yetenek etkisi 3 sn uzar", (m, v) => m.buffs += v),
                    N("Fıçı Simyası", "◆", 3, .08f, "Kademe başına fıçılardan +%8 asker", (m, v) => m.barrelMul += v),
                    N("Altın Kokusu", "$", 3, .05f, "Kademe başına +%5 altın ödülü", (m, v) => m.goldMul += v),
                    N("Altın Dokunuş", "★", 1, .15f, "Zirve: demir ve yiyecek ödülü +%15", (m, v) => m.marketMul += v),
                    N("Işık Kalkanı", "◈", 3, .05f, "Kademe başına %5 daha az hasar", (m, v) => m.guard *= 1 - v),
                    N("Şifa Işığı", "↺", 3, .004f, "Kademe başına saniyede +%0,4 can yenileme", (m, v) => m.regen += v),
                    N("Koruyucu Ruh", "♥", 1, .25f, "Zirve: +%25 kahraman canı", (m, v) => m.heroHpMul += v),
                }
            },
            // Deniz: taretler ve istihkâm
            new Tree
            {
                branches = new[] { "Taretçi", "Saha İşleri", "Dayanıklılık" }, colors = new[] { "#ffb02e", "#9fe8ff", "#8be39d" },
                nodes = new[]
                {
                    N("Kalibre", "♜", 3, .15f, "Kademe başına taret ve kule hasarı +%15", (m, v) => m.turretMul += v),
                    N("Uzun Mesai", "◇", 3, 2, "Kademe başına taretler 2 sn daha uzun kalır", (m, v) => m.skillTurret += v),
                    N("Kale Kulesi", "♖", 1, 1, "Zirve: Hayatta Kalma'ya +1 hazır kuleyle başla", (m, v) => m.startTurret += (int)v),
                    N("Barikat Ustası", "▬", 3, .1f, "Kademe başına tabelalar %10 daha az asker götürür", (m, v) => m.boardMul *= 1 - v),
                    N("Fıçı Tamiri", "◆", 3, .08f, "Kademe başına fıçılardan +%8 asker", (m, v) => m.barrelMul += v),
                    N("Seri Üretim", "»", 1, .15f, "Zirve: yetenek %15 daha sık", (m, v) => m.skillCd *= 1 - v),
                    N("Çelik Yelek", "■", 3, .05f, "Kademe başına %5 daha az hasar", (m, v) => m.guard *= 1 - v),
                    N("Saha Hekimi", "+", 3, .08f, "Kademe başına +%8 kahraman canı", (m, v) => m.heroHpMul += v),
                    N("Onarım Kiti", "↺", 1, .01f, "Zirve: kahraman saniyede %1 can yeniler", (m, v) => m.regen += v),
                }
            },
            // Cem: şans ve kapılar
            new Tree
            {
                branches = new[] { "Şans", "Kapı Ustası", "Blöf" }, colors = new[] { "#ffd23f", "#5ad1ff", "#ff6f91" },
                nodes = new[]
                {
                    N("Hileli Zar", "♦", 3, .06f, "Kademe başına yetenek %6 daha sık", (m, v) => m.skillCd *= 1 - v),
                    N("Büyük Bahis", "✧", 3, .15f, "Kademe başına yetenek hasarı +%15", (m, v) => m.skillMul *= 1 + v),
                    N("Şanslı Gün", "★", 1, 2, "Zirve: zar etkileri 2 sn uzar", (m, v) => m.buffs += v),
                    N("Kapı Bekçisi", "▲", 3, .1f, "Kademe başına iyi kapılar %10 daha güçlü", (m, v) => m.gateBoost += v),
                    N("Sigorta", "■", 3, .12f, "Kademe başına kötü kapıların zararı %12 azalır", (m, v) => m.badGateMul *= 1 - v),
                    N("Çifte Kâr", "$", 1, .2f, "Zirve: altın ödülü +%20", (m, v) => m.goldMul += v),
                    N("Poker Yüzü", "◈", 3, .05f, "Kademe başına %5 daha az hasar", (m, v) => m.guard *= 1 - v),
                    N("Keskin Göz", "◎", 3, .06f, "Kademe başına +%6 mermi hasarı", (m, v) => m.dmgMul *= 1 + v),
                    N("As Kartı", "♠", 1, .2f, "Zirve: +%20 başlangıç askeri", (m, v) => m.startMul += v),
                }
            },
        };

        static int Base(int hero) => hero * Nodes;
        public static int Rank(SaveData d, int hero, int node) { Ensure(d); return d.talents[Base(hero) + node]; }
        public static bool IsCap(int node) => node % 3 == 2;
        public static int Total(SaveData d, int hero) => Progress.HeroLevel(d, hero) * 2;
        public static int Spent(SaveData d, int hero) { Ensure(d); int s = 0; for (int i = 0; i < Nodes; i++) s += d.talents[Base(hero) + i]; return s; }
        public static int Free(SaveData d, int hero) => Math.Max(0, Total(d, hero) - Spent(d, hero));
        public static int BranchSpent(SaveData d, int hero, int branch) => Rank(d, hero, branch * 3) + Rank(d, hero, branch * 3 + 1);

        public static void Ensure(SaveData d)
        {
            int need = Trees.Length * Nodes;
            if (d.talents == null || d.talents.Length < need) { var t = new int[need]; d.talents?.CopyTo(t, 0); d.talents = t; }
        }

        /// <summary>Geliştirilebilir mi? Neden olmadığını da söyler.</summary>
        public static bool CanUp(SaveData d, int hero, int node, out string why)
        {
            var n = Trees[hero].nodes[node];
            why = null;
            if (Rank(d, hero, node) >= n.max) { why = "En üst kademe"; return false; }
            if (Free(d, hero) <= 0) { why = "Puan yok: kahramanı yükselt"; return false; }
            if (IsCap(node) && BranchSpent(d, hero, node / 3) < 3) { why = "Bu dala önce 3 puan ver"; return false; }
            return true;
        }

        public static bool Up(SaveData d, int hero, int node)
        {
            if (!CanUp(d, hero, node, out _)) return false;
            d.talents[Base(hero) + node]++;
            return true;
        }

        /// <summary>Denge simülasyonu için: boş puanları sırayla (hasar ve asker dalları önce) dağıtır.</summary>
        public static void AutoSpend(SaveData d, int hero)
        {
            int[] order = { 0, 3, 1, 4, 2, 5, 6, 7, 8 };
            for (int guard = 0; guard < 40 && Free(d, hero) > 0; guard++)
            {
                bool any = false;
                foreach (int nd in order) if (Free(d, hero) > 0 && Up(d, hero, nd)) any = true;
                if (!any) break;
            }
        }

        public static bool ResetFree(SaveData d) => !d.talentResetUsed;
        public static void Reset(SaveData d, int hero) { Ensure(d); for (int i = 0; i < Nodes; i++) d.talents[Base(hero) + i] = 0; d.talentResetUsed = true; }

        public static void Apply(SaveData d, int hero, BattleMods m)
        {
            if (hero < 0 || hero >= Trees.Length) return;
            Ensure(d);
            var t = Trees[hero];
            for (int i = 0; i < Nodes; i++) { int r = d.talents[Base(hero) + i]; if (r > 0) t.nodes[i].apply(m, r * t.nodes[i].per); }
        }
    }
}
