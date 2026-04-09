using System.Collections.Generic;
using System.Linq;

namespace BugsFinder.Models;

/// <summary>
/// Abstract definition of a shaped BugsFinder map.
/// Concrete subclasses describe one country's border — grid dimensions,
/// mine count, and the set of active (playable) cells.
/// To add a new country: subclass <see cref="SpecialMap"/>, override the
/// five abstract members, and add an instance to <see cref="SpecialMapRegistry.Maps"/>.
/// </summary>
public abstract class SpecialMap
{
    /// <summary>Stable identifier used for command parameters and persistence.</summary>
    public abstract string Id { get; }

    /// <summary>Display label shown in the UI (may include emoji).</summary>
    public abstract string DisplayName { get; }

    /// <summary>Flag emoji shown alongside the map name.</summary>
    public abstract string FlagEmoji { get; }

    /// <summary>Number of rows in the bounding grid.</summary>
    public abstract int Rows { get; }

    /// <summary>Number of columns in the bounding grid.</summary>
    public abstract int Cols { get; }

    /// <summary>Total mines to scatter across active cells.</summary>
    public abstract int MineCount { get; }

    /// <summary>
    /// Builds the set of (row, col) pairs that are inside the country border.
    /// Only these cells are playable; all others are rendered as empty space.
    /// </summary>
    public abstract HashSet<(int Row, int Col)> BuildActiveCells();

    /// <summary>
    /// Builds the set of (row, col) pairs for purely decorative cells
    /// (e.g. island markers). They are rendered but cannot be interacted with.
    /// Returns an empty set by default; override in subclasses that need decorations.
    /// </summary>
    public virtual HashSet<(int Row, int Col)> BuildDecorativeCells() => [];
}

/// <summary>
/// Vietnam map — 28 × 26 bounding grid, 207 active cells, 35 mines (~17 % density).
/// Wide grid makes the S-shape clearly visible:
///   • Rows  0-1  : narrow northern tip (3-5 cells)
///   • Rows  2-5  : Red River Delta / Hà Nội — widens to 13 cells
///   • Rows  6-10 : gradually narrows toward the waist
///   • Rows 11-12 : Quảng Bình — only 3 cells wide (most recognisable feature)
///   • Rows 13-19 : central coast — coast shifts east, widens slightly (4-6 cells)
///   • Rows 20-22 : TP.HCM / Mekong fan — widens quickly (9-15 cells)
///   • Rows 23-27 : Cà Mau tip — tapers back to 3 cells
/// Cell size is 20 px (+ 1 px margin) so 28 rows fit on a standard 1080 p screen.
/// </summary>
public sealed class VietnamMap : SpecialMap
{
    public override string Id          => "Vietnam";
    public override string DisplayName => "Việt Nam";
    public override string FlagEmoji   => "🇻🇳";
    // Rows = 35  (indices 0-34).  The south taper uses 10 rows so Cà Mau
    // peninsula looks long and gradual instead of abruptly cut off.
    // A ScrollViewer in the UI handles any screen height automatically.
    public override int    Rows        => 34;
    public override int    Cols        => 34;
    public override int    MineCount   => 25;

    // Each tuple: (row, firstActiveCol, lastActiveCol) — both ends inclusive.
    // Cols 0-25.  Coast runs along the right side; western highlands on the left.
    //
    // Key shape anchors:
    //   • Widest north  (row 4)  : cols 6-18  → 13 cells
    //   • Narrowest waist (rows 11-12): cols 14-16 → 3 cells  ← S-shape "pinch"
    //   • Widest south  (row 24) : cols 6-20  → 15 cells
    //   • Cà Mau tip    (row 34) : col  13    →  1 cell
    private static readonly (int Row, int StartCol, int EndCol)[] Ranges =
    [
        // ── North (rows 0-4) ──────────────────────────────────────────────────
        ( 0, 12, 12),
        ( 1, 11, 15),
        ( 2,  9, 17), 
        ( 3,  7, 19), 
        ( 4,  6, 18), 

        // ── Narrowing toward waist (rows 5-10) ───────────────────────────────
        ( 5,  7, 17),
        ( 6,  10, 15),
        ( 7,  9, 15),
        ( 8, 11, 14),
        ( 9, 12, 14),
        (10, 13, 15),

        // ── Narrow waist (rows 11-12) — most distinctive feature ────────────
        (11, 14, 16),
        (12, 15, 17),

        // ── Central coast — shifts east (rows 13-19) ─────────────────────────
        (13, 16, 17),
        (14, 15, 18),
        (15, 16, 18),
        (16, 17, 19),
        (17, 18, 20),
        (18, 19, 20),
        (19, 18, 21),

        // ── South widens (rows 20-24) — Mekong fan ───────────────────────────
        (20, 18, 21),
        (21, 17, 22),
        (22, 18, 22),
        (23, 19, 23),
        (24, 18, 23),

        // ── Cà Mau peninsula — long gradual taper (rows 25-34) ───────────────
        (25, 17, 24),
        (26, 14, 24),
        (27, 12, 23),
        (28, 10, 22),
        (29, 11, 21),
        (30, 12, 19),
        (31, 13, 18),
        (32, 14, 17),
        (33, 14, 15)    ];

    public override HashSet<(int Row, int Col)> BuildActiveCells()
    {
        var cells = new HashSet<(int, int)>();
        foreach (var (row, startCol, endCol) in Ranges)
            for (int c = startCol; c <= endCol; c++)
                cells.Add((row, c));
        return cells;
    }

    // Decorative island markers in the East Sea (South China Sea):
    //   (8, 29)  — Hoàng Sa / Paracel Islands area
    //   (24, 30) — Trường Sa / Spratly Islands area (north)
    //   (26, 31) — Trường Sa / Spratly Islands area (south)
    public override HashSet<(int Row, int Col)> BuildDecorativeCells() =>
    [
        ( 7, 27),
        ( 8, 29),
        ( 9, 28),
        (23, 29),
        (23, 30),
        (24, 30),
        (24, 32),
        (25, 30),
        (26, 31),
    ];
}

/// <summary>
/// Central catalogue of all available <see cref="SpecialMap"/> definitions.
/// To add a new country: instantiate its subclass and append it to <see cref="Maps"/>.
/// </summary>
public static class SpecialMapRegistry
{
    /// <summary>All registered special maps, in display order.</summary>
    public static readonly IReadOnlyList<SpecialMap> Maps = new List<SpecialMap>
    {
        new VietnamMap(),
        // new JapanMap(),
        // new FranceMap(),
        // …add new countries here
    };

    /// <summary>The default map used when the player first selects Special mode.</summary>
    public static SpecialMap Default => Maps[0];

    /// <summary>Finds a map by its <see cref="SpecialMap.Id"/>, or null if not found.</summary>
    public static SpecialMap? Find(string id) =>
        Maps.FirstOrDefault(m => m.Id == id);
}
