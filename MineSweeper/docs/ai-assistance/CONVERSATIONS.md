# MineSweeper — Development Conversation Log

> Reconstructed history of conversations that shaped the app from initial scaffold to its current state.  
> Format: `**User:**` / `**Assistant:**` exchanges, grouped by feature/session.

---

## Session 1 — Project Scaffold & Core Game Logic

**User:**
I need you to initialize a complete WPF Minesweeper desktop application in .NET 8, using Visual Studio. Create the full project structure and basic UI per these requirements:

**Tech Stack**

- .NET 8, WPF
- MVVM pattern (no external MVVM framework — implement RelayCommand and ViewModelBase manually to demonstrate understanding of the pattern)
- CommunityToolkit.Mvvm NuGet package (to reduce INotifyPropertyChanged boilerplate)

**Project Structure**

```
MineSweeper/
├── MineSweeper.sln
└── MineSweeper/
    ├── Models/
    │   ├── Cell.cs
    │   ├── Board.cs
    │   └── GameState.cs          (enum: NotStarted, Playing, Won, Lost)
    ├── ViewModels/
    │   ├── ViewModelBase.cs
    │   ├── RelayCommand.cs
    │   ├── MainViewModel.cs
    │   └── CellViewModel.cs
    ├── Views/
    │   ├── MainWindow.xaml
    │   └── MainWindow.xaml.cs
    ├── Services/
    │   └── GameEngine.cs
    ├── Converters/
    └── App.xaml
```

**Game Logic**

- `Cell`: IsRevealed, IsFlagged, IsMine, AdjacentMineCount, Row, Column
- `Board`: GenerateMines with first-click safety (clicked cell + 8 neighbors never get mines), CalculateAdjacentCounts, RevealCell with BFS flood-fill, FlagCell, CheckWinCondition
- `GameEngine`: NewGame(Difficulty), Difficulty enum Easy/Medium/Hard, timer starting from first click

**UI**

- Header: mine counter (left), emoji reset button center (🙂/😎/😵), timer (right)
- Game board: ItemsControl + UniformGrid, 30×30px cells, left-click reveal, right-click flag
- Bottom bar: Easy / Medium / Hard difficulty chips
- Number colors: 1=Blue, 2=Green, 3=Red, 4=DarkBlue, 5=DarkRed, 6=Teal, 7=Black, 8=Gray
- Cell styles: unrevealed (light gray, 3D border), revealed (flat white/gray), mine-hit (red background)

**Additional requirements**

1. On loss: reveal all mines, disable board
2. On win: show congratulations, disable board
3. Timer starts on first click, stops on win/loss
4. No code-behind logic (only InitializeComponent)
5. XML doc comments on all public classes and methods

Create all files with complete, non-placeholder code, ready to build in Visual Studio 2022.

**Assistant:**
Created the complete project. Key decisions:

- `ViewModelBase` hand-written with `SetProperty<T>` helper (CommunityToolkit.Mvvm reference kept in .csproj but MVVM is hand-rolled as requested)
- `Board.GenerateMines()` defers mine placement until first click; safe zone = clicked cell + all 8 neighbors in a HashSet
- `Board.RevealCell()` uses iterative BFS queue to avoid stack overflow on large flood-fills
- `GameEngine` owns a `DispatcherTimer` that starts on first reveal and stops on win/loss
- `CellViewModel.DisplayText`: `""` → `"🚩"` → `"💣"` → number → `""` (blank revealed)
- `NumberToColorConverter` maps count 1–8 to `SolidColorBrush` resources defined in XAML
- Hard difficulty uses a 16-column × 30-row grid; `UniformGrid` columns are data-bound

All files created and ready to build.

---

## Session 2 — Visual Polish & Theme System

**User:**
The basic game works. Now I want to make it look really polished. I'm thinking VS Code-style aesthetics — dark theme by default. Also add Light and Blue themes that users can switch at runtime. The theme chips should be in the header row alongside the difficulty chips. Persist the selected theme across app restarts.

**Assistant:**
Added a full theme system:

- Created `Themes/DarkTheme.xaml`, `LightTheme.xaml`, `BlueTheme.xaml` as ResourceDictionary files
  - Dark: VS Code Dark+ palette (`#1E1E1E` bg, `#007ACC` accent)
  - Light: VS Code Light+ palette (`#F3F3F3` bg, `#0078D4` accent)
  - Blue: Tomorrow Night Blue (`#002451` bg, `#FFD700` amber accent)
