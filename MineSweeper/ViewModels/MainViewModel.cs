using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using BugsFinder.Models;
using BugsFinder.Services;
using QuizQuestion = BugsFinder.Models.QuizQuestion;

namespace BugsFinder.ViewModels;

/// <summary>
/// Root ViewModel for <c>MainWindow.xaml</c>. Owns the <see cref="GameEngine"/>,
/// builds the <see cref="CellViewModel"/> collection, and exposes every property
/// and command consumed by the view.
/// </summary>
public sealed class MainViewModel : ViewModelBase
{
    private readonly GameEngine   _engine = new();
    private readonly AudioService _audio  = new();

    private ObservableCollection<CellViewModel> _cells = new();
    private GameState  _gameState;
    private int        _elapsedSeconds;
    private Difficulty _currentDifficulty  = Difficulty.Easy;
    private SpecialMap? _currentSpecialMap;

    // ── Quiz popup ────────────────────────────────────────────────────────────
    private QuizQuestion? _activeQuiz;
    private bool          _isQuizPopupVisible;
    private bool          _isQuizAnswered;
    private string        _quizResultMessage  = string.Empty;
    private bool          _quizResultIsCorrect;
    // "Normal" | "Correct" | "Wrong"  — one element per option button (0-3)
    private readonly string[] _quizOptionStates = ["Normal", "Normal", "Normal", "Normal"];

    // ── Difficulty unlock (session-scoped) ────────────────────────────────────
    private bool   _isHardUnlocked;
    private bool   _isSpecialUnlocked;
    private bool   _isUnlockConfirmVisible;
    private string _pendingUnlockMode = string.Empty;

    // ── Close confirm popup ───────────────────────────────────────────────────
    private bool _isCloseConfirmVisible;

    // ── Result popup (loss only) ──────────────────────────────────────────────
    private bool   _isResultVisible;
    private string _resultEmoji   = string.Empty;
    private string _resultMessage = string.Empty;

    // ── Leaderboard popup (post-win) ──────────────────────────────────────────
    private bool   _isLeaderboardVisible;
    private bool   _isLeaderboardSubmitted;
    private string _leaderboardNameInput    = string.Empty;
    private string _pendingLeaderboardKey   = string.Empty;
    private int    _pendingLeaderboardSecs;
    private string _leaderboardWinEmoji     = string.Empty;
    private string _leaderboardWinMessage   = string.Empty;
    private int    _leaderboardCountdown;
    private readonly DispatcherTimer _leaderboardCountdownTimer;
    private ObservableCollection<LeaderboardEntryViewModel> _leaderboardEntries = new();

    // ── Leaderboard browser (view all records by mode) ────────────────────────
    private bool _isLeaderboardBrowserVisible;
    private int  _selectedLeaderboardTab;
    private readonly ObservableCollection<LeaderboardEntryViewModel> _easyLeaderboard    = new();
    private readonly ObservableCollection<LeaderboardEntryViewModel> _mediumLeaderboard  = new();
    private readonly ObservableCollection<LeaderboardEntryViewModel> _hardLeaderboard    = new();
    private readonly ObservableCollection<LeaderboardEntryViewModel> _specialLeaderboard = new();

    // ── World record broken popup ─────────────────────────────────────────────
    private bool   _isWorldRecordBrokenVisible;
    private bool   _isGiftOpened;
    private string _worldRecordPrizeLink = string.Empty;

    // ── Board collection ──────────────────────────────────────────────────────

    /// <summary>Flat, row-major cell collection bound to the board ItemsControl.</summary>
    public ObservableCollection<CellViewModel> Cells
    {
        get => _cells;
        private set => SetProperty(ref _cells, value);
    }

    // ── Header properties ─────────────────────────────────────────────────────

    /// <summary>Remaining bugs (MineCount − FlaggedCount), zero-padded to three digits.</summary>
    public string MineCounterDisplay =>
        Math.Max(0, Math.Min(999, RemainingMines)).ToString("D3");

    /// <summary>Elapsed seconds (D3), or the challenge countdown when a challenge is active.</summary>
    public string TimerDisplay =>
        _engine.IsInChallenge
            ? _engine.ChallengeSecondsLeft.ToString("D3")
            : Math.Min(999, _elapsedSeconds).ToString("D3");

    /// <summary><see langword="true"/> while a 5-second challenge countdown is running.</summary>
    public bool IsInChallenge => _engine.IsInChallenge;

    /// <summary>Emoji on the reset button: 🙂 playing, 😎 won, 😵 lost.</summary>
    public string StatusEmoji => _gameState switch
    {
        GameState.Won  => "😎",
        GameState.Lost => "😵",
        _              => "🙂"
    };

    // ── Board geometry ────────────────────────────────────────────────────────

    /// <summary>Row count — drives UniformGrid.Rows.</summary>
    public int Rows => _engine.Board?.Rows    ?? 9;

    /// <summary>Column count — drives UniformGrid.Columns.</summary>
    public int Cols => _engine.Board?.Columns ?? 9;

    // ── Remaining bugs ────────────────────────────────────────────────────────

    /// <summary>Raw remaining bug count (MineCount − FlaggedCount).</summary>
    public int RemainingMines =>
        _engine.Board is { } b ? b.MineCount - b.FlaggedCount : 0;

    // ── Stars (session-scoped lives) ──────────────────────────────────────────

