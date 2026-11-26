using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using Unity.Notifications.Android;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using Unity.Notifications.iOS;
#endif

namespace SuruSavasi.Services
{
    /// <summary>Telefon bildirimleri (yerel, sunucusuz). Oyun arka plana geçince planlanır, açılınca silinir.</summary>
    public interface INotifications
    {
        void Init();
        void CancelAll();
        void Schedule(int id, string title, string body, DateTime when, string data);
        /// <summary>Oyun bir bildirime dokunularak açıldıysa/öne geldiyse o bildirimin verisi (bir kez döner), yoksa null.</summary>
        string TakeOpened();
    }

    /// <summary>Aynı bildirimi iki kez işlememek için son işlenen bildirim verisi.</summary>
    static class OpenedGuard
    {
        const string Key = "surusavasi-notif-handled";
        public static string Filter(string data)
        {
            if (string.IsNullOrEmpty(data) || PlayerPrefs.GetString(Key, "") == data) return null;
            PlayerPrefs.SetString(Key, data); PlayerPrefs.Save();
            return data;
        }
    }

    public static class NotificationsFactory
    {
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("surusavasi-notif", 1) == 1;
            set { PlayerPrefs.SetInt("surusavasi-notif", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Tür bazında açık/kapalı (Ayarlar > Bildirim türleri).</summary>
        public static bool CatOn(Core.Meta.NoteCat c) => PlayerPrefs.GetInt("surusavasi-notif-" + (int)c, 1) == 1;
        public static void SetCat(Core.Meta.NoteCat c, bool on) { PlayerPrefs.SetInt("surusavasi-notif-" + (int)c, on ? 1 : 0); PlayerPrefs.Save(); }

        public static INotifications Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidNotifications();
#elif UNITY_IOS && !UNITY_EDITOR
            return new IosNotifications();
#else
            return new EditorNotifications();
#endif
        }
    }

    /// <summary>Editor: bildirim gönderilmez, planlananlar konsola yazılır.</summary>
    public class EditorNotifications : INotifications
    {
        /// <summary>Test: "dokunulmuş bildirim" taklidi (Editor menüsü ve otomatik görüntü kullanır).</summary>
        public static string Pending;
        public void Init() { }
        public void CancelAll() { }
        public void Schedule(int id, string title, string body, DateTime when, string data) =>
            Debug.Log($"[Bildirim] {when:dd.MM HH:mm} · {title} · {body} → {data}");
        public string TakeOpened() { var p = Pending; Pending = null; return p; }
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    public class AndroidNotifications : INotifications
    {
        const string Channel = "surusavasi_main";
        public void Init()
        {
            AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel(Channel, "Sürü Savaşı", "İnşaat, üretim ve ödül hatırlatmaları", Importance.Default));
            if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed) new PermissionRequest(); // Android 13+
        }
        public void CancelAll() { AndroidNotificationCenter.CancelAllScheduledNotifications(); AndroidNotificationCenter.CancelAllDisplayedNotifications(); }
        public void Schedule(int id, string title, string body, DateTime when, string data) =>
            AndroidNotificationCenter.SendNotificationWithExplicitID(new AndroidNotification { Title = title, Text = body, FireTime = when, IntentData = data }, Channel, id);
        public string TakeOpened() => OpenedGuard.Filter(AndroidNotificationCenter.GetLastNotificationIntent()?.Notification.IntentData);
    }
#endif

#if UNITY_IOS && !UNITY_EDITOR
    public class IosNotifications : INotifications
    {
        public void Init()
        {
            var req = new AuthorizationRequest(AuthorizationOption.Alert | AuthorizationOption.Badge | AuthorizationOption.Sound, false);
        }
        public void CancelAll() { iOSNotificationCenter.RemoveAllScheduledNotifications(); iOSNotificationCenter.RemoveAllDeliveredNotifications(); }
        public string TakeOpened() => OpenedGuard.Filter(iOSNotificationCenter.GetLastRespondedNotification()?.Data);
        public void Schedule(int id, string title, string body, DateTime when, string data)
        {
            var span = when - DateTime.Now; if (span.TotalSeconds < 5) return;
            iOSNotificationCenter.ScheduleNotification(new iOSNotification(id.ToString())
            {
                Title = title, Body = body, ShowInForeground = false, Data = data,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = span, Repeats = false },
            });
        }
    }
#endif
}
