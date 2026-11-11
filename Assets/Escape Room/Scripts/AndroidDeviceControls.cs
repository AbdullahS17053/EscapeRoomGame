using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JNI bridge to com.escape.room.kiosk.DeviceControls for Staff Menu.
/// Editor stubs return safe fake data so the UI can be exercised in Play Mode.
/// </summary>
public static class AndroidDeviceControls
{
    const string ControlsClass = "com.escape.room.kiosk.DeviceControls";

    public struct WifiNetwork
    {
        public string ssid;
        public int levelDb;
        public bool secure;
    }

    public static void GrantStaffPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CallVoid("grantStaffPermissions", GetActivity());
#endif
    }

    public static bool IsWifiEnabled
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CallBool("isWifiEnabled", GetActivity());
#else
            return true;
#endif
        }
    }

    public static bool SetWifiEnabled(bool enabled)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallBool("setWifiEnabled", GetActivity(), enabled);
#else
        return true;
#endif
    }

    public static bool StartWifiScan()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallBool("startWifiScan", GetActivity());
#else
        return true;
#endif
    }

    public static List<WifiNetwork> GetWifiScanResults()
    {
        var list = new List<WifiNetwork>();
        string raw;
#if UNITY_ANDROID && !UNITY_EDITOR
        raw = CallString("getWifiScanResults", GetActivity());
#else
        raw = "Venue-WiFi|-45|1\nGuest-Network|-62|1\nOpen-Lobby|-70|0";
#endif
        if (string.IsNullOrEmpty(raw))
            return list;

        string[] lines = raw.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            string[] parts = line.Split('|');
            if (parts.Length < 3)
                continue;

            var net = new WifiNetwork
            {
                ssid = parts[0],
                levelDb = int.TryParse(parts[1], out int level) ? level : -100,
                secure = parts[2].Trim() == "1"
            };
            list.Add(net);
        }

        return list;
    }

    public static string GetCurrentSsid()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallString("getCurrentSsid", GetActivity()) ?? "";
#else
        return "Venue-WiFi";
#endif
    }

    public static string ConnectToWifi(string ssid, string password, bool isOpen)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallString("connectToWifi", GetActivity(), ssid ?? "", password ?? "", isOpen) ?? "failed";
#else
        return "ok";
#endif
    }

    public static bool IsBluetoothEnabled
    {
        get
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return CallBool("isBluetoothEnabled", GetActivity());
#else
            return false;
#endif
        }
    }

    public static bool SetBluetoothEnabled(bool enabled)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallBool("setBluetoothEnabled", GetActivity(), enabled);
#else
        return true;
#endif
    }

    public static int GetVolume()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallInt("getVolume", GetActivity());
#else
        return 8;
#endif
    }

    public static int GetMaxVolume()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallInt("getMaxVolume", GetActivity());
#else
        return 15;
#endif
    }

    public static void SetVolume(int volume)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CallVoid("setVolume", GetActivity(), volume);
#endif
    }

    public static float GetBrightness()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return CallFloat("getBrightness", GetActivity());
#else
        return 0.7f;
#endif
    }

    public static void SetBrightness(float brightness01)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CallVoid("setBrightness", GetActivity(), brightness01);
#endif
    }

    public static void OpenAndroidSettings()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CallVoid("openAndroidSettings", GetActivity());
#else
        Debug.Log("AndroidDeviceControls: OpenAndroidSettings (Editor stub)");
#endif
    }

    public static void RestoreAppOnlyLockTaskPackages()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        CallVoid("restoreAppOnlyLockTaskPackages", GetActivity());
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

    static AndroidJavaClass Controls()
    {
        return new AndroidJavaClass(ControlsClass);
    }

    static bool CallBool(string method, AndroidJavaObject activity)
    {
        try
        {
            using (var c = Controls())
                return c.CallStatic<bool>(method, activity);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
            return false;
        }
    }

    static bool CallBool(string method, AndroidJavaObject activity, bool arg)
    {
        try
        {
            using (var c = Controls())
                return c.CallStatic<bool>(method, activity, arg);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
            return false;
        }
    }

    static int CallInt(string method, AndroidJavaObject activity)
    {
        try
        {
            using (var c = Controls())
                return c.CallStatic<int>(method, activity);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
            return 0;
        }
    }

    static float CallFloat(string method, AndroidJavaObject activity)
    {
        try
        {
            using (var c = Controls())
                return c.CallStatic<float>(method, activity);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
            return 0.5f;
        }
    }

    static string CallString(string method, AndroidJavaObject activity)
    {
        try
        {
            using (var c = Controls())
                return c.CallStatic<string>(method, activity);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
            return "";
        }
    }

    static string CallString(string method, AndroidJavaObject activity, string a, string b, bool c)
    {
        try
        {
            using (var cls = Controls())
                return cls.CallStatic<string>(method, activity, a, b, c);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
            return e.Message;
        }
    }

    static void CallVoid(string method, AndroidJavaObject activity)
    {
        try
        {
            using (var c = Controls())
                c.CallStatic(method, activity);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
        }
    }

    static void CallVoid(string method, AndroidJavaObject activity, int arg)
    {
        try
        {
            using (var c = Controls())
                c.CallStatic(method, activity, arg);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
        }
    }

    static void CallVoid(string method, AndroidJavaObject activity, float arg)
    {
        try
        {
            using (var c = Controls())
                c.CallStatic(method, activity, arg);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"AndroidDeviceControls.{method}: {e.Message}");
        }
    }
#endif
}