    /// <summary>Current star count. Stars survive across games in the same session.</summary>
    public int Stars => _engine.Stars;

    // ── Difficulty selection ──────────────────────────────────────────────────

    /// <summary><see langword="true"/> when Easy is active.</summary>
    public bool IsEasySelected    => _currentDifficulty == Difficulty.Easy;

    /// <summary><see langword="true"/> when Medium is active.</summary>
    public bool IsMediumSelected  => _currentDifficulty == Difficulty.Medium;

    /// <summary><see langword="true"/> when Hard is active.</summary>
    public bool IsHardSelected    => _currentDifficulty == Difficulty.Hard;

    /// <summary><see langword="true"/> when Special is active.</summary>
    public bool IsSpecialSelected => _currentDifficulty == Difficulty.Special;

    // ── Difficulty lock / unlock ──────────────────────────────────────────────

    /// <summary>Star cost to unlock Hard mode for the current session.</summary>
    public const int HardUnlockCost    = 10;

    /// <summary>Star cost to unlock Special mode for the current session.</summary>
    public const int SpecialUnlockCost = 15;

    /// <summary><see langword="true"/> when Hard mode has been unlocked this session.</summary>
    public bool IsHardUnlocked    => _isHardUnlocked;

    /// <summary><see langword="true"/> when Special mode has been unlocked this session.</summary>
    public bool IsSpecialUnlocked => _isSpecialUnlocked;

    /// <summary>Label shown on the Hard difficulty chip (includes lock emoji when locked).</summary>
    public string HardChipLabel    => _isHardUnlocked    ? "Hard"    : "Hard 🔒";

    /// <summary>Label shown on the Special difficulty chip (includes lock emoji when locked).</summary>
    public string SpecialChipLabel => _isSpecialUnlocked ? "Special" : "Special 🔒";

    /// <summary>Tooltip text for the Hard chip — <see langword="null"/> when unlocked.</summary>
    public string? HardChipTooltip
    {
        get
        {
            if (_isHardUnlocked) return null;
            int have   = _engine.Stars;
            int needed = HardUnlockCost - have;
            return needed > 0
                ? $"🔒 Hard Mode is locked\nCollect {needed} more ⭐ to unlock  (need {HardUnlockCost} ⭐ total, you have {have} ⭐)"
                : $"✅ You have enough stars!\nClick to spend {HardUnlockCost} ⭐ and unlock Hard Mode";
        }
    }

    /// <summary>Tooltip text for the Special chip — <see langword="null"/> when unlocked.</summary>
    public string? SpecialChipTooltip
    {
        get
        {
            if (_isSpecialUnlocked) return null;
            int have   = _engine.Stars;
            int needed = SpecialUnlockCost - have;
            return needed > 0
                ? $"🔒 Special Mode is locked\nCollect {needed} more ⭐ to unlock  (need {SpecialUnlockCost} ⭐ total, you have {have} ⭐)"
                : $"✅ You have enough stars!\nClick to spend {SpecialUnlockCost} ⭐ and unlock Special Mode";
        }
    }

    // ── Unlock confirm popup ──────────────────────────────────────────────────

    /// <summary>Whether the unlock-confirmation popup is visible.</summary>
    public bool IsUnlockConfirmVisible
    {
        get => _isUnlockConfirmVisible;
        private set => SetProperty(ref _isUnlockConfirmVisible, value);
    }

    /// <summary>Title text inside the unlock confirm popup.</summary>
    public string UnlockConfirmTitle => _pendingUnlockMode == "Hard"
        ? "Unlock Hard Mode? 🔓"
        : "Unlock Special Mode? 🔓";

    /// <summary>Body text inside the unlock confirm popup.</summary>
    public string UnlockConfirmBody
    {
        get
        {
            int    cost  = _pendingUnlockMode == "Hard" ? HardUnlockCost : SpecialUnlockCost;
            string mode  = _pendingUnlockMode == "Hard" ? "Hard" : "Special";
            int    after = _engine.Stars - cost;
            return $"Spend {cost} ⭐ to unlock {mode} Mode for this session.\n" +
                   $"You currently have {_engine.Stars} ⭐.\n" +
                   $"After unlocking you will have {after} ⭐ remaining.";
        }
    }

    // ── Quiz popup ────────────────────────────────────────────────────────────

    /// <summary>Whether the quiz popup is currently visible.</summary>
    public bool IsQuizPopupVisible
    {
        get => _isQuizPopupVisible;
        private set => SetProperty(ref _isQuizPopupVisible, value);
    }

    /// <summary><see langword="true"/> after the player has submitted an answer.</summary>
    public bool IsQuizAnswered
    {
        get => _isQuizAnswered;
        private set => SetProperty(ref _isQuizAnswered, value);
    }

    /// <summary>The question text for the active quiz.</summary>
    public string QuizQuestionText => _activeQuiz?.Question ?? string.Empty;

    /// <summary>Text for answer option 0.</summary>
    public string QuizOption0 => GetQuizOption(0);
    /// <summary>Text for answer option 1.</summary>
    public string QuizOption1 => GetQuizOption(1);
    /// <summary>Text for answer option 2.</summary>
    public string QuizOption2 => GetQuizOption(2);
    /// <summary>Text for answer option 3.</summary>
    public string QuizOption3 => GetQuizOption(3);

