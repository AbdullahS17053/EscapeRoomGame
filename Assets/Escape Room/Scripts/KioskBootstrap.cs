using UnityEngine;

/// <summary>
/// Full kiosk bootstrap for Device Owner tablets.
///
/// Kiosk mode is currently off (kioskEnabled is false), so the app runs as a normal app.
/// To turn it back on:
///   1. Tick Kiosk Enabled on _Managers > KioskBootstrap in Main Scene.
///   2. In Assets/Plugins/Android/AndroidManifest.xml and Kiosk.androidlib/src/main/AndroidManifest.xml,
///      set lockTaskMode to if_whitelisted and uncomment the kiosk permissions.
///   3. In Kiosk.androidlib/src/main/AndroidManifest.xml, set BootCompletedReceiver to enabled="true".
///
/// Kiosk policies are applied on every app launch.
/// Lock task is enabled when a JSON game is selected (EnableGameplayKiosk),
/// and disabled when returning to the main menu via escape_word (DisableGameplayKiosk).
/// Once policies are applied, Android also enters lock task on each app launch,
/// because the activities use lockTaskMode="if_whitelisted".
///
/// Provisioning (factory-reset device, no accounts):
///   adb install -r EscapeRoom.apk
///   adb shell dpm set-device-owner com.escape.room/com.escape.room.kiosk.KioskDeviceAdminReceiver
/// Staff: long-press configured corner → Admin PIN → Staff Menu
/// Escape word: Admin PIN → main menu
/// </summary>
public class KioskBootstrap : MonoBehaviour
{
    public static KioskBootstrap Instance { get; private set; }

