using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SuruEconomy
{
    /// <summary>Mağaza makbuzlarını çözer ve doğrular. Ağ çağrısı gerektirmeyen kısımlar burada (test edilebilir);
    /// Apple'ın sunucusuna istek EconomyModule'dedir.</summary>
    public static class Receipts
    {
        /// <summary>Unity IAP'nin birleşik makbuzu: {"Store":"GooglePlay","TransactionID":"…","Payload":"…"}. Düz metin de olabilir (Editor).</summary>
        public static (string store, string txId, string payload) ParseUnified(string receipt)
        {
            if (string.IsNullOrEmpty(receipt) || receipt[0] != '{') return ("", "", receipt ?? "");
            try
            {
                using var doc = JsonDocument.Parse(receipt);
                var r = doc.RootElement;
                return (Str(r, "Store"), Str(r, "TransactionID"), Str(r, "Payload"));
            }
            catch (JsonException) { return ("", "", receipt); }
        }

        static string Str(JsonElement e, string name) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v)
                ? (v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : v.ToString()) : "";

        /// <summary>Editor / test makbuzu: yalnızca üretim (production) dışındaki ortamlarda kabul edilir.</summary>
        public static Verdict Editor(string opId, bool production) =>
            production ? Verdict.No("editor_receipt_in_production")
                       : new Verdict { Ok = true, Verified = false, TxId = "editor-" + opId, Why = "test" };

        /// <summary>Google Play: satın alma verisinin RSA (SHA1) imzası uygulamanın lisans anahtarıyla doğrulanır.
        /// Anahtar Play Console > Para kazanma kurulumu > Lisanslama bölümündedir (StoreConfig.GooglePlayPublicKey).</summary>
        public static Verdict Google(string unifiedReceipt, string productId, string publicKeyBase64, string packageName)
        {
            var (_, unityTx, payload) = ParseUnified(unifiedReceipt);
            string data, sig;
            try
            {
                using var p = JsonDocument.Parse(payload);
                data = Str(p.RootElement, "json"); sig = Str(p.RootElement, "signature");
            }
            catch (JsonException) { return Verdict.No("google_bad_payload"); }
            if (string.IsNullOrEmpty(data)) return Verdict.No("google_no_data");

            string orderId, pkg, pid, token; int state;
            try
            {
                using var d = JsonDocument.Parse(data);
                var r = d.RootElement;
                orderId = Str(r, "orderId"); pkg = Str(r, "packageName"); pid = Str(r, "productId"); token = Str(r, "purchaseToken");
                state = r.TryGetProperty("purchaseState", out var ps) && ps.ValueKind == JsonValueKind.Number ? ps.GetInt32() : 0;
            }
            catch (JsonException) { return Verdict.No("google_bad_data"); }

            if (pid != productId) return Verdict.No("google_wrong_product");
            if (!string.IsNullOrEmpty(packageName) && pkg != packageName) return Verdict.No("google_wrong_package");
            if (state != 0) return Verdict.No("google_not_purchased");
            string tx = !string.IsNullOrEmpty(orderId) ? orderId : !string.IsNullOrEmpty(token) ? token : unityTx;
            if (string.IsNullOrEmpty(tx)) return Verdict.No("google_no_tx");

            if (string.IsNullOrEmpty(publicKeyBase64))
                return new Verdict { Ok = true, Verified = false, TxId = tx, Why = "google_key_missing" }; // anahtar girilmemiş: günlük sınırla kabul
            if (string.IsNullOrEmpty(sig)) return Verdict.No("google_no_signature");
            if (!VerifyRsaSha1(data, sig, publicKeyBase64)) return Verdict.No("google_bad_signature");
            return new Verdict { Ok = true, Verified = true, TxId = tx };
        }

        public static bool VerifyRsaSha1(string data, string signatureBase64, string publicKeyBase64)
        {
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64), out _);
                return rsa.VerifyData(Encoding.UTF8.GetBytes(data), Convert.FromBase64String(signatureBase64),
                    HashAlgorithmName.SHA1, RSASignaturePadding.Pkcs1);
            }
            catch (FormatException) { return false; }
            catch (CryptographicException) { return false; }
        }

        /// <summary>Apple verifyReceipt yanıtını değerlendirir: durum 0 olmalı, paket kimliği uymalı ve
        /// makbuzda bu ürüne ait (tercihen aynı işlem kimlikli) bir satın alma bulunmalı.</summary>
        public static Verdict AppleResponse(string responseJson, string productId, string unityTxId, string bundleId)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var r = doc.RootElement;
                int status = r.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : -1;
                if (status == 21005 || (status >= 21100 && status <= 21199)) return Verdict.No(Verdict.Retry); // Apple geçici hatası
                if (status != 0) return Verdict.No("apple_status_" + status);
                if (!r.TryGetProperty("receipt", out var rc)) return Verdict.No("apple_no_receipt");
                if (!string.IsNullOrEmpty(bundleId) && Str(rc, "bundle_id") != bundleId) return Verdict.No("apple_wrong_bundle");
                string found = "";
                if (rc.TryGetProperty("in_app", out var arr) && arr.ValueKind == JsonValueKind.Array)
                    foreach (var it in arr.EnumerateArray())
                    {
                        if (Str(it, "product_id") != productId) continue;
                        string tx = Str(it, "transaction_id");
                        if (!string.IsNullOrEmpty(unityTxId) && tx != unityTxId) { if (found == "") found = "?"; continue; }
                        found = tx; break;
                    }
                if (found == "" ) return Verdict.No("apple_product_not_in_receipt");
                if (found == "?") return Verdict.No("apple_tx_not_in_receipt");
                return new Verdict { Ok = true, Verified = true, TxId = found };
            }
            catch (JsonException) { return Verdict.No(Verdict.Retry); }
        }

        /// <summary>Henüz sunucu doğrulayıcısı olmayan mağazalar (Samsung, Amazon, Huawei): işlem kimliğiyle,
        /// tekrar kullanılmasın diye kaydedilir ve günlük sınırla kabul edilir.</summary>
        public static Verdict Unverified(string store, string unifiedReceipt)
        {
            var (_, tx, payload) = ParseUnified(unifiedReceipt);
            if (string.IsNullOrEmpty(tx))
            {
                if (string.IsNullOrEmpty(payload)) return Verdict.No(store + "_empty_receipt");
                using var sha = SHA256.Create();
                tx = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
            }
            return new Verdict { Ok = true, Verified = false, TxId = tx, Why = store + "_unverified" };
        }
    }
}
