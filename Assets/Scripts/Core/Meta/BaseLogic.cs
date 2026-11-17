using System;

namespace SuruSavasi.Core.Meta
{
    public class BuildingDef
    {
        public BuildingKind kind;
        public string name;
        public int gold, iron, food;   // 0. seviyeden 1'e maliyet tabanı
        public float g;                // maliyet büyümesi
        public Func<float, string> effect;
        public Func<int, string> extra; // 5. ve 10. seviye ek bonusu (kademe)
    }

    /// <summary>Üs kuralları: bina maliyeti, inşaat süresi, tek inşaatçı, hızlandırma, üretim ve takas.
    /// Prototipteki formüllerle aynı. Zaman UTC milisaniye.</summary>
    public static class BaseLogic
    {
        static string F1(float v) => v.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',');

        public static readonly BuildingDef[] Defs =
        {
            new BuildingDef { kind = BuildingKind.Farm, name = "Çiftlik", gold = 40, iron = 20, g = 1.5f, effect = e => $"Dakikada {F1(.8f * e)} yiyecek", extra = m => $"Kahraman canı +%{Math.Round(4f * m)}" },
            new BuildingDef { kind = BuildingKind.Mine, name = "Altın Madeni", iron = 30, food = 30, g = 1.5f, effect = e => $"Dakikada {F1(1f * e)} altın", extra = m => $"Zafer altını +%{Math.Round(4f * m)}" },
            new BuildingDef { kind = BuildingKind.Quarry, name = "Demir Ocağı", gold = 40, food = 20, g = 1.5f, effect = e => $"Dakikada {F1(.7f * e)} demir", extra = m => $"Yetenek bekleme −%{Math.Round(2f * m)}" },
            new BuildingDef { kind = BuildingKind.Armory, name = "Silahhane", gold = 80, iron = 50, g = 1.6f, effect = e => $"Ateş gücü +%{Math.Round(5 * e)}", extra = m => $"Yetenek +%{5 * m}, yeni silahlar açar" },
            new BuildingDef { kind = BuildingKind.Barracks, name = "Kışla", gold = 60, food = 40, g = 1.6f, effect = e => $"Başlangıç gücü +%{Math.Round(6 * e)}", extra = m => $"+{2 * m} asker" },
            new BuildingDef { kind = BuildingKind.Hospital, name = "Hastane", gold = 70, food = 50, g = 1.55f, effect = e => $"Savaşta can yenilenmesi %{F1(.2f * e)}/sn", extra = m => $"Kahraman canı +%{Math.Round(2f * m)}" },
            new BuildingDef { kind = BuildingKind.Tower, name = "Gözetleme Kulesi", gold = 60, iron = 60, g = 1.55f, effect = e => $"Ateş hızı +%{Math.Round(1.2f * e)}", extra = m => "" },
            new BuildingDef { kind = BuildingKind.Market, name = "Pazar", gold = 80, iron = 30, food = 30, g = 1.55f, effect = e => $"Takas oranı %{Math.Round(TradeRateFor(e) * 100)}", extra = m => $"Zafer demir/yiyeceği +%{Math.Round(1f * m)}" },
            new BuildingDef { kind = BuildingKind.Headquarters, name = "Karargâh", gold = 150, iron = 100, food = 100, g = 1.7f, effect = e => $"Diğer binalar en fazla Sv. {Progress.CapFromHq((int)Math.Round(e))}", extra = m => "" },
            new BuildingDef { kind = BuildingKind.Wall, name = "Kale Duvarı", iron = 60, food = 30, g = 1.6f, effect = e => $"Alınan hasar −%{Math.Round(3 * e)}", extra = m => $"Ek hasar −%{4 * m}" },
        };

        public static BuildingDef Def(BuildingKind k) { foreach (var d in Defs) if (d.kind == k) return d; return Defs[0]; }

        public static int Cost(int baseCost, float g, int L) => baseCost <= 0 ? 0 : (int)Math.Ceiling(baseCost * Math.Pow(1 + (g - 1) * .65, L));
        public static int CostGold(BuildingKind k, int L) { var d = Def(k); return Cost(d.gold, d.g, L); }
        public static int CostIron(BuildingKind k, int L) { var d = Def(k); return Cost(d.iron, d.g, L); }
        public static int CostFood(BuildingKind k, int L) { var d = Def(k); return Cost(d.food, d.g, L); }
        public static int Duration(int L) => BuildingData.BuildSeconds(L);
        public static int ReqStars(int L) => BuildingData.RequiredStars(L);

        public static long Now => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // ---------- İnşaatçılar (1 bedava, 2. elmasla) ----------
        public const int MaxBuilders = 2, SecondBuilderGems = 350;
        public static int Slots(SaveData d) => Math.Max(1, Math.Min(MaxBuilders, d.builders));
        public static int SlotKind(SaveData d, int s) => s == 0 ? d.buildKind : d.build2Kind;
        public static int SlotFrom(SaveData d, int s) => s == 0 ? d.buildFrom : d.build2From;
        public static long SlotEnd(SaveData d, int s) => s == 0 ? d.buildEnd : d.build2End;
        public static void SetSlot(SaveData d, int s, int kind, int from, long end)
        {
            if (s == 0) { d.buildKind = kind; d.buildFrom = from; d.buildEnd = end; }
            else { d.build2Kind = kind; d.build2From = from; d.build2End = end; }
        }
        public static void ClearSlot(SaveData d, int s) => SetSlot(d, s, -1, 0, 0);
        /// <summary>Bu binayı inşa eden inşaatçı (yoksa -1).</summary>
        public static int SlotOf(SaveData d, BuildingKind k) { for (int s = 0; s < MaxBuilders; s++) if (SlotKind(d, s) == (int)k) return s; return -1; }
        public static int FreeSlot(SaveData d) { for (int s = 0; s < Slots(d); s++) if (SlotKind(d, s) < 0) return s; return -1; }
        public static int BusyCount(SaveData d) { int n = 0; for (int s = 0; s < MaxBuilders; s++) if (SlotKind(d, s) >= 0) n++; return n; }
        public static bool Building(SaveData d) => BusyCount(d) > 0;
        public static bool Busy(SaveData d, BuildingKind k) => SlotOf(d, k) >= 0;
        public static float RemainingSlot(SaveData d, int s) => s < 0 || SlotKind(d, s) < 0 ? 0 : Math.Max(0, (SlotEnd(d, s) - Now) / 1000f);
        public static float Remaining(SaveData d, BuildingKind k) => RemainingSlot(d, SlotOf(d, k));