    /// <summary>Highlight state for option button 0: "Normal", "Correct", or "Wrong".</summary>
    public string QuizOption0State => _quizOptionStates[0];
    /// <summary>Highlight state for option button 1.</summary>
    public string QuizOption1State => _quizOptionStates[1];
    /// <summary>Highlight state for option button 2.</summary>
    public string QuizOption2State => _quizOptionStates[2];
    /// <summary>Highlight state for option button 3.</summary>
    public string QuizOption3State => _quizOptionStates[3];

    /// <summary>Result message shown after the player answers.</summary>
    public string QuizResultMessage
    {
        get => _quizResultMessage;
        private set => SetProperty(ref _quizResultMessage, value);
    }

    /// <summary><see langword="true"/> when the player answered correctly (used for result colour).</summary>
    public bool QuizResultIsCorrect
    {
        get => _quizResultIsCorrect;
        private set => SetProperty(ref _quizResultIsCorrect, value);
    }

    /// <summary>Label for the quiz action button: "Close" after answering, "Skip" before.</summary>
    public string QuizCloseButtonText => _isQuizAnswered ? "Close" : "Skip (lose opportunity)";

    private string GetQuizOption(int i) =>
        _activeQuiz is { } q && i < q.Options.Count ? q.Options[i].Value : string.Empty;

    // ── Cell sizing ───────────────────────────────────────────────────────────

    /// <summary>Cell size in device-independent pixels: 20 for Special, 36 otherwise.</summary>
    public double CellSize    => _currentDifficulty == Difficulty.Special ? 20 : 36;

    /// <summary>Cell margin: 1 px for Special, 2 px otherwise.</summary>
    public Thickness CellMargin => _currentDifficulty == Difficulty.Special
        ? new Thickness(1) : new Thickness(2);

    /// <summary>Cell font size: 11 px for Special, 14 px otherwise.</summary>
    public double CellFontSize => _currentDifficulty == Difficulty.Special ? 11 : 14;

    /// <summary>Maximum board height — capped for Special maps so window never overflows.</summary>
    public double BoardMaxHeight => _currentDifficulty == Difficulty.Special
        ? Math.Max(400, SystemParameters.WorkArea.Height - 230)
        : double.PositiveInfinity;

    // ── Close confirm popup ───────────────────────────────────────────────────

    /// <summary>Whether the "Exit game?" confirmation popup is visible.</summary>
    public bool IsCloseConfirmVisible
    {
        get => _isCloseConfirmVisible;
        private set => SetProperty(ref _isCloseConfirmVisible, value);
    }

    // ── Tutorial ──────────────────────────────────────────────────────────────

    /// <summary>First-run interactive how-to-play overlay.</summary>
    public TutorialViewModel Tutorial { get; } = new();

    // ── Full-screen (always on) ───────────────────────────────────────────────

    /// <summary>Always <see langword="true"/> — the application always runs in borderless full-screen.</summary>
    public bool IsFullScreen => true;

    // ── Extreme mode ──────────────────────────────────────────────────────────

    /// <summary>Whether Extreme mode is active.</summary>
    public bool IsExtremeMode => _engine.IsExtremeMode;

    /// <summary>Label for the Extreme mode toggle button.</summary>
    public string ExtremeButtonText => _engine.IsExtremeMode ? "⚡ON" : "⚡";

    // ── Theme ─────────────────────────────────────────────────────────────────

    /// <summary><see langword="true"/> when the Dark theme is active.</summary>
    public bool IsDarkTheme  => ThemeService.CurrentTheme == "Dark";

    /// <summary><see langword="true"/> when the Light theme is active.</summary>
    public bool IsLightTheme => ThemeService.CurrentTheme == "Light";

    /// <summary><see langword="true"/> when the Blue theme is active.</summary>
    public bool IsBlueTheme  => ThemeService.CurrentTheme == "Blue";

    // ── Result popup (loss only) ──────────────────────────────────────────────

    /// <summary>Whether the loss result popup is visible.</summary>
    public bool IsResultVisible
    {
        get => _isResultVisible;
        private set => SetProperty(ref _isResultVisible, value);
    }

    /// <summary>Large emoji shown in the result popup (😵).</summary>
    public string ResultEmoji
    {
        get => _resultEmoji;
        private set => SetProperty(ref _resultEmoji, value);
    }

    /// <summary>Flavour message shown in the result popup.</summary>
    public string ResultMessage
    {
        get => _resultMessage;
        private set => SetProperty(ref _resultMessage, value);
    }

    // ── Leaderboard popup (post-win) ──────────────────────────────────────────

    /// <summary>Whether the post-win leaderboard popup is visible.</summary>
    public bool IsLeaderboardVisible
    {
        get => _isLeaderboardVisible;
        private set => SetProperty(ref _isLeaderboardVisible, value);
    }

    /// <summary>Whether the player has submitted their name (switches popup to leaderboard view).</summary>
    public bool IsLeaderboardSubmitted
    {
        get => _isLeaderboardSubmitted;
        private set => SetProperty(ref _isLeaderboardSubmitted, value);
    }

    /// <summary>Player name input bound two-way to the leaderboard name TextBox.</summary>
    public string LeaderboardNameInput
    {
        get => _leaderboardNameInput;
        set => SetProperty(ref _leaderboardNameInput, value);
    }