- Each theme defines: `AppBg`, `CardBg`, `CellUnrevealed`, `CellRevealed`, `CellMine`, `TextPrimary`, `TextSecondary`, `TextMuted`, `Num1`–`Num8`, `CurtainColor`, `TutPrimaryBg/Fg`, `TutSecondaryBg/Fg`, `TutSkipFg/HoverBg`
- Created `Services/ThemeService.cs` — static `Apply(theme)` method swaps `Application.Current.Resources.MergedDictionaries` at runtime
- Created `Services/SettingsService.cs` — JSON persistence at `%LocalAppData%\MineSweeper\settings.json` storing theme + future settings
- `App.xaml.cs` `OnStartup` loads saved theme before MainWindow opens
- `MainViewModel` exposes `ThemeCommand` and `CurrentTheme` for chip highlighting
- `NumberToColorConverter` uses `Application.Current.FindResource(key)` so number colors auto-update on theme switch

---

## Session 3 — Special Maps (Country-Shaped Boards)

**User:**
I want to add a "Special" difficulty mode where the board is shaped like a real country instead of a rectangle. Start with Vietnam — it has a distinctive S-shape. Cells outside the border should be hidden. Same game rules inside.

**Assistant:**
Implemented the Special Maps architecture:

- Created `Models/SpecialMap.cs` — abstract base with `Rows`, `Cols`, `MineCount`, `GetActiveCells()`, `GetDecorativeCells()`
  - `SpecialMapRegistry` — static dictionary keyed by string ID; `GetAll()` returns registered maps
- Created `VietnamMap` nested class (34×34 bounding box, 207 active cells, 25 mines):
  - Hand-crafted cell coordinates trace Vietnam's S-shaped coastline from Hà Giang (north tip) through the Red River Delta, narrow Quảng Bình waist, central coast bulge, Mekong fan, down to Cà Mau peninsula
  - Decorative cells mark approximate Hoàng Sa (Paracel) and Trường Sa (Spratly) island regions
- `Board` constructor overloaded to accept `HashSet<(int,int)> activeCells` and `HashSet<(int,int)> decorativeCells`
  - Mine generation skips non-active cells
  - Neighbor calculation only considers active neighbors
  - `Cell.IsDecorative` flag: visible but not interactive
- `GameEngine.NewGame` branches on `Difficulty.Special` to instantiate correct SpecialMap
- `MainViewModel` adjusts cell size (20px Special vs 36px normal) and font size (11 vs 14) dynamically
- Added "Special" chip to difficulty bar; chip label shows map name ("Việt Nam")

---

## Session 4 — Star / Shield System

**User:**
Add a shield mechanic to the game. Players earn stars by revealing hidden bonus cells scattered on the board. When a player hits a mine and has at least one star, the mine is absorbed and auto-flagged — the game continues. If they have no stars, it's an instant loss. Display the star count in the header with a bounce animation on change.

**Assistant:**
Implemented the star/shield system:

- `GameEngine` gained session-scoped `Stars` property, initialized from `SettingsService.DefaultStars`
  - On mine hit with stars > 0: one star consumed (`Stars--`), mine cell un-revealed and auto-flagged (`ShieldBugCell`), game resumes
  - On mine hit with stars = 0: normal loss path
- `GameEngine.SpendStars(int)` and `GainStars(int)` helpers for external callers
- `SecretCellsFound` event raised when revealing hidden bonus cells; stars already incremented before event fires
- `StarShielded` event raised when a star absorbs a mine hit
- `MainViewModel` exposes `Stars` property, subscribes to both events, and raises `StarGained` / `StarLost` events upward
- Header updated: star counter TextBlock (⭐ + count) positioned left of mine counter
- `MainWindow.xaml.cs` — `AnimateStar(bool gain)`:
  - Gain: `ScaleTransform` peak 1.45×, 140ms `QuadraticEase EaseOut` back to 1.0
  - Loss: peak 0.75×, same duration
- `StarScaleTransform` defined as named resource on the star TextBlock via `RenderTransform`

---

## Session 5 — Challenge Mode

**User:**
Add a tension mechanic: every N seconds (varying by difficulty) a 5-second countdown fires. The player must correctly identify a mine — by flagging it or revealing a mine cell (a star absorbs it) — before the clock reaches zero. Failure means immediate game over. The reset button should pulse visually during the countdown.

**Assistant:**
Implemented challenge mode inside `GameEngine`'s existing 1-second `DispatcherTimer`:

