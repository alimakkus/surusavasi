using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SuruSavasi.EditorTools
{
    /// <summary>Menü: Sürü Savaşı > Kayıt. Mevcut kaydı yedekleyip sıfırdan başlamak (yeni oyuncu testi) ve yedeği geri yüklemek için.
    /// Yedekler proje kökündeki SaveBackups klasörüne yazılır (git'e girmez).</summary>
    public static class SaveTools
    {
        const string Key = "surusavasi-v2";
        static string Dir => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "SaveBackups");

        [MenuItem("Sürü Savaşı/Kayıt/Yedekle ve sıfırla (yeni oyuncu)")]
        public static void BackupAndReset()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Kayıt] Önce Play modundan çık."); return; }
            Directory.CreateDirectory(Dir);
            string json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                string f = Path.Combine(Dir, "kayit_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
                File.WriteAllText(f, json);
                Debug.Log("[Kayıt] Yedeklendi: " + f);
            }
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            Debug.Log("[Kayıt] Sıfırlandı. Play'e basınca yeni oyuncu olarak başlarsın.");
        }

        [MenuItem("Sürü Savaşı/Kayıt/Son yedeği geri yükle")]
        public static void RestoreLatest()
        {
            if (Application.isPlaying) { Debug.LogWarning("[Kayıt] Önce Play modundan çık."); return; }
            if (!Directory.Exists(Dir)) { Debug.LogWarning("[Kayıt] Yedek yok."); return; }
            var f = Directory.GetFiles(Dir, "kayit_*.json").OrderByDescending(x => x).FirstOrDefault();
            if (f == null) { Debug.LogWarning("[Kayıt] Yedek yok."); return; }
            PlayerPrefs.SetString(Key, File.ReadAllText(f));
            PlayerPrefs.Save();
            Debug.Log("[Kayıt] Geri yüklendi: " + f);
        }
    }
}
