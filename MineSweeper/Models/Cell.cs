namespace BugsFinder.Models;

/// <summary>Represents a single cell on the BugsFinder game board.</summary>
public class Cell
{
    /// <summary>Gets or sets the zero-based row index of this cell.</summary>
    public int Row { get; set; }

    /// <summary>Gets or sets the zero-based column index of this cell.</summary>
    public int Column { get; set; }

    /// <summary>Gets or sets whether the cell has been revealed by the player.</summary>
    public bool IsRevealed { get; set; }

    /// <summary>Gets or sets whether the cell has been flagged by the player.</summary>
    public bool IsFlagged { get; set; }

    /// <summary>Gets or sets whether this cell contains a mine.</summary>
    public bool IsBug { get; set; }

    /// <summary>Gets or sets the count of mines in the 8 neighbouring cells.</summary>
    public int AdjacentMineCount { get; set; }

    /// <summary>
    /// Gets or sets whether this cell is inside the playable area.
    /// Always <see langword="true"/> for standard rectangular boards;
    /// <see langword="false"/> for cells outside the country border in Special maps.
    /// Inactive cells are rendered as empty space and cannot be interacted with.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this mine cell is a quiz mine. Correctly flagging it for the first
    /// time triggers a multiple-choice quiz popup with a star reward.
    /// </summary>
    public bool IsQuizMine { get; set; }

    /// <summary>
    /// Whether this is a hidden bonus cell. Revealing it grants the player one star.
    /// Secret cells are always blank (no adjacent bugs, not a bug themselves).
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// Whether this cell is purely decorative (e.g. an island marker on a special map).
    /// Decorative cells are rendered but cannot be clicked, flagged, or contain mines.
    /// </summary>
    public bool IsDecorative { get; set; }
}
