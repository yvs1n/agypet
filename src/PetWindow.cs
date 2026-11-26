using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
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

        // Independent Popup for the hover status card (never shifts PetWindow's Left/Top!)
        readonly Popup hoverPopup;
        readonly Border hoverCard;
        readonly StackPanel cardHeaderWrap = new StackPanel();
        readonly ScrollViewer sessionScroll;
        readonly StackPanel sessionList = new StackPanel();

        Mascot mascot;
        readonly DispatcherTimer pollTimer;
        readonly DispatcherTimer animTimer;
        readonly DispatcherTimer hoverLeaveTimer;
        readonly DispatcherTimer menuDismissTimer;

        readonly uint selfPid = (uint)Process.GetCurrentProcess().Id;
        bool mouseDown;
        bool dragging;
        POINT dragStartCursor;
        double dragStartLeft, dragStartTop;
        bool wasOnChatScreen;
        bool userMovedWhileInFront;
        bool contextMenuOpen;
        bool externalMenuOpen;
        DateTime menuCooldownUntil = DateTime.MinValue;
        HwndSource hwndSource;
        IntPtr menuOpenedForeground = IntPtr.Zero;
        bool prevMenuLeftDown, prevMenuRightDown, prevMenuMiddleDown, prevMenuEscDown;
        readonly ContextMenu contextMenu = new ContextMenu();

        bool previewMode;
        MascotMood previewMood = MascotMood.Idle;
        MascotMood lastAppliedMood = MascotMood.Idle;
        DateTime lastBusySwitchAt = DateTime.MinValue;
        double targetLeft = double.NaN, targetTop = double.NaN;
        DateTime lastAnimTime = DateTime.UtcNow;

        const int MinBusyDwellMs = 1200;

        const double StagePadX = 32;
        const double StagePadY = 24;
        const double StagePadBottom = 4;
        const double CardWidth = 264;
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
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            SizeToContent = SizeToContent.WidthAndHeight;

            petHitArea = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x01, 0, 0, 0)),
                Cursor = Cursors.Hand,
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

            hoverCard = new Border
            {
                Width = CardWidth,
                Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x11, 0x13, 0x18)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(13, 10, 13, 10),
                Child = cardRoot,
                Opacity = 0,
                Margin = new Thickness(6),
            };

            hoverPopup = new Popup
            {
                PlacementTarget = petHitArea,
                Placement = PlacementMode.Left,
                AllowsTransparency = true,
                StaysOpen = true,
                PopupAnimation = PopupAnimation.None,
                Child = hoverCard,
            };

            RebuildMascot();
            menuDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            menuDismissTimer.Tick += (s, e) => CheckContextMenuDismiss();

            contextMenu.Opened += (s, e) =>
            {
                contextMenuOpen = true;
                hoverPopup.IsOpen = false;
                PopulateContextMenu();
                StartContextMenuWatch();
            };
            contextMenu.Closed += (s, e) =>
            {
                contextMenuOpen = false;
                menuCooldownUntil = DateTime.UtcNow.AddMilliseconds(220);
                StopContextMenuWatch();
                RestoreTaskbarTopmost();
            };
            PopulateContextMenu();
            petHitArea.ContextMenu = contextMenu;

            petHitArea.MouseLeftButtonDown += OnPetMouseDown;
            petHitArea.MouseMove += OnPetMouseMove;
            petHitArea.MouseLeftButtonUp += OnPetMouseUp;
            petHitArea.MouseEnter += (s, e) => ShowCard();
            petHitArea.MouseLeave += (s, e) => ScheduleHideCard();
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
                StopContextMenuWatch();
                if (hwndSource != null)
                {
                    hwndSource.RemoveHook(OnWndProc);
                    hwndSource = null;
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
            SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            hwndSource = HwndSource.FromHwnd(hwnd);
            if (hwndSource != null)
                hwndSource.AddHook(OnWndProc);
        }

        public static bool ShouldForceTopmostOnPosChanging(
            bool dockedOnTaskbar,
            uint flags,
            IntPtr insertAfter,
            uint insertAfterPid,
            uint selfPid)
        {
            if (!dockedOnTaskbar) return false;
            if ((flags & SWP_NOZORDER) != 0) return false;
            if (insertAfter == HWND_TOPMOST) return false;
            if (insertAfterPid != 0 && insertAfterPid == selfPid) return false;
            return true;
        }

        IntPtr OnWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_MOUSEACTIVATE)
            {
                handled = true;
                return new IntPtr(MA_NOACTIVATE);
            }
            if (msg == WM_WINDOWPOSCHANGING && IsDockedOnTaskbar() && lParam != IntPtr.Zero)
            {
                try
                {
                    var wp = (WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(WINDOWPOS));
                    uint afterPid = 0;
                    if (wp.hwndInsertAfter != IntPtr.Zero && wp.hwndInsertAfter != HWND_TOPMOST)
                        GetWindowThreadProcessId(wp.hwndInsertAfter, out afterPid);
                    if (ShouldForceTopmostOnPosChanging(true, wp.flags, wp.hwndInsertAfter, afterPid, selfPid))
                    {
                        wp.hwndInsertAfter = HWND_TOPMOST;
                        Marshal.StructureToPtr(wp, lParam, false);
                    }
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        bool IsDockedOnTaskbar()
        {
            return !tracker.IsInFront && !settings.HasCustomFreePos && settings.MinimizedCorner != null &&
                   settings.MinimizedCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase);
        }

        void RestoreTaskbarTopmost()
        {
            if (!IsDockedOnTaskbar()) return;
            EnsureTopmost();
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
            {
                if (!contextMenuOpen && !externalMenuOpen && IsDockedOnTaskbar())
                    EnsureTopmost();
            }));
        }

        public void RebuildMascot()
        {
            stage.Children.Clear();
            mascot = new Mascot(settings.PixelSize, settings.Skin);
            mascot.Mood = previewMode ? previewMood : monitor.Latest.Mood;
            lastAppliedMood = mascot.Mood;
            lastBusySwitchAt = DateTime.UtcNow;
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
                return tracker.IsInFront && !tracker.IsMinimized && !userMovedWhileInFront &&
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

        public void ApplySize(string size)
        {
            settings.Size = size;
            settings.Save();
            RebuildMascot();
            SnapToTarget();
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
        }

        public void SetMinimizedCorner(string corner)
        {
            settings.HasCustomFreePos = false;
            settings.MinimizedCorner = PetSettings.NormalizeCorner(corner);
            settings.Save();
            if (!tracker.IsInFront) SnapToTarget();
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

        MascotMood ResolveCurrentMood()
        {
            if (previewMode) return previewMood;

            // Inside Antigravity: react specifically to the active chat, voice mode, dictation, and user typing!
            if (tracker.IsInFront)
            {
                if (tracker.InVoiceMode)
                    return tracker.VoiceAgentSpeaking ? MascotMood.Speaking : MascotMood.Listening;
                if (tracker.InDictation)
                    return MascotMood.Listening;

                var focused = monitor.FocusedSession;
                if (focused != null && focused.IsBusy)
                    return focused.Mood;
                if (tracker.IsUserTyping)
                    return MascotMood.Typing;
                if (focused != null)
                    return focused.Mood;
                return monitor.Latest.Mood;
            }

            // Outside Antigravity / Minimized:
            //  - 2+ active sessions -> Multitasking juggle!
            //  - 1 active session  -> React directly to that 1 session!
            //  - 0 active sessions -> Background (if live server running), Done, Idle, or Sleeping!
            var actives = monitor.ActiveSessions;
            if (actives != null && actives.Count >= 2)
                return MascotMood.Multitasking;
            if (actives != null && actives.Count == 1)
                return actives[0].Mood;
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
            mascot.Mood = nextMood;
        }

        void SyncActivity()
        {
            ApplyResolvedMascotState();

            if (hoverPopup.IsOpen || !previewMode)
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
                    Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                };
                hdr.Children.Add(countBadge);

                if (actives.Count > 2)
                {
                    var scrollHint = new TextBlock
                    {
                        Text = "Scroll ↕",
                        Foreground = BrushHex(0x94, 0xA3, 0xB8),
                        FontSize = 10.5,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
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
                    Background = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)),
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
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
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
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
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
                    Foreground = BrushHex(0xA1, 0xA1, 0xAA),
                    FontSize = 11,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
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
                        Height = 5,
                        Width = Math.Max(3, doneW),
                        CornerRadius = activeSliceW > 0 ? new CornerRadius(3, 0, 0, 3) : new CornerRadius(3),
                        Background = darkSkin
                            ? (Brush)BrushHex(0xF4, 0xF4, 0xF5)
                            : new LinearGradientBrush(Color.FromRgb(0x34, 0xA8, 0x53), Color.FromRgb(0x25, 0x75, 0xFC), 0),
                    });
                }
                if (activeSliceW > 1)
                {
                    barRow.Children.Add(new Border
                    {
                        Height = 5,
                        Width = activeSliceW,
                        CornerRadius = doneW > 0 ? new CornerRadius(0, 3, 3, 0) : new CornerRadius(3),
                        Background = new SolidColorBrush(darkSkin ? Color.FromArgb(0x66, 0xE4, 0xE4, 0xE7) : Color.FromArgb(0x77, 0x38, 0xBD, 0xF8)),
                    });
                }

                stack.Children.Add(new Border
                {
                    Height = 5,
                    Width = innerW,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                    Child = barRow,
                    Margin = new Thickness(0, 8, 0, 4),
                });

                string stepInfo = snap.StepsTotal > 0 ? ("  ·  " + snap.StepsDone + "/" + snap.StepsTotal + " steps") : "";
                stack.Children.Add(new TextBlock
                {
                    Text = snap.PercentDone + "% done  ·  " + snap.PercentLeft + "% left" + stepInfo,
                    Foreground = BrushHex(0xCB, 0xD5, 0xE1),
                    FontSize = 10.8,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });
            }
            else if (snap.IsBusy)
            {
                stack.Children.Add(new Border
                {
                    Height = 5,
                    Width = innerW,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                    Child = new Border
                    {
                        Height = 5,
                        Width = innerW * 0.28,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        CornerRadius = new CornerRadius(3),
                        Background = new SolidColorBrush(darkSkin ? Color.FromArgb(0x88, 0xF4, 0xF4, 0xF5) : Color.FromArgb(0x99, 0x25, 0x75, 0xFC)),
                    },
                    Margin = new Thickness(0, 8, 0, 4),
                });
                stack.Children.Add(new TextBlock
                {
                    Text = "Running · No step checklist yet",
                    Foreground = BrushHex(0x94, 0xA3, 0xB8),
                    FontSize = 10.5,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });
            }

            return stack;
        }

        static Brush DotBrushForMood(MascotMood mood, bool darkSkin)
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
            if (dragging || IsAnyMenuActive)
            {
                if (!dragging && !contextMenuOpen && !externalMenuOpen && IsDockedOnTaskbar())
                    EnsureTopmost();
                return;
            }
            tracker.TrackCliWindows = settings.CliPlacement != "off";
            tracker.Poll();
            monitor.PreferredChatTitle = tracker.ActiveChatTitle;
            monitor.PreferredSource = tracker.IsCliInFront ? "Antigravity CLI" : "";

            if (!previewMode)
                ApplyResolvedMascotState();

            bool nowInFront = tracker.IsInFront;
            bool hasTargetBox = tracker.IsCliInFront ? !tracker.WindowBoundsPx.IsEmpty : !tracker.ChatBoxBoundsPx.IsEmpty;
            bool nowOnChatScreen = nowInFront && !tracker.IsMinimized && hasTargetBox;

            if (nowInFront && hoverPopup.IsOpen)
                hoverPopup.IsOpen = false;

            // Whenever the user enters an Antigravity chat (from another app OR back from Antigravity Settings),
            // ALWAYS snap back cleanly above the chatbox!
            if (nowOnChatScreen && !wasOnChatScreen)
                userMovedWhileInFront = false;
            wasOnChatScreen = nowOnChatScreen;

            if (nowOnChatScreen && userMovedWhileInFront) return;

            // Keep AgyPet above Shell_TrayWnd when docked along the Windows taskbar itself.
            if (IsDockedOnTaskbar())
            {
                EnsureTopmost();
            }

            Point desired = ComputeDesiredPosition();
            targetLeft = desired.X;
            targetTop = desired.Y;
        }

        void TickSmoothGlide()
        {
            DateTime now = DateTime.UtcNow;
            double dt = Math.Min(0.05, Math.Max(0.001, (now - lastAnimTime).TotalSeconds));
            lastAnimTime = now;

            if (dragging || IsAnyMenuActive || double.IsNaN(targetLeft)) return;

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
            tracker.Poll();
            bool hasTargetBox = tracker.IsCliInFront ? !tracker.WindowBoundsPx.IsEmpty : !tracker.ChatBoxBoundsPx.IsEmpty;
            wasOnChatScreen = tracker.IsInFront && !tracker.IsMinimized && hasTargetBox;
            Point desired = ComputeDesiredPosition();
            Left = targetLeft = desired.X;
            Top = targetTop = desired.Y;
            if (IsDockedOnTaskbar())
                EnsureTopmost();
        }

        public static Point PlaceAboveChatBox(Rect chatBoxDip, double spriteW, double spriteH, double padX, double padY)
        {
            double visualRight = chatBoxDip.Right - 14.0;
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

        Point ComputeDesiredPosition()
        {
            double dpiX, dpiY;
            GetDpiScale(out dpiX, out dpiY);
            Rect work = SystemParameters.WorkArea;
            Rect screen = new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
            double spriteW = 11.0 * settings.PixelSize;
            double spriteH = 9.0 * settings.PixelSize;
            double petW = petHitArea.ActualWidth > 10 ? petHitArea.ActualWidth : (spriteW + StagePadX * 2);

            double petLeft, petTop;

            // Perch above Antigravity chatbox OR on active agy CLI terminal window when focused
            if (tracker.IsInFront && !tracker.IsMinimized && tracker.IsCliInFront &&
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
            else if (tracker.IsInFront && !tracker.IsMinimized && !tracker.IsCliInFront && !tracker.ChatBoxBoundsPx.IsEmpty)
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
            else if (settings.HasCustomFreePos)
            {
                petLeft = settings.FreeX;
                petTop = settings.FreeY;
            }
            else
            {
                Point cornerPt = PlaceInCorner(settings.MinimizedCorner, work, screen, spriteW, spriteH, StagePadX, StagePadY);
                petLeft = cornerPt.X;
                petTop = cornerPt.Y;
            }

            bool allowTaskbarBand = !tracker.IsInFront && (settings.HasCustomFreePos ||
                (settings.MinimizedCorner != null && settings.MinimizedCorner.StartsWith("taskbar-", StringComparison.OrdinalIgnoreCase)));
            double maxBottom = allowTaskbarBand ? Math.Max(work.Bottom, screen.Bottom) : work.Bottom;
            double minTop = allowTaskbarBand ? Math.Min(work.Top, screen.Top) : work.Top;

            petLeft = Math.Max(work.Left - StagePadX * 0.5, Math.Min(work.Right - petW + StagePadX * 0.5, petLeft));
            petTop = Math.Max(minTop - StagePadY * 0.4, Math.Min(maxBottom - spriteH - StagePadY, petTop));
            return new Point(petLeft, petTop);
        }

        // ---- Hover Card Popup (active ONLY outside Antigravity) ----

        void ShowCard()
        {
            hoverLeaveTimer.Stop();
            if (!settings.ShowHoverCard || dragging || IsAnyMenuActive || tracker.IsInFront) return;
            RenderHoverContent();

            Rect work = SystemParameters.WorkArea;
            double spriteH = 9.0 * settings.PixelSize;
            bool nearBottom = Top + StagePadY + spriteH > work.Bottom - 70;
            if (nearBottom)
            {
                hoverPopup.Placement = PlacementMode.Top;
                hoverPopup.VerticalOffset = -4;
                hoverPopup.HorizontalOffset = 0;
            }
            else
            {
                bool openOnRight = Left < work.Left + CardWidth + 24;
                hoverPopup.Placement = openOnRight ? PlacementMode.Right : PlacementMode.Left;
                hoverPopup.VerticalOffset = 6;
                hoverPopup.HorizontalOffset = openOnRight ? -10 : 10;
            }

            if (!hoverPopup.IsOpen)
            {
                hoverCard.Opacity = 0;
                hoverPopup.IsOpen = true;
            }
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(140))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            hoverCard.BeginAnimation(OpacityProperty, anim);
        }

        void ScheduleHideCard()
        {
            hoverLeaveTimer.Stop();
            hoverLeaveTimer.Start();
        }

        void HideCard()
        {
            if (!hoverPopup.IsOpen) return;
            var anim = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(120));
            anim.Completed += (s, e) =>
            {
                if (!petHitArea.IsMouseOver && !hoverCard.IsMouseOver)
                    hoverPopup.IsOpen = false;
            };
            hoverCard.BeginAnimation(OpacityProperty, anim);
        }

        // ---- Poke Easter Egg + Free Dragging ----

        void OnPetMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            mouseDown = true;
            dragging = false;
            GetCursorPos(out dragStartCursor);
            dragStartLeft = Left;
            dragStartTop = Top;

            // Double-click when calm triggers joyful dance; single click perched on chatbox/CLI gives subtle wince
            if (mascot != null && e.ClickCount >= 2 &&
                (mascot.Mood == MascotMood.Idle || mascot.Mood == MascotMood.Done))
            {
                mascot.CancelHurt();
                mascot.Dance();
            }
            else if (mascot != null && IsPerchedOnTarget)
            {
                mascot.Poke();
            }

            petHitArea.CaptureMouse();
            e.Handled = true;
        }

        void OnPetMouseMove(object sender, MouseEventArgs e)
        {
            if (!mouseDown) return;
            POINT cur;
            GetCursorPos(out cur);
            double dxPx = cur.X - dragStartCursor.X;
            double dyPx = cur.Y - dragStartCursor.Y;

            if (!dragging)
            {
                if (Math.Abs(dxPx) < DragThresholdPx && Math.Abs(dyPx) < DragThresholdPx) return;
                dragging = true;
                if (mascot != null) mascot.CancelHurt();
                hoverPopup.IsOpen = false;
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
            petHitArea.ReleaseMouseCapture();

            if (!dragging) return;

            dragging = false;
            targetLeft = Left;
            targetTop = Top;

            if (tracker.IsInFront)
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

        public static bool IsPointInsideAnyRect(double x, double y, IList<Rect> rects)
        {
            if (rects == null) return false;
            for (int i = 0; i < rects.Count; i++)
            {
                Rect r = rects[i];
                if (!r.IsEmpty && x >= r.Left && x <= r.Right && y >= r.Top && y <= r.Bottom)
                    return true;
            }
            return false;
        }

        public static bool ShouldDismissMenu(
            bool cursorInsideMenu,
            bool leftJustPressed,
            bool rightJustPressed,
            bool middleJustPressed,
            bool escapePressed,
            bool foregroundMovedAway)
        {
            if (escapePressed || foregroundMovedAway) return true;
            return !cursorInsideMenu && (leftJustPressed || rightJustPressed || middleJustPressed);
        }

        void StartContextMenuWatch()
        {
            menuOpenedForeground = GetForegroundWindow();
            prevMenuLeftDown = IsKeyDown(VK_LBUTTON);
            prevMenuRightDown = IsKeyDown(VK_RBUTTON);
            prevMenuMiddleDown = IsKeyDown(VK_MBUTTON);
            prevMenuEscDown = IsKeyDown(VK_ESCAPE);
            menuDismissTimer.Start();
        }

        void StopContextMenuWatch()
        {
            menuDismissTimer.Stop();
            menuOpenedForeground = IntPtr.Zero;
        }

        static bool AddVisualHandleRect(Visual visual, HashSet<IntPtr> seen, List<Rect> rects)
        {
            if (visual == null) return false;
            var src = PresentationSource.FromVisual(visual) as HwndSource;
            if (src == null || src.Handle == IntPtr.Zero) return false;
            if (seen.Add(src.Handle))
            {
                RECT r;
                if (GetWindowRect(src.Handle, out r) && r.Right > r.Left && r.Bottom > r.Top)
                    rects.Add(new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top));
            }
            return true;
        }

        static void CollectSubmenuRects(ItemsControl parent, HashSet<IntPtr> seen, List<Rect> rects)
        {
            if (parent == null) return;
            foreach (object obj in parent.Items)
            {
                var mi = obj as MenuItem;
                if (mi == null || !mi.IsSubmenuOpen) continue;
                foreach (object child in mi.Items)
                {
                    if (AddVisualHandleRect(child as Visual, seen, rects))
                        break;
                }
                CollectSubmenuRects(mi, seen, rects);
            }
        }

        List<Rect> GetOpenContextMenuRects()
        {
            var rects = new List<Rect>();
            var seen = new HashSet<IntPtr>();
            AddVisualHandleRect(contextMenu, seen, rects);
            CollectSubmenuRects(contextMenu, seen, rects);
            return rects;
        }

        void CheckContextMenuDismiss()
        {
            if (!contextMenuOpen || !contextMenu.IsOpen)
            {
                StopContextMenuWatch();
                return;
            }

            bool leftDown = IsKeyDown(VK_LBUTTON);
            bool rightDown = IsKeyDown(VK_RBUTTON);
            bool middleDown = IsKeyDown(VK_MBUTTON);
            bool escDown = IsKeyDown(VK_ESCAPE);

            bool leftJustPressed = leftDown && !prevMenuLeftDown;
            bool rightJustPressed = rightDown && !prevMenuRightDown;
            bool middleJustPressed = middleDown && !prevMenuMiddleDown;
            bool escJustPressed = escDown && !prevMenuEscDown;
            prevMenuLeftDown = leftDown;
            prevMenuRightDown = rightDown;
            prevMenuMiddleDown = middleDown;
            prevMenuEscDown = escDown;

            IntPtr fg = GetForegroundWindow();
            uint fgPid = 0;
            if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out fgPid);
            bool foregroundMovedAway = fg != IntPtr.Zero && fg != menuOpenedForeground && fgPid != selfPid;

            bool cursorInside = true;
            if (leftJustPressed || rightJustPressed || middleJustPressed)
            {
                var rects = GetOpenContextMenuRects();
                POINT pt;
                if (rects.Count > 0 && GetCursorPos(out pt))
                    cursorInside = IsPointInsideAnyRect(pt.X, pt.Y, rects);
            }

            if (ShouldDismissMenu(cursorInside, leftJustPressed, rightJustPressed, middleJustPressed, escJustPressed, foregroundMovedAway))
            {
                contextMenu.IsOpen = false;
                StopContextMenuWatch();
            }
        }

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

            var cliMenu = new MenuItem { Header = "CLI Terminal Placement" };
            foreach (var opt in PetSettings.CliLocations)
            {
                string value = opt.Id;
                cliMenu.Items.Add(CheckItem(opt.Label, settings.CliPlacement == value, () => SetCliPlacement(value)));
            }
            contextMenu.Items.Add(cliMenu);

            var sizeMenu = new MenuItem { Header = "Size" };
            sizeMenu.Items.Add(CheckItem("Small", settings.Size == "small", () => ApplySize("small")));
            sizeMenu.Items.Add(CheckItem("Medium", settings.Size == "medium", () => ApplySize("medium")));
            sizeMenu.Items.Add(CheckItem("Large", settings.Size == "large", () => ApplySize("large")));
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
                if (!settings.ShowHoverCard) hoverPopup.IsOpen = false;
            }));
            contextMenu.Items.Add(CheckItem("Start with Windows", settings.StartWithWindows, () =>
            {
                settings.StartWithWindows = !settings.StartWithWindows;
                settings.Save();
                PetSettings.ApplyStartup(settings.StartWithWindows, Process.GetCurrentProcess().MainModule.FileName);
            }));
            contextMenu.Items.Add(ActionItem("Snap Back Above Chatbox", ResetPosition));
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

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOZORDER = 0x0004;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_FRAMECHANGED = 0x0020;
        const int WM_MOUSEACTIVATE = 0x0021;
        const int WM_WINDOWPOSCHANGING = 0x0046;
        const int MA_NOACTIVATE = 3;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int VK_LBUTTON = 0x01;
        const int VK_RBUTTON = 0x02;
        const int VK_MBUTTON = 0x04;
        const int VK_ESCAPE = 0x1B;

        static bool IsKeyDown(int vKey)
        {
            return (GetAsyncKeyState(vKey) & 0x8000) != 0;
        }

        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out POINT lpPoint);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    }
}
