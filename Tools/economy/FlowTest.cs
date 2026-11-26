using System;
using System.Collections.Generic;
using System.Text.Json;
using SuruEconomy;
using SuruSavasi.Core.Meta;

// İstemci defteri (GemLedger) + sunucu kuralları uçtan uca: çevrimdışı oyun, satın alma, kaybolan yanıt, hile denemesi.
namespace SuruSavasi.Core { public class SaveData { public int diamonds; public List<GemOp> gemOps = new(); public int gemSeq, gemServerBal = -1; public long gemSyncAt; } }

static class FlowTest
{
    static readonly JsonSerializerOptions O = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, IncludeFields = true };
    class Req { public int @base; public List<GemOp> ops = new(); }
    class Res { public int bal; public List<string> acked = new(); public int capped, rejSpend, rejBuy; }

    // İstemci tarafı: Wallet.Add/Spend'in yaptığı
    static void Earn(SuruSavasi.Core.SaveData d, int n, string r) { GemLedger.Record(d, "dev1", "e", n, r); d.diamonds += n; }
    static void Spend(SuruSavasi.Core.SaveData d, int n) { GemLedger.Record(d, "dev1", "s", n, "x"); d.diamonds -= n; }

    // Ağ: istemci JSON'u → sunucu → JSON yanıt (gerçekte JsonUtility; alan adları aynı)
    static Res Sync(SuruSavasi.Core.SaveData d, EcoState server, bool production, bool loseResponse = false)
    {
        var req = new Req { @base = GemLedger.BaseBalance(d), ops = new List<GemOp>(d.gemOps) };
        var sreq = JsonSerializer.Deserialize<SyncRequest>(JsonSerializer.Serialize(req, O), O);
        var verdicts = new Dictionary<string, Verdict>();
        foreach (var op in sreq.Ops) if (op.K == "p") verdicts[op.Id] = Receipts.Editor(op.Id, production);
        var sres = EconomyRules.Apply(server, sreq, verdicts, DateTime.UtcNow);
        if (loseResponse) return null;
        var res = JsonSerializer.Deserialize<Res>(JsonSerializer.Serialize(sres, O), O);
        GemLedger.ApplyServer(d, res.bal, res.acked);
        return res;
    }

    public static int Run(Action<bool, string> check)
    {
        // Sürüm öncesi oyuncu: 250 elması var, defter boş
        var d = new SuruSavasi.Core.SaveData { diamonds = 250 }; var srv = new EcoState();
        Earn(d, 10, "login"); Earn(d, 6, "level"); Spend(d, 50);
        check(d.diamonds == 216 && GemLedger.BaseBalance(d) == 250, "istemci bakiyesi ve taban");
        var r = Sync(d, srv, false);
        check(srv.Bal == 216 && d.diamonds == 216 && GemLedger.Pending(d) == 0, "ilk eşitleme: taban + işlemler");

        // Satın alma (Editor, test ortamı): 100'lük paket ilk alım 200
        var p = GemLedger.Record(d, "dev1", "p", 200, "purchase:diamonds_100", "editor", "diamonds_100", "editor"); d.diamonds += 200;
        // Yanıt kaybolur: sunucu uyguladı ama istemci bilmiyor
        Sync(d, srv, false, loseResponse: true);
        check(srv.Bal == 416 && GemLedger.Pending(d) == 1, "yanıt kayboldu, işlem bekliyor");
        Earn(d, 5, "quest:boss");
        Sync(d, srv, false);
        check(srv.Bal == 421 && d.diamonds == 421 && GemLedger.Pending(d) == 0, "tekrar gönderimde çift sayılmaz");

        // Hile: kayıt dosyasında elmas 99999 yapılır, sonra harcanır
        d.diamonds = 99999; Spend(d, 5000);
        Sync(d, srv, false);
        check(srv.Bal == 421 && d.diamonds == 421 && srv.Flags >= 1, "kayıt düzenleme hilesi sunucuda geçmez");

        // Hile: sahte kazanç (bölümden 10000 elmas)
        Earn(d, 10000, "level"); Sync(d, srv, false);
        check(d.diamonds < 421 + 400, "sahte kazanç günlük sınıra takılır: " + d.diamonds);

        // Canlı ortamda Editor makbuzu geçmez
        int before = d.diamonds;
        GemLedger.Record(d, "dev1", "p", 1100, "purchase:diamonds_550", "editor", "diamonds_550", "editor"); d.diamonds += 1100;
        r = Sync(d, srv, true);
        check(r.rejBuy == 1 && d.diamonds == before, "sahte satın alma geri alınır");

        // Başka cihaz: aynı hesap, kayıt bulut kopyasından gelir (eski bakiye), sunucu bakiyesi geçerli olur
        var d2 = new SuruSavasi.Core.SaveData { diamonds = 50, gemSeq = 0 };
        Sync(d2, srv, false);
        check(d2.diamonds == srv.Bal, "ikinci cihaz sunucu bakiyesini alır");

        // Uzun çevrimdışı: 700 işlem → liste sıkıştırılır, toplam korunur
        var d3 = new SuruSavasi.Core.SaveData(); var s3 = new EcoState();
        for (int i = 0; i < 700; i++) Earn(d3, 1, i % 2 == 0 ? "base_chest" : "night_raid");
        check(GemLedger.Pending(d3) <= GemLedger.MaxOps && GemLedger.Net(d3) == 700 && d3.diamonds == 700, "sıkıştırma toplamı korur");
        Sync(d3, s3, false);
        check(d3.diamonds == 40 && GemLedger.Pending(d3) == 0, "sıkıştırılmış kazançlar da sınırlı (20+20)");
        return 0;
    }
}
