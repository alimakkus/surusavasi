using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEditor;
using UnityEngine;

/// <summary>Elmas defterinin oyun kodundaki bağlantısını sınar (Wallet → GemLedger, satın alma toplama). Kayda dokunmaz.
/// Komut satırı: Unity -batchmode -executeMethod EconomyCheck.Run -quit</summary>
public static class EconomyCheck
{
    class MemSave : ISaveService
    {
        public SaveData Data { get; } = new SaveData();
        public void Load(System.Action<SaveData> done) => done?.Invoke(Data);
        public void Save() { }
    }

    [MenuItem("Sürü Savaşı/Test/Elmas defteri")]
    public static void Run()
    {
        var s = new MemSave(); var w = new Wallet(s); var d = s.Data;
        int fail = 0;
        void Check(bool ok, string n) { if (!ok) { fail++; Debug.LogError("[ElmasTest] HATA: " + n); } }
        Check(Wallet.LedgerOn, "defter açık (bulut paketi kurulu)");
        w.Add(Currency.Diamonds, 10, "login");
        w.Add(Currency.Gold, 100, "level");
        Check(w.Spend(Currency.Diamonds, 4, "skin:x"), "harcama");
        w.BeginPurchase(Shop.StoreCode("Editor (sahte)"), "founder_pack", "editor");
        w.Add(Currency.Gold, 500, "purchase:founder_pack"); w.Add(Currency.Diamonds, 350, "purchase:founder_pack");
        w.EndPurchase();
        Check(d.gemOps.Count == 3, "3 işlem (altın sayılmaz): " + d.gemOps.Count);
        Check(d.gemOps[0].k == "e" && d.gemOps[0].n == 10 && d.gemOps[1].k == "s" && d.gemOps[1].n == 4, "kazanç/harcama sırası");
        Check(d.gemOps[2].k == "p" && d.gemOps[2].n == 350 && d.gemOps[2].store == "editor" && d.gemOps[2].pid == "founder_pack", "satın alma tek işlem");
        Check(d.diamonds == 356 && GemLedger.Net(d) == 356 && GemLedger.BaseBalance(d) == 0, "bakiye");
        Check(d.gemOps[0].id != d.gemOps[1].id && d.gemOps[0].id.StartsWith(Wallet.DeviceTag), "işlem kimlikleri");
        var json = JsonUtility.ToJson(d); var back = JsonUtility.FromJson<SaveData>(json);
        Check(back.gemOps.Count == 3 && back.gemOps[2].rc == "editor", "kayıtta saklanır");
        GemLedger.ApplyServer(d, 300, new System.Collections.Generic.List<string> { d.gemOps[0].id, d.gemOps[1].id });
        Check(d.diamonds == 650 && GemLedger.Pending(d) == 1 && d.gemServerBal == 300, "sunucu yanıtı uygulanır");
        Check(Shop.StoreCode("Google Play") == "google" && Shop.StoreCode("App Store") == "apple" && Shop.StoreCode("Huawei AppGallery") == "huawei", "mağaza kodları");
        Debug.Log(fail == 0 ? "[ElmasTest] Tüm kontroller geçti" : $"[ElmasTest] {fail} hata");
    }
}
