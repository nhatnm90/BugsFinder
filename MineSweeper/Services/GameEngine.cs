using System;
using System.Windows.Threading;
using BugsFinder.Models;

namespace BugsFinder.Services;

/// <summary>Difficulty presets that map to board size and mine count.</summary>
public enum Difficulty
{
    /// <summary>9 × 9 board with 10 mines.</summary>
    Easy,

    /// <summary>16 × 16 board with 40 mines.</summary>
    Medium,

    /// <summary>16 × 30 board with 99 mines (Expert).</summary>
    Hard,

    /// <summary>Country-shaped board defined by a <see cref="SpecialMap"/>.</summary>
    Special
}

/// <summary>
/// Central game engine. Owns the active <see cref="Board"/>, drives
/// <see cref="GameState"/> transitions, controls the timer, manages the
/// session-scoped star count, and places secret bonus cells.
/// All public methods must be called from the UI thread.
/// </summary>
public class GameEngine
{
    private readonly DispatcherTimer _timer;
    private readonly HashSet<(int, int)> _triggeredQuizCells = new();
    private int  _elapsedSeconds;
    private int  _secondsSinceLastChallenge;
    private bool _isInChallenge;
    private int  _challengeSecondsLeft;
    private bool _challengePaused;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>
    /// When <see langword="true"/> the periodic challenge countdown is active.
    /// When <see langword="false"/> (default) challenges never fire and the timer
    /// only shows elapsed time.
    /// </summary>
    public bool IsExtremeMode { get; private set; }

    /// <summary>Gets the current game board, or <see langword="null"/> before the first game.</summary>
    public Board? Board { get; private set; }

    /// <summary>Gets the current game state.</summary>
    public GameState GameState { get; private set; } = GameState.NotStarted;

    /// <summary>Gets the active difficulty preset.</summary>
    public Difficulty Difficulty { get; private set; } = Difficulty.Easy;

    /// <summary>
    /// Gets the active special map, or <see langword="null"/> when
    /// <see cref="Difficulty"/> is not <see cref="Difficulty.Special"/>.
    /// </summary>
    public SpecialMap? ActiveSpecialMap { get; private set; }

    /// <summary>Whole seconds elapsed since the first reveal; stops when game ends.</summary>
    public int ElapsedSeconds => _elapsedSeconds;

    /// <summary><see langword="true"/> when a 5-second challenge countdown is active.</summary>
    public bool IsInChallenge => _isInChallenge;

    /// <summary>Remaining seconds in the current challenge (1–5). Zero when no challenge is active.</summary>
    public int ChallengeSecondsLeft => _challengeSecondsLeft;

