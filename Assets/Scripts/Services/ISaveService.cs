using System;
using SuruSavasi.Core;

namespace SuruSavasi.Services
{
    /// <summary>Kayıt: şimdilik cihazda, sonra bulut kaydı (Unity Cloud Save) eklenecek.</summary>
    public interface ISaveService
    {
        SaveData Data { get; }
        void Load(Action<SaveData> done);
        void Save();
    }
}
