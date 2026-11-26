using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SuruEconomy;

// Sunucu elmas kurallarının başsız testi. Çalıştırma: Tools/economy/run_tests.ps1
static class TestMain
{
    static int fail, pass;
    static void Check(bool ok, string name) { if (ok) pass++; else { fail++; Console.WriteLine("HATA: " + name); } }
    static readonly DateTime Wed = new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc); // çarşamba
    static readonly DateTime Sat = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);
    static int seq;
    static Op E(string r, int n, string d = "") => new Op { Id = "o" + (++seq), K = "e", R = r, N = n, D = d };
    static Op S(int n) => new Op { Id = "o" + (++seq), K = "s", R = "x", N = n };
    static Op P(string store, string pid, int n, string rc = "r") => new Op { Id = "o" + (++seq), K = "p", Store = store, Pid = pid, N = n, Rc = rc };
    static SyncResult Run(EcoState s, int bas, Dictionary<string, Verdict> v, DateTime t, params Op[] ops) =>
        EconomyRules.Apply(s, new SyncRequest { Base = bas, Ops = new List<Op>(ops) }, v ?? new Dictionary<string, Verdict>(), t);

    static int Main()
    {
        // 1) İlk bakiye
        var s = new EcoState(); var r = Run(s, 300, null, Wed);
        Check(s.Bal == 300 && s.Seeded, "seed 300");
        var s2 = new EcoState(); r = Run(s2, 9000, null, Wed);
        Check(s2.Bal == EconomyRules.SeedCap && r.Msg == "seed_capped", "seed sınırı");
        r = Run(s2, 99999, null, Wed); Check(s2.Bal == EconomyRules.SeedCap, "seed bir kez");

        // 2) Günlük sınır
        s = new EcoState(); r = Run(s, 0, null, Wed, E("level", 300), E("level", 200));
        Check(s.Bal == 400 && r.Capped == 100 && r.Acked.Count == 2, "level günlük 400");
        r = Run(s, 0, null, Wed.AddDays(1), E("level", 50)); Check(s.Bal == 450, "ertesi gün sınır sıfırlanır");
        r = Run(s, 0, null, Wed, E("quest:boss", 5), E("quest:gates", 5)); Check(s.Bal == 460, "quest:* kategori");
        r = Run(s, 0, null, Wed, E("bilinmeyen", 80)); Check(s.Bal == 510, "other sınırı 50");
        r = Run(s, 0, null, Wed, E("purchase:diamonds_100", 100)); Check(s.Bal == 510, "kazanç olarak satın alma reddedilir");

        // 3) Harcama
        r = Run(s, 0, null, Wed, S(500)); Check(s.Bal == 10, "harcama");
        r = Run(s, 0, null, Wed, S(50)); Check(s.Bal == 10 && r.RejSpend == 1 && s.Flags == 1, "bakiye yetmeyen harcama");

        // 4) Tekrar gönderim
        var op = E("login", 10);
        Run(s, 0, null, Wed, op); r = Run(s, 0, null, Wed, op);
        Check(s.Bal == 20 && r.Acked.Contains(op.Id), "aynı işlem iki kez sayılmaz");

        // 5) Editor makbuzu
        s = new EcoState(); var p1 = P("editor", "diamonds_100", 200);
        var v = new Dictionary<string, Verdict> { { p1.Id, Receipts.Editor(p1.Id, true) } };
        r = Run(s, 0, v, Wed, p1); Check(s.Bal == 0 && r.RejBuy == 1, "editor makbuzu canlıda reddedilir");
        p1 = P("editor", "diamonds_100", 200); v = new() { { p1.Id, Receipts.Editor(p1.Id, false) } };
        r = Run(s, 0, v, Wed, p1); Check(s.Bal == 200 && s.FirstDone && r.Bought == 200, "ilk alım 2 kat");
        var p2 = P("editor", "diamonds_100", 200); v = new() { { p2.Id, Receipts.Editor(p2.Id, false) } };
        r = Run(s, 0, v, Wed, p2); Check(s.Bal == 300 && r.Capped == 100, "ikinci alım 2 kat olmaz");
        var p3 = P("editor", "diamonds_100", 150); v = new() { { p3.Id, Receipts.Editor(p3.Id, false) } };
        r = Run(s, 0, v, Sat, p3); Check(s.Bal == 450, "hafta sonu %50");

        // 6) Aynı makbuz iki kez
        s = new EcoState(); var g1 = P("google", "diamonds_550", 1100); var g2 = P("google", "diamonds_550", 1100);
        var same = new Verdict { Ok = true, Verified = true, TxId = "GPA.1" };
        r = Run(s, 0, new() { { g1.Id, same }, { g2.Id, same } }, Wed, g1, g2);
        Check(s.Bal == 1100 && r.Acked.Count == 2, "aynı işlem kimliği tek kredi");

        // 7) Google imzası
        using (var rsa = RSA.Create(2048))
        {
            string pub = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            string data = "{\"orderId\":\"GPA.3333\",\"packageName\":\"com.test.suru\",\"productId\":\"diamonds_100\",\"purchaseState\":0,\"purchaseToken\":\"tok\"}";
            string sig = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(data), HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1));
            string rc = Unified("GooglePlay", "tok", JsonSerializer.Serialize(new Dictionary<string, string> { { "json", data }, { "signature", sig } }));
            var gv = Receipts.Google(rc, "diamonds_100", pub, "com.test.suru");
            Check(gv.Ok && gv.Verified && gv.TxId == "GPA.3333", "google imza geçerli");
            string bad = Unified("GooglePlay", "tok", JsonSerializer.Serialize(new Dictionary<string, string> { { "json", data.Replace("diamonds_100", "diamonds_7000") }, { "signature", sig } }));
            Check(!Receipts.Google(bad, "diamonds_7000", pub, "").Ok, "değiştirilmiş veri reddedilir");
            Check(!Receipts.Google(rc, "diamonds_550", pub, "").Ok, "yanlış ürün reddedilir");
            Check(!Receipts.Google(rc, "diamonds_100", pub, "com.baska").Ok, "yanlış paket reddedilir");
            var nokey = Receipts.Google(rc, "diamonds_100", "", "");
            Check(nokey.Ok && !nokey.Verified, "anahtar yoksa doğrulanmamış kabul");
            Check(!Receipts.Google("çöp", "diamonds_100", pub, "").Ok, "bozuk makbuz");
        }

        // 8) Apple yanıtı
        string ap = "{\"status\":0,\"receipt\":{\"bundle_id\":\"com.test.suru\",\"in_app\":[{\"product_id\":\"diamonds_100\",\"transaction_id\":\"1000\"},{\"product_id\":\"pack_speed\",\"transaction_id\":\"1001\"}]}}";
        var av = Receipts.AppleResponse(ap, "pack_speed", "1001", "com.test.suru");
        Check(av.Ok && av.Verified && av.TxId == "1001", "apple geçerli");
        Check(!Receipts.AppleResponse(ap, "pack_speed", "999", "").Ok, "apple işlem kimliği yok");
        Check(!Receipts.AppleResponse(ap, "diamonds_7000", "", "").Ok, "apple ürün yok");
        Check(!Receipts.AppleResponse(ap, "pack_speed", "1001", "com.baska").Ok, "apple yanlış paket");
        Check(Receipts.AppleResponse("{\"status\":21003}", "x", "", "").Why == "apple_status_21003", "apple durum hatası");
        Check(Receipts.AppleResponse("{\"status\":21005}", "x", "", "").Why == Verdict.Retry, "apple geçici hata");

        // 9) Doğrulanamayan mağaza: günde 3
        s = new EcoState(); var ops = new List<Op>(); v = new();
        for (int i = 0; i < 5; i++) { var q = P("samsung", "pack_speed", 150, Unified("Samsung", "S" + i, "x")); ops.Add(q); v[q.Id] = Receipts.Unverified("samsung", q.Rc); }
        r = EconomyRules.Apply(s, new SyncRequest { Ops = ops }, v, Wed);
        Check(s.Bal == 450 && r.RejBuy == 2, "doğrulanamayan mağaza günde 3");

        // 10) Başarım
        s = new EcoState(); r = Run(s, 0, null, Wed, E("achievement:kill", 10), E("achievement:kill", 25), E("achievement:kill", 60), E("achievement:kill", 60));
        Check(s.Bal == 95, "başarım başına 95");

        // 11) Çevrimdışı günler
        s = new EcoState();
        r = Run(s, 0, null, Wed, E("level", 400, "20261123"), E("level", 400, "20261124"), E("level", 400, "20261125"), E("level", 400, "20261001"), E("level", 100, "20991231"));
        Check(s.Bal == 1200 && r.Capped == 500, "çevrimdışı günlere ayrı sınır, eski/gelecek gün bugüne");

        // 12) Aylık kart
        s = new EcoState(); r = Run(s, 0, null, Wed, E("monthly_card", 50)); Check(s.Bal == 0, "kartsız aylık kart elması yok");
        var mc = P("editor", "monthly_card", 300); r = Run(s, 0, new() { { mc.Id, Receipts.Editor(mc.Id, false) } }, Wed, mc, E("monthly_card", 50));
        Check(s.Bal == 350, "kart alınınca günlük 50");

        // 13) Tek seferlik ürün
        s = new EcoState(); var st1 = P("editor", "starter_pack", 500); var st2 = P("editor", "starter_pack", 500);
        r = Run(s, 0, new() { { st1.Id, Receipts.Editor(st1.Id, false) }, { st2.Id, Receipts.Editor(st2.Id, false) } }, Wed, st1, st2);
        Check(s.Bal == 500, "başlangıç paketi bir kez");

        // 14) Geçici hata onaylanmaz
        s = new EcoState(); var ap1 = P("apple", "diamonds_100", 200);
        r = Run(s, 0, new() { { ap1.Id, Verdict.No(Verdict.Retry) } }, Wed, ap1);
        Check(!r.Acked.Contains(ap1.Id) && s.Bal == 0 && r.RejBuy == 0, "geçici hata sonra denenir");

        // 15) JSON gidiş-dönüş (Cloud Save'de saklanan biçim)
        var o = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        s = new EcoState(); Run(s, 77, null, Wed, E("level", 10), E("achievement:kill", 10));
        var back = JsonSerializer.Deserialize<EcoState>(JsonSerializer.Serialize(s, o), o);
        Check(back.Bal == 97 && back.Used.Count == 2 && back.Ids.Count == 2 && back.Life["ach:kill"] == 10, "durum kaydı gidiş-dönüş");
        var req = JsonSerializer.Deserialize<SyncRequest>("{\"base\":5,\"ops\":[{\"id\":\"a\",\"k\":\"e\",\"n\":3,\"r\":\"login\",\"d\":\"20261125\"}]}", o);
        Check(req.Base == 5 && req.Ops.Count == 1 && req.Ops[0].R == "login", "istemci isteği (JsonUtility biçimi) okunur");
        string outJson = JsonSerializer.Serialize(Run(new EcoState(), 1, null, Wed), o);
        Check(outJson.Contains("\"bal\":1") && outJson.Contains("\"acked\":[]"), "yanıt camelCase: " + outJson);

        FlowTest.Run(Check);

        Console.WriteLine($"Sunucu elmas testleri: {pass} geçti, {fail} hata");
        return fail == 0 ? 0 : 1;
    }

    static string Unified(string store, string tx, string payload) =>
        JsonSerializer.Serialize(new Dictionary<string, string> { { "Store", store }, { "TransactionID", tx }, { "Payload", payload } });
}