- Challenge intervals: Easy/Special = 10s, Medium = 15s, Hard = 20s
- Challenge flow: `_secondsSinceLastChallenge` increments each tick; at threshold → `_isInChallenge = true`, `_challengeSecondsLeft = 5`; each tick decrements; at 0 → `RevealAllMines()` + `GameState.Lost`
- Challenge resolves on: correct flag (`IsMine && IsFlagged`), or mine reveal (including star-shielded hits)
- `ChallengeStarted` / `ChallengeTick(int remaining)` / `ChallengeSuccess` / `ChallengeFailed` events
- `MainViewModel` forwards `ChallengeStarted` → `ChallengeStarted` event; `ChallengeSuccess` / `ChallengeFailed` → `ChallengeEnded` event (code-behind subscribes to both)
- `TimerDisplay`: shows `ChallengeSecondsLeft.ToString("D3")` while challenge active, elapsed otherwise
- `MainWindow.xaml.cs`:
  - `StartChallengeFlash()`: `DoubleAnimation` 1.0 ↔ 0.15 on `ResetBtn.Opacity`, 350ms period, `RepeatBehavior.Forever`, `QuadraticEase EaseInOut`
  - `StopChallengeFlash()`: clears animation, restores `Opacity = 1.0`

---

## Session 6 — Secret Bonus Cells

**User:**
Add hidden bonus cells on the board. When the player reveals one, they earn a star. Make it subtle — no visual indicator until revealed. Cap how many can exist per game.

**Assistant:**
Added secret cells to `GameEngine` and `Board`:

- After mines are placed, `GameEngine` selects 1–`MineCount/10` random blank cells (cells with `AdjacentMineCount == 0` and not a mine) and marks `Cell.IsSecret = true`
- Revealing a secret cell triggers `StarGained` event and plays a distinct audio cue
- `CellViewModel` does not expose `IsSecret` — no visual difference until revealed
- `Cell` model gained `IsSecret` flag alongside existing `IsDecorative`
- Cap ensures secret count scales with difficulty density

---

## Session 7 — Audio System

**User:**
Add sound effects for all game events. I don't want to manage audio files — generate everything in code. No external WAV files. Create the sound for the list of actions as below:
flood, reveal, explode, win, lose, time counter, etc and the suitable relation sounds.

**Assistant:**
Created `Services/AudioService.cs` with full PCM synthesis:

All 9 sounds generated at construction time, stored as `SoundPlayer` instances in a dictionary:

| Key               | Sound                | Design                                |
| ----------------- | -------------------- | ------------------------------------- |
| `reveal`          | Single cell reveal   | 880 Hz sine, 80ms, linear decay       |
| `flood`           | Flood-fill expand    | C5→E5→G5 ascending arpeggio, ~240ms   |
| `explode`         | Mine hit / loss      | 75 Hz sine + filtered noise, 600ms    |
| `win`             | Victory jingle       | C4→E4→G4→C5, ~680ms                   |
| `flag`            | Flag toggle          | 700 Hz tick, 55ms                     |
| `secret`          | Secret cell found    | E6→G6 "ting-ting", ~170ms             |
| `star-used`       | Star shield consumed | 600→440 Hz descending glide, 180ms    |
| `challenge-alert` | Challenge starts     | E5→C5→A4 descending alarm, ~300ms     |
| `tick`            | Challenge countdown  | 880+1320 Hz dual-tone + 660 Hz, 180ms |

PCM samples wrapped in a minimal valid WAV container (44-byte header) and loaded via `new MemoryStream(bytes)` into `SoundPlayer`. All playback is fire-and-forget on a background thread. Deterministic noise uses a fixed seed.

`MainViewModel` holds `AudioService` instance and calls appropriate method on every game event.

---

## Session 8 — Tutorial Overlay

**User:**
Add an interactive tutorial that runs on every app launch. It should spotlight different UI regions with a dim overlay, walk the player through game mechanics step by step, and be skippable at any point.
The tutorial was shown anytime the application runs no matter what the app was opened before. The user can skip this one or go to the end and start with the easy mode first. Each step can be follow the flow to help the new user that is familiar with the Minesweeper or not, can easy to catch up and play such as:

1. number of bugs
2. time counting begins right after the first click of the player.
3. button to switch themes, change mode (map)
4. ...

**Assistant:**
Created `ViewModels/TutorialViewModel.cs` and integrated into `MainWindow`:

**TutorialViewModel:**

