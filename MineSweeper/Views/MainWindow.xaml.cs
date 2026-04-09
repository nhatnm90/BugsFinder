using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using BugsFinder.ViewModels;

namespace BugsFinder.Views;

/// <summary>
/// Code-behind for <c>MainWindow.xaml</c>.
/// Contains only component initialisation, tutorial spotlight positioning,
/// and fullscreen toggle; all game logic resides in <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    private MainViewModel _vm = null!;

    /// <summary>Initialises the window and wires up the tutorial after layout.</summary>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _vm = (MainViewModel)DataContext;

        // Reposition spotlight whenever the tutorial step changes or is dismissed
        _vm.Tutorial.StepChanged += (_, _) =>
            Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateTutorial);

        // Reposition tutorial whenever the window is resized
        SizeChanged += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render, UpdateTutorial);

        // Minimize to taskbar
        _vm.MinimizeRequested += (_, _) => WindowState = WindowState.Minimized;

        // Gift animation when WR popup gift is opened
        _vm.GiftOpened += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            () => AnimateGiftOpen());

        // Exit confirmation
        _vm.CloseConfirmed += (_, _) => Application.Current.Shutdown();

        // Star animations — bounce scale up when gained, small shrink when lost
        _vm.StarGained += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            () => AnimateStar(gain: true));
        _vm.StarLost   += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            () => AnimateStar(gain: false));

        // Challenge flash — reset button emoji pulses during countdown
        _vm.ChallengeStarted += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            () => StartChallengeFlash());
        _vm.ChallengeEnded   += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            () => StopChallengeFlash());

        // Always start in borderless fullscreen
        GoFullScreen();

        // Intro splash — runs on top of everything; reveals tutorial when complete
        StartSplashAnimation();
    }

    // ── Star animation ────────────────────────────────────────────────────────

    /// <summary>
    /// Briefly scales the star counter up (gain) or down (loss) to give tactile feedback.
    /// </summary>
    private void AnimateStar(bool gain)
    {
        double peak     = gain ? 1.45 : 0.75;
        var    duration = new Duration(TimeSpan.FromMilliseconds(140));
        var    ease     = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        var ax = new DoubleAnimation(peak, 1.0, duration) { AutoReverse = false, EasingFunction = ease };
        var ay = new DoubleAnimation(peak, 1.0, duration) { AutoReverse = false, EasingFunction = ease };

        // First snap to peak, then animate back to 1
        StarScaleTransform.ScaleX = peak;
        StarScaleTransform.ScaleY = peak;
        StarScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, ax);
        StarScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, ay);
    }

    // ── Challenge flash ───────────────────────────────────────────────────────

    /// <summary>
    /// Starts an infinite opacity pulse on the reset-button emoji to signal a challenge.
    /// </summary>
    private void StartChallengeFlash()
    {
        var anim = new DoubleAnimation(1.0, 0.15,
            new Duration(TimeSpan.FromMilliseconds(350)))
        {
            AutoReverse    = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        ResetBtn.BeginAnimation(OpacityProperty, anim);
    }

    /// <summary>Stops the challenge pulse and restores full opacity on the reset button.</summary>
    private void StopChallengeFlash()
    {
        ResetBtn.BeginAnimation(OpacityProperty, null);
        ResetBtn.Opacity = 1.0;
    }

    // ── Full-screen (always on) ───────────────────────────────────────────────

    /// <summary>Enters borderless maximised mode. The application always runs fullscreen.</summary>
    private void GoFullScreen()
    {
        SizeToContent = SizeToContent.Manual;
        WindowStyle   = WindowStyle.None;
        WindowState   = WindowState.Maximized;
        ResizeMode    = ResizeMode.NoResize;
    }

    // ── Tutorial positioning ──────────────────────────────────────────────────

    /// <summary>
    /// Computes and applies curtain positions and card placement for the
    /// current tutorial step. Called once on load, then on every StepChanged event.
    /// </summary>
    private void UpdateTutorial()
    {
        if (!_vm.Tutorial.IsVisible)
            return;

        double w = RootGrid.ActualWidth;
        double h = RootGrid.ActualHeight;

        Rect spot = GetSpotRect(_vm.Tutorial.CurrentTarget, w, h);

        SetCurtains(w, h, spot);
        PositionCard(w, h, spot);
    }

    /// <summary>
    /// Returns the bounding rectangle (relative to <c>RootGrid</c>) of the
    /// UI element identified by <paramref name="target"/>, with a small padding.
    /// Returns <see cref="Rect.Empty"/> when the target is <c>None</c>.
    /// </summary>
    private Rect GetSpotRect(TutorialTarget target, double totalWidth, double totalHeight)
    {
        const double pad = 8;

        FrameworkElement? el = target switch
        {
            TutorialTarget.Header          => HeaderCard,
            TutorialTarget.ResetButton     => ResetBtn,
            TutorialTarget.Board           => BoardCard,
            TutorialTarget.DifficultyBar   => DiffBar,
            TutorialTarget.ExtremeButton   => ExtremeBtn,
            TutorialTarget.LeaderboardButton => LeaderboardBtn,
            _                              => null
        };

        if (el is null) return Rect.Empty;

        try
        {
            var transform = el.TransformToAncestor(RootGrid);
            Rect raw = transform.TransformBounds(new Rect(el.RenderSize));

            // Expand by padding, clamp to grid bounds
            return new Rect(
                Math.Max(0, raw.X - pad),
                Math.Max(0, raw.Y - pad),
                Math.Min(totalWidth,  raw.Right  + pad) - Math.Max(0, raw.X - pad),
                Math.Min(totalHeight, raw.Bottom + pad) - Math.Max(0, raw.Y - pad));
        }
        catch
        {
            return Rect.Empty;
        }
    }

    /// <summary>
    /// Sizes and positions the four curtain <see cref="Rectangle"/>s to cover
    /// everything outside <paramref name="spot"/>. When spot is empty the entire
    /// window is dimmed.
    /// </summary>
    private void SetCurtains(double w, double h, Rect spot)
    {
        if (spot == Rect.Empty)
        {
            // Full-screen dim: all area in CurtainTop, others zero
            SetRect(CurtainTop,        0, 0, w, h);
            SetRect(CurtainLeft,       0, 0, 0, 0);
            SetRect(CurtainRight,      0, 0, 0, 0);
            SetRect(CurtainBottom,     0, 0, 0, 0);
            SetRect(SpotlightBlocker,  0, 0, 0, 0);
            return;
        }

        double x1 = spot.Left;
        double y1 = spot.Top;
        double x2 = spot.Right;
        double y2 = spot.Bottom;

        // Top stripe (full width)
        SetRect(CurtainTop,    0,  0,  w,      y1);
        // Left stripe (between top and bottom stripes)
        SetRect(CurtainLeft,   0,  y1, x1,     y2 - y1);
        // Right stripe (between top and bottom stripes)
        SetRect(CurtainRight,  x2, y1, w - x2, y2 - y1);
        // Bottom stripe (full width)
        SetRect(CurtainBottom, 0,  y2, w,       h - y2);
        // Transparent blocker over spotlight — prevents game interaction during tutorial
        SetRect(SpotlightBlocker, x1, y1, x2 - x1, y2 - y1);
    }

    /// <summary>
    /// Positions the tutorial description card.
    /// Preference order: below spotlight → above spotlight → vertically centred.
    /// Horizontally centred unless that would clip the card off-screen.
    /// </summary>
    private void PositionCard(double w, double h, Rect spot)
    {
        // Force a measure so DesiredSize is accurate
        TutorialCard.Measure(new Size(TutorialCard.Width, double.PositiveInfinity));
        double cw = TutorialCard.DesiredSize.Width;
        double ch = TutorialCard.DesiredSize.Height;

        const double gap = 14;

        // Horizontal: centred, clamped to grid
        double left = Math.Max(0, Math.Min((w - cw) / 2, w - cw));

        double top;
        if (spot == Rect.Empty)
        {
            // Welcome / finale slides: card centred over the dim
            top = (h - ch) / 2;
        }
        else if (spot.Bottom + gap + ch <= h)
        {
            // Fits below the spotlight
            top = spot.Bottom + gap;
        }
        else if (spot.Top - gap - ch >= 0)
        {
            // Fits above the spotlight
            top = spot.Top - gap - ch;
        }
        else
        {
            // Fallback: vertical centre
            top = (h - ch) / 2;
        }

        Canvas.SetLeft(TutorialCard, left);
        Canvas.SetTop(TutorialCard,  Math.Max(0, Math.Min(top, h - ch)));
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static void SetRect(Rectangle r, double left, double top, double width, double height)
    {
        Canvas.SetLeft(r, left);
        Canvas.SetTop(r, top);
        r.Width  = Math.Max(0, width);
        r.Height = Math.Max(0, height);
    }

    // ── Splash intro animation ────────────────────────────────────────────────

    /// <summary>
    /// Starts the intro sequence: logo fades in, holds briefly, then both halves
    /// fly off-screen (top up, bottom down) to reveal the game + tutorial.
    /// </summary>
    private void StartSplashAnimation()
    {
        var fadeIn = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(700)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        fadeIn.Completed += (_, _) =>
        {
            // Hold the fully-visible logo for 800 ms, then split
            var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            hold.Tick += (_, _) => { hold.Stop(); StartSplashSplit(); };
            hold.Start();
        };

        SplashLogoPanel.BeginAnimation(OpacityProperty, fadeIn);
    }

    /// <summary>
    /// Flies the top half up and the bottom half down off-screen while simultaneously
    /// sliding the app name in from below. Finishes with a full overlay fade-out.
    /// </summary>
    private void StartSplashSplit()
    {
        double flyDist  = ActualHeight / 2 + 300;
        var    splitDur = new Duration(TimeSpan.FromMilliseconds(1500));
        var    splitEase = new CubicEase { EasingMode = EasingMode.EaseIn };

        // Logo halves fly apart
        var topAnim = new DoubleAnimation(0, -flyDist, splitDur) { EasingFunction = splitEase };
        var botAnim = new DoubleAnimation(0,  flyDist, splitDur) { EasingFunction = splitEase };

        // App name slides up (191 → 166) and fades in over the first 700 ms of the split
        var textSlideEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var textSlideDur  = new Duration(TimeSpan.FromMilliseconds(1500));
        var textSlide = new DoubleAnimation(291, 0, textSlideDur) { EasingFunction = textSlideEase };
        var textFade  = new DoubleAnimation(0, 1, textSlideDur)     { EasingFunction = textSlideEase };

        // After split: hold briefly, then fade the entire overlay out smoothly
        topAnim.Completed += (_, _) =>
        {
            var hold = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            hold.Tick += (_, _) =>
            {
                hold.Stop();
                var fadeOut = new DoubleAnimation(1, 0,
                    new Duration(TimeSpan.FromMilliseconds(450)))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                };
                fadeOut.Completed += (_, _) => SplashOverlay.Visibility = Visibility.Collapsed;
                SplashOverlay.BeginAnimation(OpacityProperty, fadeOut);
            };
            hold.Start();
        };

        SplashTopTranslate.BeginAnimation(TranslateTransform.YProperty, topAnim);
        SplashBottomTranslate.BeginAnimation(TranslateTransform.YProperty, botAnim);
        SplashTextTranslate.BeginAnimation(TranslateTransform.YProperty, textSlide);
        SplashAppName.BeginAnimation(OpacityProperty, textFade);
    }

    // ── Gift-open animation ───────────────────────────────────────────────────

    /// <summary>
    /// Bounces the 🎊 emoji in from scale 0 → 1.25 → 1.0 when the gift is opened.
    /// </summary>
    private void AnimateGiftOpen()
    {
        GiftOpenScale.ScaleX = 0.1;
        GiftOpenScale.ScaleY = 0.1;

        var expand = new DoubleAnimation(0.1, 1.25, new Duration(TimeSpan.FromMilliseconds(220)))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        expand.Completed += (_, _) =>
        {
            var settle = new DoubleAnimation(1.25, 1.0, new Duration(TimeSpan.FromMilliseconds(100)));
            GiftOpenScale.BeginAnimation(ScaleTransform.ScaleXProperty, settle);
            GiftOpenScale.BeginAnimation(ScaleTransform.ScaleYProperty,
                new DoubleAnimation(1.25, 1.0, new Duration(TimeSpan.FromMilliseconds(100))));
        };
        GiftOpenScale.BeginAnimation(ScaleTransform.ScaleXProperty, expand);
        GiftOpenScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.1, 1.25, new Duration(TimeSpan.FromMilliseconds(220)))
            { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } });
    }

    // ── Hyperlink navigation ──────────────────────────────────────────────────

    /// <summary>Opens the prize URL in the system default browser.</summary>
    private void OnPrizeLinkNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
