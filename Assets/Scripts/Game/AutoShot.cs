#if UNITY_STANDALONE || UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using SuruSavasi.Core;
using SuruSavasi.Core.Meta;
using SuruSavasi.Services;
using UnityEngine;

namespace SuruSavasi.Game
{
    /// <summary>Otomatik ekran görüntüsü: yalnızca "-autoshot <klasör>" ile başlatılan Windows sürümünde çalışır
    /// (Tools/autoshot/run_autoshot.ps1). Örnek bir oyuncu durumu kurar, ekranları sırayla açıp PNG kaydeder ve kapanır.
    /// Bilgisayar başında olmadan arayüzü denetlemek için.</summary>
    public class AutoShot : MonoBehaviour
    {
        static string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, "-autoshot");
            if (i < 0 || i + 1 >= a.Length) return;
            dir = a[i + 1]; Directory.CreateDirectory(dir);
            PlayerPrefs.DeleteAll();                       // temiz, ayrı bir oyuncu (bu sürümün kendi kaydı)
            PlayerPrefs.SetInt("surusavasi-consent", 0); PlayerPrefs.Save();
            var go = new GameObject("AutoShot"); DontDestroyOnLoad(go); go.AddComponent<AutoShot>();
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(1.2f);
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log("[AutoShot] " + name);
        }

        IEnumerator Start()
        {
            while (!GameServices.Ready || GameServices.Save?.Data == null) yield return null;
            var d = GameServices.Save.Data;
            d.unlocked = 14; for (int i = 0; i < 13; i++) d.stars[i] = i % 3 + 1;
            d.diamonds = 420; d.founderBought = true; d.gold = 5200; d.iron = 2600; d.food = 3100;
            Daily.Ensure(d).last = Daily.Today;
            Tutorial.SkipAll();
            Season.Ensure(d);
            d.seasonPts = 7 * Season.TierPts + 130; d.seasonDayPts = 140;
            for (int t = 1; t <= 3; t++) d.seasonFree |= 1 << (t - 1);
            d.seasonWeekProg[0] = 999; d.seasonWeekProg[1] = 3;
            GameServices.Save.Save();

            while (GameFlow.I == null || GameFlow.I.screen == GameScreen.Splash) yield return null;
            var f = GameFlow.I;
            f.Show(GameScreen.Base); yield return Shot("01_us");
            f.Show(GameScreen.Season); yield return Shot("02_sezon");
            SeasonScreen.ShowTasks(true); yield return Shot("03_sezon_gorevler");
            SeasonScreen.ShowTasks(false); Season.UnlockPremium(d); f.Show(GameScreen.Season); yield return Shot("04_sezon_bilet");
            d.seasonPts = Season.Tiers * Season.TierPts + 650; f.Show(GameScreen.Season); yield return Shot("05_sezon_son");
            f.Show(GameScreen.Account); yield return Shot("06_hesap");
            Season.ClaimPrem(d, Season.Tiers, (k, n) => { }, new System.Random(1)); d.unlocked = 14;
            SkinScreen.Open(f, 0); yield return Shot("07_gorunum_buz");
            var sat = DateTime.Today; while (sat.DayOfWeek != DayOfWeek.Saturday) sat = sat.AddDays(1);
            SnowEvent.Clock = () => sat.AddHours(14);
            f.Show(GameScreen.Modes); yield return Shot("08_modlar_firtina");
            // Not: Windows test sürümünde URP dünya çizimi çalışmıyor (serileştirme uyumsuzluğu), savaş görüntüsü alınmaz.
            Debug.Log("[AutoShot] bitti");
            Application.Quit();
        }
    }
}
#endif
