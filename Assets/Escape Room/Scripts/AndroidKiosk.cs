using UnityEngine;

/// <summary>
/// JNI bridge to com.escape.room.kiosk.KioskManager.
/// Device-owner provisioning:
///   adb install -r EscapeRoom.apk
///   adb shell dpm set-device-owner com.escape.room/com.escape.room.kiosk.KioskDeviceAdminReceiver
/// </summary>
public static class AndroidKiosk
{
    const string ManagerClass = "com.escape.room.kiosk.KioskManager";

    public static bool IsDeviceOwner
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CallBool("isDeviceOwner", GetActivity());
#else
            return false;
#endif
        }
    }

    public static bool IsInLockTaskMode
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CallBool("isInLockTaskMode", GetActivity());
#else
            return false;
#endif
        }
    }

    public static bool EnableKioskPolicies()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallBool("enableKioskPolicies", GetActivity());
#else
        return false;
#endif
    }

    public static bool DisableKioskPolicies()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallBool("disableKioskPolicies", GetActivity());
#else
        return false;
#endif
    }

    public static void StartLockTask()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CallVoid("startLockTask", GetActivity());
#endif
    }

    public static void StopLockTask()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CallVoid("stopLockTask", GetActivity());
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    static AndroidJavaObject GetActivity()
    {
        using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            return unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }
    }

    static bool CallBool(string method, AndroidJavaObject activity)
    {
        if (activity == null)
            return false;

        try
        {
            using (var manager = new AndroidJavaClass(ManagerClass))
            {
                return manager.CallStatic<bool>(method, activity);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"AndroidKiosk.{method} failed: {e.Message}");
            return false;
        }
    }

    static void CallVoid(string method, AndroidJavaObject activity)
    {
        if (activity == null)
            return;

        try
        {
            using (var manager = new AndroidJavaClass(ManagerClass))
            {
                manager.CallStatic(method, activity);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"AndroidKiosk.{method} failed: {e.Message}");
        }
    }
#endif
}
