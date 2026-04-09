using System;
using System.Windows.Input;
using MineSweeper.Models;

namespace MineSweeper.ViewModels;

/// <summary>
/// ViewModel for a single Minesweeper cell. Wraps a <see cref="Cell"/> model and
/// exposes UI-friendly properties and commands consumed by the cell
/// <c>Button</c> in <c>MainWindow.xaml</c>.
/// </summary>
public sealed class CellViewModel : ViewModelBase
{
    private readonly Cell _cell;
    private readonly Action<int, int> _onReveal;
    private readonly Action<int, int> _onFlag;
    private bool _isGameOver;

    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Zero-based row index of the wrapped cell.</summary>
    public int Row    => _cell.Row;

    /// <summary>Zero-based column index of the wrapped cell.</summary>
    public int Column => _cell.Column;

    // ── Display properties (consumed by XAML bindings) ────────────────────────

    /// <summary>
    /// Text shown inside the cell button:
    /// <list type="bullet">
    ///   <item>Unrevealed &amp; unflagged → empty string</item>
    ///   <item>Flagged (not revealed)     → 🚩</item>
    ///   <item>Revealed bug               → 🐞</item>
    ///   <item>Revealed with count &gt; 0 → digit character 1–8</item>
    ///   <item>Revealed blank (count = 0) → empty string</item>
    /// </list>
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (_cell.IsRevealed)
                return _cell.IsBug ? "🐞"
                     : _cell.AdjacentMineCount > 0 ? _cell.AdjacentMineCount.ToString()
                     : string.Empty;

            return _cell.IsFlagged ? "🚩" : string.Empty;
        }
    }

    /// <summary>
    /// Whether this cell can receive user input (not yet revealed, not decorative, game still running).
    /// Bound to the <c>Button.IsEnabled</c> property; disabling prevents both
    /// left-click and right-click <c>InputBindings</c> from firing.
    /// </summary>
    public bool IsEnabled => !_cell.IsRevealed && !_isGameOver && !_cell.IsDecorative;

    /// <summary>
    /// Whether this cell is a decorative marker (e.g. an island on a special map).
    /// Decorative cells are visible but cannot be interacted with.
    /// </summary>
    public bool IsDecorative => _cell.IsDecorative;

    /// <summary>
    /// Forwarded from the model. Used by XAML <c>DataTrigger</c>s to switch the
    /// cell between its 3-D unrevealed look and the flat revealed look.
    /// </summary>
    public bool IsRevealed => _cell.IsRevealed;

    /// <summary>Forwarded from the model. Used by XAML <c>DataTrigger</c>s.</summary>
    public bool IsFlagged  => _cell.IsFlagged;

    /// <summary>
    /// <see langword="true"/> when the cell is both a mine and has been revealed
    /// (i.e., the board is in a lost state). Triggers the red mine background in XAML.
    /// </summary>
    public bool IsMineRevealed => _cell.IsRevealed && _cell.IsBug;

    /// <summary>
    /// Adjacent mine count forwarded from the model.
    /// Bound to <c>NumberToColorConverter</c> to produce the correct foreground colour.
    /// </summary>
    public int AdjacentMineCount => _cell.AdjacentMineCount;

    /// <summary>
    /// Whether this cell is inside the playable area. Always <see langword="true"/>
    /// for regular boards; <see langword="false"/> for cells outside the country
    /// border in Special maps. Inactive cells are hidden in the UI.
    /// </summary>
    public bool IsActive => _cell.IsActive;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Left-click: reveals this cell via the parent ViewModel callback.</summary>
    public ICommand RevealCommand { get; }

    /// <summary>Right-click: toggles a flag on this cell via the parent ViewModel callback.</summary>
    public ICommand FlagCommand   { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the <see cref="CellViewModel"/> with the underlying model and
    /// action callbacks wired up by <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="cell">The model cell this ViewModel wraps.</param>
    /// <param name="onReveal">Callback fired when the player left-clicks the cell.</param>
    /// <param name="onFlag">Callback fired when the player right-clicks the cell.</param>
    public CellViewModel(Cell cell, Action<int, int> onReveal, Action<int, int> onFlag)
    {
        _cell     = cell;
        _onReveal = onReveal;
        _onFlag   = onFlag;

        RevealCommand = new RelayCommand(
            _      => _onReveal(_cell.Row, _cell.Column),
            _      => !_cell.IsRevealed && !_isGameOver && !_cell.IsDecorative);

        FlagCommand = new RelayCommand(
            _      => _onFlag(_cell.Row, _cell.Column),
            _      => !_cell.IsRevealed && !_isGameOver && !_cell.IsDecorative);
    }

    // ── Public methods ────────────────────────────────────────────────────────

    /// <summary>
    /// Re-reads all display properties from the underlying <see cref="Cell"/> model
    /// and raises <see cref="System.ComponentModel.INotifyPropertyChanged.PropertyChanged"/>
    /// for each of them. Called by <see cref="MainViewModel"/> after every board mutation.
    /// </summary>
    /// <param name="isGameOver">
    /// <see langword="true"/> when the game has ended (won or lost); disables the cell.
    /// </param>
    public void Refresh(bool isGameOver)
    {
        _isGameOver = isGameOver;

        OnPropertyChanged(nameof(DisplayText));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsRevealed));
        OnPropertyChanged(nameof(IsFlagged));
        OnPropertyChanged(nameof(IsMineRevealed));
        OnPropertyChanged(nameof(AdjacentMineCount));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(IsDecorative));

        ((RelayCommand)RevealCommand).RaiseCanExecuteChanged();
        ((RelayCommand)FlagCommand).RaiseCanExecuteChanged();
    }
}
