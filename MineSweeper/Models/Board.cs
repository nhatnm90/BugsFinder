using System;
using System.Collections.Generic;
using System.Linq;

namespace BugsFinder.Models;

/// <summary>
/// Represents the BugsFinder game board. Encapsulates all game logic:
/// mine generation with first-click safety, BFS flood-fill reveal, flagging,
/// and win detection.
/// </summary>
public class Board
{
    private readonly Cell[,] _cells;
    private readonly Random _random = new();
    private bool _minesGenerated;

    // Non-null only for Special (shaped) maps.
    private readonly HashSet<(int Row, int Col)>? _activeCells;
    private readonly HashSet<(int Row, int Col)>? _decorativeCells;

    /// <summary>Gets the number of rows on this board.</summary>
    public int Rows { get; }

    /// <summary>Gets the number of columns on this board.</summary>
    public int Columns { get; }

    /// <summary>Gets the total number of mines on this board.</summary>
    public int MineCount { get; }

    /// <summary>Gets the number of cells the player has flagged.</summary>
    public int FlaggedCount { get; private set; }

    /// <summary>
    /// Enumerates all cells in row-major order (row 0 left-to-right, then row 1, etc.)
    /// so that an <c>ObservableCollection</c> laid out in a <c>UniformGrid</c> maps correctly.
    /// </summary>
    public IEnumerable<Cell> Cells
    {
        get
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    yield return _cells[r, c];
        }
    }

    /// <summary>
    /// Initialises a new empty board. Mines are not placed until
    /// <see cref="GenerateMines"/> is called on the player's first click.
    /// </summary>
    /// <param name="rows">Number of rows.</param>
    /// <param name="columns">Number of columns.</param>
    /// <param name="mineCount">Total mines to place.</param>
    /// <param name="activeCells">
    /// Optional set of (row, col) pairs that are inside the playable area.
    /// Pass <see langword="null"/> (default) for a standard full-grid rectangular board.
    /// For Special maps this should be the country-border cell set from
    /// <see cref="SpecialMap.BuildActiveCells"/>.
    /// </param>
    public Board(int rows, int columns, int mineCount,
                 HashSet<(int Row, int Col)>? activeCells    = null,
                 HashSet<(int Row, int Col)>? decorativeCells = null)
    {
        Rows = rows;
        Columns = columns;
        MineCount = mineCount;
        _activeCells    = activeCells;
        _decorativeCells = decorativeCells;
        _cells = new Cell[rows, columns];

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                bool decorative = decorativeCells?.Contains((r, c)) == true;
                // Decorative cells are visible (IsActive = true) but not in the
                // playable _activeCells set, so they can never receive mines.
                bool active = decorative || (activeCells is null || activeCells.Contains((r, c)));
                _cells[r, c] = new Cell
                {
                    Row = r, Column = c,
                    IsActive    = active,
                    IsDecorative = decorative,
                };
            }
    }

    /// <summary>
    /// Places mines randomly while guaranteeing that
    /// <paramref name="firstClickRow"/>/<paramref name="firstClickCol"/> and all 8 of its
    /// neighbours are always mine-free (first-click safety rule).
    /// </summary>
    /// <param name="firstClickRow">Row of the first revealed cell.</param>
    /// <param name="firstClickCol">Column of the first revealed cell.</param>
    public void GenerateMines(int firstClickRow, int firstClickCol)
    {
        if (_minesGenerated) return;
        _minesGenerated = true;

        // Build safe zone: the clicked cell and its 8 neighbours (active cells only)
        var safeZone = new HashSet<(int, int)>();
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                int nr = firstClickRow + dr, nc = firstClickCol + dc;
                if (IsValid(nr, nc))
                    safeZone.Add((nr, nc));
            }

        // For shaped boards use the active-cell list as the candidate pool so we
        // never spin randomly against a sparse grid for a long time.
        if (_activeCells is not null)
        {
            var candidates = _activeCells
                .Where(p => !safeZone.Contains(p))
                .ToList();
            int toPlace = Math.Min(MineCount, candidates.Count);

            // Fisher-Yates partial shuffle to pick toPlace candidates
            for (int i = 0; i < toPlace; i++)
            {
                int j = _random.Next(i, candidates.Count);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                _cells[candidates[i].Row, candidates[i].Col].IsBug = true;
            }
        }
        else
        {
            int available = Rows * Columns - safeZone.Count;
            int toPlace = Math.Min(MineCount, available);

            int placed = 0;
            while (placed < toPlace)
            {
                int r = _random.Next(Rows);
                int c = _random.Next(Columns);
                if (!safeZone.Contains((r, c)) && !_cells[r, c].IsBug)
                {
                    _cells[r, c].IsBug = true;
                    placed++;
                }
            }
        }

        CalculateAdjacentCounts();
    }

    /// <summary>Computes <see cref="Cell.AdjacentMineCount"/> for every non-mine cell.</summary>
    public void CalculateAdjacentCounts()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                if (!_cells[r, c].IsBug)
                    _cells[r, c].AdjacentMineCount = CountAdjacentMines(r, c);
    }

    /// <summary>
    /// Reveals the cell at (<paramref name="row"/>, <paramref name="col"/>).
    /// If the cell has <c>AdjacentMineCount == 0</c> a BFS flood-fill automatically
    /// reveals all connected blank cells and their numbered borders.
    /// </summary>
    /// <returns><see langword="true"/> when a mine was revealed (triggers game over).</returns>
    public bool RevealCell(int row, int col)
    {
        if (!IsValid(row, col)) return false;

        var cell = _cells[row, col];
        if (cell.IsRevealed || cell.IsFlagged) return false;

        cell.IsRevealed = true;
        if (cell.IsBug) return true;

        if (cell.AdjacentMineCount == 0)
            FloodFill(row, col);

        return false;
    }

    /// <summary>
    /// Toggles the flag on the unrevealed cell at (<paramref name="row"/>, <paramref name="col"/>).
    /// Has no effect on already-revealed cells.
    /// </summary>
    public void FlagCell(int row, int col)
    {
        if (!IsValid(row, col)) return;

        var cell = _cells[row, col];
        if (cell.IsRevealed) return;

        if (cell.IsFlagged)
        {
            cell.IsFlagged = false;
            FlaggedCount--;
        }
        else
        {
            cell.IsFlagged = true;
            FlaggedCount++;
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when every non-mine cell has been revealed,
    /// indicating the player has won.
    /// </summary>
    public bool CheckWinCondition()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                var cell = _cells[r, c];
                if (cell.IsActive && !cell.IsDecorative && !cell.IsBug && !cell.IsRevealed)
                    return false;
            }
        return true;
    }

    /// <summary>
    /// Returns the cell at (<paramref name="row"/>, <paramref name="col"/>),
    /// or <see langword="null"/> if the coordinates are out of range.
    /// </summary>
    public Cell? GetCell(int row, int col)
        => row >= 0 && row < Rows && col >= 0 && col < Columns
            ? _cells[row, col] : null;

    /// <summary>Count of secret cells that have not yet been revealed.</summary>
    public int UnrevealedSecretCount
    {
        get
        {
            int n = 0;
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Columns; c++)
                    if (_cells[r, c].IsSecret && !_cells[r, c].IsRevealed) n++;
            return n;
        }
    }

    /// <summary>
    /// Randomly marks <paramref name="count"/> blank (non-mine, zero-adjacent) active cells
    /// as secret bonus cells. Must be called after <see cref="GenerateMines"/>.
    /// </summary>
    public void PlaceSecretCells(int count)
    {
        var candidates = new List<Cell>();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                var cell = _cells[r, c];
                if (cell.IsActive && !cell.IsDecorative && !cell.IsBug && cell.AdjacentMineCount == 0)
                    candidates.Add(cell);
            }

        int take = Math.Min(count, candidates.Count);
        for (int i = 0; i < take; i++)
        {
            int j = _random.Next(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            candidates[i].IsSecret = true;
        }
    }

    /// <summary>
    /// Randomly designates <paramref name="count"/> mine cells as quiz mines.
    /// Must be called after <see cref="GenerateMines"/>.
    /// </summary>
    public void PlaceQuizMines(int count)
    {
        var mines = new List<Cell>();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                var cell = _cells[r, c];
                if (cell.IsBug && cell.IsActive && !cell.IsDecorative)
                    mines.Add(cell);
            }

        int take = Math.Min(count, mines.Count);
        for (int i = 0; i < take; i++)
        {
            int j = _random.Next(i, mines.Count);
            (mines[i], mines[j]) = (mines[j], mines[i]);
            mines[i].IsQuizMine = true;
        }
    }

    /// <summary>
    /// Called when a star shields the player from a bug hit.
    /// Un-reveals the cell and auto-flags it so it cannot be clicked again.
    /// </summary>
    public void ShieldBugCell(int row, int col)
    {
        var cell = _cells[row, col];
        cell.IsRevealed = false;
        if (!cell.IsFlagged)
        {
            cell.IsFlagged = true;
            FlaggedCount++;
        }
    }

    /// <summary>
    /// Reveals all mine cells so the player can see the full board on loss.
    /// </summary>
    public void RevealAllMines()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                if (_cells[r, c].IsBug)
                    _cells[r, c].IsRevealed = true;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Iterative BFS flood-fill from a blank cell. Reveals all connected
    /// blank cells and their numbered border cells.
    /// </summary>
    private void FloodFill(int startRow, int startCol)
    {
        var queue   = new Queue<(int r, int c)>();
        var visited = new HashSet<(int, int)> { (startRow, startCol) };
        queue.Enqueue((startRow, startCol));

        while (queue.Count > 0)
        {
            var (r, c) = queue.Dequeue();

            foreach (var (nr, nc) in GetNeighbors(r, c))
            {
                if (visited.Contains((nr, nc))) continue;
                visited.Add((nr, nc));

                var neighbor = _cells[nr, nc];
                if (neighbor.IsRevealed || neighbor.IsFlagged || neighbor.IsBug) continue;

                neighbor.IsRevealed = true;

                if (neighbor.AdjacentMineCount == 0)
                    queue.Enqueue((nr, nc));
            }
        }
    }

    private int CountAdjacentMines(int row, int col)
    {
        int count = 0;
        foreach (var (nr, nc) in GetNeighbors(row, col))
            if (_cells[nr, nc].IsBug) count++;
        return count;
    }

    private IEnumerable<(int r, int c)> GetNeighbors(int row, int col)
    {
        for (int dr = -1; dr <= 1; dr++)
            for (int dc = -1; dc <= 1; dc++)
            {
                if (dr == 0 && dc == 0) continue;
                int nr = row + dr, nc = col + dc;
                if (IsValid(nr, nc)) yield return (nr, nc);
            }
    }

    private bool IsValid(int row, int col) =>
        row >= 0 && row < Rows && col >= 0 && col < Columns &&
        (_activeCells is null || _activeCells.Contains((row, col))) &&
        (_decorativeCells is null || !_decorativeCells.Contains((row, col)));
}
