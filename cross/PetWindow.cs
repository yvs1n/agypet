using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AgyPet
{
    // Floating always-on-top transparent companion overlay for macOS & Linux built with Avalonia.
    class PetWindow : Window
    {
        readonly PetSettings settings;
        readonly ActivityMonitor monitor;
        readonly Updater updater;
        readonly PlatformWindowTracker tracker = new PlatformWindowTracker();

        readonly Canvas stage = new Canvas { ClipToBounds = false };
        readonly Border petHitArea;

        // Dead-center Hover Card Window matching the website's OKLCH hue-250 popover & motion system
        readonly Window hoverWindow;
        readonly Border hoverCard;
        readonly StackPanel cardHeaderWrap = new StackPanel();
        readonly ScrollViewer sessionScroll;
        readonly StackPanel sessionList = new StackPanel();
        readonly ScaleTransform cardScale = new ScaleTransform(0.96, 0.96);
        readonly TranslateTransform cardSlide = new TranslateTransform(0, 4);
        bool isHoverOpen;

        // Custom website-themed dark rounded right-click menu overlay (with hoverable submenus)
        Window menuWindow;
        Window submenuWindow;
        Control activeSubmenuRow;
        bool isMenuOpen;

        MascotView mascot;
        readonly DispatcherTimer pollTimer;
        readonly DispatcherTimer animTimer;
        readonly DispatcherTimer hoverLeaveTimer;
        readonly DispatcherTimer menuDeactivateTimer;

        bool mouseDown;
        bool dragging;
        bool wokeFromSleepOnPress;
        PixelPoint dragStartCursorPx;
        double dragStartLeftDip, dragStartTopDip;
        bool wasOnChatScreen;
        bool userMovedWhileInFront;

        bool previewMode;
        double activeRenderPixelSize = PetSettings.DefaultPixelSize;
        MascotMood previewMood = MascotMood.Idle;
        MascotMood lastAppliedMood = MascotMood.Idle;
        string lastFocusedSessionId = "";
        DateTime lastBusySwitchAt = DateTime.MinValue;
        double targetLeftDip = double.NaN, targetTopDip = double.NaN;
        DateTime lastAnimTime = DateTime.UtcNow;

        const int MinBusyDwellMs = 280;
        const double StagePadX = 36;
        const double StagePadY = 24;
        const double StagePadBottom = 4;
        const double CardWidth = 268;
        const double DragThresholdPx = 5.0;

        public bool IsPreviewMode { get { return previewMode; } }
        public MascotMood PreviewMood { get { return previewMood; } }
        public bool IsAnyAntigravityOpen { get { return tracker.IsAnyAntigravityOpen; } }
        double ActivePixelSize { get { return PetSettings.ClampPixelSize(activeRenderPixelSize > 0 ? activeRenderPixelSize : settings.PixelSize); } }

        public static bool ShouldAllowRightClickContextMenu(bool isMacOs)
        {
            return !isMacOs;
        }

        public static string ResolveAwayCornerForPlatform(bool isMacOs, bool isAnyAntigravityOpen, string effectiveCorner)
        {
            if (isMacOs && !isAnyAntigravityOpen)
                return "bottom-center";
            return effectiveCorner;
        }

        public PetWindow(PetSettings settings, ActivityMonitor monitor, Updater updater)
        {
            this.settings = settings;
            this.monitor = monitor;
            this.updater = updater;

            Title = "AgyPet";
            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            CanResize = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            Icon = PetTheme.CreateMascotWindowIcon();

            stage.Background = PetTheme.FrozenArgb(0x01, 0, 0, 0);
            stage.Cursor = new Cursor(StandardCursorType.Hand);

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
                Background = PetTheme.BgPopover,
                BorderBrush = PetTheme.BorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12),
                Child = cardRoot,
                Opacity = 0,
                Margin = new Thickness(6),
                RenderTransformOrigin = new RelativePoint(0.5, 0.9, RelativeUnit.Relative),
                RenderTransform = cardTransform,
                Transitions = new Transitions
                {
                    new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(170), Easing = new QuarticEaseOut() },
                },
            };

            hoverWindow = new Window
            {
                Title = "AgyPet Card",
                SystemDecorations = SystemDecorations.None,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                CanResize = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = hoverCard,
            };
            hoverWindow.Opened += (s, e) => ConfigureNativeOverlayWindow(hoverWindow);
            hoverWindow.SizeChanged += (s, e) => { if (isHoverOpen) UpdateHoverPosition(); };
            hoverCard.PointerEntered += (s, e) => ShowCard();
            hoverCard.PointerExited += (s, e) => ScheduleHideCard();

            RebuildMascot();

            stage.PointerPressed += OnPetPointerPressed;
            stage.PointerMoved += OnPetPointerMoved;
            stage.PointerReleased += OnPetPointerReleased;
            stage.PointerEntered += (s, e) => ShowCard();
            stage.PointerExited += (s, e) => ScheduleHideCard();

            hoverLeaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
            hoverLeaveTimer.Tick += (s, e) => { hoverLeaveTimer.Stop(); HideCard(); };

            menuDeactivateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
            menuDeactivateTimer.Tick += (s, e) =>
            {
                menuDeactivateTimer.Stop();
                if ((menuWindow == null || !menuWindow.IsActive) && (submenuWindow == null || !submenuWindow.IsActive))
                    CloseContextMenu();
            };

            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            pollTimer.Tick += (s, e) => PollTarget();
            pollTimer.Start();

            animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            animTimer.Tick += (s, e) => TickSmoothGlide();
            animTimer.Start();

            monitor.Updated += () => Dispatcher.UIThread.Post(SyncActivity);
            Opened += (s, e) =>
            {
                ConfigureNativeOverlayWindow(this);
                SyncActivity();
                SnapToTarget();
            };
            Deactivated += (s, e) =>
            {
                if (IsVisible) ConfigureNativeOverlayWindow(this);
            };
            Closed += (s, e) =>
            {
                CloseContextMenu();
                CloseHoverWindow();
                try { hoverWindow.Close(); } catch { }
            };
        }

        static void ConfigureNativeOverlayWindow(Window win, bool bringFront = false)
        {
            if (!OperatingSystem.IsMacOS()) return;
            try
            {
                var ph = win.TryGetPlatformHandle();
                if (ph == null || ph.Handle == IntPtr.Zero) return;
                MacOverlay.ConfigureWindow(ph.Handle, bringFront);
            }
            catch { }
        }

        Screen ActiveScreen
        {
            get
            {
                if (Screens != null && Screens.Primary != null) return Screens.Primary;
                if (Screens != null && Screens.All != null && Screens.All.Count > 0) return Screens.All[0];
                return null;
            }
        }

        double CurrentScaling
        {
            get
            {
                var sc = ActiveScreen;
                return sc != null && sc.Scaling > 0.1 ? sc.Scaling : 1.0;
            }
        }

        void GetScreenRectsDip(out ScreenRectDip work, out ScreenRectDip screen)
        {
            var sc = ActiveScreen;
            double scale = CurrentScaling;
            if (sc != null)
            {
                var wa = sc.WorkingArea;
                var b = sc.Bounds;
                work = new ScreenRectDip(wa.X / scale, wa.Y / scale, wa.Width / scale, wa.Height / scale);
                screen = new ScreenRectDip(b.X / scale, b.Y / scale, b.Width / scale, b.Height / scale);
            }
            else
            {
                work = new ScreenRectDip(0, 0, 1440, 860);
                screen = new ScreenRectDip(0, 0, 1440, 900);
            }
        }

        double CurrentLeftDip
        {
            get { return Position.X / CurrentScaling; }
        }

        double CurrentTopDip
        {
            get { return Position.Y / CurrentScaling; }
        }

        void SetPositionDip(double leftDip, double topDip)
        {
            double s = CurrentScaling;
            Position = new PixelPoint((int)Math.Round(leftDip * s), (int)Math.Round(topDip * s));
        }

        public static string ResolveEffectiveSize(
            string userSize,
            string corner,
            bool pinAboveTaskbar,
            bool hasCustomFreePos,
            bool outsideApp,
            bool previewMode,
            ScreenRectDip workArea,
            ScreenRectDip screenBounds)
        {
            string normalizedSize = (userSize == "small" || userSize == "large") ? userSize : "medium";
            if (!outsideApp || hasCustomFreePos)
                return normalizedSize;

            string effectiveCorner = PetSettings.ResolvePinnedCorner(corner, pinAboveTaskbar);
            if (!effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase))
                return normalizedSize;

            double bottomGap = screenBounds.Bottom - workArea.Bottom;
            double topGap = workArea.Top - screenBounds.Top;
            double barHeight = bottomGap >= 16.0 ? bottomGap : (topGap >= 16.0 ? topGap : 0.0);
            if (barHeight < 16.0)
                return normalizedSize;

            const double MinBufferDip = 6.0;
            if (barHeight >= 9.0 * PetSettings.PixelSizeFor("large") + MinBufferDip) return "large";
            if (barHeight >= 9.0 * PetSettings.PixelSizeFor("medium") + MinBufferDip) return "medium";
            return "small";
        }

        public static double ResolveEffectivePixelSize(
            double userPx,
            string corner,
            bool pinAboveTaskbar,
            bool hasCustomFreePos,
            bool outsideApp,
            bool previewMode,
            ScreenRectDip workArea,
            ScreenRectDip screenBounds)
        {
            double clamped = PetSettings.ClampPixelSize(userPx);
            if (!outsideApp || hasCustomFreePos)
                return clamped;

            string effectiveCorner = PetSettings.ResolvePinnedCorner(corner, pinAboveTaskbar);
            if (!effectiveCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase))
                return clamped;

            double bottomGap = screenBounds.Bottom - workArea.Bottom;
            double topGap = workArea.Top - screenBounds.Top;
            double barHeight = bottomGap >= 16.0 ? bottomGap : (topGap >= 16.0 ? topGap : 0.0);
            if (barHeight < 16.0)
                return clamped;

            const double MinBufferDip = 6.0;
            double maxFitPx = Math.Floor(((barHeight - MinBufferDip) / 9.0) * 10.0) / 10.0;
            return Math.Max(PetSettings.MinPixelSize, Math.Min(clamped, maxFitPx));
        }

        double ResolveCurrentEffectivePixelSize()
        {
            ScreenRectDip work, screen;
            GetScreenRectsDip(out work, out screen);
            bool outsideApp = !tracker.IsInFront || tracker.IsMinimized;
            string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);
            string awayCorner = ResolveAwayCornerForPlatform(OperatingSystem.IsMacOS(), tracker.IsAnyAntigravityOpen, effectiveCorner);
            return ResolveEffectivePixelSize(
                settings.PixelSize, awayCorner, settings.PinAboveTaskbar,
                settings.HasCustomFreePos, outsideApp, previewMode, work, screen);
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
            mascot = new MascotView(ActivePixelSize, settings.Skin);
            ApplyResolvedMascotState();
            stage.Width = mascot.Width;
            stage.Height = mascot.Height;
            stage.Children.Add(mascot);
        }

        public void ApplySkin(string skin)
        {
            settings.Skin = MascotCore.NormalizeSkin(skin);
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

        public void SetMinimizedCorner(string corner)
        {
            settings.HasCustomFreePos = false;
            settings.MinimizedCorner = PetSettings.NormalizeCorner(corner);
            settings.Save();
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

        public void ResetPosition()
        {
            settings.HasCustomFreePos = false;
            settings.Save();
            userMovedWhileInFront = false;
            SnapToTarget();
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
                settings.HideWhenInactive, settings.HideWhenMinimized,
                tracker.IsAppOpen, tracker.IsAppMinimized,
                tracker.HasActiveCli, tracker.HasVisibleCli, tracker.IsCliMinimized,
                previewMode, isMenuOpen);

            if (shouldHide)
            {
                hoverLeaveTimer.Stop();
                CloseHoverWindow();
                if (IsVisible) Hide();
                return true;
            }
            if (!IsVisible)
            {
                if (!dragging) EnsureEffectiveSize();
                double dx, dy;
                ComputeDesiredPositionDip(out dx, out dy);
                SetPositionDip(dx, dy);
                targetLeftDip = dx;
                targetTopDip = dy;
                Show();
                ConfigureNativeOverlayWindow(this);
            }
            return false;
        }

        MascotMood ResolveCurrentMood()
        {
            if (previewMode) return previewMood;
            var actives = monitor.ActiveSessions;
            int activeCount = actives != null ? actives.Count : 0;
            bool hasSurface = !tracker.WindowBoundsDip.IsEmpty;

            if (!tracker.IsInFront || settings.PinAboveTaskbar || !hasSurface)
            {
                if (activeCount >= 2) return MascotMood.Multitasking;
                if (activeCount == 1) return actives[0].Mood;
                return monitor.Latest.Mood;
            }

            var focused = monitor.FocusedSession;
            if (focused != null && focused.IsBusy) return focused.Mood;
            if (focused != null) return focused.Mood;
            return monitor.Latest.Mood;
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
                nextMood != lastAppliedMood &&
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
            if (isHoverOpen || !previewMode)
                RenderHoverContent();
        }

        void RenderHoverContent()
        {
            cardHeaderWrap.Children.Clear();
            sessionList.Children.Clear();
            bool darkSkin = settings.Skin == MascotCore.DarkSkin;

            if (previewMode)
            {
                sessionScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                sessionList.Children.Add(BuildSessionItem(new ActivitySnapshot
                {
                    Mood = previewMood,
                    SessionTitle = "Preview Mode",
                    ActionLabel = ActivityMonitor.MoodDefaultLabel(previewMood),
                    FocusDetail = OperatingSystem.IsMacOS()
                        ? "Menu bar → Preview Mood → Live Mode to resume tracking"
                        : "Right-click → Live Mode to resume tracking",
                }, darkSkin, false));
                return;
            }

            var actives = monitor.ActiveSessions;
            if (actives != null && actives.Count > 1)
            {
                var hdr = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                hdr.Children.Add(new TextBlock
                {
                    Text = actives.Count + " active sessions",
                    Foreground = PetTheme.InkPrimary,
                    FontSize = 11.5,
                    FontWeight = FontWeight.SemiBold,
                    FontFamily = PetTheme.UiFont,
                });
                if (actives.Count > 2)
                {
                    var scrollHint = new TextBlock
                    {
                        Text = "Scroll ↕",
                        Foreground = PetTheme.InkMuted,
                        FontSize = 10.5,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        FontFamily = PetTheme.UiFont,
                    };
                    DockPanel.SetDock(scrollHint, Dock.Right);
                    hdr.Children.Insert(0, scrollHint);
                }
                cardHeaderWrap.Children.Add(hdr);
                sessionScroll.VerticalScrollBarVisibility = actives.Count > 2 ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
                for (int i = 0; i < actives.Count; i++)
                    sessionList.Children.Add(BuildSessionItem(actives[i], darkSkin, i > 0));
            }
            else
            {
                sessionScroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                var single = (actives != null && actives.Count == 1) ? actives[0] : monitor.Latest;
                sessionList.Children.Add(BuildSessionItem(single, darkSkin, false));
            }
        }

        Control BuildSessionItem(ActivitySnapshot snap, bool darkSkin, bool withTopDivider)
        {
            var stack = new StackPanel();
            if (withTopDivider)
            {
                stack.Children.Add(new Border
                {
                    Height = 1,
                    Background = PetTheme.BorderSubtle,
                    Margin = new Thickness(0, 8, 0, 8),
                });
            }

            double innerW = CardWidth - 36;
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            headerRow.Children.Add(new Ellipse
            {
                Width = 7,
                Height = 7,
                Fill = PetTheme.DotBrushForMood(snap.Mood, darkSkin),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 7, 0),
            });
            headerRow.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(snap.SessionTitle) ? snap.Source : snap.SessionTitle,
                Foreground = PetTheme.InkSecondary,
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                FontFamily = PetTheme.UiFont,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = innerW - 18,
            });
            stack.Children.Add(headerRow);

            stack.Children.Add(new TextBlock
            {
                Text = snap.ActionLabel,
                Foreground = PetTheme.InkPrimary,
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold,
                FontFamily = PetTheme.UiFont,
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
                    Foreground = PetTheme.InkMuted,
                    FontSize = 11,
                    FontFamily = PetTheme.UiFont,
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
                            ? PetTheme.InkPrimary
                            : (IBrush)new LinearGradientBrush
                            {
                                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                                EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                                GradientStops = new GradientStops
                                {
                                    new GradientStop(Color.FromRgb(0x47, 0xB8, 0x66), 0),
                                    new GradientStop(Color.FromRgb(0x2E, 0x69, 0xE8), 1),
                                },
                            },
                    });
                }
                if (activeSliceW > 1)
                {
                    barRow.Children.Add(new Border
                    {
                        Height = 4,
                        Width = activeSliceW,
                        CornerRadius = doneW > 0 ? new CornerRadius(0, 2, 2, 0) : new CornerRadius(2),
                        Background = PetTheme.FrozenArgb(darkSkin ? (byte)0x66 : (byte)0x77, darkSkin ? (byte)0xE4 : (byte)0x42, darkSkin ? (byte)0xE4 : (byte)0x7A, darkSkin ? (byte)0xE7 : (byte)0xF2),
                    });
                }
                stack.Children.Add(new Border
                {
                    Height = 4,
                    Width = innerW,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(2),
                    Background = PetTheme.BgPillHover,
                    Child = barRow,
                    Margin = new Thickness(0, 8, 0, 4),
                });

                string stepInfo = snap.StepsTotal > 0 ? ("  ·  " + snap.StepsDone + "/" + snap.StepsTotal + " steps") : "";
                stack.Children.Add(new TextBlock
                {
                    Text = snap.PercentDone + "% done  ·  " + snap.PercentLeft + "% left" + stepInfo,
                    Foreground = PetTheme.InkSecondary,
                    FontSize = 10.8,
                    FontFamily = PetTheme.UiFont,
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
                    Background = PetTheme.BgPillHover,
                    Child = new Border
                    {
                        Height = 4,
                        Width = innerW * 0.28,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        CornerRadius = new CornerRadius(2),
                        Background = PetTheme.FrozenArgb(darkSkin ? (byte)0x88 : (byte)0xAA, darkSkin ? (byte)0xF1 : (byte)0x2E, darkSkin ? (byte)0xF3 : (byte)0x69, darkSkin ? (byte)0xF7 : (byte)0xE8),
                    },
                    Margin = new Thickness(0, 8, 0, 4),
                });
                stack.Children.Add(new TextBlock
                {
                    Text = "Running · No step checklist yet",
                    Foreground = PetTheme.InkMuted,
                    FontSize = 10.5,
                    FontFamily = PetTheme.UiFont,
                });
            }

            return stack;
        }

        void PollTarget()
        {
            tracker.TrackCliWindows = settings.CliPlacement != "off";
            tracker.Poll(CurrentScaling);

            monitor.PreferredChatCandidates = tracker.ActiveChatCandidates;
            monitor.PreferredChatTitle = tracker.ActiveChatTitle;
            monitor.PreferredSource = tracker.IsCliInFront ? "Antigravity CLI" : "";
            monitor.PreferAppOnly = tracker.IsInFront && !tracker.IsCliInFront;

            if (!previewMode)
                ApplyResolvedMascotState();

            if (SyncVisibility()) return;

            bool nowInFront = tracker.IsInFront;
            bool nowOnChatScreen = !settings.PinAboveTaskbar && nowInFront && !tracker.IsMinimized && !tracker.WindowBoundsDip.IsEmpty;

            if (nowInFront && isHoverOpen)
                CloseHoverWindow();

            if (nowOnChatScreen && !wasOnChatScreen)
                userMovedWhileInFront = false;
            wasOnChatScreen = nowOnChatScreen;

            if (nowOnChatScreen && userMovedWhileInFront) return;
            if (!dragging) EnsureEffectiveSize();

            double dx, dy;
            ComputeDesiredPositionDip(out dx, out dy);
            targetLeftDip = dx;
            targetTopDip = dy;
            UpdateMascotEyeGaze();
        }

        void UpdateMascotEyeGaze()
        {
            if (mascot == null || mascot.Mood != MascotMood.Idle) return;
            double cx, cy;
            if (!PlatformWindowTracker.TryGetCursorDip(CurrentScaling, out cx, out cy)) return;
            double eyeCenterX = CurrentLeftDip + StagePadX + 5.5 * ActivePixelSize;
            double eyeCenterY = CurrentTopDip + StagePadY + 3.4 * ActivePixelSize;
            double nx, ny;
            MascotCore.NormalizeLookOffset(cx - eyeCenterX, cy - eyeCenterY, out nx, out ny);
            mascot.SetLookOffset(nx, ny);
        }

        void TickSmoothGlide()
        {
            DateTime now = DateTime.UtcNow;
            double dt = Math.Min(0.05, Math.Max(0.001, (now - lastAnimTime).TotalSeconds));
            lastAnimTime = now;
            if (dragging || isMenuOpen || !IsVisible || double.IsNaN(targetLeftDip)) return;

            double curX = CurrentLeftDip;
            double curY = CurrentTopDip;
            double dx = targetLeftDip - curX;
            double dy = targetTopDip - curY;
            if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
            {
                if (Math.Abs(dx) > 0.05 || Math.Abs(dy) > 0.05)
                    SetPositionDip(targetLeftDip, targetTopDip);
                return;
            }
            double factor = 1.0 - Math.Exp(-14.0 * dt);
            SetPositionDip(curX + dx * factor, curY + dy * factor);
        }

        void SnapToTarget()
        {
            tracker.TrackCliWindows = settings.CliPlacement != "off";
            tracker.Poll(CurrentScaling);
            wasOnChatScreen = !settings.PinAboveTaskbar && tracker.IsInFront && !tracker.IsMinimized && !tracker.WindowBoundsDip.IsEmpty;
            if (!dragging) EnsureEffectiveSize();
            double dx, dy;
            ComputeDesiredPositionDip(out dx, out dy);
            SetPositionDip(dx, dy);
            targetLeftDip = dx;
            targetTopDip = dy;
            SyncVisibility();
        }

        public static void PlaceInCorner(
            string corner,
            ScreenRectDip workArea,
            ScreenRectDip screenBounds,
            double spriteW,
            double spriteH,
            double padX,
            double padY,
            out double leftDip,
            out double topDip)
        {
            string normalized = PetSettings.NormalizeCorner(corner);
            double petW = spriteW + padX * 2;
            bool isTaskbar = normalized.StartsWith("taskbar-", StringComparison.Ordinal);

            if (normalized.EndsWith("-left", StringComparison.Ordinal))
                leftDip = workArea.Left + 8.0;
            else if (normalized.EndsWith("-center", StringComparison.Ordinal))
                leftDip = workArea.Left + (workArea.Width - petW) * 0.5;
            else
                leftDip = workArea.Right - petW - 8.0;

            if (isTaskbar)
            {
                double bottomGap = screenBounds.Bottom - workArea.Bottom;
                double topGap = workArea.Top - screenBounds.Top;
                if (bottomGap >= 16.0)
                {
                    topDip = workArea.Bottom + (bottomGap - spriteH) * 0.5 - padY;
                }
                else if (topGap >= 16.0)
                {
                    topDip = screenBounds.Top + (topGap - spriteH) * 0.5 - padY;
                }
                else
                {
                    double barH = Math.Max(spriteH + 8.0, 44.0);
                    topDip = screenBounds.Bottom - barH + (barH - spriteH) * 0.5 - padY;
                }
            }
            else if (normalized.StartsWith("bottom-", StringComparison.Ordinal))
            {
                topDip = workArea.Bottom - spriteH - padY;
            }
            else
            {
                topDip = workArea.Top + 8.0;
            }
        }

        void ComputeDesiredPositionDip(out double petLeft, out double petTop)
        {
            ScreenRectDip work, screen;
            GetScreenRectsDip(out work, out screen);
            double spriteW = 11.0 * ActivePixelSize;
            double spriteH = 9.0 * ActivePixelSize;
            double petW = spriteW + StagePadX * 2;
            string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);
            string awayCorner = ResolveAwayCornerForPlatform(OperatingSystem.IsMacOS(), tracker.IsAnyAntigravityOpen, effectiveCorner);

            if (!settings.PinAboveTaskbar && tracker.IsInFront && !tracker.IsMinimized && !tracker.WindowBoundsDip.IsEmpty)
            {
                var winDip = tracker.WindowBoundsDip;
                if (tracker.IsCliInFront && settings.CliPlacement != "off")
                {
                    string cli = PetSettings.NormalizeCliPlacement(settings.CliPlacement);
                    bool isLeft = cli.EndsWith("-left", StringComparison.Ordinal);
                    bool isCenter = cli.EndsWith("-center", StringComparison.Ordinal);
                    bool isTop = cli.StartsWith("top-", StringComparison.Ordinal);
                    petLeft = isLeft ? (winDip.Left + 22.0 - StagePadX)
                        : isCenter ? (winDip.Left + (winDip.Width - petW) * 0.5)
                        : (winDip.Right - 22.0 - spriteW - StagePadX);
                    double vBot = isTop
                        ? Math.Max(work.Top + spriteH + 6.0, winDip.Top + 2.0)
                        : Math.Min(work.Bottom, winDip.Bottom - 20.0);
                    petTop = vBot - spriteH - StagePadY;
                }
                else
                {
                    // Perch neatly along the bottom-right composer area or top-right header edge inside Antigravity
                    double rightInset = Math.Min(42.0, Math.Max(22.0, winDip.Width * 0.04));
                    petLeft = winDip.Right - rightInset - spriteW - StagePadX;
                    double perchBottom = Math.Max(work.Top + spriteH + 8.0, winDip.Top + 28.0);
                    petTop = perchBottom - spriteH - StagePadY;
                }
            }
            else if (settings.HasCustomFreePos)
            {
                petLeft = settings.FreeX;
                petTop = settings.FreeY;
            }
            else
            {
                PlaceInCorner(awayCorner, work, screen, spriteW, spriteH, StagePadX, StagePadY, out petLeft, out petTop);
            }

            bool pinnedAway = !tracker.IsInFront || settings.PinAboveTaskbar;
            bool allowBar = pinnedAway && (settings.HasCustomFreePos || awayCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase));
            double maxBottom = allowBar ? Math.Max(work.Bottom, screen.Bottom) : work.Bottom;
            double minTop = allowBar ? Math.Min(work.Top, screen.Top) : work.Top;
            petLeft = Math.Max(work.Left - StagePadX, Math.Min(work.Right - spriteW - StagePadX, petLeft));
            petTop = Math.Max(minTop - StagePadY * 0.4, Math.Min(maxBottom - spriteH - StagePadY, petTop));
        }

        // ---- Dead-Center Website-Themed Hover Progress Card ----

        public void HideHoverCard()
        {
            CloseHoverWindow();
        }

        void CloseHoverWindow()
        {
            isHoverOpen = false;
            if (hoverWindow.IsVisible) hoverWindow.Hide();
        }

        void UpdateHoverPosition()
        {
            ScreenRectDip work, screen;
            GetScreenRectsDip(out work, out screen);
            double spriteW = 11.0 * ActivePixelSize;
            double spriteH = 9.0 * ActivePixelSize;
            hoverCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double popupW = hoverWindow.Width > 10 && !double.IsNaN(hoverWindow.Width)
                ? hoverWindow.Width
                : Math.Max(CardWidth + 12.0, hoverCard.DesiredSize.Width);
            double popupH = hoverWindow.Bounds.Height > 10
                ? hoverWindow.Bounds.Height
                : Math.Max(70.0, hoverCard.DesiredSize.Height);

            double winLeft = CurrentLeftDip;
            double winTop = CurrentTopDip;
            string effectiveCorner = PetSettings.ResolvePinnedCorner(settings.MinimizedCorner, settings.PinAboveTaskbar);
            string awayCorner = ResolveAwayCornerForPlatform(OperatingSystem.IsMacOS(), tracker.IsAnyAntigravityOpen, effectiveCorner);
            bool inTaskbar = !settings.HasCustomFreePos && (!tracker.IsInFront || settings.PinAboveTaskbar) &&
                             awayCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase);
            bool nearBottom = (winTop + StagePadY + spriteH) > (work.Bottom - 70.0);
            bool dockTopEdge = winTop < work.Top + 12.0;

            double x, y;
            if (inTaskbar || nearBottom)
            {
                double desiredX = winLeft + StagePadX + spriteW * 0.5 - popupW * 0.5;
                x = Math.Max(work.Left + 4.0, Math.Min(work.Right - popupW - 4.0, desiredX));
                y = dockTopEdge ? (winTop + StagePadY + spriteH) : (winTop + StagePadY - popupH);
                if (y < work.Top + 4.0 && !dockTopEdge) y = winTop + StagePadY + spriteH;
            }
            else
            {
                bool openOnRight = winLeft < work.Left + popupW + 24.0;
                x = openOnRight
                    ? (winLeft + StagePadX + spriteW + 4.0)
                    : Math.Max(work.Left + 4.0, winLeft + StagePadX - popupW - 4.0);
                y = Math.Max(work.Top + 4.0, Math.Min(work.Bottom - popupH - 4.0, winTop + StagePadY * 0.35));
            }

            double scale = CurrentScaling;
            hoverWindow.Position = new PixelPoint((int)Math.Round(x * scale), (int)Math.Round(y * scale));
        }

        void ShowCard()
        {
            hoverLeaveTimer.Stop();
            if (!settings.ShowHoverCard || dragging || isMenuOpen || tracker.IsInFront || !IsVisible) return;
            RenderHoverContent();

            if (!isHoverOpen)
            {
                hoverCard.Opacity = 0;
                cardScale.ScaleX = 0.96;
                cardScale.ScaleY = 0.96;
                cardSlide.Y = 4;
                isHoverOpen = true;
                UpdateHoverPosition();
                hoverWindow.Show();
                hoverWindow.Topmost = false;
                hoverWindow.Topmost = true;
            }
            UpdateHoverPosition();
            hoverCard.Opacity = 1.0;
            cardScale.ScaleX = 1.0;
            cardScale.ScaleY = 1.0;
            cardSlide.Y = 0;
        }

        void ScheduleHideCard()
        {
            hoverLeaveTimer.Stop();
            hoverLeaveTimer.Start();
        }

        void HideCard()
        {
            if (!isHoverOpen) return;
            if (!stage.IsPointerOver && !hoverCard.IsPointerOver)
            {
                hoverCard.Opacity = 0;
                CloseHoverWindow();
            }
        }

        // ---- Pointer Drag + Clicks + Dark Website-Themed Context Menu ----

        void OnPetPointerPressed(object sender, PointerPressedEventArgs e)
        {
            var pt = e.GetCurrentPoint(this);
            if (pt.Properties.IsRightButtonPressed)
            {
                if (!ShouldAllowRightClickContextMenu(OperatingSystem.IsMacOS()))
                {
                    e.Handled = true;
                    return;
                }
                CloseHoverWindow();
                OpenContextMenu(e);
                e.Handled = true;
                return;
            }
            if (!pt.Properties.IsLeftButtonPressed) return;

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
            var screenPt = this.PointToScreen(pt.Position);
            dragStartCursorPx = screenPt;
            dragStartLeftDip = CurrentLeftDip;
            dragStartTopDip = CurrentTopDip;
            e.Pointer.Capture(stage);
            e.Handled = true;
        }

        void OnPetPointerMoved(object sender, PointerEventArgs e)
        {
            if (!mouseDown || settings.LockPosition) return;
            var screenPt = this.PointToScreen(e.GetPosition(this));
            double dxPx = screenPt.X - dragStartCursorPx.X;
            double dyPx = screenPt.Y - dragStartCursorPx.Y;

            if (!dragging)
            {
                if (Math.Abs(dxPx) < DragThresholdPx && Math.Abs(dyPx) < DragThresholdPx) return;
                dragging = true;
                if (mascot != null) mascot.CancelHurt();
                CloseHoverWindow();
            }

            double s = CurrentScaling;
            double nx = dragStartLeftDip + dxPx / s;
            double ny = dragStartTopDip + dyPx / s;
            SetPositionDip(nx, ny);
            targetLeftDip = nx;
            targetTopDip = ny;
        }

        void OnPetPointerReleased(object sender, PointerReleasedEventArgs e)
        {
            if (!mouseDown) return;
            mouseDown = false;
            try { e.Pointer.Capture(null); } catch { }

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
            targetLeftDip = CurrentLeftDip;
            targetTopDip = CurrentTopDip;
            if (tracker.IsInFront && !settings.PinAboveTaskbar)
            {
                userMovedWhileInFront = true;
            }
            else
            {
                settings.HasCustomFreePos = true;
                settings.FreeX = targetLeftDip;
                settings.FreeY = targetTopDip;
                settings.Save();
            }
        }

        static void CloseMenuWindowSafely(Window win)
        {
            if (win == null) return;
            try
            {
                var border = win.Content as Border;
                var scroll = border != null ? border.Child as ScrollViewer : null;
                if (scroll != null) scroll.Content = null;
                win.Content = null;
            }
            catch { }
            try { win.Close(); } catch { }
        }

        void ScheduleMenuFocusCheck()
        {
            if (menuDeactivateTimer == null) return;
            menuDeactivateTimer.Stop();
            menuDeactivateTimer.Start();
        }

        void CloseContextMenu()
        {
            if (menuDeactivateTimer != null) menuDeactivateTimer.Stop();
            isMenuOpen = false;
            activeSubmenuRow = null;
            CloseMenuWindowSafely(submenuWindow);
            submenuWindow = null;
            CloseMenuWindowSafely(menuWindow);
            menuWindow = null;
            Dispatcher.UIThread.Post(() =>
            {
                if (!IsVisible) return;
                Topmost = true;
                ConfigureNativeOverlayWindow(this, bringFront: true);
            });
        }

        void OpenContextMenu(PointerPressedEventArgs e)
        {
            CloseContextMenu();
            isMenuOpen = true;

            var rows = new StackPanel { Spacing = 1 };
            rows.Children.Add(SubmenuRow("Skin", () => new Control[]
            {
                CheckRow("Classic (Official Antigravity Colors)", settings.Skin == MascotCore.ClassicSkin, () => ApplySkin(MascotCore.ClassicSkin)),
                CheckRow("Dark Remake (Monochrome)", settings.Skin == MascotCore.DarkSkin, () => ApplySkin(MascotCore.DarkSkin)),
            }));

            rows.Children.Add(SubmenuRow("Location When Away / Minimized", () =>
            {
                var awayRows = new List<Control>();
                bool addedSep = false;
                foreach (var opt in PetSettings.AwayLocations)
                {
                    if (!addedSep && opt.Id.StartsWith("taskbar-", StringComparison.Ordinal))
                    {
                        awayRows.Add(MenuSeparator());
                        addedSep = true;
                    }
                    string id = opt.Id;
                    awayRows.Add(CheckRow(opt.Label, !settings.HasCustomFreePos && settings.MinimizedCorner == id, () => SetMinimizedCorner(id)));
                }
                return awayRows;
            }));
            rows.Children.Add(CheckRow("Pin Above Taskbar (Stay in Place)", settings.PinAboveTaskbar, () => SetPinAboveTaskbar(!settings.PinAboveTaskbar)));
            rows.Children.Add(CheckRow("Lock Position (Disable Dragging)", settings.LockPosition, () => SetLockPosition(!settings.LockPosition)));

            rows.Children.Add(SubmenuRow("CLI Terminal Placement", () =>
            {
                var cliRows = new List<Control>();
                foreach (var opt in PetSettings.CliLocations)
                {
                    string id = opt.Id;
                    cliRows.Add(CheckRow(opt.Label, settings.CliPlacement == id, () => SetCliPlacement(id)));
                }
                return cliRows;
            }));

            rows.Children.Add(SubmenuRow("Auto-Hide", () => new Control[]
            {
                CheckRow("Hide When Inactive (App & CLI Closed)", settings.HideWhenInactive, () => SetHideWhenInactive(!settings.HideWhenInactive)),
                CheckRow("Hide When Minimized", settings.HideWhenMinimized, () => SetHideWhenMinimized(!settings.HideWhenMinimized)),
            }));

            rows.Children.Add(SubmenuRow("Size", () => new Control[]
            {
                BuildSizeSliderPanel(),
                MenuSeparator(),
                ActionRow("Reset Default (100% · 4.2 px)", () => ApplyPixelSize(PetSettings.DefaultPixelSize, true)),
            }));

            rows.Children.Add(SubmenuRow("Preview Mood", () =>
            {
                var prevRows = new List<Control>();
                prevRows.Add(CheckRow("Live Mode (Auto-react to Antigravity)", !previewMode, () => SetPreviewMood(null)));
                prevRows.Add(MenuSeparator());
                foreach (MascotMood m in Enum.GetValues(typeof(MascotMood)))
                {
                    var captured = m;
                    prevRows.Add(CheckRow(captured.ToString(), previewMode && previewMood == captured, () => SetPreviewMood(captured)));
                }
                return prevRows;
            }));

            rows.Children.Add(MenuSeparator());
            rows.Children.Add(CheckRow("Show Hover Progress Card", settings.ShowHoverCard, () =>
            {
                settings.ShowHoverCard = !settings.ShowHoverCard;
                settings.Save();
                if (!settings.ShowHoverCard) CloseHoverWindow();
            }));
            rows.Children.Add(CheckRow("Launch on Login", settings.StartWithWindows, () =>
            {
                settings.StartWithWindows = !settings.StartWithWindows;
                settings.Save();
                PetSettings.ApplyStartup(settings.StartWithWindows, Environment.ProcessPath ?? "");
            }));
            rows.Children.Add(ActionRow(settings.PinAboveTaskbar ? "Reset Pinned Position" : "Snap Back Above Window", ResetPosition));
            rows.Children.Add(CheckRow("Auto-Update from GitHub", settings.AutoUpdate, () => { settings.AutoUpdate = !settings.AutoUpdate; settings.Save(); }));
            if (updater != null)
            {
                string updLabel = updater.UpdateReady
                    ? "Install Update (" + updater.ReadyTag + ")"
                    : "Check for Updates (" + updater.Status + ")";
                rows.Children.Add(ActionRow(updLabel, () => updater.CheckNow()));
            }
            rows.Children.Add(ActionRow("Send Feedback (GitHub Issue)...", () => FeedbackWindow.ShowDialogOnce(settings.Skin, settings.Size)));
            rows.Children.Add(SubmenuRow("Credits", () => new Control[]
            {
                ActionRow("Made by Yassin Ragab (yassinr.me)", () => OpenUrl("https://yassinr.me")),
                ActionRow("GitHub: yvs1n/agypet", () => OpenUrl("https://github.com/yvs1n/agypet")),
                ActionRow("For Antigravity, built with Antigravity", () => OpenUrl("https://antigravity.google")),
            }));
            rows.Children.Add(MenuSeparator());
            rows.Children.Add(ActionRow("Quit AgyPet", () => Program.ShutdownApplication()));

            menuWindow = CreateDarkMenuWindow(rows);
            ScreenRectDip work, screen;
            GetScreenRectsDip(out work, out screen);
            var anchorPx = this.PointToScreen(e.GetPosition(this));
            double scale = CurrentScaling;
            double ax = anchorPx.X / scale;
            double ay = anchorPx.Y / scale;

            menuWindow.Deactivated += (s, ev) => ScheduleMenuFocusCheck();
            menuWindow.KeyDown += (s, ke) => { if (ke.Key == Key.Escape) CloseContextMenu(); };

            menuWindow.Show();
            double mw = Math.Max(220, menuWindow.Bounds.Width);
            double mh = Math.Max(200, menuWindow.Bounds.Height);
            double mx = Math.Max(work.Left + 6, Math.Min(work.Right - mw - 6, ax));
            double my = Math.Max(work.Top + 6, Math.Min(work.Bottom - mh - 6, ay - (ay + mh > work.Bottom - 8 ? mh : 0)));
            menuWindow.Position = new PixelPoint((int)Math.Round(mx * scale), (int)Math.Round(my * scale));
            menuWindow.Activate();
        }

        Window CreateDarkMenuWindow(Control body)
        {
            var scroll = new ScrollViewer
            {
                MaxHeight = 460,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = body,
            };
            var card = new Border
            {
                Background = PetTheme.BgPopover,
                BorderBrush = PetTheme.BorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(5),
                Child = scroll,
            };
            var win = new Window
            {
                SystemDecorations = SystemDecorations.None,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                CanResize = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = card,
            };
            win.Opened += (s, e) => ConfigureNativeOverlayWindow(win);
            return win;
        }

        Control MenuSeparator()
        {
            return new Border
            {
                Height = 1,
                Background = PetTheme.BorderSubtle,
                Margin = new Thickness(8, 4, 8, 4),
            };
        }

        Control BuildSizeSliderPanel()
        {
            var valueText = new TextBlock
            {
                Text = PetSettings.FormatSizeLabel(settings.PixelSize),
                Foreground = PetTheme.AccentBlueHi,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                FontFamily = PetTheme.UiFont,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            var labelText = new TextBlock
            {
                Text = "Size",
                Foreground = PetTheme.InkSecondary,
                FontSize = 11.5,
                FontWeight = FontWeight.SemiBold,
                FontFamily = PetTheme.UiFont,
            };
            var hdr = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(valueText, Dock.Right);
            hdr.Children.Add(valueText);
            hdr.Children.Add(labelText);

            var slider = new Slider
            {
                Minimum = PetSettings.MinPixelSize,
                Maximum = PetSettings.MaxPixelSize,
                Value = settings.PixelSize,
                TickFrequency = 0.1,
                IsSnapToTickEnabled = true,
                Width = 186,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            slider.PropertyChanged += (s, e) =>
            {
                if (e.Property == RangeBase.ValueProperty)
                {
                    double next = PetSettings.ClampPixelSize(slider.Value);
                    valueText.Text = PetSettings.FormatSizeLabel(next);
                    ApplyPixelSize(next, save: false);
                }
            };
            slider.PointerReleased += (s, e) => settings.Save();

            var panel = new StackPanel
            {
                Width = 190,
                Margin = new Thickness(8, 6, 8, 6),
            };
            panel.Children.Add(hdr);
            panel.Children.Add(slider);
            return panel;
        }

        Control CheckRow(string label, bool isChecked, Action onClick)
        {
            return BuildMenuRow(label, isChecked, false, () =>
            {
                CloseContextMenu();
                onClick();
            });
        }

        Control ActionRow(string label, Action onClick)
        {
            return BuildMenuRow(label, false, false, () =>
            {
                CloseContextMenu();
                onClick();
            });
        }

        internal static Control BuildFreshMenuBody(Func<IEnumerable<Control>> buildItems)
        {
            var stack = new StackPanel { Spacing = 1 };
            if (buildItems != null)
            {
                foreach (var it in buildItems())
                    if (it != null) stack.Children.Add(it);
            }
            return stack;
        }

        Control SubmenuRow(string label, Func<IEnumerable<Control>> buildItems)
        {
            Border row = null;
            row = BuildMenuRow(label, false, true, () => OpenSubmenu(row, buildItems));
            row.PointerEntered += (s, e) => OpenSubmenu(row, buildItems);
            return row;
        }

        void OpenSubmenu(Control anchorRow, Func<IEnumerable<Control>> buildItems)
        {
            if (menuWindow == null || anchorRow == null) return;
            if (activeSubmenuRow == anchorRow && submenuWindow != null) return;
            CloseMenuWindowSafely(submenuWindow);
            submenuWindow = null;
            activeSubmenuRow = anchorRow;

            submenuWindow = CreateDarkMenuWindow(BuildFreshMenuBody(buildItems));
            submenuWindow.Deactivated += (s, ev) => ScheduleMenuFocusCheck();
            submenuWindow.KeyDown += (s, ke) => { if (ke.Key == Key.Escape) CloseContextMenu(); };
            submenuWindow.Show();

            ScreenRectDip work, screen;
            GetScreenRectsDip(out work, out screen);
            double scale = CurrentScaling;
            var pt = anchorRow.PointToScreen(new Point(anchorRow.Bounds.Width, 0));
            var leftPt = anchorRow.PointToScreen(new Point(0, 0));
            double sw = Math.Max(180, submenuWindow.Bounds.Width);
            double sh = Math.Max(80, submenuWindow.Bounds.Height);
            double sx = (pt.X / scale) + 4;
            if (sx + sw > work.Right - 6) sx = (leftPt.X / scale) - sw - 4;
            double sy = Math.Max(work.Top + 6, Math.Min(work.Bottom - sh - 6, (pt.Y / scale) - 5));
            submenuWindow.Position = new PixelPoint((int)Math.Round(sx * scale), (int)Math.Round(sy * scale));
        }

        Border BuildMenuRow(string label, bool isChecked, bool hasSubmenu, Action onClick)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(18)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            if (isChecked)
            {
                var chk = new TextBlock
                {
                    Text = "✓",
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = PetTheme.AccentBlueHi,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                };
                Grid.SetColumn(chk, 0);
                grid.Children.Add(chk);
            }

            var txt = new TextBlock
            {
                Text = label,
                Foreground = PetTheme.InkPrimary,
                FontSize = 12.5,
                FontWeight = FontWeight.Medium,
                FontFamily = PetTheme.UiFont,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(txt, 1);
            grid.Children.Add(txt);

            if (hasSubmenu)
            {
                var arrow = new TextBlock
                {
                    Text = "›",
                    FontSize = 14,
                    Foreground = PetTheme.InkMuted,
                    Margin = new Thickness(12, -1, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(arrow, 2);
                grid.Children.Add(arrow);
            }

            var row = new Border
            {
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(9, 5.5, 10, 5.5),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = grid,
            };
            row.PointerEntered += (s, e) =>
            {
                row.Background = PetTheme.BgPillHover;
                row.BorderBrush = PetTheme.BorderSubtle;
                if (!hasSubmenu && submenuWindow != null)
                {
                    CloseMenuWindowSafely(submenuWindow);
                    submenuWindow = null;
                    activeSubmenuRow = null;
                }
            };
            row.PointerExited += (s, e) =>
            {
                row.Background = Brushes.Transparent;
                row.BorderBrush = Brushes.Transparent;
            };
            row.PointerPressed += (s, e) =>
            {
                if (e.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
                {
                    onClick();
                    e.Handled = true;
                }
            };
            return row;
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { }
        }

        // macOS Objective-C NSWindow helper so overlay stays visible across Spaces without hiding when menus deactivate
        static class MacOverlay
        {
            const string ObjC = "/usr/lib/libobjc.A.dylib";
            [DllImport(ObjC)] static extern IntPtr sel_registerName(string name);
            [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void SendVoid(IntPtr receiver, IntPtr selector);
            [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void SendULong(IntPtr receiver, IntPtr selector, ulong arg);
            [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void SendBool(IntPtr receiver, IntPtr selector, bool arg);

            public static void ConfigureWindow(IntPtr nsWindow, bool bringFront = false)
            {
                if (nsWindow == IntPtr.Zero) return;
                // NSWindowCollectionBehaviorCanJoinAllSpaces (1) | NSWindowCollectionBehaviorFullScreenAuxiliary (256)
                SendULong(nsWindow, sel_registerName("setCollectionBehavior:"), 1UL | 256UL);
                SendBool(nsWindow, sel_registerName("setHidesOnDeactivate:"), false);
                if (bringFront)
                    SendVoid(nsWindow, sel_registerName("orderFrontRegardless"));
            }
        }
    }
}