    /// <summary>Win emoji shown at the top of the leaderboard popup (😎).</summary>
    public string LeaderboardWinEmoji => _leaderboardWinEmoji;

    /// <summary>Win message shown below the emoji in the leaderboard popup.</summary>
    public string LeaderboardWinMessage => _leaderboardWinMessage;

    /// <summary>Player's completion time shown in the input phase (e.g. "⏱ Your time: 42s").</summary>
    public string LeaderboardTimeText => $"⏱ Your time: {_pendingLeaderboardSecs}s";

    /// <summary>Auto-close countdown text shown in the leaderboard phase.</summary>
    public string LeaderboardCountdownText => $"Auto-closing in {_leaderboardCountdown}s…";

    /// <summary>Top-10 leaderboard entries for the current mode, populated after name submission.</summary>
    public ObservableCollection<LeaderboardEntryViewModel> LeaderboardEntries => _leaderboardEntries;

    // ── Leaderboard browser ───────────────────────────────────────────────────

    /// <summary>Whether the leaderboard browser popup is visible.</summary>
    public bool IsLeaderboardBrowserVisible
    {
        get => _isLeaderboardBrowserVisible;
        private set => SetProperty(ref _isLeaderboardBrowserVisible, value);
    }

    /// <summary>Index of the currently selected leaderboard tab (0=Easy, 1=Medium, 2=Hard, 3=Special).</summary>
    public int SelectedLeaderboardTab
    {
        get => _selectedLeaderboardTab;
        set => SetProperty(ref _selectedLeaderboardTab, value);
    }

    /// <summary>Easy mode leaderboard entries (top 10).</summary>
    public ObservableCollection<LeaderboardEntryViewModel> EasyLeaderboard    => _easyLeaderboard;
    /// <summary>Medium mode leaderboard entries (top 10).</summary>
    public ObservableCollection<LeaderboardEntryViewModel> MediumLeaderboard  => _mediumLeaderboard;
    /// <summary>Hard mode leaderboard entries (top 10).</summary>
    public ObservableCollection<LeaderboardEntryViewModel> HardLeaderboard    => _hardLeaderboard;
    /// <summary>Special mode leaderboard entries (top 10).</summary>
    public ObservableCollection<LeaderboardEntryViewModel> SpecialLeaderboard => _specialLeaderboard;

    /// <summary><see langword="true"/> when the Easy leaderboard has at least one entry.</summary>
    public bool HasEasyLeaderboard    => _easyLeaderboard.Count    > 0;
    /// <summary><see langword="true"/> when the Medium leaderboard has at least one entry.</summary>
    public bool HasMediumLeaderboard  => _mediumLeaderboard.Count  > 0;
    /// <summary><see langword="true"/> when the Hard leaderboard has at least one entry.</summary>
    public bool HasHardLeaderboard    => _hardLeaderboard.Count    > 0;
    /// <summary><see langword="true"/> when the Special leaderboard has at least one entry.</summary>
    public bool HasSpecialLeaderboard => _specialLeaderboard.Count > 0;

    // ── World record broken popup ─────────────────────────────────────────────

    /// <summary>Whether the world-record-broken celebration popup is visible.</summary>
    public bool IsWorldRecordBrokenVisible
    {
        get => _isWorldRecordBrokenVisible;
        private set => SetProperty(ref _isWorldRecordBrokenVisible, value);
    }

    /// <summary>Whether the player has already clicked the gift to reveal the prize.</summary>
    public bool IsGiftOpened
    {
        get => _isGiftOpened;
        private set => SetProperty(ref _isGiftOpened, value);
    }

    /// <summary>URL for the prize link shown after the gift is opened.</summary>
    public string WorldRecordPrizeLink => _worldRecordPrizeLink;

    // ── Star animation events ─────────────────────────────────────────────────

    /// <summary>Raised each time a star is gained from a secret cell.</summary>
    public event EventHandler? StarGained;

    /// <summary>Raised each time a star is consumed by the shield mechanic.</summary>
    public event EventHandler? StarLost;

    // ── Challenge events ──────────────────────────────────────────────────────

    /// <summary>Raised when a 30-second challenge period starts.</summary>
    public event EventHandler? ChallengeStarted;

    /// <summary>Raised when a challenge ends (success, failure, or new game).</summary>
    public event EventHandler? ChallengeEnded;

    /// <summary>Raised when the player clicks the gift box to open it.</summary>
    public event EventHandler? GiftOpened;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Starts a new game with the current difficulty.</summary>
    public ICommand NewGameCommand { get; }

    /// <summary>Toggles Extreme mode on or off.</summary>
    public ICommand ToggleExtremeModeCommand { get; }

    /// <summary>Applies a theme by name and persists the choice.</summary>
    public ICommand ChangeThemeCommand { get; }

    /// <summary>Changes difficulty and immediately starts a new game.</summary>
    public ICommand ChangeDifficultyCommand { get; }

    /// <summary>Dismisses the loss result popup without starting a new game.</summary>
    public ICommand DismissResultCommand { get; }

    /// <summary>Minimises the window to the taskbar.</summary>
    public ICommand MinimizeCommand { get; }

    /// <summary>Raised when the player requests the window to be minimised.</summary>
    public event EventHandler? MinimizeRequested;

    /// <summary>Shows the exit-confirmation popup.</summary>
    public ICommand RequestCloseCommand { get; }

