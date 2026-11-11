package com.escape.room.kiosk;

import android.app.Activity;
import android.app.admin.DevicePolicyManager;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothManager;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.media.AudioManager;
import android.net.wifi.ScanResult;
import android.net.wifi.WifiConfiguration;
import android.net.wifi.WifiInfo;
import android.net.wifi.WifiManager;
import android.os.Build;
import android.provider.Settings;
import android.util.Log;
import android.view.Window;
import android.view.WindowManager;

import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

/**
 * Device controls for Staff Menu (Wi-Fi, Bluetooth, volume, brightness, Settings).
 * Intended for Device Owner / kiosk tablets.
 */
public class DeviceControls {
    private static final String TAG = "DeviceControls";
    private static final String SETTINGS_PACKAGE = "com.android.settings";

    private static ComponentName adminComponent(Context context) {
        return new ComponentName(context, KioskDeviceAdminReceiver.class);
    }

    public static void grantStaffPermissions(Context context) {
        if (context == null || !KioskManager.isDeviceOwner(context)) {
            return;
        }
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.M) {
            return;
        }

        DevicePolicyManager dpm =
                (DevicePolicyManager) context.getSystemService(Context.DEVICE_POLICY_SERVICE);
        ComponentName admin = adminComponent(context);
        String pkg = context.getPackageName();

        String[] permissions = new String[]{
                android.Manifest.permission.ACCESS_FINE_LOCATION,
                android.Manifest.permission.ACCESS_COARSE_LOCATION,
                android.Manifest.permission.ACCESS_WIFI_STATE,
                android.Manifest.permission.CHANGE_WIFI_STATE,
                android.Manifest.permission.BLUETOOTH,
                android.Manifest.permission.BLUETOOTH_ADMIN,
                android.Manifest.permission.BLUETOOTH_CONNECT,
                android.Manifest.permission.BLUETOOTH_SCAN,
                "android.permission.NEARBY_WIFI_DEVICES",
                android.Manifest.permission.WRITE_SETTINGS
        };

