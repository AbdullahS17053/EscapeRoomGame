using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using Newtonsoft.Json;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Manager References")]
    public UIManager uiManager;
    public ResourceManager resourceManager;
    public PhotoManager photoManager;
    public GameDataBootstrap bootstrap;

    [Header("Game State")]
    public GameData currentGameData;
    public StageData currentStage;
    public TriggerData currentTrigger; // null when on stage level
    public string teamName = "";
    private readonly List<int> stageHistory = new List<int>();
    private const int PastLastStage = -1;
    private Coroutine loadStageRoutine;
    
    // Tracks the directory of the currently loaded game's JSON file to isolate media loading
    public string CurrentLevelPath { get; private set; }

    private void Awake()
    {
        // Singleton pattern to access GameManager from anywhere
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        // Wait for bootstrap to copy bundled data on first run
        if (bootstrap != null)
            yield return StartCoroutine(bootstrap.EnsureGameDataReady());

        InitializeGameSelection();
    }

    public void InitializeGameSelection()
    {
        string gamesRoot = GameDataBootstrap.GamesRootPath;
        string[] jsonFiles = System.Array.Empty<string>();

        if (Directory.Exists(gamesRoot))
        {
            // Search recursively to find JSONs in extracted folders
            jsonFiles = Directory.GetFiles(gamesRoot, "*.json", SearchOption.AllDirectories);
        }
        else
        {
            Debug.LogWarning("Games folder not found at: " + gamesRoot);
        }

        if (jsonFiles.Length == 0)
        {
            Debug.LogWarning("No JSON game files found in: " + gamesRoot);
        }

        // Tell UIManager to show the game selection menu with whatever files exist (or empty array to clear spawned buttons)
        if (uiManager != null)
        {
            uiManager.ShowGameSelection(jsonFiles);
        }
    }

    /// <summary>
    /// Clears all loaded data in Application.persistentDataPath, stops active stage/media,
    /// resets game state, and clears spawned game buttons from the UI.
    /// </summary>
    public void ClearAllPersistentData()
    {
        Debug.Log("Clearing all persistent game data...");

        // 1. Cleanup media/audio and stage objects
        if (resourceManager != null)
        {
            resourceManager.CleanupStage();
        }

        // 2. Reset state
        currentGameData = null;
        currentStage = null;
        currentTrigger = null;
        CurrentLevelPath = null;
        teamName = "";
        stageHistory.Clear();
        AudioListener.volume = 1f;

        if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();
        if (TimerManager.Instance != null) TimerManager.Instance.StopTimer();
        if (photoManager != null) photoManager.ClearPhoto();
        if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.ResetAll();

        // 3. Clear persistentDataPath contents
        string persistentPath = Application.persistentDataPath;
        if (Directory.Exists(persistentPath))
        {
            DirectoryInfo di = new DirectoryInfo(persistentPath);
            foreach (FileInfo file in di.GetFiles())
            {
                try { file.Delete(); }
                catch (System.Exception e) { Debug.LogWarning($"Failed to delete file {file.FullName}: {e.Message}"); }
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                try { dir.Delete(true); }
                catch (System.Exception e) { Debug.LogWarning($"Failed to delete dir {dir.FullName}: {e.Message}"); }
            }
        }

        // 4. Refresh UI to clear spawned buttons and update menu state
        InitializeGameSelection();
    }


    public void LoadGameData(string filePath)
    {
        if (File.Exists(filePath))
        {
            CurrentLevelPath = Path.GetDirectoryName(filePath);
            // Copy PCM sound out of videos in the background, before their clips play
            VideoSoundFallback.PrepareFolder(Path.Combine(CurrentLevelPath, "media"));

            string jsonContent = File.ReadAllText(filePath);
            currentGameData = JsonConvert.DeserializeObject<GameData>(jsonContent);
            PreparePlayableStages();

            Debug.Log($"Game Loaded: {currentGameData.game.name}");
            
            uiManager.SetupGameLogo(currentGameData.game);

            int startingScore = currentGameData.game != null ? currentGameData.game.starting_score : 0;
            int masterVolume = currentGameData.game != null ? currentGameData.game.master_volume_at_start : 100;
            AudioListener.volume = Mathf.Clamp01(masterVolume / 100f);

            if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore(startingScore);
            if (TimerManager.Instance != null) TimerManager.Instance.StopTimer();
            if (photoManager != null) photoManager.ClearPhoto();
            if (CollectibleManager.Instance != null || uiManager != null)
                CollectibleManager.Ensure(uiManager.transform).ResetAll();
            teamName = "";
            stageHistory.Clear();

            // Initialize the game with the first stage in play order
            if (currentGameData.stages.Count > 0)
                LoadStage(currentGameData.stages[0].stage_number);

            // Lock the device for gameplay once a JSON is selected
            if (KioskBootstrap.Instance != null)
                KioskBootstrap.Instance.EnableGameplayKiosk();
        }
        else
        {
            Debug.LogError("JSON file not found at: " + filePath);
        }
    }

    /// <summary>
    /// Unloads the current JSON game, disables gameplay kiosk, and shows the main menu.
    /// </summary>
    public void ReturnToMainMenu()
    {
        Debug.Log("Returning to main menu (escape word / unload game).");

        if (uiManager != null)
            uiManager.CloseAllDialogs();

        if (resourceManager != null)
            resourceManager.CleanupStage();

        currentGameData = null;
        currentStage = null;
        currentTrigger = null;
        CurrentLevelPath = null;
        teamName = "";
        stageHistory.Clear();
        AudioListener.volume = 1f;

        if (ScoreManager.Instance != null) ScoreManager.Instance.ResetScore();
        if (TimerManager.Instance != null) TimerManager.Instance.StopTimer();
        if (photoManager != null) photoManager.ClearPhoto();
        if (CollectibleManager.Instance != null)
            CollectibleManager.Instance.ResetAll();

        if (KioskBootstrap.Instance != null)
            KioskBootstrap.Instance.DisableGameplayKiosk();

        InitializeGameSelection();
    }

    /// <summary>
    /// Escape word exit: in kiosk mode, asks for the admin PIN so players cannot unlock the tablet.
    /// </summary>
    void RequestReturnToMainMenu()
    {
        if (KioskBootstrap.Instance != null && KioskBootstrap.Instance.IsKioskEnabled)
            KioskBootstrap.Instance.RequestAdminPin(ReturnToMainMenu, ReturnToStage);
        else
            ReturnToMainMenu();
    }

    public bool IsEscapeWord(string input)
    {
        if (currentGameData?.game == null || string.IsNullOrWhiteSpace(currentGameData.game.escape_word))
            return false;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return string.Equals(
            input.Trim(),
            currentGameData.game.escape_word.Trim(),
            System.StringComparison.OrdinalIgnoreCase);
    }

    // ──────────────────────────────────────────────
    //  STAGE MANAGEMENT
    // ──────────────────────────────────────────────

    /// <summary>
    /// Archived stages never play, and list_position is the play order. The first
    /// stage, "next stage" and every stage lookup all rely on this list.
    /// </summary>
    private void PreparePlayableStages()
    {
        if (currentGameData.stages == null)
            currentGameData.stages = new List<StageData>();

        currentGameData.stages = currentGameData.stages
            .Where(s => !s.is_archived)
            .OrderBy(s => s.list_position)
            .ToList();
    }

    /// <summary>
    /// Advances to the stage after this one in play order. Past the last stage it asks
    /// for a stage that does not exist, which is what starts the end-game video.
    /// </summary>
    private void LoadNextStage(StageData from)
    {
        int index = currentGameData.stages.IndexOf(from);
        bool hasNext = index >= 0 && index + 1 < currentGameData.stages.Count;
        LoadStage(hasNext ? currentGameData.stages[index + 1].stage_number : PastLastStage);
    }

    public void LoadStage(int stageNumber, bool fromBack = false)
    {
        // Trigger media has closed, so music it lowered comes back up during the transition.
        if (resourceManager != null)
            resourceManager.SetMusicDip(false);

        if (loadStageRoutine != null)
        {
            StopCoroutine(loadStageRoutine);
            if (TransitionController.Instance != null)
                TransitionController.Instance.Cancel();
        }

        loadStageRoutine = StartCoroutine(LoadStageRoutine(stageNumber, fromBack));
    }

    private IEnumerator LoadStageRoutine(int stageNumber, bool fromBack)
    {
        if (currentGameData == null || currentGameData.stages == null)
        {
            Debug.LogError("No game data loaded.");
            yield break;
        }

        StageData nextStage = currentGameData.stages.FirstOrDefault(s => s.stage_number == stageNumber);
        if (nextStage == null)
        {
            if (TryPlayEndGameVideo())
                yield break;

            Debug.LogError($"Stage {stageNumber} not found in JSON!");
            yield break;
        }

        TransitionController trans = uiManager != null
            ? TransitionController.Ensure(uiManager.transform)
            : TransitionController.Instance;
        string type = string.IsNullOrEmpty(nextStage.transition_type) ? "fade" : nextStage.transition_type;
        float duration = nextStage.transition_duration > 0f ? nextStage.transition_duration : 0.5f;

        if (trans != null)
            yield return trans.PlayOut(type, duration, nextStage.transition_direction, nextStage.transition_easing);

        ApplyStageNow(nextStage, fromBack);

        if (trans != null)
            yield return trans.PlayIn(type, duration, nextStage.transition_direction, nextStage.transition_easing);

        loadStageRoutine = null;
    }

    private void ApplyStageNow(StageData nextStage, bool fromBack)
    {
        if (!fromBack && currentStage != null && currentStage.stage_number != nextStage.stage_number)
            stageHistory.Add(currentStage.stage_number);

        currentStage = nextStage;
        currentTrigger = null;

        Debug.Log($"Switching to Stage {nextStage.stage_number}: {currentStage.title}");

        resourceManager.CleanupStage(currentStage);
        uiManager.ApplyUIContext(UIContext.FromStage(currentStage));
        resourceManager.SetupStageMedia(currentStage);

        if (TimerManager.Instance != null)
            TimerManager.Instance.SetupTimer(currentStage);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ApplyPoints(currentStage.points_awarded);
    }

    /// <summary>
    /// Plays the score-tiered end-game video when navigation would go past the last stage.
    /// Empty URLs skip that tier with no fallback. All empty → false (stay / log).
    /// </summary>
    private bool TryPlayEndGameVideo()
    {
        GameConfig config = currentGameData != null ? currentGameData.game : null;
        if (config == null) return false;

        bool anyUrl = !string.IsNullOrEmpty(config.end_game_video_low_url)
            || !string.IsNullOrEmpty(config.end_game_video_medium_url)
            || !string.IsNullOrEmpty(config.end_game_video_high_url);
        if (!anyUrl) return false;

        int score = ScoreManager.Instance != null ? ScoreManager.Instance.GetCurrentScore() : 0;
        string url;
        int volume;
        bool allowControls;
        bool allowBack;
        bool allowReplay;
        bool allowPlayPause;
        bool allowSkip;
        string fitMode;

        if (score <= 25)
        {
            url = config.end_game_video_low_url;
            volume = config.end_game_video_low_volume;
            allowControls = config.end_game_video_low_allow_controls;
            allowBack = config.end_game_video_low_allow_back;
            allowReplay = config.end_game_video_low_allow_replay;
            allowPlayPause = config.end_game_video_low_allow_play_pause;
            allowSkip = config.end_game_video_low_allow_skip;
            fitMode = config.end_game_video_low_fit_mode;
        }
        else if (score <= 75)
        {
            url = config.end_game_video_medium_url;
            volume = config.end_game_video_medium_volume;
            allowControls = config.end_game_video_medium_allow_controls;
            allowBack = config.end_game_video_medium_allow_back;
            allowReplay = config.end_game_video_medium_allow_replay;
            allowPlayPause = config.end_game_video_medium_allow_play_pause;
            allowSkip = config.end_game_video_medium_allow_skip;
            fitMode = config.end_game_video_medium_fit_mode;
        }
        else
        {
            url = config.end_game_video_high_url;
            volume = config.end_game_video_high_volume;
            allowControls = config.end_game_video_high_allow_controls;
            allowBack = config.end_game_video_high_allow_back;
            allowReplay = config.end_game_video_high_allow_replay;
            allowPlayPause = config.end_game_video_high_allow_play_pause;
            allowSkip = config.end_game_video_high_allow_skip;
            fitMode = config.end_game_video_high_fit_mode;
        }

        if (string.IsNullOrEmpty(url)) return false;

        string localPath = resourceManager != null ? resourceManager.GetLocalPath(url) : null;
        if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
        {
            Debug.LogWarning($"End-game video missing at: {localPath}");
            return false;
        }

        TriggerData endTrigger = new TriggerData
        {
            media_url = url,
            media_display = "fullscreen",
            media_video_fit_mode = string.IsNullOrEmpty(fitMode) ? "contain" : fitMode,
            video_volume = volume,
            allow_video_controls = allowControls,
            allow_back_button = allowBack,
            allow_replay_button = allowReplay,
            allow_play_pause_button = allowPlayPause,
            allow_skip_button = allowSkip,
            next_stage_id = currentStage != null ? currentStage.id : ""
        };

        if (resourceManager != null)
            resourceManager.StopStageMedia();

        if (uiManager != null)
        {
            uiManager.ApplyUIContext(UIContext.FromTrigger(endTrigger));
            uiManager.ShowVideoOverlay(endTrigger, isEndGame: true);
        }

        return true;
    }

    public void GoBack()
    {
        if (stageHistory.Count == 0)
        {
            Debug.Log("GoBack: no previous stage in history.");
            return;
        }

        int previous = stageHistory[stageHistory.Count - 1];
        stageHistory.RemoveAt(stageHistory.Count - 1);
        LoadStage(previous, fromBack: true);
    }

    public void RunBackgroundVideoEndAction(StageData stage)
    {
        if (stage == null)
        {
            ReturnToStage();
            return;
        }

        string action = string.IsNullOrEmpty(stage.background_video_end_action)
            ? "next_stage"
            : stage.background_video_end_action.Trim().ToLowerInvariant();

        switch (action)
        {
            case "goto_stage_number":
                if (stage.background_video_end_goto_stage_number.HasValue)
                    LoadStage(stage.background_video_end_goto_stage_number.Value);
                else
                    LoadNextStage(stage);
                break;
            case "go_back":
                GoBack();
                break;
            case "repeat_media":
                if (resourceManager != null)
                    resourceManager.RestartBackgroundVideo();
                break;
            case "stop_audio":
                if (resourceManager != null)
                    resourceManager.StopAllAudio();
                break;
            case "show_scoreboard":
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.SetPillVisible(true);
                break;
            case "next_stage":
            default:
                LoadNextStage(stage);
                break;
        }
    }

    // ──────────────────────────────────────────────
    //  TRIGGER MANAGEMENT
    // ──────────────────────────────────────────────

    /// <summary>
    /// Activates a trigger as its own UI context. Hides stage UI elements
    /// that the trigger doesn't own, destroys stage buttons, then executes the action.
    /// </summary>
    public void ActivateTrigger(TriggerData trigger)
    {
        // toggle_subtitles stays on the stage: it flips the setting and relabels its button.
        if (Subtitles.IsToggle(trigger))
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ApplyPoints(trigger.points_awarded);
            Subtitles.Toggle();
            uiManager.RefreshSubtitleToggles();
            return;
        }

        currentTrigger = trigger;
        Debug.Log($"Activating Trigger: {trigger.button_text} - Action: {trigger.action_type}");

        if (trigger.action_type == "stop_audio")
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ApplyPoints(trigger.points_awarded);
            if (resourceManager != null)
                resourceManager.StopAllAudio();
            return;
        }

        // 1. Switch UI to trigger context (hides logo, title, destroys stage buttons)
        UIContext ctx = UIContext.FromTrigger(trigger);
        uiManager.ApplyUIContext(ctx);

        // 2. Stop stage background media if this trigger has its own media
        if (ctx.hasOwnMedia)
        {
            resourceManager.StopStageMedia(trigger);
        }

        // 3. Execute the trigger's action
        ExecuteTriggerAction(trigger);
    }

    /// <summary>
    /// Returns to the current stage's UI context. Called when a trigger completes
    /// without navigating to a different stage (e.g., wrong code, hint closed).
    /// </summary>
    public void ReturnToStage()
    {
        if (currentStage == null) return;

        Debug.Log($"Returning to Stage {currentStage.stage_number}: {currentStage.title}");
        currentTrigger = null;

        // Re-apply stage UI context
        uiManager.ApplyUIContext(UIContext.FromStage(currentStage));

        // Re-setup stage media (background video/audio)
        resourceManager.SetupStageMedia(currentStage);
    }

    /// <summary>
    /// Resolves the trigger's next_stage_id to determine where to go next.
    /// "__NEXT_STAGE__" → advance, same stage ID → return to stage, other ID → jump.
    /// </summary>
    public void NavigateFromTrigger(TriggerData trigger)
    {
        if (trigger == null || currentStage == null)
        {
            ReturnToStage();
            return;
        }

        string nextId = trigger.next_stage_id;

        if (nextId == "__PLAY_MEDIA__")
        {
            string destinationId = !string.IsNullOrEmpty(trigger.correct_media_next_stage_id)
                ? trigger.correct_media_next_stage_id
                : "__NEXT_STAGE__";

            if (!string.IsNullOrEmpty(trigger.media_url))
            {
                TriggerData mediaTrigger = CloneForMedia(trigger, trigger.media_url, trigger.media_type, trigger.video_volume, destinationId);
                uiManager.ShowTriggerMedia(mediaTrigger);
                return;
            }
            else
            {
                nextId = destinationId;
            }
        }

        if (nextId == "__GOTO_STAGE_NUMBER__")
        {
            int targetNumber = trigger.correct_media_goto_stage_number > 0
                ? trigger.correct_media_goto_stage_number
                : trigger.goto_stage_number;
            LoadStage(targetNumber);
            return;
        }

        if (string.IsNullOrEmpty(nextId) || nextId == currentStage.id)
        {
            // Same stage or no destination — return to stage context
            ReturnToStage();
        }
        else if (nextId == "__NEXT_STAGE__")
        {
            // Advance to the next stage in play order
            LoadNextStage(currentStage);
        }
        else
        {
            // Jump to a specific stage by ID
            var target = currentGameData.stages.FirstOrDefault(s => s.id == nextId);
            if (target != null)
                LoadStage(target.stage_number);
            else
            {
                Debug.LogWarning($"Target stage ID '{nextId}' not found. Returning to current stage.");
                ReturnToStage();
            }
        }
    }

    // ──────────────────────────────────────────────
    //  TRIGGER ACTION EXECUTION
    // ──────────────────────────────────────────────

    /// <summary>
    /// Executes the trigger's action. All logic is JSON-driven — no hardcoded behavior.
    /// </summary>
    private void ExecuteTriggerAction(TriggerData trigger)
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ApplyPoints(trigger.points_awarded);

        switch (trigger.action_type)
        {
            case "play_media":
                uiManager.ShowTriggerMedia(trigger);
                break;

            case "team_entry":
                uiManager.ShowInputDialog("Enter Team Name", (input) =>
                {
                    if (IsEscapeWord(input))
                    {
                        RequestReturnToMainMenu();
                        return;
                    }

                    Debug.Log($"Team Name Entered: {input}");
                    teamName = input;
                    NavigateFromTrigger(trigger);
                }, () => ReturnToStage());
                break;

            case "take_photo":
                Debug.Log($"Photo Prompt: {trigger.photo_prompt}");
                uiManager.ShowPhotoConfirmation(trigger);
                break;

            case "enter_code":
                uiManager.ShowInputDialog("Enter Code", (input) =>
                {
                    if (IsEscapeWord(input))
                    {
                        RequestReturnToMainMenu();
                        return;
                    }

                    string code = (input ?? "").Trim().ToUpperInvariant();
                    if (trigger.correct_answers != null &&
                        trigger.correct_answers.Any(answer => (answer ?? "").Trim().ToUpperInvariant() == code))
                    {
                        Debug.Log("Code Correct!");
                        if (ScoreManager.Instance != null)
                            ScoreManager.Instance.ApplyPoints(trigger.correct_points_awarded);
                        if (trigger.stop_timer_on_correct && TimerManager.Instance != null)
                        {
                            if (ScoreManager.Instance != null)
                                ScoreManager.Instance.ApplyPoints(TimerManager.Instance.GetRemainingCountdownMinutes());
                            TimerManager.Instance.StopTimer();
                        }
                        NavigateFromTrigger(trigger);
                    }
                    else
                    {
                        Debug.Log("Incorrect Code!");
                        HandleIncorrectCode(trigger);
                    }
                }, () => ReturnToStage());
                break;

            case "goto_stage_number":
                LoadStage(trigger.goto_stage_number);
                break;

            case "hint":
                BeginHint(trigger);
                break;

            case "next_stage":
                if (currentStage != null)
                    LoadNextStage(currentStage);
                break;

            case "go_back":
                GoBack();
                break;

            case "repeat_media":
                if (resourceManager != null)
                    resourceManager.RestartBackgroundVideo();
                break;

            case "show_scoreboard":
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.SetPillVisible(true);
                break;

            case "stop_audio":
                if (resourceManager != null)
                    resourceManager.StopAllAudio();
                break;

            default:
                Debug.LogWarning($"Action type '{trigger.action_type}' not implemented.");
                break;
        }
    }

    private void HandleIncorrectCode(TriggerData trigger)
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ApplyPoints(trigger.incorrect_points_awarded);

        if (!string.IsNullOrEmpty(trigger.incorrect_media_url))
        {
            string destId = ResolveIncorrectMediaDestination(trigger);
            TriggerData failMediaTrigger = new TriggerData
            {
                media_url = trigger.incorrect_media_url,
                subtitle_position = trigger.subtitle_position,
                media_display = !string.IsNullOrEmpty(trigger.incorrect_media_display)
                    ? trigger.incorrect_media_display
                    : trigger.media_display,
                media_video_fit_mode = !string.IsNullOrEmpty(trigger.incorrect_media_video_fit_mode)
                    ? trigger.incorrect_media_video_fit_mode
                    : trigger.media_video_fit_mode,
                video_volume = TriggerMediaHelper.FailVideoVolume(trigger),
                media_type = !string.IsNullOrEmpty(trigger.incorrect_media_type)
                    ? trigger.incorrect_media_type
                    : trigger.media_type,
                allow_skip_button = trigger.allow_skip_button,
                allow_back_button = trigger.allow_back_button,
                allow_replay_button = trigger.allow_replay_button,
                allow_play_pause_button = trigger.allow_play_pause_button,
                // The JSON has no controls switch for wrong-answer media; the web player shows none
                allow_video_controls = false,
                text_overlay = trigger.text_overlay,
                text_overlay_bg_color = trigger.text_overlay_bg_color,
                text_overlay_bg_opacity = trigger.text_overlay_bg_opacity,
                duration_seconds = trigger.duration_seconds,
                prevent_audio_dip = trigger.prevent_audio_dip,
                prevent_stage_audio_dip = trigger.prevent_stage_audio_dip,
                next_stage_id = destId
            };
            uiManager.ShowTriggerMedia(failMediaTrigger);
            return;
        }

        NavigateIncorrect(trigger);
    }

    private string ResolveIncorrectMediaDestination(TriggerData trigger)
    {
        if (!string.IsNullOrEmpty(trigger.incorrect_media_next_stage_id))
            return trigger.incorrect_media_next_stage_id;

        if (trigger.incorrect_media_goto_stage_number > 0 && currentGameData != null)
        {
            var byNumber = currentGameData.stages.FirstOrDefault(s => s.stage_number == trigger.incorrect_media_goto_stage_number);
            if (byNumber != null) return byNumber.id;
        }

        return currentStage != null ? currentStage.id : "";
    }

    private void NavigateIncorrect(TriggerData trigger)
    {
        string id = trigger != null ? trigger.incorrect_next_stage_id : null;

        if (string.IsNullOrEmpty(id) || (currentStage != null && id == currentStage.id))
        {
            ReturnToStage();
            return;
        }

        if (id == "__NEXT_STAGE__")
        {
            LoadNextStage(currentStage);
            return;
        }

        if (id == "__GOTO_STAGE_NUMBER__")
        {
            LoadStage(trigger.incorrect_goto_stage_number);
            return;
        }

        if (id == "__PLAY_MEDIA__")
        {
            if (string.IsNullOrEmpty(trigger.incorrect_media_url))
            {
                ReturnToStage();
                return;
            }

            string destId = ResolveIncorrectMediaDestination(trigger);
            TriggerData failMediaTrigger = new TriggerData
            {
                media_url = trigger.incorrect_media_url,
                subtitle_position = trigger.subtitle_position,
                media_type = !string.IsNullOrEmpty(trigger.incorrect_media_type)
                    ? trigger.incorrect_media_type
                    : trigger.media_type,
                media_display = !string.IsNullOrEmpty(trigger.incorrect_media_display)
                    ? trigger.incorrect_media_display
                    : trigger.media_display,
                media_video_fit_mode = !string.IsNullOrEmpty(trigger.incorrect_media_video_fit_mode)
                    ? trigger.incorrect_media_video_fit_mode
                    : trigger.media_video_fit_mode,
                video_volume = TriggerMediaHelper.FailVideoVolume(trigger),
                allow_skip_button = trigger.allow_skip_button,
                allow_back_button = trigger.allow_back_button,
                allow_replay_button = trigger.allow_replay_button,
                allow_play_pause_button = trigger.allow_play_pause_button,
                // The JSON has no controls switch for wrong-answer media; the web player shows none
                allow_video_controls = false,
                text_overlay = trigger.text_overlay,
                text_overlay_bg_color = trigger.text_overlay_bg_color,
                text_overlay_bg_opacity = trigger.text_overlay_bg_opacity,
                duration_seconds = trigger.duration_seconds,
                prevent_audio_dip = trigger.prevent_audio_dip,
                prevent_stage_audio_dip = trigger.prevent_stage_audio_dip,
                next_stage_id = destId
            };
            uiManager.ShowTriggerMedia(failMediaTrigger);
            return;
        }

        var target = currentGameData.stages.FirstOrDefault(s => s.id == id);
        if (target != null)
            LoadStage(target.stage_number);
        else
        {
            Debug.LogWarning($"Incorrect target stage ID '{id}' not found. Returning to current stage.");
            ReturnToStage();
        }
    }

    private void BeginHint(TriggerData trigger)
    {
        if (trigger.hint_requires_code)
        {
            uiManager.ShowInputDialog("Enter Code", (input) =>
            {
                if (IsEscapeWord(input))
                {
                    RequestReturnToMainMenu();
                    return;
                }

                HintOption match = FindHintOption(trigger, input);
                if (match == null)
                {
                    Debug.Log("Hint code did not match.");
                    BeginHint(trigger);
                    return;
                }

                ApplyHintPenalty(trigger.hint_points_penalty);
                ApplyHintPenalty(match.points_penalty);
                PlayHintMedia(trigger, match);
            }, () => ReturnToStage());
            return;
        }

        ApplyHintPenalty(trigger.hint_points_penalty);
        HintOption first = trigger.hint_options != null && trigger.hint_options.Count > 0
            ? trigger.hint_options[0]
            : null;
        PlayHintMedia(trigger, first);
    }

    private static HintOption FindHintOption(TriggerData trigger, string input)
    {
        if (trigger.hint_options == null) return null;
        string code = (input ?? "").Trim().ToUpperInvariant();
        foreach (HintOption option in trigger.hint_options)
        {
            if (option != null && !string.IsNullOrEmpty(option.code)
                && option.code.Trim().ToUpperInvariant() == code)
                return option;
        }
        return null;
    }

    private static void ApplyHintPenalty(int amount)
    {
        if (amount == 0 || ScoreManager.Instance == null) return;
        ScoreManager.Instance.ApplyPoints(-Mathf.Abs(amount));
    }

    private void PlayHintMedia(TriggerData trigger, HintOption option)
    {
        string url = option != null && !string.IsNullOrEmpty(option.media_url)
            ? option.media_url
            : trigger.media_url;
        string mediaType = option != null && !string.IsNullOrEmpty(option.media_type)
            ? option.media_type
            : trigger.media_type;
        int volume = option != null ? option.video_volume : trigger.video_volume;

        if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(trigger.text_overlay))
        {
            uiManager.ShowHintDialog(trigger);
            return;
        }

        if (string.IsNullOrEmpty(url))
        {
            uiManager.ShowHintDialog(trigger);
            return;
        }

        TriggerData mediaTrigger = CloneForMedia(trigger, url, mediaType, volume, trigger.next_stage_id);
        uiManager.ShowTriggerMedia(mediaTrigger);
    }

    private static TriggerData CloneForMedia(TriggerData source, string url, string mediaType, int volume, string nextId)
    {
        return new TriggerData
        {
            media_url = url,
            media_type = mediaType,
            media_display = source.media_display,
            media_video_fit_mode = source.media_video_fit_mode,
            video_volume = volume,
            allow_skip_button = source.allow_skip_button,
            allow_back_button = source.allow_back_button,
            allow_replay_button = source.allow_replay_button,
            allow_play_pause_button = source.allow_play_pause_button,
            allow_video_controls = source.allow_video_controls,
            text_overlay = source.text_overlay,
            text_overlay_bg_color = source.text_overlay_bg_color,
            text_overlay_bg_opacity = source.text_overlay_bg_opacity,
            duration_seconds = source.duration_seconds,
            video_transition_type = source.video_transition_type,
            video_transition_duration = source.video_transition_duration,
            video_transition_direction = source.video_transition_direction,
            poster_url = source.poster_url,
            prevent_audio_dip = source.prevent_audio_dip,
            prevent_stage_audio_dip = source.prevent_stage_audio_dip,
            subtitle_position = source.subtitle_position,
            next_stage_id = nextId
        };
    }
}