    public enum ScreenCorner
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }

    [Header("Admin Exit")]
    [Tooltip("Used only when no game is loaded, or the game's JSON sets no pins.")]
    [SerializeField] string adminPin = "2468";
    [SerializeField] ScreenCorner adminCorner = ScreenCorner.TopLeft;
    [SerializeField] float cornerSizePixels = 96f;
    [SerializeField] float longPressSeconds = 3f;

    [Header("Behaviour")]
    [Tooltip("Master switch. Off = normal app: no lock task, policies, Staff Menu or escape word PIN. See the class comment for the manifest changes needed to turn it on.")]
    [SerializeField] bool kioskEnabled = false;
    [Tooltip("If true, enter lock task on app Start. Prefer false — enable on game select instead.")]
    [SerializeField] bool enterLockTaskOnStart = false;
    [SerializeField] bool reenterLockTaskOnFocus = true;

    bool intentionallyUnlocked;
    bool gameplayKioskActive;
    GameObject pinDialog;
    bool staffMenuOpen;
    float pressTimer;
    bool pressingInCorner;

    public bool IsKioskEnabled => kioskEnabled;
    public bool IsGameplayKioskActive => gameplayKioskActive;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (!kioskEnabled)
            return;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.fullScreen = true;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        if (!kioskEnabled)
        {
            // Tablets provisioned earlier stay Device Owner, so lift what kiosk mode left on them.
            if (AndroidKiosk.IsDeviceOwner)
                AndroidKiosk.DisableKioskPolicies();
            return;
        }

        // Apply policies now so a provisioned tablet does not need a game selected first.
        if (AndroidKiosk.IsDeviceOwner)
            AndroidKiosk.EnableKioskPolicies();

        if (!enterLockTaskOnStart)
            return;

        EnableGameplayKiosk();
    }

    void Update()
    {
        if (!kioskEnabled)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Swallow Android Back while in kiosk / staff UI.
        }

        UpdateAdminExitGesture();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus || !reenterLockTaskOnFocus)
            return;

        if (!gameplayKioskActive || intentionallyUnlocked)
            return;

        AndroidDeviceControls.RestoreAppOnlyLockTaskPackages();

        if (AndroidKiosk.IsDeviceOwner && !AndroidKiosk.IsInLockTaskMode)
            TryEnterKiosk();
    }

    /// <summary>
    /// Call when a JSON game starts (after LoadGameData / LoadStage 1).
    /// </summary>
    public void EnableGameplayKiosk()
    {
        if (!kioskEnabled)
            return;

        gameplayKioskActive = true;
        intentionallyUnlocked = false;
        TryEnterKiosk();
    }

    /// <summary>
    /// Call when returning to the main menu (escape word) or leaving gameplay lock.
    /// </summary>
    public void DisableGameplayKiosk()
    {
        if (!kioskEnabled)
            return;

        gameplayKioskActive = false;
        intentionallyUnlocked = true;
        AndroidDeviceControls.RestoreAppOnlyLockTaskPackages();
        AndroidKiosk.StopLockTask();
        Debug.Log("KioskBootstrap: gameplay kiosk disabled.");
    }

    void TryEnterKiosk()
    {
        if (!AndroidKiosk.IsDeviceOwner)
        {
            Debug.Log("KioskBootstrap: not device owner — soft kiosk only (never-sleep / fullscreen).");
            return;
        }

        intentionallyUnlocked = false;
        AndroidKiosk.EnableKioskPolicies();
        AndroidDeviceControls.RestoreAppOnlyLockTaskPackages();
        AndroidKiosk.StartLockTask();
        Debug.Log("KioskBootstrap: lock task started.");
    }

    void UpdateAdminExitGesture()
    {
        if (pinDialog != null || staffMenuOpen || StaffMenuController.Current != null)
            return;

        bool down = Input.GetMouseButton(0);
        Vector2 pos = Input.mousePosition;

        if (down && IsInAdminCorner(pos))
        {
            if (!pressingInCorner)
            {
                pressingInCorner = true;
                pressTimer = 0f;
            }

            pressTimer += Time.unscaledDeltaTime;
            if (pressTimer >= longPressSeconds)
            {
                pressingInCorner = false;
                pressTimer = 0f;
                RequestAdminPin(OpenStaffMenu);
            }
        }
        else
        {
            pressingInCorner = false;
            pressTimer = 0f;
        }
    }

    bool IsInAdminCorner(Vector2 screenPos)
    {
        float w = Screen.width;
        float h = Screen.height;
        float s = cornerSizePixels;

        switch (adminCorner)
        {
            case ScreenCorner.TopLeft:
                return screenPos.x <= s && screenPos.y >= h - s;
            case ScreenCorner.TopRight:
                return screenPos.x >= w - s && screenPos.y >= h - s;
            case ScreenCorner.BottomLeft:
                return screenPos.x <= s && screenPos.y <= s;
            case ScreenCorner.BottomRight:
                return screenPos.x >= w - s && screenPos.y <= s;
            default:
                return false;
        }
    }

    /// <summary>
    /// Shows the Admin PIN dialog. onSuccess runs only for the correct PIN;
    /// onFailed runs for a wrong PIN or Cancel.
    /// </summary>
    public void RequestAdminPin(System.Action onSuccess, System.Action onFailed = null)
    {
        if (pinDialog != null)
            return;

        UIManager ui = FindUiManager();
        if (ui == null)
        {
            Debug.LogWarning("KioskBootstrap: UIManager missing — cannot show admin PIN dialog.");
            return;
        }

        // Stage changes destroy this dialog without a callback, so its open state is the
        // object reference rather than a flag that the callbacks would have to clear.
        pinDialog = ui.ShowInputDialog(
            "Admin PIN",
            entered =>
            {
                if (!IsAcceptedPin(entered))
                {
                    Debug.Log("KioskBootstrap: incorrect admin PIN.");
                    onFailed?.Invoke();
                    return;
                }

                onSuccess?.Invoke();
            },
            onFailed,
            submitOnEscapeWord: false);
    }

    /// <summary>
    /// A loaded game's JSON decides staff access: operator_pin, or creator_pin, which
    /// also grants operator access. adminPin only applies when the JSON sets neither.
    /// </summary>
    bool IsAcceptedPin(string entered)
    {
        GameConfig game = GameManager.Instance != null ? GameManager.Instance.currentGameData?.game : null;
        bool hasOperatorPin = game != null && !string.IsNullOrEmpty(game.operator_pin);
        bool hasCreatorPin = game != null && !string.IsNullOrEmpty(game.creator_pin);

        if (!hasOperatorPin && !hasCreatorPin)
            return entered == adminPin;

        return (hasOperatorPin && entered == game.operator_pin)
            || (hasCreatorPin && entered == game.creator_pin);
    }

    void OpenStaffMenu()
    {
        Transform parent = ResolveCanvasParent();
        if (parent == null)
        {
            Debug.LogWarning("KioskBootstrap: no Canvas/UIManager for Staff Menu.");
            return;
        }

        staffMenuOpen = true;
        StaffMenuController.Show(
            parent,
            onResume: () =>
            {
                staffMenuOpen = false;
                Debug.Log("KioskBootstrap: Resume Game.");
            },
            onExitKiosk: () =>
            {
                staffMenuOpen = false;
                DisableGameplayKiosk();
                Debug.Log("KioskBootstrap: Exit Kiosk — lock task stopped.");
            },
            onOpenSettings: () =>
            {
                staffMenuOpen = false;
                // Keep gameplay session so focus can re-lock when returning from Settings.
                intentionallyUnlocked = false;
                AndroidDeviceControls.OpenAndroidSettings();
                Debug.Log("KioskBootstrap: opening Android Settings.");
            });
    }

    static UIManager FindUiManager()
    {
        if (GameManager.Instance != null && GameManager.Instance.uiManager != null)
            return GameManager.Instance.uiManager;
        return Object.FindFirstObjectByType<UIManager>();
    }

    static Transform ResolveCanvasParent()
    {
        UIManager ui = FindUiManager();
        if (ui != null)
            return ui.transform;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        return canvas != null ? canvas.transform : null;
    }
}