        public enum UpResult { Ok, MaxLevel, Busy, Stars, Resources, NeedHq }

        public static UpResult Check(SaveData d, BuildingKind k)
        {
            int L = Progress.Level(d, k);
            if (L >= Progress.MaxLevel(k)) return UpResult.MaxLevel;
            if (L >= Progress.LevelCap(d, k)) return UpResult.NeedHq;
            if (Busy(d, k) || FreeSlot(d) < 0) return UpResult.Busy;
            if (Progress.TotalStars(d) < ReqStars(L)) return UpResult.Stars;
            if (d.gold < CostGold(k, L) || d.iron < CostIron(k, L) || d.food < CostFood(k, L)) return UpResult.Resources;
            return UpResult.Ok;
        }

        /// <summary>İnşaatı boş inşaatçıyla başlatır, kaynakları düşer. Harcama Wallet üzerinden yapılmalı: spend(c, amount).</summary>
        public static bool Start(SaveData d, BuildingKind k, Func<string, int, bool> spend)
        {
            if (Check(d, k) != UpResult.Ok) return false;
            int L = Progress.Level(d, k), s = FreeSlot(d);
            TickProduction(d);
            if (!spend("gold", CostGold(k, L)) || !spend("iron", CostIron(k, L)) || !spend("food", CostFood(k, L))) return false;
            SetSlot(d, s, (int)k, L, Now + Duration(L) * 1000L);
            return true;
        }

        /// <summary>Süresi biten bir inşaatı tamamlar. Tamamlanan binayı döner, yoksa null.</summary>
        public static BuildingKind? Complete(SaveData d) => CompleteAt(d, Now, true);
        public static BuildingKind? CompleteAt(SaveData d, long nowMs, bool tickProduction)
        {
            for (int s = 0; s < MaxBuilders; s++)
            {
                int kind = SlotKind(d, s);
                if (kind < 0 || nowMs < SlotEnd(d, s)) continue;
                if (tickProduction) TickProduction(d);
                d.buildings[kind] = SlotFrom(d, s) + 1;
                ClearSlot(d, s);
                return (BuildingKind)kind;
            }
            return null;
        }

        /// <summary>Hızlandırma bedeli (elmas): son 15 saniye bedava, sonra kalan her dakika 1 elmas.</summary>
        public static int SpeedCost(SaveData d, BuildingKind k) { float rem = Remaining(d, k); return rem <= 15 ? 0 : (int)Math.Ceiling(rem / 60); }
        public static void FinishNow(SaveData d, BuildingKind k) { int s = SlotOf(d, k); if (s >= 0) SetSlot(d, s, SlotKind(d, s), SlotFrom(d, s), Now); }
        /// <summary>Ödüllü reklam: inşaatı kısaltır.</summary>
        public static void Shorten(SaveData d, BuildingKind k, int seconds) { int s = SlotOf(d, k); if (s >= 0) SetSlot(d, s, SlotKind(d, s), SlotFrom(d, s), SlotEnd(d, s) - seconds * 1000L); }

        // ---------- Üretim ----------
        public static float RateGold(SaveData d) => 1f * Progress.Eff(d, BuildingKind.Mine);
        public static float RateIron(SaveData d) => .7f * Progress.Eff(d, BuildingKind.Quarry);
        public static float RateFood(SaveData d) => .8f * Progress.Eff(d, BuildingKind.Farm);
        public const float CapMinutes = 480; // 8 saat birikir, sonra durur

        public static void TickProduction(SaveData d)
        {
            long now = Now;
            if (d.lastProd <= 0) d.lastProd = now;
            float mins = Math.Max(0, (now - d.lastProd) / 60000f);
            d.lastProd = now;
            d.pendGold = Math.Min(RateGold(d) * CapMinutes, d.pendGold + RateGold(d) * mins);
            d.pendIron = Math.Min(RateIron(d) * CapMinutes, d.pendIron + RateIron(d) * mins);
            d.pendFood = Math.Min(RateFood(d) * CapMinutes, d.pendFood + RateFood(d) * mins);
        }

        /// <summary>Biriken üretimi alır: (altın, demir, yiyecek).</summary>
        public static (int g, int i, int f) Collect(SaveData d)
        {
            TickProduction(d);
            int g = (int)Math.Floor(d.pendGold), i = (int)Math.Floor(d.pendIron), f = (int)Math.Floor(d.pendFood);
            d.pendGold -= g; d.pendIron -= i; d.pendFood -= f;
            return (g, i, f);
        }

        // ---------- Pazar takası ----------
        public static float TradeRateFor(float eff) => Math.Min(.9f, (50 + 2 * eff) / 100f);
        public static float TradeRate(SaveData d) => TradeRateFor(Progress.Eff(d, BuildingKind.Market));
    }
}
