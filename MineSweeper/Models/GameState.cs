namespace MineSweeper.Models;

/// <summary>Represents the lifecycle state of a Minesweeper game session.</summary>
public enum GameState
{
    /// <summary>Game has not yet started; mines have not been placed.</summary>
    NotStarted,

    /// <summary>Game is actively in progress.</summary>
    Playing,

    /// <summary>Player revealed all safe cells — victory.</summary>
    Won,

    /// <summary>Player revealed a mine — defeat.</summary>
    Lost
}
