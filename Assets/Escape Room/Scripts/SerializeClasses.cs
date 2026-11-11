using System;
using System.Collections.Generic;
using Newtonsoft.Json;

// --- ROOT DATA STRUCTURE ---

[Serializable]
public class GameData
{
    public string format_version; // "0.2.0"
    public string exported_by;    // "ALPHA v0.2.0"
    public DateTime exported_at;
    public GameConfig game;
    public List<StageData> stages;
    public List<object> audio_tracks; // Empty in current export
    public List<Collectible> collectibles;
    public List<SubtitleTrack> subtitle_tracks;
}

// --- GAME GLOBAL CONFIGURATION ---

[Serializable]
public class GameConfig
{
    public string id;
    public string name;           // "TEST GAME DELTA"
    public string description;    // "DELTA - First test game..."
    public int button_count;      // 4
    public string escape_word;    // "ESCAPE"
    public string operator_pin;   // "1234"
    public string creator_pin;    // "8272"

    // Target Screen
    public string screen_format;  // "xiaomi_pad_7"
    public int screen_width;      // 3200
    public int screen_height;     // 2136

    // UI Visuals
    public string default_button_color;
    public string default_button_pressed_color;
    public string video_controls_style;
    public string video_controls_color;
    public string video_controls_text_color;
    public string video_controls_font;
    public float video_controls_size;
    public float video_controls_spacing;
    public float video_controls_font_size;
    
    // Branding
    public string logo_url;
    public string logo_position;
    public int logo_size_percentage;
    public bool progress_indicator_visible;
    public int starting_score = 0;
    public int master_volume_at_start = 100;
    public string subtitle_position;  // "above" or "below" for stage background videos

    // End Game Videos (Low/Medium/High)
    public string end_game_video_low_url;
    public int end_game_video_low_volume;
    public bool end_game_video_low_allow_controls;
    public bool end_game_video_low_allow_back;
    public bool end_game_video_low_allow_replay;
    public bool end_game_video_low_allow_play_pause;
    public bool end_game_video_low_allow_skip;
    public string end_game_video_low_fit_mode = "contain";
    public string end_game_video_medium_url;
    public int end_game_video_medium_volume;
    public bool end_game_video_medium_allow_controls;
    public bool end_game_video_medium_allow_back;
    public bool end_game_video_medium_allow_replay;
    public bool end_game_video_medium_allow_play_pause;
    public bool end_game_video_medium_allow_skip;
    public string end_game_video_medium_fit_mode = "contain";
    public string end_game_video_high_url;
    public int end_game_video_high_volume;
    public bool end_game_video_high_allow_controls;
    public bool end_game_video_high_allow_back;
    public bool end_game_video_high_allow_replay;
    public bool end_game_video_high_allow_play_pause;
    public bool end_game_video_high_allow_skip;
    public string end_game_video_high_fit_mode = "contain";
}

// --- STAGE DATA ---

[Serializable]
public class StageData
{
    public string id;
    public int stage_number;
    public string title;               // e.g., "Welcome to Test Game Delta"
    public string title_line2;
    public string scene;               // e.g., "Opening", "Photo", "Solve puzzle"
    
    // Background Media
    public string background_image_url;
    public string background_video_url; // Used for HUD loops
    public int background_video_volume;
    public bool background_video_loop;
    public string background_video_fit_mode = "contain";
    public string background_video_end_action;
    public int? background_video_end_goto_stage_number;
    public string stage_layout = "fullscreen";
    public string trigger_media_display = "fullscreen";
    public string lower_background_image_url;
    public string lower_background_video_url;
    public int lower_background_video_volume = 50;
    public bool lower_background_video_loop = true;
    public string lower_background_video_fit_mode = "contain";
    public bool lower_background_video_show_controls;
    public bool lower_background_video_allow_play_pause = true;
    public bool lower_background_video_allow_replay = true;
    public bool lower_background_video_allow_skip = true;
    public bool lower_background_video_allow_back = true;
    public bool background_video_show_controls;
    public bool background_video_allow_play_pause = true;
    public bool background_video_allow_replay = true;
    public bool background_video_allow_skip = true;
    public bool background_video_allow_back = true;
    
    // Audio
    public string stage_audio_url;     // Used for stage music
    public int stage_audio_volume = 50;     // Web default when the JSON leaves it out
    public bool stage_audio_loop = true;
    public float stage_audio_fade_in_duration;
    public float stage_audio_fade_out_duration;
    public bool allow_previous_stage_audio_to_continue;

    // Timer Logic
    public bool show_timer;
    public bool timer_starts_here;     // Stage 3 starts the 55m timer
    public bool timer_stops_here;      // Stage 4 stops the timer
    public int timer_duration_minutes; // 55
    public string timer_position;
    public string timer_direction;     // "up" or "down"

