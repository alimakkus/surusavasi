using System;

namespace SuruSavasi.Services
{
    /// <summary>KVKK/GDPR ve iOS ATT onayı. Reklam ve analiz başlamadan önce sorulur.</summary>
    public interface IConsent
    {
        bool HasAnswered { get; }
        bool PersonalizedAds { get; }
        bool AnalyticsAllowed { get; }
        void Request(Action done);
    }
}
