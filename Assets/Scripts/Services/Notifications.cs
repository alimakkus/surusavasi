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
        void Schedule(int id, string title, string body, DateTime when);
    }

    public static class NotificationsFactory
    {
        public static bool Enabled
        {
            get => PlayerPrefs.GetInt("surusavasi-notif", 1) == 1;
            set { PlayerPrefs.SetInt("surusavasi-notif", value ? 1 : 0); PlayerPrefs.Save(); }
        }

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
        public void Init() { }
        public void CancelAll() { }
        public void Schedule(int id, string title, string body, DateTime when) =>
            Debug.Log($"[Bildirim] {when:dd.MM HH:mm} · {title} · {body}");
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
        public void Schedule(int id, string title, string body, DateTime when) =>
            AndroidNotificationCenter.SendNotificationWithExplicitID(new AndroidNotification { Title = title, Text = body, FireTime = when }, Channel, id);
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
        public void Schedule(int id, string title, string body, DateTime when)
        {
            var span = when - DateTime.Now; if (span.TotalSeconds < 5) return;
            iOSNotificationCenter.ScheduleNotification(new iOSNotification(id.ToString())
            {
                Title = title, Body = body, ShowInForeground = false,
                Trigger = new iOSNotificationTimeIntervalTrigger { TimeInterval = span, Repeats = false },
            });
        }
    }
#endif
}
