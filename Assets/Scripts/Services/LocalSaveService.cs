using System;
using SuruSavasi.Core;

namespace SuruSavasi.Services
{
    public class LocalSaveService : ISaveService
    {
        public SaveData Data { get; private set; } = new SaveData();
        public void Load(Action<SaveData> done) { Data = SaveSystem.Load(); done?.Invoke(Data); }
        public void Save() => SaveSystem.Save(Data);
    }
}
