namespace SuruEconomy
{
    /// <summary>Mağaza ayarları. Uygulama mağazalara eklenince bu değerleri doldurup modülü yeniden yükleyin
    /// (Unity > Services > Deployment > SuruEconomy > Deploy).</summary>
    public static class StoreConfig
    {
        /// <summary>Android paket adı, örn. "com.sirketadi.surusavasi". Boşsa kontrol edilmez.</summary>
        public const string AndroidPackage = "";

        /// <summary>iOS paket kimliği (Bundle ID). Boşsa kontrol edilmez.</summary>
        public const string AppleBundleId = "";

        /// <summary>Google Play lisans anahtarı (Base64, "MIIBIjAN…" ile başlar).
        /// Play Console > uygulama > Para kazanma kurulumu > Lisanslama. Boşsa Google satın almaları imzasız,
        /// günlük sınırla kabul edilir.</summary>
        public const string GooglePlayPublicKey = "";
    }
}
