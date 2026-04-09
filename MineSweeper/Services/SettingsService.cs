using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MineSweeper.Services;

// ── Data contracts ────────────────────────────────────────────────────────────

/// <summary>Official world-record entry for a single difficulty mode.</summary>
public readonly record struct WorldRecord(
    /// <summary>Display string shown in the leaderboard (e.g. "6.75s").</summary>
    string TimeDisplay,
    /// <summary>Record holder name(s).</summary>
    string Holder,
    /// <summary>Actual world-record time in seconds (e.g. 6.75).</summary>
    double RealSeconds);

/// <summary>Root settings object persisted to disk.</summary>
internal sealed class AppSettings
{
    [JsonPropertyName("theme")]
    public string Theme { get; set; } = "Dark";

    /// <summary>Per-mode leaderboard lists (sorted ascending by Seconds, capped at 10).</summary>
    [JsonPropertyName("leaderboards")]
    public Dictionary<string, List<GameRecord>> Leaderboards { get; set; } = new();
}

/// <summary>A single leaderboard entry: player name + completion time.</summary>
public sealed class GameRecord
{
    [JsonPropertyName("seconds")]
    public int Seconds { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// ── Service ───────────────────────────────────────────────────────────────────

/// <summary>
/// Manages all persisted settings (theme, per-mode leaderboards)
/// in a single JSON file at <c>%LocalAppData%\MineSweeper\settings.json</c>.
/// Call <see cref="Load"/> once at startup before accessing any property.
/// </summary>
public static class SettingsService
{
    /// <summary>
    /// Default number of stars granted when the application starts.
    /// Configured via <c>appsettings.json → defaultStars</c>; falls back to 14.
    /// </summary>
    public static int DefaultStars { get; private set; } = 14;

    // ── Application configuration (appsettings.json) ─────────────────────────

    private sealed class AppConfig
    {
        [JsonPropertyName("claudeProSubscriptionUrl")]
        public string ClaudeProSubscriptionUrl { get; set; } = "https://claude.ai/upgrade";

        [JsonPropertyName("defaultStars")]
        public int DefaultStars { get; set; } = 14;

        [JsonPropertyName("worldRecordBufferMultiplier")]
        public double WorldRecordBufferMultiplier { get; set; } = 1.0;
    }

    /// <summary>URL for the Claude Pro subscription prize shown when a world record is broken.</summary>
    public static string ClaudeProSubscriptionUrl { get; private set; } = "https://claude.ai/upgrade";

    /// <summary>
    /// Multiplier applied to each world-record's real time to derive the effective
    /// beat threshold. Default 1.0 = exact record. Increase for demo/testing
    /// (e.g. 10.0 lets a player finish Easy in ≤4 s to trigger the prize popup).
    /// </summary>
    public static double WorldRecordBufferMultiplier { get; private set; } = 1.0;

    /// <summary>Official world records per mode key. Keyed by "Easy", "Medium", "Hard".</summary>
    public static IReadOnlyDictionary<string, WorldRecord> WorldRecords { get; } =
        new Dictionary<string, WorldRecord>
        {
            ["Easy"]   = new("0.49s",  "Kamil Murański & Ze-En Ju", 0.49),
            ["Medium"] = new("6.75s",  "Kamil Murański",             6.75),
            ["Hard"]   = new("27.53s", "Ze-En Ju",                  27.53),
        };

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MineSweeper", "settings.json");

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented               = true,
        PropertyNameCaseInsensitive = true
    };

    private static AppSettings _s = new();

    // ── Read-only accessors ───────────────────────────────────────────────────

    /// <summary>The last saved theme name, or <c>"Dark"</c> if not set.</summary>
    public static string Theme => _s.Theme;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Loads settings from disk. Safe to call even if the file does not exist.</summary>
    public static void Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return;
            var json = File.ReadAllText(SettingsPath);
            _s = JsonSerializer.Deserialize<AppSettings>(json, _opts) ?? new();
        }
        catch { _s = new(); }

        // Load appsettings.json (application-level config shipped with the executable)
        try
        {
            var cfgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(cfgPath))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(cfgPath), _opts);
                if (cfg != null)
                {
                    ClaudeProSubscriptionUrl    = cfg.ClaudeProSubscriptionUrl;
                    DefaultStars                = cfg.DefaultStars;
                    WorldRecordBufferMultiplier = Math.Max(0.01, cfg.WorldRecordBufferMultiplier);
                }
            }
        }
        catch { }
    }

    // ── Write helpers ─────────────────────────────────────────────────────────

    /// <summary>Persists the active theme name.</summary>
    public static void SaveTheme(string theme)
    {
        _s.Theme = theme;
        Persist();
    }

    /// <summary>
    /// Returns up to 10 leaderboard entries for <paramref name="modeKey"/>,
    /// sorted ascending by seconds.
    /// </summary>
    public static List<GameRecord> GetLeaderboard(string modeKey)
    {
        if (!_s.Leaderboards.TryGetValue(modeKey, out var list)) return new();
        return list.OrderBy(r => r.Seconds).Take(10).ToList();
    }

    /// <summary>
    /// Adds an entry to the leaderboard for <paramref name="modeKey"/>, keeps the
    /// top 10 by shortest time, and persists.
    /// </summary>
    /// <returns>0-based rank of the new entry in the top-10 list, or -1 if it did not make the top 10.</returns>
    public static int AddToLeaderboard(string modeKey, int seconds, string name)
    {
        if (!_s.Leaderboards.TryGetValue(modeKey, out var list))
        {
            list = new();
            _s.Leaderboards[modeKey] = list;
        }

        var entry = new GameRecord { Seconds = seconds, Name = name };
        list.Add(entry);
        list.Sort((a, b) => a.Seconds.CompareTo(b.Seconds));

        int rank = list.IndexOf(entry); // 0-based position after sort

        if (list.Count > 10)
            list.RemoveRange(10, list.Count - 10);

        // If entry was beyond position 9 it was truncated
        if (rank >= 10) rank = -1;

        Persist();
        return rank;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void Persist()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(_s, _opts));
        }
        catch { /* silently ignore if disk is unavailable */ }
    }
}