- `TutorialStep` record struct: `Title`, `Description`, `TutorialTarget`
- `TutorialTarget` enum: `None` (full dim), `Header`, `ResetButton`, `Board`, `DifficultyBar`, `ExtremeButton`
- Properties: `IsVisible`, `StepTitle`, `StepDescription`, `CurrentTarget`, `StepProgress`, `NextButtonText`, `CanGoPrev`, `CanSkip`
- Commands: `NextCommand` (advances; dismisses on last slide), `PrevCommand`, `SkipCommand`
- `StepChanged` event raised on every step transition and on dismiss
- Session-scoped dismissal only — tutorial always shows on startup

**Slides (12 total):**

1. Welcome — Target: None
2. The Game Board — Target: Board
3. Left-click to Reveal — Target: Board
4. Right-click to Flag — Target: Board
5. Reading the Numbers — Target: Board
6. Bug Counter, Stars & Timer — Target: Header
7. Quiz Mines — Target: Board
8. New Game Button — Target: ResetButton
9. Extreme Mode — Target: ExtremeButton
10. Difficulty Levels — Target: DifficultyBar
11. Unlocking Hard & Special — Target: DifficultyBar
12. You're Ready! — Target: None

**Spotlight system (`MainWindow.xaml.cs`):**

- `TutorialOverlay` Canvas with 4 curtain Rectangles + `SpotlightBlocker` (`IsHitTestVisible=True`, transparent)
- `GetSpotRect()`: maps `TutorialTarget` → named element → `TransformToAncestor(RootGrid).TransformBounds()`, 8px padding
- `SetCurtains()`: sizes Top/Left/Right/Bottom around spotlight; `SpotlightBlocker` covers the spotlight itself to block game input during tutorial
- `PositionCard()`: card placed below → above → centered; horizontally clamped; 14px gap
- Subscribes to `Tutorial.StepChanged` + `SizeChanged` → `Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateTutorial)`

---

## Session 9 — Always-On Fullscreen & Extreme Mode

**User:**
Make the app always run in borderless fullscreen — no restore button. Replace the fullscreen toggle with an Extreme Mode toggle (⚡). When Extreme Mode is active, the existing periodic countdown challenges fire as normal. When it's off, no challenges ever trigger and the timer simply counts up. Add the Extreme Mode explanation to the tutorial.

**Assistant:**
**Fullscreen (always-on):**

- `MainWindow.xaml.cs`: replaced `ToggleFullScreen()` with a one-way `GoFullScreen()` called once in `OnLoaded`; removed all F11/Escape key handlers
- `MainViewModel.IsFullScreen` is now a computed property that always returns `true`
- Window permanently configured: `WindowStyle=None`, `WindowState=Maximized`, `ResizeMode=NoResize`

**Extreme Mode:**

- `GameEngine.IsExtremeMode` property + `SetExtremeMode(bool)` method
  - `SetExtremeMode(false)` immediately cancels any active challenge (fires `ChallengeSuccess` so the flash animation stops) without awarding a star
  - All challenge-countdown logic inside the timer tick is gated on `if (IsExtremeMode)`
- `MainViewModel`: `IsExtremeMode`, `ExtremeButtonText` ("⚡ Extreme" / "⚡ Extreme ON"), `ToggleExtremeModeCommand`
- `MainWindow.xaml`: `ExtremeBtn` (x:Name) in the top-right control strip; uses `selected-chip` styling when active
- Tutorial slide 9 added: spotlights `ExtremeButton`, explains ON/OFF behavior and the timer display change

---

## Session 10 — Close Button & Confirmation Dialog

**User:**
Add an application close button in the top-right corner of the screen. Clicking it should show a confirmation popup ("Exit game?") before shutting down the application.

**Assistant:**

- `MainViewModel`: `IsCloseConfirmVisible` property, `RequestCloseCommand` (shows popup), `CancelCloseCommand` (hides popup), `ConfirmCloseCommand` (hides popup + raises `CloseConfirmed` event)
- `MainWindow.xaml.cs`: subscribes to `CloseConfirmed` → `Application.Current.Shutdown()`
- `MainWindow.xaml`:
  - Close button styled as a small circular button (✕), top-right corner (`HorizontalAlignment="Right"`, `Margin="0,14,14,0"`, `Panel.ZIndex="10"`) with red hover state (`CloseButtonStyle`)
  - Exit confirm popup (ZIndex=20): dim backdrop + card with "Exit game?" heading, Cancel and Confirm buttons

---

## Session 11 — Difficulty Lock / Unlock System

