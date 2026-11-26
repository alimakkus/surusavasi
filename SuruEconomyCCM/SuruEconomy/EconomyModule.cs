using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Model;

namespace SuruEconomy
{
    /// <summary>Sürü Savaşı elmas sunucusu. Oyun elmas kazanç/harcama/satın almalarını bir defter olarak gönderir;
    /// sunucu makbuzları doğrular, sınırları uygular ve gerçek bakiyeyi Cloud Save "Protected" alanında tutar.
    /// İstemci çağrısı: CloudCodeService.Instance.CallModuleEndpointAsync("SuruEconomy", "Sync", { "request": json })</summary>
    public class EconomyModule
    {
        const string Key = "economy";
        static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        static readonly JsonSerializerOptions Json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        readonly IGameApiClient api;
        readonly ILogger<EconomyModule> log;

        public EconomyModule(IGameApiClient api, ILogger<EconomyModule> log) { this.api = api; this.log = log; }

        [CloudCodeFunction("Sync")]
        public async Task<string> Sync(IExecutionContext context, string request)
        {
            var req = JsonSerializer.Deserialize<SyncRequest>(request ?? "{}", Json) ?? new SyncRequest();
            bool production = string.Equals(context.EnvironmentName, "production", StringComparison.OrdinalIgnoreCase);

            // Makbuzlar önce (ağ çağrısı gerekebilir), sonra kurallar uygulanır
            var verdicts = new Dictionary<string, Verdict>();
            foreach (var op in req.Ops.Where(o => o.K == "p").Take(20))
                verdicts[op.Id] = await Verify(op, production);

            for (int attempt = 0; ; attempt++)
            {
                var (state, writeLock) = await Load(context);
                var res = EconomyRules.Apply(state, req, verdicts, DateTime.UtcNow);
                try
                {
                    await Save(context, state, writeLock);
                    if (res.RejBuy > 0 || res.RejSpend > 0 || res.Msg == "seed_capped")
                        log.LogWarning("Şüpheli: oyuncu {Player} rejBuy={RejBuy} rejSpend={RejSpend} flags={Flags} msg={Msg}",
                            context.PlayerId, res.RejBuy, res.RejSpend, state.Flags, res.Msg);
                    return JsonSerializer.Serialize(res, Json);
                }
                catch (Exception e) when (attempt < 2)
                {
                    // Aynı anda başka bir eşitleme yazdıysa (yazma kilidi) baştan okunup yeniden denenir
                    log.LogInformation("Yeniden deneniyor: {Error}", e.Message);
                }
            }
        }

        /// <summary>Sadece bakiyeyi okur (değiştirmez).</summary>
        [CloudCodeFunction("Balance")]
        public async Task<int> Balance(IExecutionContext context) => (await Load(context)).state.Bal;

        async Task<(EcoState state, string? writeLock)> Load(IExecutionContext context)
        {
            var r = await api.CloudSaveData.GetProtectedItemsAsync(context, context.ServiceToken, context.ProjectId,
                context.PlayerId!, new List<string> { Key });
            var item = r.Data?.Results?.FirstOrDefault();
            string? json = item?.Value?.ToString();
            var s = string.IsNullOrEmpty(json) ? new EcoState() : JsonSerializer.Deserialize<EcoState>(json, Json) ?? new EcoState();
            return (s, item?.WriteLock);
        }

        async Task Save(IExecutionContext context, EcoState s, string? writeLock)
        {
            var body = new SetItemBody(Key, JsonSerializer.Serialize(s, Json), writeLock);
            await api.CloudSaveData.SetProtectedItemAsync(context, context.ServiceToken, context.ProjectId, context.PlayerId!, body);
        }

        async Task<Verdict> Verify(Op op, bool production)
        {
            switch (op.Store)
            {
                case "editor": return Receipts.Editor(op.Id, production);
                case "google": return Receipts.Google(op.Rc, op.Pid, StoreConfig.GooglePlayPublicKey, StoreConfig.AndroidPackage);
                case "apple": return await Apple(op);
                case "samsung":
                case "amazon":
                case "huawei": return Receipts.Unverified(op.Store, op.Rc);
                default: return Verdict.No("unknown_store");
            }
        }

        /// <summary>Apple: makbuz önce canlı sunucuya, "sandbox makbuzu" (21007) derse test sunucusuna gönderilir.</summary>
        async Task<Verdict> Apple(Op op)
        {
            var (_, tx, payload) = Receipts.ParseUnified(op.Rc);
            if (string.IsNullOrEmpty(payload)) return Verdict.No("apple_empty_receipt");
            try
            {
                string body = JsonSerializer.Serialize(new Dictionary<string, object> { { "receipt-data", payload }, { "exclude-old-transactions", true } });
                string resp = await Post("https://buy.itunes.apple.com/verifyReceipt", body);
                if (resp.Contains("\"status\":21007")) resp = await Post("https://sandbox.itunes.apple.com/verifyReceipt", body);
                return Receipts.AppleResponse(resp, op.Pid, tx, StoreConfig.AppleBundleId);
            }
            catch (Exception e) when (e is HttpRequestException || e is TaskCanceledException)
            {
                log.LogWarning("Apple'a ulaşılamadı: {Error}", e.Message);
                return Verdict.No(Verdict.Retry);
            }
        }

        static async Task<string> Post(string url, string json)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var r = await Http.PostAsync(url, content);
            return await r.Content.ReadAsStringAsync();
        }
    }
}
