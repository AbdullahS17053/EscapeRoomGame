package com.escape.room.kiosk;

import android.app.Activity;
import android.app.ActivityManager;
import android.app.admin.DevicePolicyManager;
import android.content.ComponentName;
import android.content.Context;
import android.os.Build;
import android.os.UserManager;
import android.util.Log;
import android.view.WindowManager;

/**
 * JNI entry points for Unity (AndroidKiosk.cs).
 *
 * Provisioning (ops):
 *   adb install -r EscapeRoom.apk
 *   adb shell dpm set-device-owner com.escape.room/com.escape.room.kiosk.KioskDeviceAdminReceiver
 * Then launch the app once so enableKioskPolicies runs. startLockTask runs when a game is selected.
 */
public class KioskManager {
    private static final String TAG = "KioskManager";

    private static ComponentName adminComponent(Context context) {
        return new ComponentName(context, KioskDeviceAdminReceiver.class);
    }

    public static boolean isDeviceOwner(Context context) {
        if (context == null) {
            return false;
        }
        DevicePolicyManager dpm =
                (DevicePolicyManager) context.getSystemService(Context.DEVICE_POLICY_SERVICE);
        return dpm != null && dpm.isDeviceOwnerApp(context.getPackageName());
    }

    public static boolean enableKioskPolicies(Context context) {
        if (context == null || !isDeviceOwner(context)) {
            Log.w(TAG, "enableKioskPolicies: not device owner");
            return false;
        }

        DevicePolicyManager dpm =
                (DevicePolicyManager) context.getSystemService(Context.DEVICE_POLICY_SERVICE);
        ComponentName admin = adminComponent(context);

        try {
            dpm.setLockTaskPackages(admin, new String[]{context.getPackageName()});

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                // Android keeps the power menu available in lock task unless this is set.
                dpm.setLockTaskFeatures(admin, DevicePolicyManager.LOCK_TASK_FEATURE_NONE);
            }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                dpm.setKeyguardDisabled(admin, true);
                dpm.setStatusBarDisabled(admin, true);
            }

            dpm.addUserRestriction(admin, UserManager.DISALLOW_SAFE_BOOT);
            dpm.addUserRestriction(admin, UserManager.DISALLOW_FACTORY_RESET);
            dpm.addUserRestriction(admin, UserManager.DISALLOW_ADD_USER);

            DeviceControls.grantStaffPermissions(context);

            Log.i(TAG, "Kiosk policies enabled");
            return true;
        } catch (SecurityException e) {
            Log.e(TAG, "Failed to enable kiosk policies", e);
            return false;
        }
    }

    /**
     * Lifts everything enableKioskPolicies set. The app stays Device Owner, so this can be turned
     * back on later without a factory reset.
     */
    public static boolean disableKioskPolicies(Context context) {
        if (context == null || !isDeviceOwner(context)) {
            Log.w(TAG, "disableKioskPolicies: not device owner");
            return false;
        }

        DevicePolicyManager dpm =
                (DevicePolicyManager) context.getSystemService(Context.DEVICE_POLICY_SERVICE);
        ComponentName admin = adminComponent(context);

        try {
            dpm.setLockTaskPackages(admin, new String[0]);

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
                dpm.setLockTaskFeatures(admin, DevicePolicyManager.LOCK_TASK_FEATURE_GLOBAL_ACTIONS);
            }

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                dpm.setKeyguardDisabled(admin, false);
                dpm.setStatusBarDisabled(admin, false);
            }

            dpm.clearUserRestriction(admin, UserManager.DISALLOW_SAFE_BOOT);
            dpm.clearUserRestriction(admin, UserManager.DISALLOW_FACTORY_RESET);
            dpm.clearUserRestriction(admin, UserManager.DISALLOW_ADD_USER);

            Log.i(TAG, "Kiosk policies disabled");
            return true;
        } catch (SecurityException e) {
            Log.e(TAG, "Failed to disable kiosk policies", e);
            return false;
        }
    }

    public static void startLockTask(final Activity activity) {
        if (activity == null) {
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    activity.getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
                    activity.startLockTask();
                    Log.i(TAG, "startLockTask called");
                } catch (Exception e) {
                    Log.e(TAG, "startLockTask failed", e);
                }
            }
        });
    }

    public static void stopLockTask(final Activity activity) {
        if (activity == null) {
            return;
        }
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    activity.stopLockTask();
                    Log.i(TAG, "stopLockTask called");
                } catch (Exception e) {
                    Log.e(TAG, "stopLockTask failed", e);
                }
            }
        });
    }

    public static boolean isInLockTaskMode(Context context) {
        if (context == null) {
            return false;
        }
        ActivityManager am =
                (ActivityManager) context.getSystemService(Context.ACTIVITY_SERVICE);
        if (am == null) {
            return false;
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
            return am.getLockTaskModeState() != ActivityManager.LOCK_TASK_MODE_NONE;
        }
        //noinspection deprecation
        return am.isInLockTaskMode();
    }
}