**User:**
Hard and Special difficulty modes should be locked at the start of each session. Hard unlocks for 10 stars, Special for 15 stars. Hovering over a locked chip shows a tooltip indicating the current shortfall or confirming affordability. Clicking an affordable locked chip shows a confirmation popup that deducts the stars and unlocks the mode for the rest of the session. Locks and star balances reset when the app is closed.

**Assistant:**
Implemented a session-scoped lock system in `MainViewModel`:

**Lock state:**

- `_isHardUnlocked` / `_isSpecialUnlocked` booleans (session memory, never persisted)
- `HardUnlockCost = 10`, `SpecialUnlockCost = 15` public constants
- `HardChipLabel` / `SpecialChipLabel`: append "🔒" when locked
- `HardChipTooltip` / `SpecialChipTooltip`: return `null` when unlocked (disables WPF tooltip); when locked, show either shortfall message or "✅ You have enough stars!" depending on current balance
- Chip `Opacity` reduced to 0.55 in XAML when locked via DataTrigger

**`ChangeDifficultyCommand` gate:**

- Hard/Special clicks while locked: if insufficient stars → silently ignored; if affordable → `ShowUnlockConfirm(mode)`
- Normal difficulty switch proceeds unchanged for unlocked modes

**Unlock confirm popup (ZIndex=20):**

- `IsUnlockConfirmVisible`, `UnlockConfirmTitle`, `UnlockConfirmBody` (shows cost, current balance, balance after)
- `ConfirmUnlockCommand`: calls `SpendStars`, sets unlock flag, switches difficulty, starts new game
- `CancelUnlockCommand`: hides popup

**`SettingsService`:**

- `DefaultStars` is a runtime constant (`ConfiguredDefaultStars = 14`), not persisted to JSON — prevents stale values surviving across code changes
- Session star count initialized once in `MainViewModel` constructor via `_engine.InitializeStars(SettingsService.DefaultStars)`

**Tutorial updated:** slides 10 ("Difficulty Levels") and 11 ("Unlocking Hard & Special") added, explaining lock costs and unlock flow.

---

## Session 12 — Quiz Mines/Bugs

**User:**
Introduce quiz mines: a random subset of mines on each board are secretly designated as quiz mines. When the player correctly flags one for the first time, a multiple-choice quiz popup appears. A correct answer awards +3 stars; an incorrect answer deducts 1 star; closing without answering forfeits the opportunity with no star change. Use the question bank at `QuizGenerator/real_data.json`.

**Assistant:**
**Data layer:**

- `Models/QuizOption.cs` → `QuizOption { Value, IsAnswer }`
- `Models/QuizQuestion.cs` → `QuizQuestion { Id, Question, List<QuizOption> Options }`
- `MineSweeper.csproj`: `<EmbeddedResource Include="QuizGenerator\real_data.json"/>`

**`Services/QuizService.cs`** (static):

- Loads embedded JSON via `Assembly.GetManifestResourceStream`; `System.Net.WebUtility.HtmlDecode` applied to question and option strings
- Shuffled non-repeating pool: indices shuffled into `_pool`; when pool is exhausted it refills from the full list — every question is seen before any repeats
- `EnsureLoaded()` (idempotent) + `GetRandom() → QuizQuestion?`

**`Models/Cell.cs`**: added `IsQuizMine` bool flag

**`Models/Board.cs`**: `PlaceQuizMines(int count)` — Fisher-Yates partial shuffle over mine cells, called after `GenerateMines`

**`GameEngine`**:

- `ComputeQuizCount() → Math.Clamp(MineCount / 8, 2, 5)`
- `PlaceQuizMines` called on first click alongside `PlaceSecretCells`
- `HashSet<(int,int)> _triggeredQuizCells` — cleared on `NewGame`; prevents a mine triggering more than one quiz per game
- `QuizTriggered` event fired from `FlagCell` on first correct flag of a quiz mine cell

**`MainViewModel`**:

- `QuizService.EnsureLoaded()` called in constructor (preloads bank, no delay on first popup)
- `_activeQuiz`, `IsQuizPopupVisible`, `IsQuizAnswered`, `QuizQuestionText`, `QuizOption0–3`, `QuizResultMessage`, `QuizResultIsCorrect`
- `AnswerQuizCommand` (CommandParameter = `"0"`–`"3"`): resolves answer, awards/deducts stars, fires `StarGained`/`StarLost` animation events, sets `IsQuizAnswered = true`
- `CloseQuizCommand`: hides popup, resets state (no star change if called before answering)

**`MainWindow.xaml`** — quiz popup (ZIndex=25):

