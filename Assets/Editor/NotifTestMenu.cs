using SuruSavasi.Game;
using SuruSavasi.Services;
using UnityEditor;
using UnityEngine;

/// <summary>Editor'de "bildirime dokunma" taklidi: oyun çalışırken menüden seçilince oyun o bildirimden açılmış gibi davranır.</summary>
public static class NotifTestMenu
{
    static void Tap(string target, int id, int cat)
    {
        if (!Application.isPlaying || GameFlow.I == null) { Debug.LogWarning("[Bildirim] Önce oyunu çalıştır (Play)."); return; }
        EditorNotifications.Pending = $"{target}|{id}|{System.DateTimeOffset.UtcNow.AddHours(-5).ToUnixTimeSeconds()}|{cat}";
        GameFlow.I.CheckOpened();
    }
    [MenuItem("Sürü Savaşı/Test/Bildirime dokun/Sezon görevleri")] static void A() => Tap("season_tasks", 12, 1);
    [MenuItem("Sürü Savaşı/Test/Bildirime dokun/Kar Fırtınası")] static void B() => Tap("modes", 20, 1);
    [MenuItem("Sürü Savaşı/Test/Bildirime dokun/İttifak patronu")] static void C() => Tap("alliance:2", 31, 2);
    [MenuItem("Sürü Savaşı/Test/Bildirime dokun/Günlük ödül")] static void D() => Tap("daily", 40, 3);
    [MenuItem("Sürü Savaşı/Test/Bildirim sayaçları")] static void E() => Debug.Log("[Bildirim] kimlik:dokunma:dönüş → " + NotifStats.TallyText());
}
