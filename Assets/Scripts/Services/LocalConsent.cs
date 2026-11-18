using System;
using UnityEngine;

namespace SuruSavasi.Services
{
    /// <summary>KVKK/GDPR onayı: ilk açılışta sorulur, cihazda saklanır, Ayarlar'dan değiştirilebilir.
    /// Cevap reklam başlamadan önce LevelPlay gizlilik ayarlarına iletilir. Varsayılan: kişiselleştirilmemiş reklam.
    /// (iOS'ta ayrıca ATT izni gerekir; iOS derlemesinden önce eklenecek.)</summary>
    public class LocalConsent : IConsent
    {
        const string Key = "surusavasi-consent";
        public bool HasAnswered => PlayerPrefs.HasKey(Key);
        public bool PersonalizedAds => PlayerPrefs.GetInt(Key, 0) == 1;
        public bool AnalyticsAllowed => PlayerPrefs.GetInt(Key, 0) == 1;

        public void Request(Action done)
        {
            // Pencere oyun ekranında (GameFlow) gösterilir; burada yalnızca mevcut cevap reklama iletilir.
            ApplyToAds(PersonalizedAds);
            done?.Invoke();
        }

        public static void SetAnswer(bool allowed)
        {
            PlayerPrefs.SetInt(Key, allowed ? 1 : 0); PlayerPrefs.Save();
            ApplyToAds(allowed);
            GameServices.Analytics?.LogEvent("consent", new System.Collections.Generic.Dictionary<string, object> { { "personalized", allowed } });
        }

        /// <summary>LevelPlay gizlilik ayarları: GDPR onayı ve (reddedildiyse) CCPA "verimi satma" tercihi.</summary>
        public static void ApplyToAds(bool allowed)
        {
            Unity.Services.LevelPlay.LevelPlayPrivacySettings.SetGDPRConsent(allowed);
            Unity.Services.LevelPlay.LevelPlayPrivacySettings.SetCCPA(!allowed);
        }
    }
}