    /// <summary>
    /// Session-scoped star count. Survives across new games; initialised once
    /// via <see cref="InitializeStars"/> when the application starts.
    /// </summary>
    public int Stars { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>Raised once per second while the timer is running.</summary>
    public event EventHandler? TimerTick;

    /// <summary>Raised whenever <see cref="GameState"/> changes.</summary>
    public event EventHandler<GameState>? GameStateChanged;

    /// <summary>
    /// Raised when one or more secret bonus cells were revealed.
    /// The integer argument is the number of secrets found (always ≥ 1).
    /// Stars have already been incremented before this event fires.
    /// </summary>
    public event EventHandler<int>? SecretCellsFound;

    /// <summary>
    /// Raised when a star is consumed to block a bug hit.
    /// Stars have already been decremented before this event fires.
    /// </summary>
    public event EventHandler? StarShielded;

    /// <summary>
    /// Raised the first time a quiz mine is correctly flagged.
    /// The ViewModel should respond by showing the quiz popup.
    /// </summary>
    public event EventHandler? QuizTriggered;

    /// <summary>Raised when a challenge period begins (every 30 s of play).</summary>
    public event EventHandler? ChallengeStarted;

    /// <summary>
    /// Raised each second during a challenge. The integer argument is the
    /// remaining seconds (4, 3, 2, 1, 0). At 0 the game has already been lost.
    /// </summary>
    public event EventHandler<int>? ChallengeTick;

    /// <summary>Raised when the player resolves the challenge in time.</summary>
    public event EventHandler? ChallengeSuccess;

    /// <summary>
    /// Raised when the player earns a star by resolving an Extreme-mode challenge.
    /// Stars have already been incremented before this event fires.
    /// </summary>
    public event EventHandler? ChallengeStarEarned;

    /// <summary>Raised when the countdown expires; the game is already in the Lost state.</summary>
    public event EventHandler? ChallengeFailed;

    // ── Constructor ───────────────────────────────────────────────────────────

    /// <summary>Initialises a new <see cref="GameEngine"/> instance.</summary>
    public GameEngine()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            _elapsedSeconds++;
            TimerTick?.Invoke(this, EventArgs.Empty);

            if (GameState != GameState.Playing) return;

            if (IsExtremeMode && !_challengePaused)
            {
                if (_isInChallenge)
                {
                    _challengeSecondsLeft--;
                    ChallengeTick?.Invoke(this, _challengeSecondsLeft);

                    if (_challengeSecondsLeft <= 0)
                    {
                        _isInChallenge = false;
                        Board!.RevealAllMines();
                        GameState = GameState.Lost;
                        _timer.Stop();
                        ChallengeFailed?.Invoke(this, EventArgs.Empty);
                        GameStateChanged?.Invoke(this, GameState);
                    }
                }
                else
                {
                    _secondsSinceLastChallenge++;
                    if (_secondsSinceLastChallenge >= ChallengeIntervalSeconds())
                    {
                        _isInChallenge        = true;
                        _challengeSecondsLeft = 5;
                        _secondsSinceLastChallenge = 0;
                        ChallengeStarted?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        };
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Suspends or resumes the Extreme-mode challenge countdown without ending the challenge.
    /// Call with <see langword="true"/> when a quiz popup opens, and with <see langword="false"/>
    /// when it closes, so the countdown is frozen for the duration of the quiz.
    /// </summary>
    public void SetChallengePaused(bool paused) => _challengePaused = paused;

    /// <summary>
    /// Enables or disables Extreme mode. When disabled any active challenge is
    /// cancelled immediately and <see cref="ChallengeSuccess"/> is raised so the
    /// View can stop its flash animation.
    /// </summary>
    public void SetExtremeMode(bool value)
    {
        if (IsExtremeMode == value) return;
        IsExtremeMode = value;

        if (!value)
        {
            _secondsSinceLastChallenge = 0;
            if (_isInChallenge)
            {
                _isInChallenge        = false;
                _challengeSecondsLeft = 0;
                ChallengeSuccess?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Sets the session star count. Call once at application startup with the value
    /// from <see cref="SettingsService.DefaultStars"/>; stars then persist for the
    /// lifetime of the application regardless of how many games are played.
    /// </summary>
    public void InitializeStars(int count) => Stars = count;

    /// <summary>Deducts <paramref name="count"/> stars (floored at zero).</summary>
    public void SpendStars(int count) => Stars = Math.Max(0, Stars - count);

    /// <summary>Awards <paramref name="count"/> stars (e.g. from a correct quiz answer).</summary>
    public void GainStars(int count) => Stars += count;

    /// <summary>
    /// Resets all state and creates a fresh <see cref="Board"/> for the given
    /// <paramref name="difficulty"/>. Star count is NOT reset (session-scoped).
    /// </summary>
    public void NewGame(Difficulty difficulty)
    {
        _timer.Stop();
        _elapsedSeconds            = 0;
        _secondsSinceLastChallenge = 0;
        _isInChallenge             = false;
        _challengeSecondsLeft      = 0;
        _challengePaused           = false;
        _triggeredQuizCells.Clear();
        Difficulty                 = difficulty;
        ActiveSpecialMap           = null;
        GameState                  = GameState.NotStarted;

        var (rows, cols, mines) = GetParams(difficulty);
        Board = new Board(rows, cols, mines);

        GameStateChanged?.Invoke(this, GameState);
    }

    /// <summary>
    /// Resets all state and creates a fresh shaped <see cref="Board"/> for
    /// <paramref name="map"/>. Star count is NOT reset (session-scoped).
    /// </summary>
    public void NewGame(SpecialMap map)
    {
        _timer.Stop();
        _elapsedSeconds            = 0;
        _secondsSinceLastChallenge = 0;
        _isInChallenge             = false;
        _challengeSecondsLeft      = 0;
        _challengePaused           = false;
        _triggeredQuizCells.Clear();
        Difficulty                 = Difficulty.Special;
        ActiveSpecialMap           = map;
        GameState                  = GameState.NotStarted;

        Board = new Board(map.Rows, map.Cols, map.MineCount,
                          map.BuildActiveCells(), map.BuildDecorativeCells());

        GameStateChanged?.Invoke(this, GameState);
    }

    /// <summary>
    /// Processes a reveal action on (<paramref name="row"/>, <paramref name="col"/>).
    /// On the very first call: places mines (with first-click safety), places secret
    /// bonus cells, and starts the timer. Subsequent calls reveal cells.
    /// When a bug is hit and <see cref="Stars"/> &gt; 0 a star is consumed instead
    /// of ending the game.
    /// </summary>
    public GameState RevealCell(int row, int col)
    {
        if (Board is null || GameState is GameState.Won or GameState.Lost)
            return GameState;

        // ── First click: place mines + secrets, start timer ──────────────────
        if (GameState == GameState.NotStarted)
        {
            Board.GenerateMines(row, col);
            Board.PlaceSecretCells(ComputeSecretCount());
            Board.PlaceQuizMines(ComputeQuizCount());
            GameState = GameState.Playing;
            _timer.Start();
            GameStateChanged?.Invoke(this, GameState);
        }

        // ── Count secret cells before reveal for delta detection ─────────────
        int secretsBefore = Board.UnrevealedSecretCount;

        // ── Reveal ───────────────────────────────────────────────────────────
        bool hitBug = Board.RevealCell(row, col);

        // ── Award stars for any newly revealed secret cells ──────────────────
        int secretsFound = secretsBefore - Board.UnrevealedSecretCount;
        if (secretsFound > 0)
        {
            Stars += secretsFound;
            SecretCellsFound?.Invoke(this, secretsFound);
        }

        // ── Handle bug hit ───────────────────────────────────────────────────
        if (hitBug)
        {
            if (Stars > 0)
            {
                // Star shields the player — undo reveal, auto-flag the bug cell
                Stars--;
                Board.ShieldBugCell(row, col);
                StarShielded?.Invoke(this, EventArgs.Empty);

                // Hitting a bug cell (even shielded) counts as correctly identifying it
                if (_isInChallenge)
                    ResolveChallenge();

                // Still check win (edge case: last non-bug cell was already revealed)
                if (Board.CheckWinCondition())
                {
                    GameState = GameState.Won;
                    _timer.Stop();
                    GameStateChanged?.Invoke(this, GameState);
                }
            }
            else
            {
                Board.RevealAllMines();
                GameState = GameState.Lost;
                _timer.Stop();
                GameStateChanged?.Invoke(this, GameState);
            }
        }
        else if (Board.CheckWinCondition())
        {
            GameState = GameState.Won;
            _timer.Stop();
            GameStateChanged?.Invoke(this, GameState);
        }

        return GameState;
    }

    /// <summary>Toggles a flag on (<paramref name="row"/>, <paramref name="col"/>). Ignored when game is over.</summary>
    public void FlagCell(int row, int col)
    {
        if (Board is null || GameState is GameState.Won or GameState.Lost) return;
        Board.FlagCell(row, col);

        var cell = Board.GetCell(row, col);

        // During a challenge: correctly flagging a mine resolves the challenge
        if (_isInChallenge && cell is { IsBug: true, IsFlagged: true })
            ResolveChallenge();

        // Quiz trigger: first time this quiz mine cell is correctly flagged
        if (cell is { IsBug: true, IsQuizMine: true, IsFlagged: true }
            && !_triggeredQuizCells.Contains((row, col)))
        {
            _triggeredQuizCells.Add((row, col));
            QuizTriggered?.Invoke(this, EventArgs.Empty);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Computes how many secret cells to scatter on the current board.
    /// Scales with mine count but is capped at MineCount/10; always at least 1.
    /// </summary>
    /// <summary>Returns the idle seconds between challenges for the current difficulty.</summary>
    private int ChallengeIntervalSeconds() => Difficulty switch
    {
        Difficulty.Medium => 15,
        Difficulty.Hard   => 20,
        _                 => 10,   // Easy and Special
    };

    /// <summary>Returns how many quiz mines to scatter on the current board.</summary>
    private int ComputeQuizCount() => Math.Clamp((Board?.MineCount ?? 10) / 8, 2, 5);

    private int ComputeSecretCount()
    {
        int max = Math.Max(1, (Board?.MineCount ?? 0) / 10);
        return Random.Shared.Next(1, max + 1);
    }

    /// <summary>
    /// Clears the active challenge and raises <see cref="ChallengeSuccess"/>.
    /// The 30-second counter resets so the next challenge fires after another 30 s.
    /// </summary>
    private void ResolveChallenge()
    {
        _isInChallenge             = false;
        _challengeSecondsLeft      = 0;
        _secondsSinceLastChallenge = 0;
        Stars++;
        ChallengeStarEarned?.Invoke(this, EventArgs.Empty);
        ChallengeSuccess?.Invoke(this, EventArgs.Empty);
    }

    private static (int rows, int cols, int mines) GetParams(Difficulty d) => d switch
    {
        Difficulty.Easy   => ( 9,  9, 10),
        Difficulty.Medium => (16, 16, 40),
        Difficulty.Hard   => (16, 30, 99),
        _                 => ( 9,  9, 10)
    };
}