    /// <summary>Raised when the player confirms they want to exit.</summary>
    public event EventHandler? CloseConfirmed;

    /// <summary>Cancels the exit-confirmation popup.</summary>
    public ICommand CancelCloseCommand { get; }

    /// <summary>Confirms exit — raises <see cref="CloseConfirmed"/>.</summary>
    public ICommand ConfirmCloseCommand { get; }

    /// <summary>Submits an answer to the active quiz (CommandParameter = option index "0"–"3").</summary>
    public ICommand AnswerQuizCommand { get; }

    /// <summary>Closes the quiz popup (or skips it if not yet answered).</summary>
    public ICommand CloseQuizCommand { get; }

    /// <summary>Spends stars and unlocks the pending difficulty, then starts a new game.</summary>
    public ICommand ConfirmUnlockCommand { get; }

    /// <summary>Cancels the unlock confirmation popup.</summary>
    public ICommand CancelUnlockCommand { get; }

    /// <summary>Submits the player's name to the leaderboard and switches to the leaderboard view.</summary>
    public ICommand SubmitLeaderboardCommand { get; }

    /// <summary>Closes the post-win leaderboard popup.</summary>
    public ICommand CloseLeaderboardCommand { get; }

    /// <summary>Closes the post-win leaderboard popup and starts a new game immediately.</summary>
    public ICommand PlayAgainFromLeaderboardCommand { get; }

    /// <summary>Opens the leaderboard browser, defaulting to the active difficulty's tab.</summary>
    public ICommand OpenLeaderboardBrowserCommand { get; }

    /// <summary>Closes the leaderboard browser.</summary>
    public ICommand CloseLeaderboardBrowserCommand { get; }

    /// <summary>Selects a tab in the leaderboard browser (CommandParameter = tab index as string).</summary>
    public ICommand SelectLeaderboardTabCommand { get; }

    /// <summary>Opens the gift box in the world-record-broken popup.</summary>
    public ICommand OpenGiftCommand { get; }

