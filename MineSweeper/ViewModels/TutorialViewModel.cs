using System;
using System.Windows.Input;

namespace MineSweeper.ViewModels;

/// <summary>Identifies which named UI region the tutorial spotlight should illuminate.</summary>
public enum TutorialTarget
{
    /// <summary>No spotlight — the whole board is dimmed (welcome and finale slides).</summary>
    None,
    /// <summary>The header card containing the mine counter, reset button, and timer.</summary>
    Header,
    /// <summary>Only the circular emoji reset button.</summary>
    ResetButton,
    /// <summary>The main game board card.</summary>
    Board,
    /// <summary>The difficulty chip selector bar.</summary>
    DifficultyBar,
    /// <summary>The Extreme mode toggle button in the top-right control strip.</summary>
    ExtremeButton,
    /// <summary>The leaderboard browser button in the top-right control strip.</summary>
    LeaderboardButton
}

/// <summary>Represents a single slide in the how-to-play tour.</summary>
internal readonly record struct TutorialStep(
    string Title,
    string Description,
    TutorialTarget Target);

/// <summary>
/// Manages the interactive "How to Play" tutorial overlay.
/// The guide is shown every time the application starts and can be dismissed
/// for the current session by pressing Skip or completing all slides.
/// Pure MVVM: handles step navigation and raises <see cref="StepChanged"/>
/// so the View can update spotlight positions.
/// </summary>
public sealed class TutorialViewModel : ViewModelBase
{

    // ── Step data ─────────────────────────────────────────────────────────────

    private static readonly TutorialStep[] Steps =
    [
        new(
            "Welcome to Bugs Finder! 👋",
            "You will become the GREATEST TESTER that indicator the bug's location and make decision for deploy this release on PROD or not.\n\n" +
            "Let's get you started with a quick tour.\n\n" +
            "We've set the game to Easy mode — the perfect difficulty for learning. " +
            "Click Next to begin!",
            TutorialTarget.None),

        new(
            "The Game Board 🎮",
            "Bugs 🐞 are hiding across the tiles — your job is to find them without touching one!\n\n" +
            "Your goal: reveal every tile that does NOT contain a bug. " +
            "Clear the whole board to win!",
            TutorialTarget.Board),

        new(
            "Left-click to Reveal 👆",
            "Left-click any tile to reveal it.\n\n" +
            "• If it's safe you'll see a number — how many of the 8 surrounding " +
            "tiles contain bugs.\n" +
            "• Blank tiles have zero neighbouring bugs and auto-expand for you!",
            TutorialTarget.Board),

        new(
            "Right-click to Flag 🚩",
            "Think a tile hides a bug? Right-click it to plant a flag.\n\n" +
            "Flagged tiles are protected — you can't accidentally reveal them. " +
            "Right-click again to remove the flag.",
            TutorialTarget.Board),

        new(
            "Reading the Numbers 🔢",
            "Each number tells you exactly how many bugs touch that tile " +
            "(diagonals count too — all 8 neighbours).\n\n" +
            "Example: a '1' next to a corner tile means exactly 1 of its " +
            "neighbours is a bug. Use logic to deduce which tiles are safe!",
            TutorialTarget.Board),

        new(
            "Bug Counter, Stars & Timer 🐞 ⭐ ⏱",
            "Left panel:\n\n" +
            "1. 🐞 Remaining unflagged bugs — tracks your progress.\n\n" +
            "2. ⭐ Lucky stars — collected from hidden secret cells. " +
            "They act as shields (absorb one bug hit) AND can be spent to unlock harder modes.\n\n" +
            "Right: elapsed time — clock starts on your first click.",
            TutorialTarget.Header),

        new(
            "Quiz Bugs 🎯",
            "Some bug cells are secretly marked as Quiz Bugs.\n\n" +
            "When you correctly flag a Quiz Bug (right-click on a bug cell), " +
            "a multiple-choice question popup appears:\n\n" +
            "✅ Correct answer → +3 ⭐ stars earned  (highlighted green)\n" +
            "❌ Wrong answer   → −1 ⭐ star deducted (your pick turns red, correct turns green)\n" +
            "⏭ Skip / close   → 0  (opportunity is lost)\n\n" +
            "Each board has 2–5 Quiz Bugs hidden among the regular bugs. " +
            "They look identical — you discover them when you flag correctly!\n\n" +
            "⏸ In Extreme Mode the challenge countdown pauses while the quiz popup is open " +
            "and resumes the moment you close it.",
            TutorialTarget.Board),

        new(
            "New Game Button 🔄",
            "The emoji button in the centre restarts the game at any time.\n\n" +
            "🙂  game in progress\n" +
            "😎  you won — congratulations!\n" +
            "😵  you lost bugs on production — try again!\n\n" +
            "Click it whenever you want a fresh start.",
            TutorialTarget.ResetButton),

        new(
            "Extreme Mode ⚡",
            "The ⚡ Extreme button in the top-right corner adds a time-pressure challenge.\n\n" +
            "• Extreme OFF — the timer just counts up. No pressure, play at your own pace.\n\n" +
            "• Extreme ON — every 10–20 seconds (depending on difficulty) a 5-second " +
            "countdown fires. Flag or reveal a mine before time runs out, or it's game over!\n\n" +
            "The countdown replaces the elapsed timer and turns red while active. " +
            "Toggle Extreme at any time — even mid-game.\n\n" +
            "With-in everytime that you flag a correct bug tile, 1 start will be added as a present in this mode",
            TutorialTarget.ExtremeButton),

        new(
            "Leaderboard 🏆",
            "The 🏆 button in the top-right control strip opens the Leaderboard browser.\n\n" +
            "• Each difficulty mode has its own leaderboard tab.\n" +
            "• Entries show player name and completion time, sorted fastest first.\n" +
            "• Up to 10 top times are saved per mode, persisted across sessions.\n\n" +
            "After winning a game you will be prompted to enter your name — " +
            "your result is highlighted in the leaderboard so you can see exactly where you placed.",
            TutorialTarget.LeaderboardButton),

        new(
            "Difficulty Levels 🏆",
            "Choose how challenging the board is:\n\n" +
            "• Easy    — 9×9 grid,   10 bugs  ← always available\n" +
            "• Medium  — 16×16 grid, 40 bugs  ← always available\n" +
            "• Hard    — 30×16 grid, 99 bugs  🔒 locked at start\n" +
            "• Special — country-shaped maps  🔒 locked at start\n\n" +
            "Hard and Special modes must be unlocked with stars. " +
            "Hover over a locked chip to see how many stars you still need.",
            TutorialTarget.DifficultyBar),

        new(
            "Unlocking Hard & Special 🔓",
            "Collect ⭐ by revealing hidden secret cells scattered on the board.\n\n" +
            "• Hard Mode   — costs 10 ⭐ to unlock\n" +
            "• Special Mode — costs 15 ⭐ to unlock\n\n" +
            "When you have enough stars, click the locked chip. A confirmation " +
            "popup will show the cost. Confirm to spend the stars and unlock the " +
            "mode permanently for this session.\n\n" +
            "⚠ Unlocks reset when you close the app — stars and locks are session-only.",
            TutorialTarget.DifficultyBar),

        //new(
        //    "Special Maps 🗺️",
        //    "Special mode lets you play on maps shaped like real countries!\n\n" +
        //    "Việt Nam — navigate the S-shaped coastline from Hà Giang to Cà Mau\n\n" +
        //    "Tiles outside the country border are hidden. Only cells inside the " +
        //    "border contain bugs — same rules, unique shape. More countries coming soon!",
        //    TutorialTarget.DifficultyBar),

        new(
            "You're Ready! 🚀",
            "One last tip: your very first click is ALWAYS safe — bugs are placed after you click.\n\n" +
            "🌍 Official World Records to beat:\n" +
            "• Easy   — 0.49s  (Kamil Murański & Ze-En Ju)\n" +
            "• Medium — 6.75s  (Kamil Murański)\n" +
            "• Hard   — 27.53s (Ze-En Ju)\n\n" +
            "🎁 Break any world record and a special gift appears — " +
            "including a free 1-year Claude Pro subscription!\n\n" +
            "Now go find those bugs. The world record is yours to take!",
            TutorialTarget.None),
    ];

