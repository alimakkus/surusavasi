using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace SuruSavasi.Services
{
    /// <summary>iOS App Tracking Transparency (ATT) izni. Android ve Editor'da her zaman izinli sayılır.
    /// Reklam sistemi başlamadan önce sorulur; zaten cevaplanmışsa tekrar sorulmaz.</summary>
    public static class TrackingPermission
    {
        public const int NotDetermined = 0, Authorized = 3;

#if UNITY_IOS && !UNITY_EDITOR
        delegate void AttCallback(int status);
        [DllImport("__Internal")] static extern int _ATT_GetStatus();
        [DllImport("__Internal")] static extern void _ATT_RequestPermission(AttCallback callback);
        static Action<int> pending;

        [AOT.MonoPInvokeCallback(typeof(AttCallback))]
        static void OnAtt(int status) { var p = pending; pending = null; p?.Invoke(status); }

        public static int Status => _ATT_GetStatus();
        public static void Request(Action<int> done)
        {
            if (Status != NotDetermined) { done?.Invoke(Status); return; }
            pending = done; _ATT_RequestPermission(OnAtt);
        }
#else
        public static int Status => Authorized;
        public static void Request(Action<int> done) => done?.Invoke(Authorized);
#endif
        public static bool Allowed => Status == Authorized;
    }
}
