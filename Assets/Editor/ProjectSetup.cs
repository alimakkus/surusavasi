using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace SuruSavasi.EditorTools
{
    /// <summary>Proje ayarlarını kodla sabitler: dikey ekran, oyun adı, sürüm ve Editor'de dikey Game penceresi.</summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Version = "0.1.0";

        static ProjectSetup()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait) PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            if (PlayerSettings.productName != "Sürü Savaşı") PlayerSettings.productName = "Sürü Savaşı";
            if (PlayerSettings.bundleVersion == "1.0" || PlayerSettings.bundleVersion == "0.1") PlayerSettings.bundleVersion = Version;
            try { SetPortraitGameView(); } catch (Exception e) { Debug.Log("[ProjectSetup] Game penceresi boyutu ayarlanamadı: " + e.Message); }
        }

        // Game penceresine "Dikey 1080x1920" boyutunu ekler ve seçer (Unity'nin iç API'si, bu yüzden yansıtma ile).
        static void SetPortraitGameView()
        {
            const string label = "Dikey 1080x1920";
            var asm = typeof(Editor).Assembly;
            var sizesType = asm.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null, null);
            var groupType = sizesType.GetProperty("currentGroupType").GetValue(instance, null);
            var group = sizesType.GetMethod("GetGroup").Invoke(instance, new[] { groupType });
            var gt = group.GetType();
            int total = (int)gt.GetMethod("GetTotalCount").Invoke(group, null);
            var getSize = gt.GetMethod("GetGameViewSize");
            int index = -1;
            for (int i = 0; i < total; i++)
            {
                var sz = getSize.Invoke(group, new object[] { i });
                if ((string)sz.GetType().GetProperty("baseText").GetValue(sz, null) == label) { index = i; break; }
            }
            if (index < 0)
            {
                var sizeType = asm.GetType("UnityEditor.GameViewSize");
                var typeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                var ctor = sizeType.GetConstructor(new[] { typeEnum, typeof(int), typeof(int), typeof(string) });
                var size = ctor.Invoke(new object[] { Enum.Parse(typeEnum, "FixedResolution"), 1080, 1920, label });
                gt.GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = total;
            }
            var gvType = asm.GetType("UnityEditor.GameView");
            var prop = gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var w in Resources.FindObjectsOfTypeAll(gvType))
                if ((int)prop.GetValue(w, null) != index) prop.SetValue(w, index, null);
        }
    }
}
