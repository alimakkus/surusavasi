using System.Collections.Generic;

namespace SuruSavasi.Services
{
    /// <summary>Satılan ürünler. Ürün kimlikleri her mağazanın panelinde aynen tanımlanmalıdır.
    /// Fiyatlar mağaza panelinde belirlenir, kodda tutulmaz.</summary>
    public static class Catalog
    {
        public const string Diamonds100 = "diamonds_100";
        public const string Diamonds550 = "diamonds_550";
        public const string Diamonds1200 = "diamonds_1200";
        public const string Diamonds2600 = "diamonds_2600";
        public const string Diamonds7000 = "diamonds_7000";
        public const string StarterPack = "starter_pack";      // tek seferlik ilk alım teklifi
        public const string MonthlyCard = "monthly_card";      // 30 gün boyunca günlük elmas
        public const string NoAds = "no_ads";                  // aralık reklamlarını kaldırır
        public const string PiggyBank = "piggy_bank";          // kumbarayı kırar: biriken elmaslar (tekrar alınabilir)
        public const string FounderPack = "founder_pack";      // 72 saatlik kurucu teklifi (inşaatçı + elmas + epik ekipman)
        public const string HeroPack = "pack_hero_week";       // haftalık kahraman paketi (parça ya da görünüm + elmas), haftada 1
        public const string SpeedPack = "pack_speed";          // 3 hızlandırıcı + elmas, günde 1
        public const string SeasonPass = "season_pass";        // Sezon Bileti: o sezonun ücretli ödül yolunu açar (sezonda 1)

        public static readonly IReadOnlyList<ProductDef> All = new List<ProductDef>
        {
            new ProductDef(Diamonds100, ProductKind.Consumable, 100),
            new ProductDef(Diamonds550, ProductKind.Consumable, 550),
            new ProductDef(Diamonds1200, ProductKind.Consumable, 1200),
            new ProductDef(Diamonds2600, ProductKind.Consumable, 2600),
            new ProductDef(Diamonds7000, ProductKind.Consumable, 7000),
            new ProductDef(StarterPack, ProductKind.NonConsumable, 500),
            new ProductDef(MonthlyCard, ProductKind.Consumable, 300),
            new ProductDef(NoAds, ProductKind.NonConsumable, 0),
            new ProductDef(PiggyBank, ProductKind.Consumable, 0),
            new ProductDef(FounderPack, ProductKind.NonConsumable, Core.Meta.Offers.FounderGems),
            new ProductDef(HeroPack, ProductKind.Consumable, Core.Meta.Packs.HeroPackGems),
            new ProductDef(SpeedPack, ProductKind.Consumable, Core.Meta.Packs.SpeedPackGems),
            new ProductDef(SeasonPass, ProductKind.Consumable, 0),
        };

        public static ProductDef Find(string id)
        {
            foreach (var p in All) if (p.id == id) return p;
            return null;
        }
    }
}