    // ── State ─────────────────────────────────────────────────────────────────

    private int  _step;
    private bool _isVisible;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises the tutorial ViewModel. The overlay is always visible on startup;
    /// it is dismissed for the current session when the player presses Skip or
    /// completes all slides.
    /// </summary>
    public TutorialViewModel()
    {
        _isVisible = true;

        NextCommand = new RelayCommand(
            _ => Advance(),
            _ => _isVisible);

        PrevCommand = new RelayCommand(
            _ => Retreat(),
            _ => _isVisible && _step > 0);

        SkipCommand = new RelayCommand(
            _ => Dismiss(),
            _ => _isVisible);
    }

    // ── Bindable properties ───────────────────────────────────────────────────

    /// <summary>Whether the tutorial overlay is currently visible.</summary>
    public bool IsVisible
    {
        get => _isVisible;
        private set => SetProperty(ref _isVisible, value);
    }

    /// <summary>Title text of the current slide.</summary>
    public string StepTitle       => Steps[_step].Title;

    /// <summary>Body description of the current slide.</summary>
    public string StepDescription => Steps[_step].Description;

    /// <summary>Which UI element should be spotlighted on this slide.</summary>
    public TutorialTarget CurrentTarget => Steps[_step].Target;

    /// <summary>Human-readable progress counter, e.g. "3 / 9".</summary>
    public string StepProgress    => $"{_step + 1} / {Steps.Length}";

    /// <summary>Label for the forward button — changes to "Start! 🚀" on the last slide.</summary>
    public string NextButtonText  => _step == Steps.Length - 1 ? "Start! 🚀" : "Next →";

    /// <summary><see langword="true"/> when a Back button should be shown.</summary>
    public bool CanGoPrev         => _step > 0;

    /// <summary><see langword="true"/> when a Skip button should be shown (hidden on last slide).</summary>
    public bool CanSkip           => _step < Steps.Length - 1;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Advances to the next slide, or dismisses on the final slide.</summary>
    public ICommand NextCommand { get; }

    /// <summary>Returns to the previous slide.</summary>
    public ICommand PrevCommand { get; }

    /// <summary>Dismisses the tutorial without completing the remaining slides.</summary>
    public ICommand SkipCommand { get; }

    // ── Event ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Raised whenever the current slide changes (including on dismiss).
    /// The View subscribes to this event to reposition the spotlight overlay.
    /// </summary>
    public event EventHandler? StepChanged;

    // ── Private helpers ───────────────────────────────────────────────────────

    private void Advance()
    {
        if (_step < Steps.Length - 1) { _step++; Notify(); }
        else Dismiss();
    }

    private void Retreat()
    {
        if (_step > 0) { _step--; Notify(); }
    }

    private void Dismiss()
    {
        IsVisible = false;
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(StepDescription));
        OnPropertyChanged(nameof(CurrentTarget));
        OnPropertyChanged(nameof(StepProgress));
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(CanGoPrev));
        OnPropertyChanged(nameof(CanSkip));
        ((RelayCommand)PrevCommand).RaiseCanExecuteChanged();
        StepChanged?.Invoke(this, EventArgs.Empty);
    }
}
