package com.escape.room.kiosk;

import android.app.admin.DeviceAdminReceiver;
import android.content.Context;
import android.content.Intent;
import android.util.Log;

/**
 * Device-admin component required for Device Owner provisioning:
 *
 *   adb install -r EscapeRoom.apk
 *   adb shell dpm set-device-owner com.escape.room/com.escape.room.kiosk.KioskDeviceAdminReceiver
 *
 * Device must be factory-reset / have no accounts when setting device owner.
 */
public class KioskDeviceAdminReceiver extends DeviceAdminReceiver {
    private static final String TAG = "KioskDeviceAdmin";

    @Override
    public void onEnabled(Context context, Intent intent) {
        Log.i(TAG, "Device admin enabled");
    }

    @Override
    public void onDisabled(Context context, Intent intent) {
        Log.i(TAG, "Device admin disabled");
    }
}
