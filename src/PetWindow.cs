using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AgyPet
{
    // Floating, always-on-top transparent companion overlay:
    //  - Perches cleanly on top-right of Antigravity's chatbox (adapts to attachments, growth & all 3 sizes)
    //  - Click/poke him in the app for a "hurt/ouch" fidget reaction that preserves his current status!
    //  - Leaves the chatbox on Alt+Tab / minimize; hover outside app shows accurate multi-session progress
    //    (scrollable automatically when > 2 sessions are running!)
    class PetWindow : Window
    {
        readonly PetSettings settings;
        readonly ActivityMonitor monitor;
        readonly Updater updater;
        readonly WindowTracker tracker = new WindowTracker();

        readonly Canvas stage = new Canvas { ClipToBounds = false };
        readonly Border petHitArea;

        // Independent non-activating transparent Window for the hover status card (immune to WPF Popup MenuDropAlignment)
        readonly Window hoverWindow;
        readonly Border hoverCard;
        readonly ScaleTransform cardScale = new ScaleTransform(0.96, 0.96);
        readonly TranslateTransform cardSlide = new TranslateTransform(0, 4);
        bool isHoverOpen;

        // Website design system tokens (site/styles.css OKLCH hue-250 dark surfaces + alpha borders)
        static readonly FontFamily UiFont = new FontFamily("Plus Jakarta Sans, Segoe UI Variable, Segoe UI");
        static readonly Brush SiteBgPopover = FrozenArgb(0xF5, 0x24, 0x29, 0x34);
        static readonly Brush SiteBgPillHover = FrozenArgb(0x14, 0xF4, 0xF6, 0xFA);
        static readonly Brush SiteBorderSubtle = FrozenArgb(0x1C, 0xF5, 0xF7, 0xFC);
        static readonly Brush SiteBorderStrong = FrozenArgb(0x30, 0xF5, 0xF7, 0xFC);
        static readonly Brush SiteInkPrimary = FrozenRgb(0xF1, 0xF3, 0xF7);
        static readonly Brush SiteInkSecondary = FrozenRgb(0xAD, 0xB4, 0xC2);
        static readonly Brush SiteInkMuted = FrozenRgb(0x8C, 0x94, 0xA4);
        static readonly Brush SiteAccentBlueHi = FrozenRgb(0x42, 0x7A, 0xF2);
        readonly StackPanel cardHeaderWrap = new StackPanel();
        readonly ScrollViewer sessionScroll;
        readonly StackPanel sessionList = new StackPanel();

        Mascot mascot;
        readonly DispatcherTimer pollTimer;
        readonly DispatcherTimer animTimer;
        readonly DispatcherTimer hoverLeaveTimer;
        readonly DispatcherTimer menuDismissTimer;

        bool mouseDown;
        bool dragging;
        bool wokeFromSleepOnPress;
        POINT dragStartCursor;
        double dragStartLeft, dragStartTop;
        bool wasOnChatScreen;
        bool userMovedWhileInFront;
        bool contextMenuOpen;
        bool externalMenuOpen;
        DateTime menuCooldownUntil = DateTime.MinValue;
        DateTime menuOpenedAtUtc = DateTime.MinValue;
        DateTime lastTypingWakeUtc = DateTime.MinValue;
        int prevMenuMouseMask;
        HwndSource selfHwndSource;
        readonly ContextMenu contextMenu = new ContextMenu();

        bool previewMode;
        double activeRenderPixelSize = PetSettings.DefaultPixelSize;
        MascotMood previewMood = MascotMood.Idle;
        MascotMood lastAppliedMood = MascotMood.Idle;
        string lastFocusedSessionId = "";
        DateTime lastBusySwitchAt = DateTime.MinValue;
        double targetLeft = double.NaN, targetTop = double.NaN;
        DateTime lastAnimTime = DateTime.UtcNow;

        const int MinBusyDwellMs = 280;

        const double StagePadX = 56; // keeps total width above Win32 SM_CXMINTRACK (~136 DIP) down to MinPixelSize 2.4; click-through outside stage
        const double StagePadY = 24;
        const double StagePadBottom = 4;
        const double CardWidth = 268;
        const double DragThresholdPx = 5.0;

        public PetWindow(PetSettings settings, ActivityMonitor monitor, Updater updater)
        {
            this.settings = settings;
            this.monitor = monitor;
            this.updater = updater;

            Title = "AgyPet";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            stage.Background = new SolidColorBrush(Color.FromArgb(0x01, 0, 0, 0));
            stage.Cursor = Cursors.Hand;

            petHitArea = new Border
            {
                Background = Brushes.Transparent,
                Padding = new Thickness(StagePadX, StagePadY, StagePadX, StagePadBottom),
                Child = stage,
            };
            Content = petHitArea;

            sessionScroll = new ScrollViewer
            {
                MaxHeight = 204,
                VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                PanningMode = PanningMode.VerticalOnly,
                Content = sessionList,
            };

            var cardRoot = new StackPanel();
            cardRoot.Children.Add(cardHeaderWrap);
            cardRoot.Children.Add(sessionScroll);

            var cardTransform = new TransformGroup();
            cardTransform.Children.Add(cardScale);
            cardTransform.Children.Add(cardSlide);

            hoverCard = new Border
            {
                Width = CardWidth,
                Background = SiteBgPopover,
                BorderBrush = SiteBorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12),
                Child = cardRoot,
                Opacity = 0,
                Margin = new Thickness(6),
                RenderTransformOrigin = new Point(0.5, 0.9),
                RenderTransform = cardTransform,
            };

            hoverWindow = new Window
            {
                Title = "AgyPet Card",
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                ResizeMode = ResizeMode.NoResize,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = hoverCard,
            };
            hoverWindow.SourceInitialized += (s, e) =>
            {
                IntPtr hh = new WindowInteropHelper(hoverWindow).Handle;
                int ex = GetWindowLong(hh, GWL_EXSTYLE);
                SetWindowLong(hh, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                var src = HwndSource.FromHwnd(hh);
                if (src != null) src.AddHook(OnWndProc);
            };
            hoverWindow.SizeChanged += (s, e) => { if (isHoverOpen) UpdateHoverPopupPosition(); };

            RebuildMascot();
            AutomationProperties.SetName(stage, "AgyPet companion");
            AutomationProperties.SetName(hoverCard, "Active Antigravity sessions");
            AutomationProperties.SetName(contextMenu, "AgyPet options menu");

            menuDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            menuDismissTimer.Tick += CheckMenuOutsideClick;

            contextMenu.Opened += (s, e) =>
            {
                contextMenuOpen = true;
                CloseHoverWindow();
                PopulateContextMenu();
                menuOpenedAtUtc = DateTime.UtcNow;
                prevMenuMouseMask = CurrentMouseMask();
                menuDismissTimer.Start();
                contextMenu.Opacity = 0;
                contextMenu.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = new QuarticEase { EasingMode = EasingMode.EaseOut }
                });
            };
            contextMenu.Closed += (s, e) =>
            {
                contextMenuOpen = false;
                menuDismissTimer.Stop();
                menuCooldownUntil = DateTime.UtcNow.AddMilliseconds(220);
            };
            ApplyWebsiteMenuTheme(contextMenu);
            PopulateContextMenu();
            stage.ContextMenu = contextMenu;

            stage.MouseLeftButtonDown += OnPetMouseDown;
            stage.MouseMove += OnPetMouseMove;
            stage.MouseLeftButtonUp += OnPetMouseUp;
            stage.MouseEnter += (s, e) => ShowCard();
            stage.MouseLeave += (s, e) => ScheduleHideCard();
            hoverCard.MouseEnter += (s, e) => ShowCard();
            hoverCard.MouseLeave += (s, e) => ScheduleHideCard();
            hoverCard.PreviewMouseWheel += OnCardMouseWheel;

            hoverLeaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
            hoverLeaveTimer.Tick += (s, e) => { hoverLeaveTimer.Stop(); HideCard(); };

            tracker.Start();

            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
            pollTimer.Tick += (s, e) => PollTarget();
            pollTimer.Start();

            animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            animTimer.Tick += (s, e) => TickSmoothGlide();
            animTimer.Start();

            monitor.Updated += () => Dispatcher.BeginInvoke(new Action(SyncActivity));
            SourceInitialized += OnSourceInitialized;
            Loaded += (s, e) => { SyncActivity(); SnapToTarget(); };
            Closed += (s, e) =>
            {
                menuDismissTimer.Stop();
                CloseHoverWindow();
                try { hoverWindow.Close(); } catch { }
                if (selfHwndSource != null)
                {
                    selfHwndSource.RemoveHook(OnWndProc);
                    selfHwndSource = null;
                }
            };
        }

        void OnCardMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sessionScroll.ComputedVerticalScrollBarVisibility == Visibility.Visible)
            {
                sessionScroll.ScrollToVerticalOffset(sessionScroll.VerticalOffset - e.Delta * 0.45);
                e.Handled = true;
            }
        }

        void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
            selfHwndSource = HwndSource.FromHwnd(hwnd);
            if (selfHwndSource != null)
                selfHwndSource.AddHook(OnWndProc);
        }

        IntPtr OnWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // Never activate PetWindow on click so keyboard focus & key-up events stay intact in the active app.
            if (msg == 0x0021) // WM_MOUSEACTIVATE
            {
                handled = true;
                return new IntPtr(3); // MA_NOACTIVATE
            }
            return IntPtr.Zero;
        }

        double ActivePixelSize
        {
            get { return PetSettings.ClampPixelSize(activeRenderPixelSize > 0 ? activeRenderPixelSize : settings.PixelSize); }
        }

        public static double ResolveEffectivePixelSize(
            double userPx,
            string corner,
            bool pinAboveTaskbar,
            bool hasCustomFreePos,
            bool outsideApp,
            bool previewMode,
            Rect workArea,
            Rect screenBounds)
        {
            double clamped = PetSettings.ClampPixelSize(userPx);
            if (!outsideApp || hasCustomFreePos)
                return clamped;

            string effectiveCorner = PetSettings.ResolvePinnedCorner(corner, pinAboveTaskbar);
            if (!effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase))
                return clamped;

            double bottomGap = screenBounds.Bottom - workArea.Bottom;
            double topGap = workArea.Top - screenBounds.Top;
            double taskbarHeight = bottomGap >= 16.0 ? bottomGap : (topGap >= 16.0 ? topGap : 0.0);
            if (taskbarHeight < 16.0)
                return clamped;

            const double TaskbarMinBufferDip = 6.0;
            double maxFitPx = Math.Floor(((taskbarHeight - TaskbarMinBufferDip) / 9.0) * 10.0) / 10.0;
            return Math.Max(PetSettings.MinPixelSize, Math.Min(clamped, maxFitPx));
        }

        public static string ResolveEffectiveSize(
            string userSize,
            string corner,
            bool pinAboveTaskbar,
            bool hasCustomFreePos,
            bool outsideApp,
            bool previewMode,
            Rect workArea,
            Rect screenBounds)
        {
            string normalizedSize = (userSize == "small" || userSize == "large") ? userSize : "medium";
            if (!outsideApp || hasCustomFreePos)
                return normalizedSize;

            string effectiveCorner = PetSettings.ResolvePinnedCorner(corner, pinAboveTaskbar);
            if (!effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase))
                return normalizedSize;

            double bottomGap = screenBounds.Bottom - workArea.Bottom;
            double topGap = workArea.Top - screenBounds.Top;
            double taskbarHeight = bottomGap >= 16.0 ? bottomGap : (topGap >= 16.0 ? topGap : 0.0);
            if (taskbarHeight < 16.0)
                return normalizedSize;

            double largeSpriteH = 9.0 * PetSettings.PixelSizeFor("large");
            double mediumSpriteH = 9.0 * PetSettings.PixelSizeFor("medium");
            const double TaskbarMinBufferDip = 6.0;
            if (taskbarHeight >= largeSpriteH + TaskbarMinBufferDip)
                return "large";
            if (taskbarHeight >= mediumSpriteH + TaskbarMinBufferDip)
                return "medium";
            return "small";
        }

        bool IsInTaskbarMode(out double barTop, out double barHeight)
        {
            barTop = 0;
            barHeight = 0;
            if (settings.HasCustomFreePos) return false;
            if (tracker.IsInFront && !settings.PinAboveTaskbar) return false;
            string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);
            if (!effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase)) return false;

            Rect work = SystemParameters.WorkArea;
            Rect screen = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            double bottomGap = screen.Bottom - work.Bottom;
            double topGap = work.Top - screen.Top;
            if (bottomGap >= 16.0)
            {
                barTop = work.Bottom;
                barHeight = bottomGap;
                return true;
            }
            if (topGap >= 16.0)
            {
                barTop = screen.Top;
                barHeight = topGap;
                return true;
            }
            return false;
        }

        double ResolveCurrentEffectivePixelSize()
        {
            Rect work = SystemParameters.WorkArea;
            Rect screen = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            bool outsideApp = !tracker.IsInFront || tracker.IsMinimized;
            return ResolveEffectivePixelSize(
                settings.PixelSize,
                settings.MinimizedCorner,
                settings.PinAboveTaskbar,
                settings.HasCustomFreePos,
                outsideApp,
                previewMode,
                work,
                screen);
        }

        void EnsureEffectiveSize()
        {
            double nextPx = ResolveCurrentEffectivePixelSize();
            if (Math.Abs(nextPx - activeRenderPixelSize) > 0.02)
                RebuildMascot(nextPx);
        }

        public void RebuildMascot(double? targetPx = null)
        {
            double resolved = targetPx.HasValue ? PetSettings.ClampPixelSize(targetPx.Value) : ResolveCurrentEffectivePixelSize();
            activeRenderPixelSize = resolved;
            stage.Children.Clear();
            mascot = new Mascot(ActivePixelSize, settings.Skin);
            ApplyResolvedMascotState();
            stage.Width = mascot.Width;
            stage.Height = mascot.Height;
            stage.Children.Add(mascot);
        }

        public void SetExternalMenuOpen(bool open)
        {
            externalMenuOpen = open;
            if (!open) menuCooldownUntil = DateTime.UtcNow.AddMilliseconds(220);
        }

        bool IsAnyMenuActive
        {
            get { return contextMenuOpen || externalMenuOpen || DateTime.UtcNow < menuCooldownUntil; }
        }

        bool IsPerchedOnTarget
        {
            get
            {
                return !settings.PinAboveTaskbar && tracker.IsInFront && !tracker.IsMinimized && !userMovedWhileInFront &&
                       (tracker.IsCliInFront ? !tracker.WindowBoundsPx.IsEmpty : !tracker.ChatBoxBoundsPx.IsEmpty);
            }
        }

        public void ApplySkin(string skin)
        {
            settings.Skin = Mascot.NormalizeSkin(skin);
            settings.Save();
            if (mascot != null) mascot.SetSkin(settings.Skin);
            SyncActivity();
        }

        public void ApplyPixelSize(double px, bool save = true)
        {
            settings.SetPixelSize(px);
            if (save) settings.Save();
            RebuildMascot(ResolveCurrentEffectivePixelSize());
            SnapToTarget();
        }

        public void ApplySize(string size)
        {
            ApplyPixelSize(PetSettings.PixelSizeFor(size), true);
        }

        public void SetPreviewMood(MascotMood? mood)
        {
            if (mood.HasValue)
            {
                previewMode = true;
                previewMood = mood.Value;
                ApplyResolvedMascotState();
                RenderHoverContent();
            }
            else
            {
                previewMode = false;
                SyncActivity();
            }
            SyncVisibility();
        }

        public void SetHideWhenInactive(bool hide)
        {
            settings.HideWhenInactive = hide;
            settings.Save();
            SyncVisibility();
        }

        public void SetHideWhenMinimized(bool hide)
        {
            settings.HideWhenMinimized = hide;
            settings.Save();
            SyncVisibility();
        }

        public static bool ShouldHideMascot(
            bool hideWhenInactive,
            bool hideWhenMinimized,
            bool isAppOpen,
            bool isAppMinimized,
            bool hasActiveCli,
            bool hasVisibleCli,
            bool isCliMinimized,
            bool previewMode,
            bool menuActive)
        {
            if (previewMode || menuActive) return false;
            bool anyRunning = isAppOpen || hasActiveCli;
            if (hideWhenInactive && !anyRunning) return true;
            bool hasVisibleTarget = (isAppOpen && !isAppMinimized) || hasVisibleCli;
            bool anyMinimized = isAppMinimized || isCliMinimized;
            if (hideWhenMinimized && anyMinimized && !hasVisibleTarget) return true;
            return false;
        }

        bool SyncVisibility()
        {
            bool shouldHide = ShouldHideMascot(
                settings.HideWhenInactive,
                settings.HideWhenMinimized,
                tracker.IsAppOpen,
                tracker.IsAppMinimized,
                tracker.HasActiveCli,
                tracker.HasVisibleCli,
                tracker.IsCliMinimized,
                previewMode,
                contextMenuOpen || externalMenuOpen);

            if (shouldHide)
            {
                hoverLeaveTimer.Stop();
                CloseHoverWindow();
                if (Visibility != Visibility.Hidden)
                    Visibility = Visibility.Hidden;
                return true;
            }

            if (Visibility != Visibility.Visible)
            {
                if (!dragging) EnsureEffectiveSize();
                Point desired = ComputeDesiredPosition();
                Left = targetLeft = desired.X;
                Top = targetTop = desired.Y;
                Visibility = Visibility.Visible;
                string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);
                if ((!tracker.IsInFront || settings.PinAboveTaskbar) && !settings.HasCustomFreePos &&
                    effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase))
                {
                    EnsureTopmost();
                }
            }
            return false;
        }

        public void SetMinimizedCorner(string corner)
        {
            settings.HasCustomFreePos = false;
            settings.MinimizedCorner = PetSettings.NormalizeCorner(corner);
            settings.Save();
            bool isTaskbar = settings.MinimizedCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase);
            tracker.TrackTaskbarItems = isTaskbar;
            if (isTaskbar) tracker.RefreshTaskbarSpansNow();
            if (!tracker.IsInFront || settings.PinAboveTaskbar) SnapToTarget();
        }

        public void SetPinAboveTaskbar(bool pin)
        {
            settings.PinAboveTaskbar = pin;
            if (pin)
            {
                settings.HasCustomFreePos = false;
                if (!PetSettings.IsBottomOrTaskbarCorner(settings.MinimizedCorner))
                    settings.MinimizedCorner = "bottom-right";
            }
            settings.Save();
            userMovedWhileInFront = false;
            SnapToTarget();
        }

        public void SetLockPosition(bool locked)
        {
            settings.LockPosition = locked;
            settings.Save();
        }

        public void SetCliPlacement(string placement)
        {
            settings.CliPlacement = PetSettings.NormalizeCliPlacement(placement);
            settings.Save();
            tracker.TrackCliWindows = settings.CliPlacement != "off";
            if (tracker.IsInFront && tracker.IsCliInFront)
            {
                userMovedWhileInFront = false;
                SnapToTarget();
            }
        }

        public void ResetPosition()
        {
            settings.HasCustomFreePos = false;
            settings.Save();
            userMovedWhileInFront = false;
            SnapToTarget();
        }

        internal static MascotMood ResolveMood(
            bool previewMode,
            MascotMood previewMood,
            bool isInFront,
            bool pinAboveTaskbar,
            bool hasFocusedSurface,
            bool inVoiceMode,
            bool voiceAgentSpeaking,
            bool inDictation,
            bool isUserTyping,
            ActivitySnapshot focused,
            IList<ActivitySnapshot> actives,
            ActivitySnapshot latest)
        {
            if (previewMode) return previewMood;

            // Outside Antigravity, minimized, pinned to taskbar, or on an overview screen without a chatbox:
            // show Multitasking whenever 2+ sessions are running concurrently!
            int activeCount = actives != null ? actives.Count : 0;
            if (!isInFront || pinAboveTaskbar || !hasFocusedSurface)
            {
                if (activeCount >= 2)
                    return MascotMood.Multitasking;
                if (activeCount == 1)
                    return actives[0].Mood;
                if (isInFront && isUserTyping)
                    return MascotMood.Typing;
                return latest != null ? latest.Mood : MascotMood.Idle;
            }

            // Inside an active Antigravity chat or agy CLI terminal: react specifically to that focused session!
            if (inVoiceMode)
                return voiceAgentSpeaking ? MascotMood.Speaking : MascotMood.Listening;
            if (inDictation)
                return MascotMood.Listening;

            if (focused != null && focused.IsBusy)
                return focused.Mood;
            if (isUserTyping)
                return MascotMood.Typing;
            if (focused != null)
                return focused.Mood;
            return latest != null ? latest.Mood : MascotMood.Idle;
        }

        MascotMood ResolveCurrentMood()
        {
            bool hasSurface = tracker.IsCliInFront ? !tracker.WindowBoundsPx.IsEmpty : !tracker.ChatBoxBoundsPx.IsEmpty;
            return ResolveMood(
                previewMode,
                previewMood,
                tracker.IsInFront,
                settings.PinAboveTaskbar,
                hasSurface,
                tracker.InVoiceMode,
                tracker.VoiceAgentSpeaking,
                tracker.InDictation,
                tracker.IsUserTyping,
                monitor.FocusedSession,
                monitor.ActiveSessions,
                monitor.Latest);
        }

        static bool IsBusyWorkMood(MascotMood mood)
        {
            return mood == MascotMood.Thinking || mood == MascotMood.Typing || mood == MascotMood.Working ||
                   mood == MascotMood.Terminal || mood == MascotMood.Browsing || mood == MascotMood.Reading ||
                   mood == MascotMood.Searching || mood == MascotMood.Delegating || mood == MascotMood.Multitasking;
        }

        void ApplyResolvedMascotState()
        {
            if (mascot == null) return;
            var focused = monitor.FocusedSession;
            string focusedId = focused != null ? (focused.SessionId ?? "") : "";
            if (!string.Equals(focusedId, lastFocusedSessionId, StringComparison.OrdinalIgnoreCase))
            {
                lastFocusedSessionId = focusedId;
                lastBusySwitchAt = DateTime.MinValue;
            }

            MascotMood nextMood = ResolveCurrentMood();

            if (!previewMode && IsBusyWorkMood(lastAppliedMood) && IsBusyWorkMood(nextMood) &&
                nextMood != lastAppliedMood && !(tracker.IsInFront && tracker.IsUserTyping) &&
                (DateTime.UtcNow - lastBusySwitchAt).TotalMilliseconds < MinBusyDwellMs)
            {
                nextMood = lastAppliedMood;
            }

            if (nextMood != lastAppliedMood)
            {
                lastAppliedMood = nextMood;
                lastBusySwitchAt = DateTime.UtcNow;
            }
            var actives = monitor.ActiveSessions;
            mascot.ActiveSessionCount = previewMode ? 3 : (actives != null && actives.Count > 0 ? actives.Count : 3);
            mascot.Mood = nextMood;
        }

        void SyncActivity()
        {
            ApplyResolvedMascotState();

            if (mascot != null)
            {
                var activeSnap = (tracker.IsInFront && monitor.FocusedSession != null) ? monitor.FocusedSession : monitor.Latest;
                string label = activeSnap != null && !string.IsNullOrEmpty(activeSnap.ActionLabel)
                    ? activeSnap.ActionLabel
                    : ActivityMonitor.MoodDefaultLabel(mascot.Mood);
                AutomationProperties.SetName(stage, "AgyPet: " + mascot.Mood + " — " + label);
            }

            if (isHoverOpen || !previewMode)
                RenderHoverContent();
        }

        void RenderHoverContent()
        {
            cardHeaderWrap.Children.Clear();
            sessionList.Children.Clear();
            bool darkSkin = settings.Skin == Mascot.DarkSkin;

            if (previewMode)
            {
                sessionScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                var fake = new ActivitySnapshot
                {
                    Mood = previewMood,
                    SessionTitle = "Preview Mode",
                    ActionLabel = ActivityMonitor.MoodDefaultLabel(previewMood),
                    FocusDetail = "Right-click → Live Mode to resume tracking",
                    IsBusy = false,
                };
                sessionList.Children.Add(BuildSessionItem(fake, darkSkin, false));
                return;
            }

            var actives = monitor.ActiveSessions;
            if (actives != null && actives.Count > 1)
            {
                // Multi-session header + scrollable list when > 2 active sessions!
                var hdr = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var countBadge = new TextBlock
                {
                    Text = actives.Count + " active sessions",
                    Foreground = SiteInkPrimary,
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = UiFont,
                };
                hdr.Children.Add(countBadge);

                if (actives.Count > 2)
                {
                    var scrollHint = new TextBlock
                    {
                        Text = "Scroll ↕",
                        Foreground = SiteInkMuted,
                        FontSize = 10.5,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        FontFamily = UiFont,
                    };
                    DockPanel.SetDock(scrollHint, Dock.Right);
                    hdr.Children.Insert(0, scrollHint);
                }

                cardHeaderWrap.Children.Add(hdr);
                sessionScroll.VerticalScrollBarVisibility = actives.Count > 2 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

                for (int i = 0; i < actives.Count; i++)
                {
                    sessionList.Children.Add(BuildSessionItem(actives[i], darkSkin, i > 0));
                }
            }
            else
            {
                sessionScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                var single = (actives != null && actives.Count == 1) ? actives[0] : monitor.Latest;
                sessionList.Children.Add(BuildSessionItem(single, darkSkin, false));
            }
        }

        UIElement BuildSessionItem(ActivitySnapshot snap, bool darkSkin, bool withTopDivider)
        {
            var stack = new StackPanel();
            if (withTopDivider)
            {
                stack.Children.Add(new Border
                {
                    Height = 1,
                    Background = SiteBorderSubtle,
                    Margin = new Thickness(0, 8, 0, 8),
                });
            }

            double innerW = CardWidth - 36;
            var dot = new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = DotBrushForMood(snap.Mood, darkSkin),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0),
            };
            var title = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(snap.SessionTitle) ? snap.Source : snap.SessionTitle,
                Foreground = SiteInkSecondary,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = UiFont,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = innerW - 18,
            };
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            headerRow.Children.Add(dot);
            headerRow.Children.Add(title);
            stack.Children.Add(headerRow);

            stack.Children.Add(new TextBlock
            {
                Text = snap.ActionLabel,
                Foreground = SiteInkPrimary,
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = UiFont,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 4, 0, 0),
            });

            if (!string.IsNullOrWhiteSpace(snap.FocusDetail) &&
                !string.Equals(snap.FocusDetail, snap.ActionLabel, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(snap.FocusDetail, snap.SessionTitle, StringComparison.OrdinalIgnoreCase))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = snap.FocusDetail,
                    Foreground = SiteInkMuted,
                    FontSize = 11,
                    FontFamily = UiFont,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            }

            if (snap.HasProgress)
            {
                double doneW = innerW * (snap.PercentDone / 100.0);
                double activeSliceW = (snap.StepsInProgress > 0 && snap.StepsTotal > 0 && snap.PercentDone < 100)
                    ? Math.Min(innerW - doneW, innerW * (snap.StepsInProgress / (double)snap.StepsTotal))
                    : 0;

                var barRow = new StackPanel { Orientation = Orientation.Horizontal };
                if (doneW > 0)
                {
                    barRow.Children.Add(new Border
                    {
                        Height = 4,
                        Width = Math.Max(3, doneW),
                        CornerRadius = activeSliceW > 0 ? new CornerRadius(2, 0, 0, 2) : new CornerRadius(2),
                        Background = darkSkin
                            ? (Brush)BrushHex(0xF1, 0xF3, 0xF7)
                            : new LinearGradientBrush(Color.FromRgb(0x47, 0xB8, 0x66), Color.FromRgb(0x2E, 0x69, 0xE8), 0),
                    });
                }
                if (activeSliceW > 1)
                {
                    barRow.Children.Add(new Border
                    {
                        Height = 4,
                        Width = activeSliceW,
                        CornerRadius = doneW > 0 ? new CornerRadius(0, 2, 2, 0) : new CornerRadius(2),
                        Background = new SolidColorBrush(darkSkin ? Color.FromArgb(0x66, 0xE4, 0xE4, 0xE7) : Color.FromArgb(0x77, 0x42, 0x7A, 0xF2)),
                    });
                }

                stack.Children.Add(new Border
                {
                    Height = 4,
                    Width = innerW,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(2),
                    Background = SiteBgPillHover,
                    Child = barRow,
                    Margin = new Thickness(0, 8, 0, 4),
                });

                string stepInfo = snap.StepsTotal > 0 ? ("  ·  " + snap.StepsDone + "/" + snap.StepsTotal + " steps") : "";
                stack.Children.Add(new TextBlock
                {
                    Text = snap.PercentDone + "% done  ·  " + snap.PercentLeft + "% left" + stepInfo,
                    Foreground = SiteInkSecondary,
                    FontSize = 10.8,
                    FontFamily = UiFont,
                });
            }
            else if (snap.IsBusy)
            {
                stack.Children.Add(new Border
                {
                    Height = 4,
                    Width = innerW,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(2),
                    Background = SiteBgPillHover,
                    Child = new Border
                    {
                        Height = 4,
                        Width = innerW * 0.28,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        CornerRadius = new CornerRadius(2),
                        Background = new SolidColorBrush(darkSkin ? Color.FromArgb(0x88, 0xF1, 0xF3, 0xF7) : Color.FromArgb(0xAA, 0x2E, 0x69, 0xE8)),
                    },
                    Margin = new Thickness(0, 8, 0, 4),
                });
                stack.Children.Add(new TextBlock
                {
                    Text = "Running · No step checklist yet",
                    Foreground = SiteInkMuted,
                    FontSize = 10.5,
                    FontFamily = UiFont,
                });
            }

            return stack;
        }

        internal static Brush DotBrushForMood(MascotMood mood, bool darkSkin)
        {
            if (darkSkin)
            {
                if (mood == MascotMood.Idle || mood == MascotMood.Sleeping) return BrushHex(0x71, 0x71, 0x7A);
                return BrushHex(0xF4, 0xF4, 0xF5);
            }
            switch (mood)
            {
                case MascotMood.Done: return BrushHex(0x34, 0xA8, 0x53);
                case MascotMood.Error: return BrushHex(0xEF, 0x44, 0x44);
                case MascotMood.Delegating: return BrushHex(0x8B, 0x5C, 0xF6);
                case MascotMood.Alert: return BrushHex(0xF5, 0x9E, 0x0B);
                case MascotMood.Question: return BrushHex(0x38, 0xBD, 0xF8);
                case MascotMood.Idle:
                case MascotMood.Sleeping: return BrushHex(0x64, 0x74, 0x8B);
                default: return BrushHex(0x25, 0x75, 0xFC);
            }
        }

        // ---- Positioning & Smooth 60fps Motion ----

        void PollTarget()
        {
            if (dragging || IsAnyMenuActive) return;
            tracker.TrackCliWindows = settings.CliPlacement != "off";
            tracker.Poll();
            if (!previewMode && tracker.IsInFront && (tracker.IsUserTyping || tracker.InVoiceMode || tracker.InDictation) &&
                (DateTime.UtcNow - lastTypingWakeUtc).TotalSeconds >= 2.0)
            {
                lastTypingWakeUtc = DateTime.UtcNow;
                monitor.WakeUp();
            }
            monitor.PreferredChatCandidates = tracker.ActiveChatCandidates;
            monitor.PreferredChatTitle = tracker.ActiveChatTitle;
            monitor.PreferredSource = tracker.IsCliInFront ? "Antigravity CLI" : "";
            monitor.PreferAppOnly = tracker.IsInFront && !tracker.IsCliInFront;

            if (!previewMode)
                ApplyResolvedMascotState();

            if (SyncVisibility()) return;

            bool nowInFront = tracker.IsInFront;
            bool hasTargetBox = tracker.IsCliInFront ? !tracker.WindowBoundsPx.IsEmpty : !tracker.ChatBoxBoundsPx.IsEmpty;
            bool nowOnChatScreen = !settings.PinAboveTaskbar && nowInFront && !tracker.IsMinimized && hasTargetBox;

            if (nowInFront && isHoverOpen)
                CloseHoverWindow();

            // Whenever the user enters an Antigravity chat (from another app OR back from Antigravity Settings),
            // ALWAYS snap back cleanly above the chatbox!
            if (nowOnChatScreen && !wasOnChatScreen)
                userMovedWhileInFront = false;
            wasOnChatScreen = nowOnChatScreen;

            if (nowOnChatScreen && userMovedWhileInFront) return;

            // Keep AgyPet above Shell_TrayWnd when docked along the Windows taskbar itself.
            string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);
            bool usingTaskbar = (!nowInFront || settings.PinAboveTaskbar) && !settings.HasCustomFreePos &&
                effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase);
            tracker.TrackTaskbarItems = effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase) && !settings.HasCustomFreePos;
            if (usingTaskbar)
                EnsureTopmost();

            if (!dragging) EnsureEffectiveSize();

            Point desired = ComputeDesiredPosition();
            targetLeft = desired.X;
            targetTop = desired.Y;
            UpdateMascotEyeGaze();
        }

        void UpdateMascotEyeGaze()
        {
            if (mascot == null || mascot.Mood != MascotMood.Idle) return;
            POINT cur;
            if (!GetCursorPos(out cur)) return;
            double dpiX, dpiY;
            GetDpiScale(out dpiX, out dpiY);
            double eyeCenterX = (Left + StagePadX + 5.5 * ActivePixelSize) * dpiX;
            double eyeCenterY = (Top + StagePadY + 3.4 * ActivePixelSize) * dpiY;
            double nx, ny;
            Mascot.NormalizeLookOffset(cur.X - eyeCenterX, cur.Y - eyeCenterY, out nx, out ny);
            mascot.SetLookOffset(nx, ny);
        }

        void TickSmoothGlide()
        {
            DateTime now = DateTime.UtcNow;
            double dt = Math.Min(0.05, Math.Max(0.001, (now - lastAnimTime).TotalSeconds));
            lastAnimTime = now;

            if (dragging || IsAnyMenuActive || Visibility != Visibility.Visible || double.IsNaN(targetLeft)) return;

            double dx = targetLeft - Left;
            double dy = targetTop - Top;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
            {
                if (Left != targetLeft || Top != targetTop)
                {
                    Left = targetLeft;
                    Top = targetTop;
                }
                return;
            }

            double factor = 1.0 - Math.Exp(-14.0 * dt);
            Left += dx * factor;
            Top += dy * factor;
        }

        void SnapToTarget()
        {
            tracker.TrackCliWindows = settings.CliPlacement != "off";
            string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);
            tracker.TrackTaskbarItems = effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase) && !settings.HasCustomFreePos;
            if (tracker.TrackTaskbarItems) tracker.RefreshTaskbarSpansNow();
            tracker.Poll();
            bool hasTargetBox = tracker.IsCliInFront ? !tracker.WindowBoundsPx.IsEmpty : !tracker.ChatBoxBoundsPx.IsEmpty;
            wasOnChatScreen = !settings.PinAboveTaskbar && tracker.IsInFront && !tracker.IsMinimized && hasTargetBox;
            if (!dragging) EnsureEffectiveSize();
            Point desired = ComputeDesiredPosition();
            Left = targetLeft = desired.X;
            Top = targetTop = desired.Y;
            SyncVisibility();
        }

        public static Point PlaceAboveChatBox(Rect chatBoxDip, double spriteW, double spriteH, double padX, double padY)
        {
            // Keep both feet clear of Antigravity's rounded top-right composer corner (even at Small size).
            double rightInset = Math.Min(28.0, Math.Max(14.0, chatBoxDip.Width * 0.12));
            double visualRight = chatBoxDip.Right - rightInset;
            double visualBottom = chatBoxDip.Top + 1.0;
            return new Point(
                visualRight - spriteW - padX,
                visualBottom - spriteH - padY);
        }

        public static Point PlaceOnCliWindow(string cliPlacement, Rect winDip, Rect workArea, double spriteW, double spriteH, double padX, double padY)
        {
            string normalized = PetSettings.NormalizeCliPlacement(cliPlacement);
            double petW = spriteW + padX * 2;
            bool isLeft = normalized.EndsWith("-left", StringComparison.Ordinal);
            bool isCenter = normalized.EndsWith("-center", StringComparison.Ordinal);
            bool isTop = normalized.StartsWith("top-", StringComparison.Ordinal);

            double left = isLeft ? (winDip.Left + 22.0 - padX)
                : isCenter ? (winDip.Left + (winDip.Width - petW) * 0.5)
                : (winDip.Right - 22.0 - spriteW - padX);

            double visualBottom = isTop
                ? Math.Max(workArea.Top + spriteH + 6.0, winDip.Top + 2.0)
                : Math.Min(workArea.Bottom, winDip.Bottom - 20.0);

            return new Point(left, visualBottom - spriteH - padY);
        }

        public static Point PlaceInCorner(string corner, Rect workArea, double spriteW, double spriteH, double padX, double padY)
        {
            return PlaceInCorner(corner, workArea, workArea, spriteW, spriteH, padX, padY);
        }

        public static Point PlaceInCorner(string corner, Rect workArea, Rect screenBounds, double spriteW, double spriteH, double padX, double padY)
        {
            string normalized = PetSettings.NormalizeCorner(corner);
            double petW = spriteW + padX * 2;
            bool isTaskbar = normalized.StartsWith("taskbar-", StringComparison.Ordinal);

            double left;
            if (normalized.EndsWith("-left", StringComparison.Ordinal))
                left = workArea.Left + 8.0;
            else if (normalized.EndsWith("-center", StringComparison.Ordinal))
                left = workArea.Left + (workArea.Width - petW) * 0.5;
            else
                left = workArea.Right - petW - 8.0;

            double top;
            if (isTaskbar)
            {
                double bottomGap = screenBounds.Bottom - workArea.Bottom;
                double topGap = workArea.Top - screenBounds.Top;
                if (bottomGap >= 16.0)
                {
                    // Center vertically inside the bottom Windows taskbar strip
                    top = workArea.Bottom + (bottomGap - spriteH) * 0.5 - padY;
                }
                else if (topGap >= 16.0)
                {
                    top = screenBounds.Top + (topGap - spriteH) * 0.5 - padY;
                }
                else
                {
                    // Auto-hide taskbar fallback: sit neatly along the bottom taskbar band
                    double barH = Math.Max(spriteH + 8.0, 44.0);
                    top = screenBounds.Bottom - barH + (barH - spriteH) * 0.5 - padY;
                }
            }
            else if (normalized.StartsWith("bottom-", StringComparison.Ordinal))
            {
                // Above-taskbar corners/middle rest the mascot's feet flush on WorkArea.Bottom
                top = workArea.Bottom - spriteH - padY;
            }
            else
            {
                top = workArea.Top + 8.0;
            }

            return new Point(left, top);
        }

        public static double ResolveTaskbarVisualLeft(
            string corner,
            Rect workArea,
            TaskbarSpan[] occupiedDip,
            double spriteW,
            out TaskbarSpan chosenGap)
        {
            const double EdgePad = 8.0;
            const double ItemMargin = 8.0;
            double minStart = workArea.Left + EdgePad;
            double maxEnd = workArea.Right - EdgePad;
            if (maxEnd <= minStart + spriteW)
            {
                chosenGap = new TaskbarSpan(workArea.Left, workArea.Right);
                return workArea.Left + Math.Max(0, (workArea.Width - spriteW) * 0.5);
            }

            var merged = new List<TaskbarSpan>();
            if (occupiedDip != null && occupiedDip.Length > 0)
            {
                var expanded = new List<TaskbarSpan>(occupiedDip.Length);
                for (int i = 0; i < occupiedDip.Length; i++)
                {
                    double s = Math.Max(workArea.Left, occupiedDip[i].Start - ItemMargin);
                    double e = Math.Min(workArea.Right, occupiedDip[i].End + ItemMargin);
                    if (e > s + 1.0) expanded.Add(new TaskbarSpan(s, e));
                }
                expanded.Sort((a, b) => a.Start.CompareTo(b.Start));
                for (int i = 0; i < expanded.Count; i++)
                {
                    var cur = expanded[i];
                    if (merged.Count == 0 || cur.Start > merged[merged.Count - 1].End)
                        merged.Add(cur);
                    else
                        merged[merged.Count - 1] = new TaskbarSpan(
                            merged[merged.Count - 1].Start,
                            Math.Max(merged[merged.Count - 1].End, cur.End));
                }
            }

            var freeGaps = new List<TaskbarSpan>();
            double cursor = minStart;
            for (int i = 0; i < merged.Count; i++)
            {
                var occ = merged[i];
                if (occ.Start > cursor)
                {
                    double ge = Math.Min(occ.Start, maxEnd);
                    if (ge > cursor) freeGaps.Add(new TaskbarSpan(cursor, ge));
                }
                if (occ.End > cursor) cursor = occ.End;
                if (cursor >= maxEnd) break;
            }
            if (cursor < maxEnd)
                freeGaps.Add(new TaskbarSpan(cursor, maxEnd));

            var qualifying = new List<TaskbarSpan>();
            for (int i = 0; i < freeGaps.Count; i++)
            {
                if (freeGaps[i].End - freeGaps[i].Start >= spriteW)
                    qualifying.Add(freeGaps[i]);
            }

            string normalized = PetSettings.NormalizeCorner(corner);
            if (qualifying.Count == 0)
            {
                if (freeGaps.Count > 0)
                {
                    TaskbarSpan widest = freeGaps[0];
                    for (int i = 1; i < freeGaps.Count; i++)
                        if (freeGaps[i].End - freeGaps[i].Start > widest.End - widest.Start)
                            widest = freeGaps[i];
                    chosenGap = widest;
                    return widest.Start + Math.Max(0, (widest.End - widest.Start - spriteW) * 0.5);
                }
                chosenGap = new TaskbarSpan(minStart, maxEnd);
                if (normalized.EndsWith("-left", StringComparison.Ordinal)) return minStart;
                if (normalized.EndsWith("-right", StringComparison.Ordinal)) return maxEnd - spriteW;
                return workArea.Left + (workArea.Width - spriteW) * 0.5;
            }

            if (normalized.EndsWith("-left", StringComparison.Ordinal))
            {
                chosenGap = qualifying[0];
                return chosenGap.Start;
            }
            if (normalized.EndsWith("-right", StringComparison.Ordinal))
            {
                chosenGap = qualifying[qualifying.Count - 1];
                return chosenGap.End - spriteW;
            }

            double idealLeft = workArea.Left + (workArea.Width - spriteW) * 0.5;
            double bestLeft = qualifying[0].Start;
            TaskbarSpan bestGap = qualifying[0];
            double bestDist = double.MaxValue;
            for (int i = 0; i < qualifying.Count; i++)
            {
                var g = qualifying[i];
                double clamped = Math.Max(g.Start, Math.Min(g.End - spriteW, idealLeft));
                double dist = Math.Abs(clamped - idealLeft);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestLeft = clamped;
                    bestGap = g;
                }
            }
            chosenGap = bestGap;
            return bestLeft;
        }

        public static Point PlaceInTaskbar(
            string corner,
            Rect workArea,
            Rect screenBounds,
            TaskbarSpan[] occupiedDip,
            double spriteW,
            double spriteH,
            double padX,
            double padY,
            out TaskbarSpan chosenGap)
        {
            double visualLeft = ResolveTaskbarVisualLeft(corner, workArea, occupiedDip, spriteW, out chosenGap);
            Point vertical = PlaceInCorner(corner, workArea, screenBounds, spriteW, spriteH, padX, padY);
            return new Point(visualLeft - padX, vertical.Y);
        }

        public static Point ComputePopupScreenPos(
            double winLeft,
            double winTop,
            double padX,
            double padY,
            double spriteW,
            double spriteH,
            double popupW,
            double popupH,
            Rect workArea,
            bool centerHorizontal,
            bool dockTopEdge)
        {
            if (centerHorizontal)
            {
                double desiredX = winLeft + padX + spriteW * 0.5 - popupW * 0.5;
                double clampedX = Math.Max(workArea.Left + 4.0, Math.Min(workArea.Right - popupW - 4.0, desiredX));
                double posY = dockTopEdge
                    ? (winTop + padY + spriteH)
                    : (winTop + padY - popupH);
                if (posY < workArea.Top + 4.0 && !dockTopEdge)
                    posY = winTop + padY + spriteH;
                return new Point(clampedX, posY);
            }

            bool openOnRight = winLeft < workArea.Left + popupW + 24.0;
            double sideX = openOnRight
                ? (winLeft + padX + spriteW + 4.0)
                : Math.Max(workArea.Left + 4.0, winLeft + padX - popupW - 4.0);
            double sideY = Math.Max(workArea.Top + 4.0, Math.Min(workArea.Bottom - popupH - 4.0, winTop + padY * 0.35));
            return new Point(sideX, sideY);
        }

        public static Point ComputePinnedAwayPosition(
            string corner,
            bool pinAboveTaskbar,
            bool hasCustomFreePos,
            double freeX,
            double freeY,
            Rect workArea,
            Rect screenBounds,
            double spriteW,
            double spriteH,
            double padX,
            double padY)
        {
            if (hasCustomFreePos)
                return new Point(freeX, freeY);

            string effectiveCorner = PetSettings.ResolvePinnedCorner(corner, pinAboveTaskbar);
            return PlaceInCorner(effectiveCorner, workArea, screenBounds, spriteW, spriteH, padX, padY);
        }

        Point ComputeDesiredPosition()
        {
            double dpiX, dpiY;
            GetDpiScale(out dpiX, out dpiY);
            Rect work = SystemParameters.WorkArea;
            Rect screen = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            double spriteW = 11.0 * ActivePixelSize;
            double spriteH = 9.0 * ActivePixelSize;
            double petW = spriteW + StagePadX * 2;

            double petLeft, petTop;
            string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);

            // Perch above Antigravity chatbox OR on active agy CLI terminal window when focused (unless pinned above taskbar)
            if (!settings.PinAboveTaskbar && tracker.IsInFront && !tracker.IsMinimized && tracker.IsCliInFront &&
                settings.CliPlacement != "off" && !tracker.WindowBoundsPx.IsEmpty)
            {
                Rect winDip = new Rect(
                    tracker.WindowBoundsPx.Left / dpiX,
                    tracker.WindowBoundsPx.Top / dpiY,
                    tracker.WindowBoundsPx.Width / dpiX,
                    tracker.WindowBoundsPx.Height / dpiY);
                Point p = PlaceOnCliWindow(settings.CliPlacement, winDip, work, spriteW, spriteH, StagePadX, StagePadY);
                petLeft = p.X;
                petTop = p.Y;
            }
            else if (!settings.PinAboveTaskbar && tracker.IsInFront && !tracker.IsMinimized && !tracker.IsCliInFront && !tracker.ChatBoxBoundsPx.IsEmpty)
            {
                Rect boxDip = new Rect(
                    tracker.ChatBoxBoundsPx.Left / dpiX,
                    tracker.ChatBoxBoundsPx.Top / dpiY,
                    tracker.ChatBoxBoundsPx.Width / dpiX,
                    tracker.ChatBoxBoundsPx.Height / dpiY);
                Point p = PlaceAboveChatBox(boxDip, spriteW, spriteH, StagePadX, StagePadY);
                petLeft = p.X;
                petTop = p.Y;
            }
            else
            {
                if (!settings.HasCustomFreePos && effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase))
                {
                    TaskbarSpan gap;
                    Point tbPt = PlaceInTaskbar(
                        effectiveCorner,
                        work,
                        screen,
                        tracker.GetTaskbarOccupiedSpansDip(dpiX),
                        spriteW,
                        spriteH,
                        StagePadX,
                        StagePadY,
                        out gap);
                    petLeft = tbPt.X;
                    petTop = tbPt.Y;
                }
                else
                {
                    Point awayPt = ComputePinnedAwayPosition(
                        settings.MinimizedCorner,
                        settings.PinAboveTaskbar,
                        settings.HasCustomFreePos,
                        settings.FreeX,
                        settings.FreeY,
                        work,
                        screen,
                        spriteW,
                        spriteH,
                        StagePadX,
                        StagePadY);
                    petLeft = awayPt.X;
                    petTop = awayPt.Y;
                }
            }

            bool pinnedAway = !tracker.IsInFront || settings.PinAboveTaskbar;
            bool allowTaskbarBand = pinnedAway && (settings.HasCustomFreePos ||
                effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase));
            double maxBottom = allowTaskbarBand ? Math.Max(work.Bottom, screen.Bottom) : work.Bottom;
            double minTop = allowTaskbarBand ? Math.Min(work.Top, screen.Top) : work.Top;

            double minLeftBound = allowTaskbarBand && !settings.HasCustomFreePos ? (work.Left - StagePadX) : (work.Left - StagePadX * 0.5);
            double maxLeftBound = allowTaskbarBand && !settings.HasCustomFreePos ? (work.Right - spriteW - StagePadX) : (work.Right - petW + StagePadX * 0.5);
            petLeft = Math.Max(minLeftBound, Math.Min(maxLeftBound, petLeft));
            petTop = Math.Max(minTop - StagePadY * 0.4, Math.Min(maxBottom - spriteH - StagePadY, petTop));
            return new Point(petLeft, petTop);
        }

        // ---- Hover Card Window (active outside Antigravity; exact screen coordinates centered above mascot) ----

        void CloseHoverWindow()
        {
            isHoverOpen = false;
            if (hoverWindow != null && hoverWindow.IsVisible)
                hoverWindow.Hide();
        }

        void UpdateHoverPopupPosition()
        {
            Rect work = SystemParameters.WorkArea;
            double spriteW = 11.0 * ActivePixelSize;
            double spriteH = 9.0 * ActivePixelSize;
            double padX = StagePadX;
            double padY = StagePadY;
            if (mascot != null && mascot.IsLoaded)
            {
                try
                {
                    Point mascotCenterInWin = mascot.TransformToAncestor(this).Transform(new Point(mascot.Width * 0.5, 0));
                    padX = mascotCenterInWin.X - spriteW * 0.5;
                    padY = mascotCenterInWin.Y;
                }
                catch { }
            }

            hoverCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double popupW = hoverWindow.ActualWidth > 10
                ? hoverWindow.ActualWidth
                : Math.Max(CardWidth + 12.0, hoverCard.DesiredSize.Width);
            double popupH = hoverWindow.ActualHeight > 10
                ? hoverWindow.ActualHeight
                : Math.Max(70.0, hoverCard.DesiredSize.Height);

            double barTop, barHeight;
            bool inTaskbar = IsInTaskbarMode(out barTop, out barHeight);
            bool nearBottom = (Top + padY + spriteH) > (work.Bottom - 70.0);
            bool dockTopEdge = inTaskbar && barTop < work.Top;

            Point pt = ComputePopupScreenPos(
                Left, Top, padX, padY, spriteW, spriteH, popupW, popupH,
                work, inTaskbar || nearBottom, dockTopEdge);
            hoverWindow.Left = pt.X;
            hoverWindow.Top = pt.Y;
        }

        void ShowCard()
        {
            hoverLeaveTimer.Stop();
            if (!settings.ShowHoverCard || dragging || IsAnyMenuActive || tracker.IsInFront || Visibility != Visibility.Visible) return;
            RenderHoverContent();

            if (!isHoverOpen)
            {
                hoverCard.Opacity = 0;
                cardScale.ScaleX = 0.96;
                cardScale.ScaleY = 0.96;
                cardSlide.Y = 4.0;
                isHoverOpen = true;
                UpdateHoverPopupPosition();
                hoverWindow.Show();
                hoverWindow.Topmost = false;
                hoverWindow.Topmost = true;
            }
            UpdateHoverPopupPosition();

            var quartOut = new QuarticEase { EasingMode = EasingMode.EaseOut };
            hoverCard.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = quartOut });
            cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = quartOut });
            cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = quartOut });
            cardSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = quartOut });
        }

        void ScheduleHideCard()
        {
            hoverLeaveTimer.Stop();
            hoverLeaveTimer.Start();
        }

        void HideCard()
        {
            if (!isHoverOpen) return;
            var easeIn = new QuadraticEase { EasingMode = EasingMode.EaseIn };
            var fade = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(130)) { EasingFunction = easeIn };
            fade.Completed += (s, e) =>
            {
                if (!stage.IsMouseOver && !hoverCard.IsMouseOver)
                    CloseHoverWindow();
            };
            hoverCard.BeginAnimation(OpacityProperty, fade);
            cardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.97, TimeSpan.FromMilliseconds(130)) { EasingFunction = easeIn });
            cardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.97, TimeSpan.FromMilliseconds(130)) { EasingFunction = easeIn });
            cardSlide.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4.0, TimeSpan.FromMilliseconds(130)) { EasingFunction = easeIn });
        }

        // ---- Poke Easter Egg + Free Dragging ----

        void OnPetMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            wokeFromSleepOnPress = false;
            if (!previewMode && mascot != null && mascot.Mood == MascotMood.Sleeping)
            {
                wokeFromSleepOnPress = true;
                monitor.WakeUp();
                SyncActivity();
                mascot.Poke();
            }

            mouseDown = true;
            dragging = false;
            GetCursorPos(out dragStartCursor);
            dragStartLeft = Left;
            dragStartTop = Top;

            stage.CaptureMouse();
            e.Handled = true;
        }

        void OnPetMouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown || settings.LockPosition) return;
            POINT cur;
            GetCursorPos(out cur);
            double dxPx = cur.X - dragStartCursor.X;
            double dyPx = cur.Y - dragStartCursor.Y;

            if (!dragging)
            {
                if (Math.Abs(dxPx) < DragThresholdPx && Math.Abs(dyPx) < DragThresholdPx) return;
                dragging = true;
                if (mascot != null) mascot.CancelHurt();
                CloseHoverWindow();
            }

            double dpiX, dpiY;
            GetDpiScale(out dpiX, out dpiY);
            Left = dragStartLeft + dxPx / dpiX;
            Top = dragStartTop + dyPx / dpiY;
            targetLeft = Left;
            targetTop = Top;
        }

        void OnPetMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!mouseDown) return;
            mouseDown = false;
            stage.ReleaseMouseCapture();

            if (!dragging)
            {
                if (wokeFromSleepOnPress)
                {
                    wokeFromSleepOnPress = false;
                    return;
                }
                if (mascot != null) mascot.RegisterClick();
                return;
            }
            wokeFromSleepOnPress = false;

            dragging = false;
            targetLeft = Left;
            targetTop = Top;

            if (tracker.IsInFront && !settings.PinAboveTaskbar)
            {
                userMovedWhileInFront = true;
            }
            else
            {
                settings.HasCustomFreePos = true;
                settings.FreeX = Left;
                settings.FreeY = Top;
                settings.Save();
            }
        }

        // ---- Right-Click Context Menu ----

        void PopulateContextMenu()
        {
            contextMenu.Items.Clear();

            var skinMenu = new MenuItem { Header = "Skin" };
            skinMenu.Items.Add(CheckItem("Classic (Official Antigravity Colors)", settings.Skin == Mascot.ClassicSkin, () => ApplySkin(Mascot.ClassicSkin)));
            skinMenu.Items.Add(CheckItem("Dark Remake (Monochrome)", settings.Skin == Mascot.DarkSkin, () => ApplySkin(Mascot.DarkSkin)));
            contextMenu.Items.Add(skinMenu);

            var cornerMenu = new MenuItem { Header = "Location When Away / Minimized" };
            bool insertedSep = false;
            foreach (var opt in PetSettings.AwayLocations)
            {
                if (!insertedSep && opt.Id.StartsWith("taskbar-", StringComparison.Ordinal))
                {
                    cornerMenu.Items.Add(new Separator());
                    insertedSep = true;
                }
                string value = opt.Id;
                cornerMenu.Items.Add(CheckItem(opt.Label, !settings.HasCustomFreePos && settings.MinimizedCorner == value, () => SetMinimizedCorner(value)));
            }
            contextMenu.Items.Add(cornerMenu);

            contextMenu.Items.Add(CheckItem("Pin Above Taskbar (Stay in Place)", settings.PinAboveTaskbar, () => SetPinAboveTaskbar(!settings.PinAboveTaskbar)));
            contextMenu.Items.Add(CheckItem("Lock Position (Disable Dragging)", settings.LockPosition, () => SetLockPosition(!settings.LockPosition)));

            var cliMenu = new MenuItem { Header = "CLI Terminal Placement" };
            foreach (var opt in PetSettings.CliLocations)
            {
                string value = opt.Id;
                cliMenu.Items.Add(CheckItem(opt.Label, settings.CliPlacement == value, () => SetCliPlacement(value)));
            }
            contextMenu.Items.Add(cliMenu);

            var autoHideMenu = new MenuItem { Header = "Auto-Hide" };
            autoHideMenu.Items.Add(CheckItem("Hide When Inactive (App & CLI Closed)", settings.HideWhenInactive, () => SetHideWhenInactive(!settings.HideWhenInactive)));
            autoHideMenu.Items.Add(CheckItem("Hide When Minimized", settings.HideWhenMinimized, () => SetHideWhenMinimized(!settings.HideWhenMinimized)));
            contextMenu.Items.Add(autoHideMenu);

            var sizeMenu = new MenuItem { Header = "Size" };
            sizeMenu.Items.Add(BuildSizeSliderItem(sizeMenu));
            sizeMenu.Items.Add(new Separator());
            sizeMenu.Items.Add(ActionItem("Reset Default (100% · 4.2 px)", () => ApplyPixelSize(PetSettings.DefaultPixelSize, true)));
            contextMenu.Items.Add(sizeMenu);

            var previewSub = new MenuItem { Header = "Preview Mood" };
            previewSub.Items.Add(CheckItem("Live Mode (Auto-react to Antigravity)", !previewMode, () => SetPreviewMood(null)));
            previewSub.Items.Add(new Separator());
            foreach (MascotMood m in Enum.GetValues(typeof(MascotMood)))
            {
                var captured = m;
                previewSub.Items.Add(CheckItem(captured.ToString(), previewMode && previewMood == captured, () => SetPreviewMood(captured)));
            }
            contextMenu.Items.Add(previewSub);

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(CheckItem("Show Hover Progress Card", settings.ShowHoverCard, () =>
            {
                settings.ShowHoverCard = !settings.ShowHoverCard;
                settings.Save();
                if (!settings.ShowHoverCard) CloseHoverWindow();
            }));
            contextMenu.Items.Add(CheckItem("Start with Windows", settings.StartWithWindows, () =>
            {
                settings.StartWithWindows = !settings.StartWithWindows;
                settings.Save();
                PetSettings.ApplyStartup(settings.StartWithWindows, Process.GetCurrentProcess().MainModule.FileName);
            }));
            contextMenu.Items.Add(ActionItem(settings.PinAboveTaskbar ? "Reset Pinned Position" : "Snap Back Above Chatbox", ResetPosition));
            contextMenu.Items.Add(CheckItem("Auto-Update from GitHub", settings.AutoUpdate, () =>
            {
                settings.AutoUpdate = !settings.AutoUpdate;
                settings.Save();
            }));
            if (updater != null)
            {
                string updLabel = updater.UpdateReady
                    ? "Install Update (" + updater.ReadyTag + ")"
                    : "Check for Updates (" + updater.Status + ")";
                contextMenu.Items.Add(ActionItem(updLabel, () => updater.CheckNow()));
            }
            contextMenu.Items.Add(ActionItem("Send Feedback (GitHub Issue)...", () => FeedbackWindow.ShowDialogOnce(settings.Skin, settings.Size)));

            var creditsMenu = new MenuItem { Header = "Credits" };
            creditsMenu.Items.Add(ActionItem("Made by Yassin Ragab (yassinr.me)", () => OpenUrl("https://yassinr.me")));
            creditsMenu.Items.Add(ActionItem("GitHub: yvs1n/agypet", () => OpenUrl("https://github.com/yvs1n/agypet")));
            creditsMenu.Items.Add(ActionItem("For Antigravity, built with Antigravity", () => OpenUrl("https://antigravity.google")));
            contextMenu.Items.Add(creditsMenu);

            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(ActionItem("Quit AgyPet", () => Application.Current.Shutdown()));
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        static Brush FrozenRgb(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        static Brush FrozenArgb(byte a, byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            br.Freeze();
            return br;
        }

        static void ApplyWebsiteMenuTheme(ContextMenu menu)
        {
            var cmTpl = new ControlTemplate(typeof(ContextMenu));
            var cmBorder = new FrameworkElementFactory(typeof(Border));
            cmBorder.SetValue(Border.BackgroundProperty, SiteBgPopover);
            cmBorder.SetValue(Border.BorderBrushProperty, SiteBorderStrong);
            cmBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            cmBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            cmBorder.SetValue(Border.PaddingProperty, new Thickness(5));
            cmBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);
            var cmItems = new FrameworkElementFactory(typeof(ItemsPresenter));
            cmItems.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
            cmBorder.AppendChild(cmItems);
            cmTpl.VisualTree = cmBorder;

            var cmStyle = new Style(typeof(ContextMenu));
            cmStyle.Setters.Add(new Setter(Control.TemplateProperty, cmTpl));
            cmStyle.Setters.Add(new Setter(ContextMenu.HasDropShadowProperty, false));
            cmStyle.Setters.Add(new Setter(Control.ForegroundProperty, SiteInkPrimary));
            cmStyle.Setters.Add(new Setter(Control.FontFamilyProperty, UiFont));
            cmStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
            menu.Style = cmStyle;

            var miTpl = new ControlTemplate(typeof(MenuItem));
            var root = new FrameworkElementFactory(typeof(Grid));

            var pill = new FrameworkElementFactory(typeof(Border), "RowBg");
            pill.SetValue(Border.CornerRadiusProperty, new CornerRadius(9));
            pill.SetValue(Border.PaddingProperty, new Thickness(9, 5.5, 10, 5.5));
            pill.SetValue(Border.MarginProperty, new Thickness(0, 0.5, 0, 0.5));
            pill.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            pill.SetValue(Border.BorderBrushProperty, Brushes.Transparent);
            pill.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            pill.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var rowGrid = new FrameworkElementFactory(typeof(Grid));
            var col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(18));
            var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            rowGrid.AppendChild(col0);
            rowGrid.AppendChild(col1);
            rowGrid.AppendChild(col2);

            var check = new FrameworkElementFactory(typeof(TextBlock), "CheckMark");
            check.SetValue(Grid.ColumnProperty, 0);
            check.SetValue(TextBlock.TextProperty, "✓");
            check.SetValue(TextBlock.FontSizeProperty, 11.0);
            check.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            check.SetValue(TextBlock.ForegroundProperty, SiteAccentBlueHi);
            check.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            check.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            check.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

            var header = new FrameworkElementFactory(typeof(ContentPresenter));
            header.SetValue(Grid.ColumnProperty, 1);
            header.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            header.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            var arrow = new FrameworkElementFactory(typeof(TextBlock), "SubArrow");
            arrow.SetValue(Grid.ColumnProperty, 2);
            arrow.SetValue(TextBlock.TextProperty, "›");
            arrow.SetValue(TextBlock.FontSizeProperty, 14.0);
            arrow.SetValue(TextBlock.ForegroundProperty, SiteInkMuted);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetValue(FrameworkElement.MarginProperty, new Thickness(12, -1, 0, 0));
            arrow.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

            rowGrid.AppendChild(check);
            rowGrid.AppendChild(header);
            rowGrid.AppendChild(arrow);
            pill.AppendChild(rowGrid);
            root.AppendChild(pill);

            var subPopup = new FrameworkElementFactory(typeof(Popup), "PART_Popup");
            subPopup.SetValue(Popup.AllowsTransparencyProperty, true);
            subPopup.SetValue(Popup.PlacementProperty, PlacementMode.Right);
            subPopup.SetValue(Popup.HorizontalOffsetProperty, 2.0);
            subPopup.SetValue(Popup.VerticalOffsetProperty, -5.0);
            subPopup.SetValue(Popup.FocusableProperty, false);
            subPopup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
            subPopup.SetBinding(Popup.IsOpenProperty, new System.Windows.Data.Binding("IsSubmenuOpen")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

            var subBorder = new FrameworkElementFactory(typeof(Border));
            subBorder.SetValue(Border.BackgroundProperty, SiteBgPopover);
            subBorder.SetValue(Border.BorderBrushProperty, SiteBorderStrong);
            subBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            subBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            subBorder.SetValue(Border.PaddingProperty, new Thickness(5));
            subBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);

            var subScroll = new FrameworkElementFactory(typeof(ScrollViewer));
            subScroll.SetValue(FrameworkElement.MaxHeightProperty, 420.0);
            subScroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            subScroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            var subItems = new FrameworkElementFactory(typeof(ItemsPresenter));
            subItems.SetValue(KeyboardNavigation.DirectionalNavigationProperty, KeyboardNavigationMode.Cycle);
            subScroll.AppendChild(subItems);
            subBorder.AppendChild(subScroll);
            subPopup.AppendChild(subBorder);
            root.AppendChild(subPopup);

            miTpl.VisualTree = root;

            var trHi = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            trHi.Setters.Add(new Setter(Border.BackgroundProperty, SiteBgPillHover, "RowBg"));
            trHi.Setters.Add(new Setter(Border.BorderBrushProperty, SiteBorderSubtle, "RowBg"));
            miTpl.Triggers.Add(trHi);

            var trChk = new Trigger { Property = MenuItem.IsCheckedProperty, Value = true };
            trChk.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "CheckMark"));
            miTpl.Triggers.Add(trChk);

            var trSub = new Trigger { Property = MenuItem.HasItemsProperty, Value = true };
            trSub.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "SubArrow"));
            miTpl.Triggers.Add(trSub);

            var miStyle = new Style(typeof(MenuItem));
            miStyle.Setters.Add(new Setter(Control.TemplateProperty, miTpl));
            miStyle.Setters.Add(new Setter(Control.ForegroundProperty, SiteInkPrimary));
            miStyle.Setters.Add(new Setter(Control.FontFamilyProperty, UiFont));
            miStyle.Setters.Add(new Setter(Control.FontSizeProperty, 12.5));
            miStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Medium));

            var sepTpl = new ControlTemplate(typeof(Separator));
            var sepLine = new FrameworkElementFactory(typeof(Border));
            sepLine.SetValue(FrameworkElement.HeightProperty, 1.0);
            sepLine.SetValue(Border.BackgroundProperty, SiteBorderSubtle);
            sepLine.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 4, 8, 4));
            sepTpl.VisualTree = sepLine;
            var sepStyle = new Style(typeof(Separator));
            sepStyle.Setters.Add(new Setter(Control.TemplateProperty, sepTpl));

            menu.Resources[typeof(MenuItem)] = miStyle;
            menu.Resources[typeof(Separator)] = sepStyle;
        }

        MenuItem BuildSizeSliderItem(MenuItem parentMenu)
        {
            var valueText = new TextBlock
            {
                Text = PetSettings.FormatSizeLabel(settings.PixelSize),
                Foreground = SiteAccentBlueHi,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = UiFont,
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            var labelText = new TextBlock
            {
                Text = "Size",
                Foreground = SiteInkSecondary,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = UiFont,
            };

            var hdr = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            DockPanel.SetDock(valueText, Dock.Right);
            hdr.Children.Add(valueText);
            hdr.Children.Add(labelText);

            var slider = new Slider
            {
                Minimum = PetSettings.MinPixelSize,
                Maximum = PetSettings.MaxPixelSize,
                Value = settings.PixelSize,
                TickFrequency = 0.1,
                SmallChange = 0.1,
                LargeChange = 0.4,
                IsSnapToTickEnabled = true,
                IsMoveToPointEnabled = true,
                Width = 186,
                FocusVisualStyle = null,
                Template = CreateWebsiteSliderTemplate(),
            };

            slider.ValueChanged += (s, e) =>
            {
                double next = PetSettings.ClampPixelSize(e.NewValue);
                valueText.Text = PetSettings.FormatSizeLabel(next);
                ApplyPixelSize(next, save: false);
            };
            slider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler((s, e) => settings.Save()));
            slider.PreviewMouseLeftButtonUp += (s, e) => settings.Save();
            slider.PreviewKeyUp += (s, e) => settings.Save();

            var panel = new StackPanel
            {
                Margin = new Thickness(8, 6, 8, 6),
                Width = 186,
            };
            panel.Children.Add(hdr);
            panel.Children.Add(slider);

            var tpl = new ControlTemplate(typeof(MenuItem));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            tpl.VisualTree = presenter;

            var hostItem = new MenuItem
            {
                Header = panel,
                StaysOpenOnClick = true,
                Template = tpl,
                Focusable = false,
            };
            return hostItem;
        }

        internal static ControlTemplate CreateWebsiteSliderTemplate()
        {
            const string xaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='{x:Type Slider}'>
  <Grid Height='20' SnapsToDevicePixels='True'>
    <Border Height='4' CornerRadius='2' VerticalAlignment='Center' Background='#14F4F6FA'/>
    <Track x:Name='PART_Track' VerticalAlignment='Center'>
      <Track.DecreaseRepeatButton>
        <RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False'>
          <RepeatButton.Template>
            <ControlTemplate TargetType='{x:Type RepeatButton}'>
              <Border Height='4' CornerRadius='2' Background='#427AF2'/>
            </ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.DecreaseRepeatButton>
      <Track.IncreaseRepeatButton>
        <RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False'>
          <RepeatButton.Template>
            <ControlTemplate TargetType='{x:Type RepeatButton}'>
              <Border Height='14' Background='Transparent'/>
            </ControlTemplate>
          </RepeatButton.Template>
        </RepeatButton>
      </Track.IncreaseRepeatButton>
      <Track.Thumb>
        <Thumb Focusable='False' Cursor='Hand'>
          <Thumb.Template>
            <ControlTemplate TargetType='{x:Type Thumb}'>
              <Ellipse Width='14' Height='14' Fill='#F1F3F7' Stroke='#427AF2' StrokeThickness='2'/>
            </ControlTemplate>
          </Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </Grid>
</ControlTemplate>";
            return (ControlTemplate)System.Windows.Markup.XamlReader.Parse(xaml);
        }

        static MenuItem CheckItem(string text, bool isChecked, Action onClick)
        {
            var item = new MenuItem { Header = text, IsChecked = isChecked };
            item.Click += (s, e) => onClick();
            return item;
        }

        static MenuItem ActionItem(string text, Action onClick)
        {
            var item = new MenuItem { Header = text };
            item.Click += (s, e) => onClick();
            return item;
        }

        void GetDpiScale(out double dpiX, out double dpiY)
        {
            var src = PresentationSource.FromVisual(this);
            if (src != null && src.CompositionTarget != null)
            {
                dpiX = src.CompositionTarget.TransformToDevice.M11;
                dpiY = src.CompositionTarget.TransformToDevice.M22;
                return;
            }
            dpiX = dpiY = 1.0;
        }

        static Brush BrushHex(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        void EnsureTopmost()
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch { }
        }

        static int CurrentMouseMask()
        {
            int mask = 0;
            if ((GetAsyncKeyState(0x01) & 0x8000) != 0) mask |= 1; // VK_LBUTTON
            if ((GetAsyncKeyState(0x02) & 0x8000) != 0) mask |= 2; // VK_RBUTTON
            if ((GetAsyncKeyState(0x04) & 0x8000) != 0) mask |= 4; // VK_MBUTTON
            return mask;
        }

        void CheckMenuOutsideClick(object sender, EventArgs e)
        {
            if (!contextMenu.IsOpen)
            {
                menuDismissTimer.Stop();
                return;
            }

            if ((DateTime.UtcNow - menuOpenedAtUtc).TotalMilliseconds < 120)
            {
                prevMenuMouseMask = CurrentMouseMask();
                return;
            }

            if ((GetAsyncKeyState(0x1B) & 0x8000) != 0) // VK_ESCAPE
            {
                contextMenu.IsOpen = false;
                return;
            }

            int mask = CurrentMouseMask();
            int newlyPressed = mask & ~prevMenuMouseMask;
            prevMenuMouseMask = mask;
            if (newlyPressed == 0) return;

            POINT pt;
            if (!GetCursorPos(out pt)) return;
            if (!IsCursorInsideAnyMenuPopup(pt.X, pt.Y))
                contextMenu.IsOpen = false;
        }

        bool IsCursorInsideAnyMenuPopup(int x, int y)
        {
            IntPtr selfHwnd = new WindowInteropHelper(this).Handle;
            IntPtr hoverHwnd = IntPtr.Zero;
            var hoverSrc = PresentationSource.FromVisual(hoverCard) as HwndSource;
            if (hoverSrc != null) hoverHwnd = hoverSrc.Handle;

            bool inside = false;
            EnumThreadWindows(GetCurrentThreadId(), (h, l) =>
            {
                if (h == selfHwnd || h == hoverHwnd || !IsWindowVisible(h)) return true;
                if (HwndSource.FromHwnd(h) == null) return true;
                RECT r;
                if (GetWindowRect(h, out r) && (r.Right - r.Left) > 4 && (r.Bottom - r.Top) > 4 &&
                    x >= r.Left && x <= r.Right && y >= r.Top && y <= r.Bottom)
                {
                    inside = true;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return inside;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;
        delegate bool EnumThreadWndProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint dwThreadId, EnumThreadWndProc lpfn, IntPtr lParam);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    }
}