    // Visual Styling
    public string title_text_bg_color;
    public int title_text_bg_opacity = 50;
    public string title_text_font_family;
    public int title_text_font_size = 48;
    public string title_animation;     // "bounce", "pulse", "static"
    public float title_animation_speed;
    public int title_box_padding_x = 32;
    public int title_box_padding_y = 24;
    public string title_box_width;

    // Transitions
    public string transition_type;     // "fade", "crossfade"
    public float transition_duration;
    public string transition_direction;
    public string transition_easing;

    // Visibility Toggles
    public bool show_logo;
    public bool show_team_name;
    public bool show_team_photo;
    public bool show_photo_caption;
    public int button_offset_x;
    public int button_offset_y;
    public float button_scale = 1f;
    public bool show_volume_controls;
    public string volume_controls_position;
    public bool show_zoom_controls;
    public bool title_line3_show_score;
    public bool title_line4_show_time;
    public bool show_score;

    // Logic & Metadata
    public bool team_entry_enabled;
    public int points_awarded;
    public List<TriggerData> triggers; // The buttons/actions
    public string game_id;
    public int list_position;
    public bool is_archived;
    public DateTime created_date;
    public DateTime updated_date;
    public string created_by_id;
    public string created_by;
    public bool is_sample;
}

// --- TRIGGER DATA ---

[Serializable]
public class TriggerData
{
    public string id;
    public string trigger_type;        // e.g., "button"
    public string trigger_value;       // The physical button ID (1, 2, etc.)
    public string action_type;         // "play_media", "team_entry", "take_photo", "enter_code", "goto_stage_number"
    public string button_text;         // e.g., "START", "Take Photo", "Enter Code"
    public string button_color;
    public string button_pressed_color;
    public string button_text_color;

    // Action Content
    public string media_url;           // Video to play on click (Stage 1 START)
    public string media_type;
    public string media_display;
    public string media_video_fit_mode = "contain";
    public int video_volume;
    public bool allow_video_controls;
    public bool allow_skip_button;
    public bool allow_back_button;
    public bool allow_replay_button;
    public bool allow_play_pause_button;
    
    public int points_awarded;
    public int correct_points_awarded;
    public int incorrect_points_awarded;

    // Code/Puzzle Logic
    public List<string> correct_answers; // ["TWIX", "MARS", "BOUNTY"]
    public bool stop_timer_on_correct;
    
    // Photo Logic
    public string photo_prompt;        // "VERIFY ID - GROUP THE TEAM..."
    public int? photo_next_stage_number; // 3
    
    // Navigation
    public string next_stage_id;
    public string correct_media_next_stage_id;
    public string incorrect_media_next_stage_id;
    public string incorrect_next_stage_id;
    public string incorrect_media_url;
    public string incorrect_media_type;
    public string incorrect_media_display;
    public string incorrect_media_video_fit_mode = "contain";
    public int correct_media_goto_stage_number;
    public int incorrect_goto_stage_number;
    public int incorrect_media_goto_stage_number;
    public int goto_stage_number;      // 1 (Used for RESET GAME)
    
    // Hints
    public List<HintOption> hint_options;
    public int hint_points_penalty;
    public bool hint_requires_code;
    public int incorrect_video_volume = 100;

    // Audio Dips
    public bool prevent_audio_dip;
    public bool prevent_stage_audio_dip;

    // Subtitles: where this trigger's media shows them, and the toggle_subtitles button
    public string subtitle_position;   // "top", "centre" or "bottom"
    public string subtitle_enabled_text;
    public string subtitle_enabled_color;
    public string subtitle_disabled_text;
    public string subtitle_disabled_color;

    // Visuals
    public string text_overlay;
    public string text_overlay_bg_color;
    public int text_overlay_bg_opacity;
    public string video_transition_type;
    public float video_transition_duration;
    public string video_transition_direction;
    public string poster_url;
    
    // Metadata & Config
    public float duration_seconds;
    public string stage_id;
    public DateTime created_date;
    public DateTime updated_date;
    public string created_by_id;
    public string created_by;
    public bool is_sample;
}

[Serializable]
public class HintOption
{
    public string code;
    public string media_url;
    public string media_type;
    public int video_volume = 100;
    public int points_penalty;
}

[Serializable]
public class Collectible
{
    public string id;
    public string game_id;
    public string name;
    public string description;
    public string icon_url;
    public string stage_id;
    public bool is_hidden;
}

[Serializable]
public class SubtitleTrack
{
    public string id;
    public string name;
    public string video_url;
    public List<SubtitleCue> cues;
    public bool not_needed;
    public bool reviewed;
    public string generated_by;
    public bool is_sample;
}

[Serializable]
public class SubtitleCue
{
    public float start;
    public float end;
    public string text;
}