        for (String permission : permissions) {
            try {
                dpm.setPermissionGrantState(
                        admin,
                        pkg,
                        permission,
                        DevicePolicyManager.PERMISSION_GRANT_STATE_GRANTED);
            } catch (Exception e) {
                Log.w(TAG, "Could not grant " + permission + ": " + e.getMessage());
            }
        }
    }

    public static boolean isWifiEnabled(Context context) {
        WifiManager wifi = wifiManager(context);
        return wifi != null && wifi.isWifiEnabled();
    }

    public static boolean setWifiEnabled(Context context, boolean enabled) {
        WifiManager wifi = wifiManager(context);
        if (wifi == null) {
            return false;
        }
        try {
            //noinspection deprecation
            return wifi.setWifiEnabled(enabled);
        } catch (Exception e) {
            Log.e(TAG, "setWifiEnabled failed", e);
            return false;
        }
    }

    public static boolean startWifiScan(Context context) {
        WifiManager wifi = wifiManager(context);
        if (wifi == null || !wifi.isWifiEnabled()) {
            return false;
        }
        try {
            //noinspection deprecation
            return wifi.startScan();
        } catch (Exception e) {
            Log.e(TAG, "startWifiScan failed", e);
            return false;
        }
    }

    /**
     * Returns newline-separated entries: ssid|levelDb|secure(0|1)
     */
    public static String getWifiScanResults(Context context) {
        WifiManager wifi = wifiManager(context);
        if (wifi == null) {
            return "";
        }
        try {
            //noinspection deprecation
            List<ScanResult> results = wifi.getScanResults();
            if (results == null || results.isEmpty()) {
                return "";
            }

            Collections.sort(results, new Comparator<ScanResult>() {
                @Override
                public int compare(ScanResult a, ScanResult b) {
                    return Integer.compare(b.level, a.level);
                }
            });

            StringBuilder sb = new StringBuilder();
            Set<String> seen = new HashSet<String>();
            for (ScanResult r : results) {
                if (r.SSID == null || r.SSID.trim().isEmpty()) {
                    continue;
                }
                String ssid = r.SSID.trim();
                if (!seen.add(ssid)) {
                    continue;
                }
                boolean secure = r.capabilities != null
                        && (r.capabilities.contains("WEP")
                        || r.capabilities.contains("WPA")
                        || r.capabilities.contains("PSK")
                        || r.capabilities.contains("EAP")
                        || r.capabilities.contains("SAE"));
                if (sb.length() > 0) {
                    sb.append('\n');
                }
                sb.append(ssid.replace('|', '/').replace('\n', ' '))
                        .append('|')
                        .append(r.level)
                        .append('|')
                        .append(secure ? '1' : '0');
            }
            return sb.toString();
        } catch (Exception e) {
            Log.e(TAG, "getWifiScanResults failed", e);
            return "";
        }
    }

    public static String getCurrentSsid(Context context) {
        WifiManager wifi = wifiManager(context);
        if (wifi == null) {
            return "";
        }
        try {
            WifiInfo info = wifi.getConnectionInfo();
            if (info == null) {
                return "";
            }
            String ssid = info.getSSID();
            if (ssid == null || ssid.equals("<unknown ssid>") || ssid.equals("0x")) {
                return "";
            }
            if (ssid.startsWith("\"") && ssid.endsWith("\"") && ssid.length() >= 2) {
                ssid = ssid.substring(1, ssid.length() - 1);
            }
            return ssid;
        } catch (Exception e) {
            Log.e(TAG, "getCurrentSsid failed", e);
            return "";
        }
    }

    /**
     * Connect to WPA/WPA2 PSK or open network. Returns "ok" or an error message.
     */
    public static String connectToWifi(Context context, String ssid, String password, boolean isOpen) {
        if (context == null || ssid == null || ssid.trim().isEmpty()) {
            return "Missing SSID";
        }
        WifiManager wifi = wifiManager(context);
        if (wifi == null) {
            return "Wi-Fi unavailable";
        }
        if (!wifi.isWifiEnabled()) {
            //noinspection deprecation
            wifi.setWifiEnabled(true);
        }

        try {
            //noinspection deprecation
            WifiConfiguration config = new WifiConfiguration();
            config.SSID = quote(ssid.trim());
            if (isOpen) {
                config.allowedKeyManagement.set(WifiConfiguration.KeyMgmt.NONE);
            } else {
                if (password == null || password.isEmpty()) {
                    return "Password required";
                }
                config.preSharedKey = quote(password);
                config.allowedKeyManagement.set(WifiConfiguration.KeyMgmt.WPA_PSK);
            }

            //noinspection deprecation
            int netId = wifi.addNetwork(config);
            if (netId == -1) {
                // Try updating an existing config with same SSID
                //noinspection deprecation
                List<WifiConfiguration> configured = wifi.getConfiguredNetworks();
                if (configured != null) {
                    for (WifiConfiguration existing : configured) {
                        if (existing.SSID != null && existing.SSID.equals(quote(ssid.trim()))) {
                            netId = existing.networkId;
                            if (!isOpen) {
                                existing.preSharedKey = quote(password);
                                //noinspection deprecation
                                wifi.updateNetwork(existing);
                            }
                            break;
                        }
                    }
                }
            }

            if (netId == -1) {
                return "Could not add network";
            }

            //noinspection deprecation
            wifi.disconnect();
            //noinspection deprecation
            boolean enabled = wifi.enableNetwork(netId, true);
            //noinspection deprecation
            wifi.reconnect();
            return enabled ? "ok" : "enableNetwork failed";
        } catch (Exception e) {
            Log.e(TAG, "connectToWifi failed", e);
            return e.getMessage() != null ? e.getMessage() : "connect failed";
        }
    }

    public static boolean isBluetoothEnabled(Context context) {
        BluetoothAdapter adapter = bluetoothAdapter(context);
        return adapter != null && adapter.isEnabled();
    }

    public static boolean setBluetoothEnabled(Context context, boolean enabled) {
        BluetoothAdapter adapter = bluetoothAdapter(context);
        if (adapter == null) {
            return false;
        }
        try {
            if (enabled) {
                //noinspection deprecation
                return adapter.enable();
            }
            //noinspection deprecation
            return adapter.disable();
        } catch (Exception e) {
            Log.e(TAG, "setBluetoothEnabled failed", e);
            return false;
        }
    }

    public static int getVolume(Context context) {
        AudioManager am = audioManager(context);
        if (am == null) {
            return 0;
        }
        return am.getStreamVolume(AudioManager.STREAM_MUSIC);
    }

    public static int getMaxVolume(Context context) {
        AudioManager am = audioManager(context);
        if (am == null) {
            return 15;
        }
        return am.getStreamMaxVolume(AudioManager.STREAM_MUSIC);
    }

    public static void setVolume(Context context, int volume) {
        AudioManager am = audioManager(context);
        if (am == null) {
            return;
        }
        int max = am.getStreamMaxVolume(AudioManager.STREAM_MUSIC);
        int clamped = Math.max(0, Math.min(volume, max));
        am.setStreamVolume(AudioManager.STREAM_MUSIC, clamped, 0);
    }

    /** Brightness 0.0–1.0 */
    public static float getBrightness(Activity activity) {
        if (activity == null) {
            return 0.5f;
        }
        try {
            int system = Settings.System.getInt(
                    activity.getContentResolver(),
                    Settings.System.SCREEN_BRIGHTNESS);
            return Math.max(0f, Math.min(1f, system / 255f));
        } catch (Exception e) {
            Window window = activity.getWindow();
            if (window != null) {
                float b = window.getAttributes().screenBrightness;
                if (b >= 0f) {
                    return b;
                }
            }
            return 0.5f;
        }
    }

    public static void setBrightness(final Activity activity, final float brightness01) {
        if (activity == null) {
            return;
        }
        final float clamped = Math.max(0.01f, Math.min(1f, brightness01));
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    if (Settings.System.canWrite(activity) || KioskManager.isDeviceOwner(activity)) {
                        Settings.System.putInt(
                                activity.getContentResolver(),
                                Settings.System.SCREEN_BRIGHTNESS,
                                Math.round(clamped * 255f));
                        try {
                            Settings.System.putInt(
                                    activity.getContentResolver(),
                                    Settings.System.SCREEN_BRIGHTNESS_MODE,
                                    Settings.System.SCREEN_BRIGHTNESS_MODE_MANUAL);
                        } catch (Exception ignored) {
                        }
                    }
                } catch (Exception e) {
                    Log.w(TAG, "System brightness write failed: " + e.getMessage());
                }

                Window window = activity.getWindow();
                if (window != null) {
                    WindowManager.LayoutParams lp = window.getAttributes();
                    lp.screenBrightness = clamped;
                    window.setAttributes(lp);
                }
            }
        });
    }

    public static void allowSettingsInLockTask(Context context) {
        if (context == null || !KioskManager.isDeviceOwner(context)) {
            return;
        }
        DevicePolicyManager dpm =
                (DevicePolicyManager) context.getSystemService(Context.DEVICE_POLICY_SERVICE);
        try {
            dpm.setLockTaskPackages(
                    adminComponent(context),
                    new String[]{context.getPackageName(), SETTINGS_PACKAGE});
        } catch (Exception e) {
            Log.e(TAG, "allowSettingsInLockTask failed", e);
        }
    }

    public static void restoreAppOnlyLockTaskPackages(Context context) {
        if (context == null || !KioskManager.isDeviceOwner(context)) {
            return;
        }
        DevicePolicyManager dpm =
                (DevicePolicyManager) context.getSystemService(Context.DEVICE_POLICY_SERVICE);
        try {
            dpm.setLockTaskPackages(
                    adminComponent(context),
                    new String[]{context.getPackageName()});
        } catch (Exception e) {
            Log.e(TAG, "restoreAppOnlyLockTaskPackages failed", e);
        }
    }

    public static void openAndroidSettings(final Activity activity) {
        if (activity == null) {
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    allowSettingsInLockTask(activity);
                    try {
                        activity.stopLockTask();
                    } catch (Exception ignored) {
                    }
                    Intent intent = new Intent(Settings.ACTION_SETTINGS);
                    intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    activity.startActivity(intent);
                    Log.i(TAG, "Opened Android Settings");
                } catch (Exception e) {
                    Log.e(TAG, "openAndroidSettings failed", e);
                }
            }
        });
    }

    private static String quote(String value) {
        return "\"" + value + "\"";
    }

    private static WifiManager wifiManager(Context context) {
        if (context == null) {
            return null;
        }
        return (WifiManager) context.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
    }

    private static AudioManager audioManager(Context context) {
        if (context == null) {
            return null;
        }
        return (AudioManager) context.getSystemService(Context.AUDIO_SERVICE);
    }

    private static BluetoothAdapter bluetoothAdapter(Context context) {
        if (context == null) {
            return null;
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            BluetoothManager bm =
                    (BluetoothManager) context.getSystemService(Context.BLUETOOTH_SERVICE);
            if (bm != null) {
                return bm.getAdapter();
            }
        }
        //noinspection deprecation
        return BluetoothAdapter.getDefaultAdapter();
    }
}