- Rules strip (always visible): "+3 ⭐ correct / −1 ⭐ wrong / 0 skip"
- Question view (collapses when `IsQuizAnswered = true`): question text + 2×2 answer grid (`QuizOptionStyle` pill buttons)
- Result view (collapses when `IsQuizAnswered = false`): result message with DataTrigger color (green correct / red wrong) + Close button

**Tutorial updated:** slide 7 ("Quiz Mines 🎯") added to the tour.

---

## Session 13 — Post-Win Leaderboard

**User:**
After winning any mode, show a popup where the player can enter their name to be added to a per-mode leaderboard. The leaderboard should display the top 10 fastest completions in ascending order; the player's newly submitted entry should be highlighted. The popup auto-closes 10 seconds after submission. Persist leaderboard data across sessions.

**Assistant:**
Replaced the previous single-record best-time system with a full per-mode leaderboard:

**`SettingsService`:**

- `AppSettings.Leaderboards: Dictionary<string, List<GameRecord>>` (replaces `Records: Dictionary<string, GameRecord?>`)
- `GetLeaderboard(modeKey) → List<GameRecord>` — returns up to 10 entries sorted ascending by seconds
- `AddToLeaderboard(modeKey, seconds, name) → int` — inserts entry, sorts, truncates to 10, returns 0-based rank of new entry (or -1 if outside top 10); persists immediately

**`ViewModels/LeaderboardEntryViewModel.cs`** (new):

- Immutable DTO: `Rank` (1-based), `Name`, `TimeDisplay` ("Xs"), `IsHighlight`

**`MainViewModel`:**

- Removed: `IsNewRecord`, `NewRecordInput`, `PendingRecordTimeDisplay`, `SaveRecordCommand`, `PersistRecord()`
- Added: `IsLeaderboardVisible`, `IsLeaderboardSubmitted`, `LeaderboardNameInput`, `LeaderboardWinEmoji`, `LeaderboardWinMessage`, `LeaderboardTimeText`, `LeaderboardCountdownText`, `LeaderboardEntries (ObservableCollection<LeaderboardEntryViewModel>)`
- Added: `SubmitLeaderboardCommand`, `CloseLeaderboardCommand`, `PlayAgainFromLeaderboardCommand`
- `DispatcherTimer` (`_leaderboardCountdownTimer`): 1-second tick; decrements `_leaderboardCountdown`; hides popup at 0
- `ShowLeaderboard()` replaces `ShowResult(won: true)` — sets win emoji/message, resets input phase, shows popup
- `StartNewGame()` stops timer and collapses leaderboard if it is open

**`MainWindow.xaml`** — leaderboard popup (ZIndex=15, two-phase):

- **Phase 1** (`IsLeaderboardSubmitted = False`): win emoji + message, "⏱ Your time: Xs", name TextBox (Enter key submits), Submit and Skip buttons
- **Phase 2** (`IsLeaderboardSubmitted = True`): top-10 table with highlighted row (`ChipSelectedBg` background on `IsHighlight = True`), auto-close countdown, Close and Play Again buttons

Loss path unchanged: result popup (😵 + message) still used for losses.

---

## Session 14 — Extreme Challenge Star Reward & Quiz Feedback Fix

**User:**
Two corrections: (1) in Extreme Mode, successfully resolving a challenge countdown should award the player +1 star. (2) Wrong quiz answers are not visibly deducting a star — the star count updates but with no feedback animation.

**Assistant:**
**Extreme challenge star reward (`GameEngine`):**

- `private void ResolveChallenge()` now does `Stars++` then fires new `ChallengeStarEarned` event before `ChallengeSuccess`
- `SetExtremeMode(false)` inlines its own cleanup without calling `ResolveChallenge`, so toggling Extreme off mid-challenge does NOT award a star

**`MainViewModel`:**

- Subscribed to `_engine.ChallengeStarEarned`: calls `NotifyStarDependents()` + raises `StarGained` → triggers star bounce animation in code-behind

**Quiz answer feedback fix:**

- `AnswerQuizCommand` — correct path: raises `StarGained` three times (one per star) after `GainStars(3)` + `NotifyStarDependents()`
- `AnswerQuizCommand` — wrong path: raises `StarLost` once after `SpendStars(1)` + `NotifyStarDependents()`
- Previously, `StarGained`/`StarLost` were only raised via `SecretCellsFound` and `StarShielded`; quiz outcomes silently updated the count without triggering the animation, making the change easy to miss
