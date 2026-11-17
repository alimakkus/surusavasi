using System;
using UnityEngine;

namespace SuruSavasi.Services
{
    /// <summary>Geçici onay: cevabı cihazda saklar. Mağazaya çıkmadan önce gerçek onay penceresi
    /// (Google UMP / iOS ATT) ile değiştirilecek. Varsayılan: kişiselleştirilmemiş reklam.</summary>
    public class LocalConsent : IConsent
    {
        const string Key = "surusavasi-consent";
        public bool HasAnswered => PlayerPrefs.HasKey(Key);
        public bool PersonalizedAds => PlayerPrefs.GetInt(Key, 0) == 1;
        public bool AnalyticsAllowed => PlayerPrefs.GetInt(Key, 0) == 1;
        public void Request(Action done) { done?.Invoke(); }
        public static void SetAnswer(bool allowed) { PlayerPrefs.SetInt(Key, allowed ? 1 : 0); PlayerPrefs.Save(); }
    }
}
