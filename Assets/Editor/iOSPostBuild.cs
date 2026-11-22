using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
using System.IO;
#endif

namespace SuruSavasi.EditorTools
{
    /// <summary>iOS derlemesinde Info.plist'e takip izni açıklamasını yazar (Apple ATT penceresinde gösterilir).</summary>
    public static class iOSPostBuild
    {
#if UNITY_IOS
        [PostProcessBuild(999)]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;
            var plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSUserTrackingUsageDescription",
                "Sana ilgi alanlarına uygun reklamlar göstermek ve Sürü Savaşı'nı ücretsiz tutabilmek için bu izni istiyoruz.");
            plist.WriteToFile(plistPath);
            UnityEngine.Debug.Log("iOSPostBuild: NSUserTrackingUsageDescription eklendi");
        }
#endif
    }
}