    /// <summary>Closes the world-record-broken popup.</summary>
    public ICommand CloseWorldRecordPopupCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Wires up engine events, initialises commands, and starts the first game.</summary>
    public MainViewModel()
    {
        _engine.InitializeStars(SettingsService.DefaultStars);
        QuizService.EnsureLoaded();

        // Leaderboard countdown timer
        _leaderboardCountdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _leaderboardCountdownTimer.Tick += (_, _) =>
        {
            _leaderboardCountdown--;
            OnPropertyChanged(nameof(LeaderboardCountdownText));
            if (_leaderboardCountdown <= 0)
            {
                _leaderboardCountdownTimer.Stop();
                IsLeaderboardVisible = false;
            }
        };

        // ── Commands ─────────────────────────────────────────────────────────

        NewGameCommand = new RelayCommand(_ => StartNewGame());

        ToggleExtremeModeCommand = new RelayCommand(_ =>
        {
            _engine.SetExtremeMode(!_engine.IsExtremeMode);
            OnPropertyChanged(nameof(IsExtremeMode));
            OnPropertyChanged(nameof(ExtremeButtonText));
            OnPropertyChanged(nameof(IsInChallenge));
            OnPropertyChanged(nameof(TimerDisplay));
        });

        DismissResultCommand = new RelayCommand(_ => IsResultVisible = false);

        ChangeThemeCommand = new RelayCommand(param =>
        {
            if (param is not string theme) return;
            ThemeService.Apply(theme);
            OnPropertyChanged(nameof(IsDarkTheme));
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsBlueTheme));
        });

        ChangeDifficultyCommand = new RelayCommand(param =>
        {
            if (param is not string s) return;

            if (s == "Hard" && !_isHardUnlocked)
            {
                if (_engine.Stars >= HardUnlockCost) ShowUnlockConfirm("Hard");
                return;
            }
            if ((s == "Special" || s.StartsWith("Special:")) && !_isSpecialUnlocked)
            {
                if (_engine.Stars >= SpecialUnlockCost) ShowUnlockConfirm("Special");
                return;
            }

            if (s == "Special" || s.StartsWith("Special:"))
            {
                string mapId = s.Contains(':') ? s[(s.IndexOf(':') + 1)..] : string.Empty;
                _currentSpecialMap = string.IsNullOrEmpty(mapId)
                    ? SpecialMapRegistry.Default
                    : SpecialMapRegistry.Find(mapId) ?? SpecialMapRegistry.Default;
                _currentDifficulty = Difficulty.Special;
            }
            else if (Enum.TryParse<Difficulty>(s, out var d) && d != Difficulty.Special)
            {
                _currentDifficulty = d;
                _currentSpecialMap = null;
            }
            else return;

            NotifyDifficultySelection();
            StartNewGame();
        });

        ConfirmUnlockCommand = new RelayCommand(_ =>
        {
            IsUnlockConfirmVisible = false;

            if (_pendingUnlockMode == "Hard")
            {
                _engine.SpendStars(HardUnlockCost);
                _isHardUnlocked    = true;
                _currentDifficulty = Difficulty.Hard;
                _currentSpecialMap = null;
                NotifyUnlockChanged();
                NotifyDifficultySelection();
                StartNewGame();
            }
            else if (_pendingUnlockMode == "Special")
            {
                _engine.SpendStars(SpecialUnlockCost);
                _isSpecialUnlocked = true;
                _currentSpecialMap = SpecialMapRegistry.Default;
                _currentDifficulty = Difficulty.Special;
                NotifyUnlockChanged();
                NotifyDifficultySelection();
                StartNewGame();
            }

            _pendingUnlockMode = string.Empty;
        });

        CancelUnlockCommand = new RelayCommand(_ =>
        {
            IsUnlockConfirmVisible = false;
            _pendingUnlockMode     = string.Empty;
        });

        AnswerQuizCommand = new RelayCommand(param =>
        {
            if (_activeQuiz is null || _isQuizAnswered) return;
            if (!int.TryParse(param?.ToString(), out int idx)) return;
            if (idx < 0 || idx >= _activeQuiz.Options.Count) return;

            bool correct    = _activeQuiz.Options[idx].IsAnswer;
            int  correctIdx = _activeQuiz.Options.FindIndex(o => o.IsAnswer);

            // Highlight correct option green; chosen wrong option red
            _quizOptionStates[correctIdx] = "Correct";
            if (!correct) _quizOptionStates[idx] = "Wrong";
            for (int i = 0; i < 4; i++)
                OnPropertyChanged($"QuizOption{i}State");

            if (correct)
            {
                _engine.GainStars(3);
                QuizResultMessage   = "🎉 Correct! You earned 3 ⭐";
                QuizResultIsCorrect = true;
                NotifyStarDependents();
                for (int i = 0; i < 3; i++)
                    StarGained?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _engine.SpendStars(1);
                QuizResultMessage   = "❌ Wrong answer — 1 ⭐ deducted";
                QuizResultIsCorrect = false;
                NotifyStarDependents();
                StarLost?.Invoke(this, EventArgs.Empty);
            }

            IsQuizAnswered = true;
            OnPropertyChanged(nameof(QuizCloseButtonText));
        });

        CloseQuizCommand = new RelayCommand(_ =>
        {
            _engine.SetChallengePaused(false);
            IsQuizPopupVisible = false;
            _activeQuiz        = null;
            IsQuizAnswered     = false;
            QuizResultMessage  = string.Empty;
            for (int i = 0; i < 4; i++) _quizOptionStates[i] = "Normal";
            for (int i = 0; i < 4; i++) OnPropertyChanged($"QuizOption{i}State");
            OnPropertyChanged(nameof(QuizCloseButtonText));
        });

        MinimizeCommand     = new RelayCommand(_ => MinimizeRequested?.Invoke(this, EventArgs.Empty));
        RequestCloseCommand = new RelayCommand(_ => IsCloseConfirmVisible = true);
        CancelCloseCommand  = new RelayCommand(_ => IsCloseConfirmVisible = false);
        ConfirmCloseCommand = new RelayCommand(_ =>
        {
            IsCloseConfirmVisible = false;
            CloseConfirmed?.Invoke(this, EventArgs.Empty);
        });

        SubmitLeaderboardCommand = new RelayCommand(_ =>
        {
            if (_isLeaderboardSubmitted) return;

            string name = string.IsNullOrWhiteSpace(_leaderboardNameInput)
                ? "Anonymous" : _leaderboardNameInput.Trim();

            int rank    = SettingsService.AddToLeaderboard(_pendingLeaderboardKey, _pendingLeaderboardSecs, name);
            var records = SettingsService.GetLeaderboard(_pendingLeaderboardKey);

            _leaderboardEntries.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                _leaderboardEntries.Add(new LeaderboardEntryViewModel(
                    rank:        i + 1,
                    name:        records[i].Name,
                    timeDisplay: $"{records[i].Seconds}s",
                    isHighlight: i == rank));
            }

            IsLeaderboardSubmitted = true;
            OnPropertyChanged(nameof(LeaderboardEntries));

            _leaderboardCountdown = 10;
            OnPropertyChanged(nameof(LeaderboardCountdownText));
            _leaderboardCountdownTimer.Start();
        });

        CloseLeaderboardCommand = new RelayCommand(_ =>
        {
            _leaderboardCountdownTimer.Stop();
            IsLeaderboardVisible = false;
        });

        PlayAgainFromLeaderboardCommand = new RelayCommand(_ =>
        {
            _leaderboardCountdownTimer.Stop();
            IsLeaderboardVisible = false;
            StartNewGame();
        });

        OpenLeaderboardBrowserCommand = new RelayCommand(_ =>
        {
            LoadAllLeaderboards();
            SelectedLeaderboardTab = _currentDifficulty switch
            {
                Difficulty.Medium  => 1,
                Difficulty.Hard    => 2,
                Difficulty.Special => 3,
                _                  => 0
            };
            IsLeaderboardBrowserVisible = true;
        });

        CloseLeaderboardBrowserCommand = new RelayCommand(_ => IsLeaderboardBrowserVisible = false);

        SelectLeaderboardTabCommand = new RelayCommand(param =>
        {
            if (int.TryParse(param?.ToString(), out int tab))
                SelectedLeaderboardTab = tab;
        });

        OpenGiftCommand = new RelayCommand(_ =>
        {
            IsGiftOpened = true;
            GiftOpened?.Invoke(this, EventArgs.Empty);
        });

        CloseWorldRecordPopupCommand = new RelayCommand(_ =>
        {
            IsWorldRecordBrokenVisible = false;
            IsGiftOpened               = false;
        });

        // ── Engine events ─────────────────────────────────────────────────────

        _engine.TimerTick += (_, _) =>
        {
            _elapsedSeconds = _engine.ElapsedSeconds;
            OnPropertyChanged(nameof(TimerDisplay));
        };

        _engine.GameStateChanged += (_, state) =>
        {
            _gameState = state;
            OnPropertyChanged(nameof(StatusEmoji));
            if (state == GameState.Won)  { _audio.PlayVictory();   ShowLeaderboard(); }
            if (state == GameState.Lost) { _audio.PlayExplosion(); ShowLossResult(); }

            if (state is GameState.Won or GameState.Lost)
                ChallengeEnded?.Invoke(this, EventArgs.Empty);
        };

        _engine.SecretCellsFound += (_, count) =>
        {
            NotifyStarDependents();
            _audio.PlaySecretFound();
            for (int i = 0; i < count; i++)
                StarGained?.Invoke(this, EventArgs.Empty);
        };

        _engine.StarShielded += (_, _) =>
        {
            NotifyStarDependents();
            _audio.PlayStarUsed();
            RefreshAllCells();
            OnPropertyChanged(nameof(RemainingMines));
            OnPropertyChanged(nameof(MineCounterDisplay));
            StarLost?.Invoke(this, EventArgs.Empty);
        };

        _engine.ChallengeStarted += (_, _) =>
        {
            OnPropertyChanged(nameof(IsInChallenge));
            OnPropertyChanged(nameof(TimerDisplay));
            _audio.PlayChallengeAlert();
            ChallengeStarted?.Invoke(this, EventArgs.Empty);
        };

        _engine.ChallengeTick += (_, _) =>
        {
            OnPropertyChanged(nameof(TimerDisplay));
            _audio.PlayCountdownTick();
        };

        _engine.ChallengeStarEarned += (_, _) =>
        {
            NotifyStarDependents();
            StarGained?.Invoke(this, EventArgs.Empty);
        };

        _engine.ChallengeSuccess += (_, _) =>
        {
            OnPropertyChanged(nameof(IsInChallenge));
            OnPropertyChanged(nameof(TimerDisplay));
            ChallengeEnded?.Invoke(this, EventArgs.Empty);
        };

        _engine.ChallengeFailed += (_, _) =>
        {
            OnPropertyChanged(nameof(IsInChallenge));
            ChallengeEnded?.Invoke(this, EventArgs.Empty);
        };

        _engine.QuizTriggered += (_, _) =>
        {
            var q = QuizService.GetRandom();
            if (q is null) return;

            // Pause challenge countdown while quiz is open
            _engine.SetChallengePaused(true);

            _activeQuiz   = q;
            IsQuizAnswered = false;
            QuizResultMessage = string.Empty;
            for (int i = 0; i < 4; i++) _quizOptionStates[i] = "Normal";
            for (int i = 0; i < 4; i++) OnPropertyChanged($"QuizOption{i}State");
            OnPropertyChanged(nameof(QuizQuestionText));
            OnPropertyChanged(nameof(QuizOption0));
            OnPropertyChanged(nameof(QuizOption1));
            OnPropertyChanged(nameof(QuizOption2));
            OnPropertyChanged(nameof(QuizOption3));
            OnPropertyChanged(nameof(QuizCloseButtonText));
            IsQuizPopupVisible = true;
        };

        StartNewGame();
    }

    // ── Private methods ───────────────────────────────────────────────────────

    private void StartNewGame()
    {
        _leaderboardCountdownTimer.Stop();
        IsLeaderboardVisible = false;
        ChallengeEnded?.Invoke(this, EventArgs.Empty);

        _elapsedSeconds = 0;
        IsResultVisible = false;

        if (_currentDifficulty == Difficulty.Special && _currentSpecialMap is not null)
            _engine.NewGame(_currentSpecialMap);
        else
            _engine.NewGame(_currentDifficulty);

        _gameState = _engine.GameState;

        RebuildCells();

        OnPropertyChanged(nameof(Rows));
        OnPropertyChanged(nameof(Cols));
        OnPropertyChanged(nameof(RemainingMines));
        OnPropertyChanged(nameof(MineCounterDisplay));
        OnPropertyChanged(nameof(TimerDisplay));
        OnPropertyChanged(nameof(StatusEmoji));
        OnPropertyChanged(nameof(Stars));
        OnPropertyChanged(nameof(IsInChallenge));
    }

    private void RebuildCells()
    {
        var newCells = new ObservableCollection<CellViewModel>();
        if (_engine.Board is { } board)
        {
            foreach (var cell in board.Cells)
                newCells.Add(new CellViewModel(cell, HandleReveal, HandleFlag));
        }
        Cells = newCells;
    }

    private void HandleReveal(int row, int col)
    {
        int before = _engine.Board?.Cells.Count(c => c.IsRevealed) ?? 0;
        _engine.RevealCell(row, col);

        if (_gameState == GameState.Playing)
        {
            int delta = (_engine.Board?.Cells.Count(c => c.IsRevealed) ?? 0) - before;
            if (delta > 1) _audio.PlayFloodReveal();
            else if (delta == 1) _audio.PlayReveal();
        }

        RefreshAllCells();
        OnPropertyChanged(nameof(RemainingMines));
        OnPropertyChanged(nameof(MineCounterDisplay));
    }

    private void HandleFlag(int row, int col)
    {
        _engine.FlagCell(row, col);
        _audio.PlayFlag();
        RefreshAllCells();
        OnPropertyChanged(nameof(RemainingMines));
        OnPropertyChanged(nameof(MineCounterDisplay));
    }

    private void ShowLeaderboard()
    {
        int bugCount = _engine.Board?.MineCount ?? 0;
        _leaderboardWinEmoji = "😎";
        _leaderboardWinMessage = _currentDifficulty switch
        {
            Difficulty.Easy    => $"You found {bugCount} bugs. Send to dev team",
            Difficulty.Medium  => "You saved this release",
            Difficulty.Hard    => "Awesome, your salary should be increased",
            Difficulty.Special => "Congrats! No bugs on last round testing. You're the greatest Vietnamese tester.",
            _                  => $"You found {bugCount} bugs!"
        };

        _pendingLeaderboardKey  = GetCurrentModeKey();
        _pendingLeaderboardSecs = _engine.ElapsedSeconds;
        _leaderboardNameInput   = string.Empty;
        _leaderboardEntries.Clear();

        OnPropertyChanged(nameof(LeaderboardWinEmoji));
        OnPropertyChanged(nameof(LeaderboardWinMessage));
        OnPropertyChanged(nameof(LeaderboardTimeText));
        OnPropertyChanged(nameof(LeaderboardNameInput));
        OnPropertyChanged(nameof(LeaderboardEntries));

        IsLeaderboardSubmitted = false;
        IsLeaderboardVisible   = true;

        // Check if the player broke a world record (threshold scaled by buffer multiplier)
        if (SettingsService.WorldRecords.TryGetValue(_pendingLeaderboardKey, out var wr)
            && _pendingLeaderboardSecs <= (int)(wr.RealSeconds * SettingsService.WorldRecordBufferMultiplier))
        {
            _worldRecordPrizeLink = SettingsService.ClaudeProSubscriptionUrl;
            OnPropertyChanged(nameof(WorldRecordPrizeLink));
            IsGiftOpened               = false;
            IsWorldRecordBrokenVisible = true;
        }
    }

    private void ShowLossResult()
    {
        ResultEmoji   = "😵";
        ResultMessage = "OMG, bugs on production! Time to update your CV 😄";
        IsResultVisible = true;
    }

    private void LoadAllLeaderboards()
    {
        LoadLeaderboard("Easy",    _easyLeaderboard);
        LoadLeaderboard("Medium",  _mediumLeaderboard);
        LoadLeaderboard("Hard",    _hardLeaderboard);
        LoadLeaderboard($"Special:{_currentSpecialMap?.Id ?? SpecialMapRegistry.Default.Id}",
                        _specialLeaderboard);

        OnPropertyChanged(nameof(HasEasyLeaderboard));
        OnPropertyChanged(nameof(HasMediumLeaderboard));
        OnPropertyChanged(nameof(HasHardLeaderboard));
        OnPropertyChanged(nameof(HasSpecialLeaderboard));
    }

    private static void LoadLeaderboard(string key,
        ObservableCollection<LeaderboardEntryViewModel> target)
    {
        target.Clear();
        var records = SettingsService.GetLeaderboard(key);
        for (int i = 0; i < records.Count; i++)
            target.Add(new LeaderboardEntryViewModel(i + 1, records[i].Name, $"{records[i].Seconds}s", false));
    }

    private string GetCurrentModeKey() => _currentDifficulty switch
    {
        Difficulty.Easy    => "Easy",
        Difficulty.Medium  => "Medium",
        Difficulty.Hard    => "Hard",
        Difficulty.Special => $"Special:{_currentSpecialMap?.Id ?? "Unknown"}",
        _                  => "Easy"
    };

    private void NotifyStarDependents()
    {
        OnPropertyChanged(nameof(Stars));
        OnPropertyChanged(nameof(HardChipTooltip));
        OnPropertyChanged(nameof(SpecialChipTooltip));
    }

    private void NotifyUnlockChanged()
    {
        NotifyStarDependents();
        OnPropertyChanged(nameof(IsHardUnlocked));
        OnPropertyChanged(nameof(IsSpecialUnlocked));
        OnPropertyChanged(nameof(HardChipLabel));
        OnPropertyChanged(nameof(SpecialChipLabel));
    }

    private void ShowUnlockConfirm(string mode)
    {
        _pendingUnlockMode = mode;
        OnPropertyChanged(nameof(UnlockConfirmTitle));
        OnPropertyChanged(nameof(UnlockConfirmBody));
        IsUnlockConfirmVisible = true;
    }

    private void NotifyDifficultySelection()
    {
        OnPropertyChanged(nameof(IsEasySelected));
        OnPropertyChanged(nameof(IsMediumSelected));
        OnPropertyChanged(nameof(IsHardSelected));
        OnPropertyChanged(nameof(IsSpecialSelected));
        OnPropertyChanged(nameof(CellSize));
        OnPropertyChanged(nameof(CellMargin));
        OnPropertyChanged(nameof(CellFontSize));
        OnPropertyChanged(nameof(BoardMaxHeight));
    }

    private void RefreshAllCells()
    {
        bool gameOver = _gameState is GameState.Won or GameState.Lost;
        foreach (var cell in _cells)
            cell.Refresh(gameOver);
    }
}
