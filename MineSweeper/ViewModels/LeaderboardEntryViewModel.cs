namespace BugsFinder.ViewModels;

/// <summary>Represents a single row in the post-win leaderboard popup.</summary>
public sealed class LeaderboardEntryViewModel
{
    /// <summary>1-based position in the leaderboard (1 = fastest).</summary>
    public int    Rank        { get; }

    /// <summary>Player name.</summary>
    public string Name        { get; }

    /// <summary>Completion time formatted as "Xs" (e.g. "42s").</summary>
    public string TimeDisplay { get; }

    /// <summary>Whether this row belongs to the player who just won.</summary>
    public bool   IsHighlight { get; }

    public LeaderboardEntryViewModel(int rank, string name, string timeDisplay, bool isHighlight)
    {
        Rank        = rank;
        Name        = name;
        TimeDisplay = timeDisplay;
        IsHighlight = isHighlight;
    }
}
