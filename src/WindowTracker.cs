using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using Microsoft.Win32;

namespace AgyPet
{
    public struct TaskbarSpan
    {
        public readonly double Start, End;
        public TaskbarSpan(double start, double end) { Start = start; End = end; }
    }

    // Tracks Antigravity App and Antigravity IDE windows, active chat title,
    // outer composer card bounds, user typing state, dictation, and voice mode (listening vs speaking).
    class WindowTracker
    {
        const string MicStore = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
        const string MicOnButton = "Turn off microphone";
        const string MicOffButton = "Turn on microphone";

        public bool IsOpen { get; private set; }
        public bool IsMinimized { get; private set; }
        public bool IsAppOpen { get; private set; }
        public bool IsAppMinimized { get; private set; }
        public bool HasActiveCli { get; private set; }
        public bool HasVisibleCli { get; private set; }
        public bool IsCliMinimized { get; private set; }
        public bool IsInFront { get; private set; }
        public bool IsTargetVisible { get; private set; }
        public bool IsCliInFront { get; private set; }
        public bool TrackCliWindows { get; set; }
        public string CliPlacementMode { get; set; }
        public volatile bool TrackTaskbarItems;
        public volatile bool TrackMusicAudio;
        public Rect WindowBoundsPx { get; private set; }
        public Rect ChatBoxBoundsPx { get; private set; }
        public IntPtr WindowHandle { get; private set; }
        public string ActiveChatTitle { get; private set; }
        public string[] ActiveChatCandidates { get; private set; }

        public bool IsUserTyping { get; private set; }
        public bool InDictation { get; private set; }
        public bool InVoiceMode { get; private set; }
        public bool VoiceAgentSpeaking { get; private set; }
        public bool IsMusicPlaying { get; private set; }

        HashSet<uint> targetPids = new HashSet<uint>();
        HashSet<uint> cliHostPids = new HashSet<uint>();
        HashSet<uint> terminalPids = new HashSet<uint>();
        bool hasActiveCliProcess;
        DateTime pidsRefreshed = DateTime.MinValue;
        readonly uint selfPid = (uint)Process.GetCurrentProcess().Id;

        volatile IntPtr probeHwnd = IntPtr.Zero;
        volatile bool probeActive;
        volatile bool probedVoice, probedDictation, probedAgentSpeaking, probedMusicPlaying;
        DateTime audioFirstSeenUtc = DateTime.MinValue;
        DateTime lastAudioActiveUtc = DateTime.MinValue;
        bool musicPlayingState;
        Rect probedChatBox = Rect.Empty;
        string[] probedChatCandidates = new string[0];
        TaskbarSpan[] probedTaskbarSpansPx = new TaskbarSpan[0];
        DateTime lastTaskbarProbeUtc = DateTime.MinValue;
        readonly object chatLock = new object();
        bool uiaStarted;

        AutomationElement cachedInputEl;
        IntPtr cachedInputHwnd = IntPtr.Zero;
        int probeCycle;
        int loopTick;
        DateTime lastKeystrokeAt = DateTime.MinValue;

        public void Start()
        {
            if (uiaStarted) return;
            uiaStarted = true;
            new Thread(ProbeLoop)
            {
                IsBackground = true,
                Name = "AgyPet UIA Probe",
                Priority = ThreadPriority.BelowNormal
            }.Start();
        }

        public static bool IsAntigravityProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            string n = StripExe(processName);
            return n.Equals("Antigravity", StringComparison.OrdinalIgnoreCase)
                || n.Equals("GoogleAntigravity", StringComparison.OrdinalIgnoreCase)
                || n.Equals("Google Antigravity", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Antigravity ", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Antigravity-", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCliProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            return StripExe(processName).Equals("agy", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTerminalProcessName(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;
            switch (StripExe(processName).ToLowerInvariant())
            {
                case "windowsterminal":
                case "powershell":
                case "pwsh":
                case "cmd":
                case "conhost":
                case "openconsole":
                case "wezterm-gui":
                case "alacritty":
                case "mintty":
                case "hyper":
                case "tabby":
                case "warp":
                    return true;
                default:
                    return false;
            }
        }

        static string StripExe(string name)
        {
            string n = name.Trim();
            return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n;
        }

        void RefreshProcessSets()
        {
            var appPids = new HashSet<uint>();
            var agyPids = new HashSet<uint>();
            var termPids = new HashSet<uint>();
            var parentMap = new Dictionary<uint, uint>();

            IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
            if (snap != IntPtr.Zero && snap != INVALID_HANDLE_VALUE)
            {
                try
                {
                    var pe = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf(typeof(PROCESSENTRY32)) };
                    if (Process32First(snap, ref pe))
                    {
                        do
                        {
                            uint pid = pe.th32ProcessID;
                            if (pid == 0 || pid == selfPid) continue;
                            parentMap[pid] = pe.th32ParentProcessID;
                            string exe = pe.szExeFile ?? "";
                            if (IsAntigravityProcessName(exe)) appPids.Add(pid);
                            else if (IsCliProcessName(exe)) agyPids.Add(pid);
                            else if (IsTerminalProcessName(exe)) termPids.Add(pid);
                        }
                        while (Process32Next(snap, ref pe));
                    }
                }
                finally { CloseHandle(snap); }
            }

            var cliHosts = new HashSet<uint>();
            foreach (uint agyPid in agyPids)
            {
                uint cur = agyPid;
                for (int hop = 0; hop < 12; hop++)
                {
                    cliHosts.Add(cur);
                    uint parent;
                    if (!parentMap.TryGetValue(cur, out parent) || parent == 0 || parent == cur) break;
                    cur = parent;
                }
            }

            targetPids = appPids;
            cliHostPids = cliHosts;
            terminalPids = termPids;
            hasActiveCliProcess = agyPids.Count > 0;
            pidsRefreshed = DateTime.Now;
        }

        public void Poll()
        {
            if ((DateTime.Now - pidsRefreshed).TotalSeconds > 1.2)
                RefreshProcessSets();

            IntPtr visibleApp = IntPtr.Zero, minApp = IntPtr.Zero;
            IntPtr visibleCli = IntPtr.Zero, minCli = IntPtr.Zero;
            IntPtr fg = GetForegroundWindow();
            uint fgPid = 0;
            if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out fgPid);

            EnumWindows((h, l) =>
            {
                if (!IsWindowVisible(h) || GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
                int len = GetWindowTextLength(h);
                if (len <= 0) return true;

                uint pid;
                GetWindowThreadProcessId(h, out pid);
                if (pid == selfPid) return true;

                if (targetPids.Contains(pid))
                {
                    if (IsIconic(h)) { if (minApp == IntPtr.Zero) minApp = h; }
                    else if (h == fg || visibleApp == IntPtr.Zero) visibleApp = h;
                    return true;
                }

                if (TrackCliWindows && hasActiveCliProcess)
                {
                    bool isCliHost = cliHostPids.Contains(pid);
                    bool isFgTerminalFallback = false;
                    if (!isCliHost && terminalPids.Contains(pid))
                    {
                        var sb = new StringBuilder(192);
                        GetWindowText(h, sb, sb.Capacity);
                        string t = sb.ToString();
                        if (h == fg || t.IndexOf("agy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            t.IndexOf("antigravity", StringComparison.OrdinalIgnoreCase) >= 0)
                            isFgTerminalFallback = true;
                    }
                    if (isCliHost || isFgTerminalFallback)
                    {
                        if (IsIconic(h)) { if (minCli == IntPtr.Zero) minCli = h; }
                        else if (h == fg || visibleCli == IntPtr.Zero) visibleCli = h;
                    }
                }
                return true;
            }, IntPtr.Zero);

            bool preferCli = visibleCli != IntPtr.Zero && (visibleCli == fg || visibleApp == IntPtr.Zero);
            IntPtr visible = preferCli ? visibleCli : visibleApp;
            IntPtr minimized = (visible != IntPtr.Zero) ? IntPtr.Zero : (minApp != IntPtr.Zero ? minApp : minCli);

            IsOpen = visible != IntPtr.Zero || minimized != IntPtr.Zero;
            IsMinimized = IsOpen && visible == IntPtr.Zero;
            IsAppOpen = visibleApp != IntPtr.Zero || minApp != IntPtr.Zero;
            IsAppMinimized = minApp != IntPtr.Zero && visibleApp == IntPtr.Zero;
            HasActiveCli = hasActiveCliProcess;
            HasVisibleCli = visibleCli != IntPtr.Zero;
            IsCliMinimized = minCli != IntPtr.Zero && visibleCli == IntPtr.Zero;
            WindowHandle = visible != IntPtr.Zero ? visible : minimized;

            bool fgTransient = fg == IntPtr.Zero;
            bool fgIsSelf = fgPid == selfPid;
            bool fgIsApp = visibleApp != IntPtr.Zero && (visibleApp == fg || (fgPid != 0 && targetPids.Contains(fgPid) && !IsIconic(fg)));
            bool fgIsCli = visibleCli != IntPtr.Zero && (visibleCli == fg || (fgPid != 0 && (cliHostPids.Contains(fgPid) || (hasActiveCliProcess && terminalPids.Contains(fgPid)))));

            bool wasCli = IsCliInFront;
            bool wasTargetVisible = IsTargetVisible;
            bool fgInTarget = visible != IntPtr.Zero && (fgIsApp || fgIsCli || ((fgIsSelf || fgTransient) && IsInFront));

            // Keep sticky target preference when neither App nor CLI holds foreground (e.g. tray open or side-by-side app)
            IntPtr candidateTarget = visible;
            bool candidateIsCli = preferCli;
            if (!fgInTarget && visibleApp != IntPtr.Zero && visibleCli != IntPtr.Zero)
            {
                if (wasCli && wasTargetVisible)
                {
                    candidateTarget = visibleCli;
                    candidateIsCli = true;
                }
                else
                {
                    candidateTarget = visibleApp;
                    candidateIsCli = false;
                }
            }

            bool spotVisible = false;
            if (candidateTarget != IntPtr.Zero)
            {
                if (fgInTarget)
                {
                    spotVisible = true;
                }
                else
                {
                    RECT tr;
                    if (GetVisualWindowRect(candidateTarget, out tr) && tr.Right > tr.Left && tr.Bottom > tr.Top)
                    {
                        Rect targetBounds = new Rect(tr.Left, tr.Top, tr.Right - tr.Left, tr.Bottom - tr.Top);
                        if (candidateIsCli)
                        {
                            Rect cliSpot = ComputeCliSpotPx(targetBounds, CliPlacementMode);
                            spotVisible = IsSpotUncoveredInZOrder(candidateTarget, cliSpot);
                        }
                        else
                        {
                            Rect chatBoxSnapshot;
                            lock (chatLock) { chatBoxSnapshot = probedChatBox; }
                            Rect appSpot = !chatBoxSnapshot.IsEmpty
                                ? ComputeAppSpotPx(chatBoxSnapshot)
                                : new Rect(targetBounds.Left + targetBounds.Width * 0.35, targetBounds.Top + targetBounds.Height * 0.45, Math.Max(40, targetBounds.Width * 0.55), Math.Max(40, targetBounds.Height * 0.45));
                            spotVisible = IsSpotUncoveredInZOrder(candidateTarget, appSpot);
                        }
                    }
                }
            }

            IsInFront = fgInTarget;
            IsTargetVisible = candidateTarget != IntPtr.Zero && (fgInTarget || spotVisible);
            IsCliInFront = IsTargetVisible && (fgInTarget ? (preferCli || fgIsCli || ((fgIsSelf || fgTransient) && wasCli)) : candidateIsCli);
            visible = candidateTarget;
            if (IsTargetVisible) WindowHandle = candidateTarget;

            if (visible != IntPtr.Zero)
            {
                RECT r;
                if (GetWindowRect(visible, out r) && r.Right > r.Left && r.Bottom > r.Top)
                    WindowBoundsPx = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);

                string rawTitle = RawWindowTitle(visible);
                var cands = new List<string>();
                if (!IsCliInFront)
                {
                    string[] uiaCands;
                    lock (chatLock) { uiaCands = probedChatCandidates; }
                    if (uiaCands != null)
                        foreach (var c in uiaCands)
                            AddUniqueCandidate(cands, c);
                }
                foreach (var wt in ParseWindowTitleCandidates(rawTitle))
                    AddUniqueCandidate(cands, wt);

                ActiveChatCandidates = cands.ToArray();
                ActiveChatTitle = cands.Count > 0 ? cands[0] : "";
            }
            else
            {
                WindowBoundsPx = Rect.Empty;
                ActiveChatCandidates = new string[0];
                ActiveChatTitle = "";
            }

            // UIA Electron composer probe runs while Antigravity App / IDE is focused OR uncovered on screen (never CLI terminals)
            bool shouldProbeApp = IsTargetVisible && !IsCliInFront && visible != IntPtr.Zero;
            probeHwnd = shouldProbeApp ? visible : IntPtr.Zero;
            probeActive = shouldProbeApp;

            lock (chatLock)
            {
                ChatBoxBoundsPx = shouldProbeApp ? probedChatBox : Rect.Empty;
            }

            // Detect live user typing strictly while App chatbox OR active CLI terminal is focused
            bool typingSurfaceActive = IsInFront && (IsCliInFront ? !WindowBoundsPx.IsEmpty : !ChatBoxBoundsPx.IsEmpty);
            if (typingSurfaceActive && AnyTypingKeyPressed())
                lastKeystrokeAt = DateTime.UtcNow;

            IsUserTyping = typingSurfaceActive && (DateTime.UtcNow - lastKeystrokeAt).TotalSeconds < 1.6;
            InVoiceMode = IsInFront && !IsCliInFront && probedVoice;
            InDictation = IsInFront && !IsCliInFront && !probedVoice && probedDictation;
            VoiceAgentSpeaking = InVoiceMode && probedAgentSpeaking;
            IsMusicPlaying = TrackMusicAudio && !InVoiceMode && probedMusicPlaying;
        }

        internal static bool UpdateMusicPlaybackState(
            float peak,
            bool inVoiceMode,
            DateTime nowUtc,
            ref DateTime audioFirstSeenUtc,
            ref DateTime lastAudioActiveUtc,
            ref bool wasPlaying)
        {
            return MascotCore.UpdateMusicPlaybackState(
                peak, inVoiceMode, nowUtc, ref audioFirstSeenUtc, ref lastAudioActiveUtc, ref wasPlaying);
        }

        // Checks whether a typing key is currently down without installing any global keyboard hook.
        static bool AnyTypingKeyPressed()
        {
            // Ignore shortcut chords (Ctrl / Alt / Win held) and read key-down strictly without clearing transition bit 0x0001
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0 ||
                (GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0)
                return false;

            for (int vk = 0x41; vk <= 0x5A; vk++)
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) return true;
            for (int vk = 0x30; vk <= 0x39; vk++)
                if ((GetAsyncKeyState(vk) & 0x8000) != 0) return true;
            if ((GetAsyncKeyState(0x20) & 0x8000) != 0 || (GetAsyncKeyState(0x08) & 0x8000) != 0)
                return true;
            return false;
        }

        public static string[] ParseWindowTitleCandidates(string full)
        {
            if (string.IsNullOrWhiteSpace(full)) return new string[0];
            string s = full.Trim();
            const string suffix = " - Antigravity";
            if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - suffix.Length).Trim();
            else if (s.Equals("Antigravity", StringComparison.OrdinalIgnoreCase))
                return new string[0];

            var list = new List<string>();
            string[] parts = s.Split(new[] { " - ", " — ", " | " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Length == 0 ||
                    part.Equals("Antigravity", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Antigravity IDE", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Visual Studio Code", StringComparison.OrdinalIgnoreCase))
                    continue;
                AddUniqueCandidate(list, part);
            }
            return list.ToArray();
        }

        public static string ParseWindowTitle(string full)
        {
            string[] cands = ParseWindowTitleCandidates(full);
            return cands.Length > 0 ? cands[0] : null;
        }

        static string RawWindowTitle(IntPtr hwnd)
        {
            try
            {
                var sb = new StringBuilder(512);
                return GetWindowText(hwnd, sb, sb.Capacity) > 0 ? sb.ToString() : null;
            }
            catch { return null; }
        }

        static void AddUniqueCandidate(List<string> list, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            string clean = value.Trim();
            if (clean.Length < 2) return;
            foreach (string existing in list)
                if (string.Equals(existing, clean, StringComparison.OrdinalIgnoreCase)) return;
            list.Add(clean);
        }

        void ProbeLoop()
        {
            var inputCond = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

            var voiceCond = new OrCondition(
                new PropertyCondition(AutomationElement.NameProperty, MicOnButton),
                new PropertyCondition(AutomationElement.NameProperty, MicOffButton),
                new PropertyCondition(AutomationElement.NameProperty, "End Voice mode"),
                new PropertyCondition(AutomationElement.NameProperty, "End Live chat"));

            var dictationCond = new OrCondition(
                new PropertyCondition(AutomationElement.NameProperty, "Stop dictation"),
                new PropertyCondition(AutomationElement.NameProperty, "Stop recording"),
                new PropertyCondition(AutomationElement.NameProperty, "Dictating"));

            var taskbarItemCond = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.SplitButton));

            while (true)
            {
                try
                {
                    IntPtr h = probeHwnd;
                    if (probeActive && h != IntPtr.Zero)
                    {
                        Rect found = FindComposerCardRect(h, inputCond);
                        lock (chatLock) { probedChatBox = found; }

                        if (loopTick++ % 2 == 0)
                        {
                            ProbeVoiceAndDictation(h, voiceCond, dictationCond);
                            string[] chatCands = ProbeChatTitleCandidates(h, found, cachedInputEl);
                            lock (chatLock) { probedChatCandidates = chatCands; }
                        }
                    }
                    else
                    {
                        cachedInputEl = null;
                        cachedInputHwnd = IntPtr.Zero;
                        probedVoice = false;
                        probedDictation = false;
                        probedAgentSpeaking = false;
                        lock (chatLock) { probedChatCandidates = new string[0]; }
                    }

                    if (TrackTaskbarItems && (DateTime.UtcNow - lastTaskbarProbeUtc).TotalMilliseconds >= 450)
                    {
                        lastTaskbarProbeUtc = DateTime.UtcNow;
                        TaskbarSpan[] spans = ScanTaskbarOccupiedSpansPx(taskbarItemCond);
                        lock (chatLock) { probedTaskbarSpansPx = spans; }
                    }

                    if (TrackMusicAudio)
                    {
                        float peak = AudioMeter.GetSystemOutputPeak();
                        probedMusicPlaying = UpdateMusicPlaybackState(
                            peak,
                            probedVoice,
                            DateTime.UtcNow,
                            ref audioFirstSeenUtc,
                            ref lastAudioActiveUtc,
                            ref musicPlayingState);
                    }
                    else
                    {
                        audioFirstSeenUtc = DateTime.MinValue;
                        lastAudioActiveUtc = DateTime.MinValue;
                        musicPlayingState = false;
                        probedMusicPlaying = false;
                    }
                }
                catch
                {
                    cachedInputEl = null;
                }
                Thread.Sleep(probeActive ? 140 : ((TrackTaskbarItems || TrackMusicAudio) ? 240 : 600));
            }
        }

        public TaskbarSpan[] GetTaskbarOccupiedSpansDip(double dpiX)
        {
            double scale = dpiX > 0.1 ? dpiX : 1.0;
            TaskbarSpan[] px;
            lock (chatLock) { px = probedTaskbarSpansPx; }
            if (px == null || px.Length == 0) return new TaskbarSpan[0];
            var result = new TaskbarSpan[px.Length];
            for (int i = 0; i < px.Length; i++)
                result[i] = new TaskbarSpan(px[i].Start / scale, px[i].End / scale);
            return result;
        }

        public void RefreshTaskbarSpansNow()
        {
            try
            {
                var taskbarItemCond = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.SplitButton));
                lastTaskbarProbeUtc = DateTime.UtcNow;
                TaskbarSpan[] spans = ScanTaskbarOccupiedSpansPx(taskbarItemCond);
                lock (chatLock) { probedTaskbarSpansPx = spans; }
            }
            catch { }
        }

        static TaskbarSpan[] ScanTaskbarOccupiedSpansPx(System.Windows.Automation.Condition itemCond)
        {
            var list = new List<TaskbarSpan>();
            try
            {
                IntPtr tray = FindWindow("Shell_TrayWnd", null);
                if (tray == IntPtr.Zero) return new TaskbarSpan[0];

                RECT trayRect;
                if (!GetWindowRect(tray, out trayRect)) return new TaskbarSpan[0];
                double barW = trayRect.Right - trayRect.Left;
                if (barW < 80) return new TaskbarSpan[0];

                // 1. UIA controls inside Shell_TrayWnd (Widgets, Start, Search, Apps, Tray Icons, Clock, ShowDesktop)
                try
                {
                    var root = AutomationElement.FromHandle(tray);
                    if (root != null)
                    {
                        var els = root.FindAll(TreeScope.Descendants, itemCond);
                        for (int i = 0; i < els.Count; i++)
                        {
                            try
                            {
                                var cur = els[i].Current;
                                if (cur.IsOffscreen) continue;
                                Rect r = cur.BoundingRectangle;
                                if (r.IsEmpty || r.Width < 6 || r.Height < 6) continue;
                                if (r.Width > barW * 0.42) continue; // skip full-bar container panels
                                if (r.Bottom < trayRect.Top - 8 || r.Top > trayRect.Bottom + 8) continue;
                                list.Add(new TaskbarSpan(r.Left, r.Right));
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // 2. Classic Win32 child HWNDs (TrayNotifyWnd, TrayClockWClass, Start, ShowDesktop)
                try
                {
                    EnumChildWindows(tray, (ch, l) =>
                    {
                        if (!IsWindowVisible(ch)) return true;
                        var sb = new StringBuilder(64);
                        GetClassName(ch, sb, sb.Capacity);
                        string cls = sb.ToString();
                        bool want = cls.IndexOf("TrayNotifyWnd", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cls.IndexOf("TrayClock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cls.IndexOf("TrayShowDesktop", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    cls.Equals("Start", StringComparison.OrdinalIgnoreCase);
                        if (!want) return true;

                        RECT cr;
                        if (GetWindowRect(ch, out cr))
                        {
                            double w = cr.Right - cr.Left;
                            if (w >= 6 && w <= barW * 0.42 && cr.Bottom >= trayRect.Top - 8 && cr.Top <= trayRect.Bottom + 8)
                                list.Add(new TaskbarSpan(cr.Left, cr.Right));
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                catch { }
            }
            catch { }
            return list.ToArray();
        }

        void ProbeVoiceAndDictation(IntPtr hwnd, System.Windows.Automation.Condition voiceCond, System.Windows.Automation.Condition dictationCond)
        {
            bool voice = false, muted = false, dictation = false, agentSpeaking = false;
            try
            {
                var win = AutomationElement.FromHandle(hwnd);
                if (win != null)
                {
                    var vEl = win.FindFirst(TreeScope.Descendants, voiceCond);
                    if (vEl != null)
                    {
                        voice = true;
                        muted = string.Equals(vEl.Current.Name, MicOffButton, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        var dEl = win.FindFirst(TreeScope.Descendants, dictationCond);
                        if (dEl != null) dictation = true;
                    }
                }

                if (!voice && !dictation && MicInUseByAntigravity())
                    dictation = true;

                if (voice)
                {
                    // Check live audio output peak: when Antigravity is playing voice audio, mascot speaks;
                    // otherwise when mic is unmuted, user is speaking so mascot listens!
                    float peak = AudioMeter.GetSystemOutputPeak();
                    agentSpeaking = peak > 0.015f || muted;
                }
            }
            catch { }

            probedVoice = voice;
            probedDictation = dictation;
            probedAgentSpeaking = agentSpeaking;
        }

        // Finds the active chat pane container enclosing the focused composer input so header scanning
        // inspects the active chat column cleanly without stopping at unrelated explorer/editor nodes.
        static AutomationElement ResolveChatPaneRoot(AutomationElement inputEl, Rect chatBox, Rect winRect)
        {
            if (inputEl == null || chatBox.IsEmpty) return null;
            var walker = TreeWalker.RawViewWalker;
            AutomationElement cur = inputEl;
            AutomationElement best = null;

            for (int depth = 0; depth < 14 && cur != null; depth++)
            {
                AutomationElement parent;
                try { parent = walker.GetParent(cur); }
                catch { break; }
                if (parent == null) break;

                Rect pr;
                try { pr = parent.Current.BoundingRectangle; }
                catch { break; }

                if (pr.IsEmpty)
                {
                    cur = parent;
                    continue;
                }
                if (pr.Width > Math.Max(chatBox.Width + 220.0, winRect.Width * 0.85))
                    break;

                if (pr.Top <= chatBox.Top - 100.0 && pr.Height >= Math.Max(220.0, chatBox.Height + 110.0))
                    best = parent;

                cur = parent;
            }
            return best;
        }

        // Reads selected conversation items and top-of-pane header labels inside the active Antigravity window
        // so switching chats updates reactions per chat even when the outer window title stays generic.
        static string[] ProbeChatTitleCandidates(IntPtr hwnd, Rect chatBox, AutomationElement inputEl)
        {
            var found = new List<string>();
            try
            {
                RECT wr;
                if (!GetWindowRect(hwnd, out wr)) return new string[0];
                var winRect = new Rect(wr.Left, wr.Top, wr.Right - wr.Left, wr.Bottom - wr.Top);
                var win = AutomationElement.FromHandle(hwnd);
                if (win == null) return new string[0];

                if (!chatBox.IsEmpty)
                {
                    AutomationElement paneRoot = ResolveChatPaneRoot(inputEl, chatBox, winRect) ?? win;
                    Rect paneRect = winRect;
                    try
                    {
                        Rect pr = paneRoot.Current.BoundingRectangle;
                        if (!pr.IsEmpty) paneRect = pr;
                    }
                    catch { }

                    var hdrCond = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
                    var hdrEls = paneRoot.FindAll(TreeScope.Descendants, hdrCond);
                    double minLeft = Math.Max(winRect.Left, chatBox.Left - 80.0);
                    double maxRight = Math.Min(winRect.Right, chatBox.Right + 80.0);
                    double paneTop = Math.Max(winRect.Top + 24.0, paneRect.Top);
                    double maxBottom = Math.Min(chatBox.Top - 16.0, paneTop + 160.0);
                    int visitedVisible = 0;

                    foreach (AutomationElement el in hdrEls)
                    {
                        try
                        {
                            var cur = el.Current;
                            if (cur.IsOffscreen || string.IsNullOrWhiteSpace(cur.Name)) continue;
                            if (visitedVisible++ > 220) break;
                            Rect r = cur.BoundingRectangle;
                            if (r.IsEmpty || r.Height > 44 || r.Width < 24) continue;
                            if (r.Top < paneTop || r.Bottom > maxBottom) continue;
                            if (r.Left < minLeft || r.Right > maxRight) continue;
                            string clean = cur.Name.Trim();
                            if (!IsIgnoredChatLabel(clean))
                                AddUniqueCandidate(found, clean);
                        }
                        catch { }
                    }
                }

                var selCond = new PropertyCondition(AutomationElement.IsSelectionItemPatternAvailableProperty, true);
                var selEls = win.FindAll(TreeScope.Descendants, selCond);
                int selVisited = 0;
                foreach (AutomationElement el in selEls)
                {
                    try
                    {
                        if (el.Current.IsOffscreen) continue;
                        if (selVisited++ > 300) break;
                        object isSelObj = el.GetCurrentPropertyValue(SelectionItemPattern.IsSelectedProperty);
                        if (isSelObj is bool && (bool)isSelObj)
                        {
                            string name = (el.Current.Name ?? "").Trim();
                            if (!IsIgnoredChatLabel(name))
                                AddUniqueCandidate(found, name);
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return found.ToArray();
        }

        static bool IsIgnoredChatLabel(string s)
        {
            if (string.IsNullOrWhiteSpace(s) || s.Length < 2 || s.Length > 120) return true;
            switch (s.ToLowerInvariant())
            {
                case "antigravity":
                case "antigravity ide":
                case "minimize":
                case "maximize":
                case "restore":
                case "close":
                case "file":
                case "edit":
                case "selection":
                case "view":
                case "go":
                case "run":
                case "terminal":
                case "help":
                case "settings":
                case "inbox":
                case "planning":
                case "fast":
                case "ask":
                case "send":
                    return true;
                default:
                    return false;
            }
        }

        static bool MicInUseByAntigravity()
        {
            try
            {
                using (var store = Registry.CurrentUser.OpenSubKey(MicStore))
                {
                    if (store == null) return false;
                    foreach (var name in store.GetSubKeyNames())
                        if (name.StartsWith("Antigravity_", StringComparison.OrdinalIgnoreCase) && IsMicKeyActive(store, name)) return true;

                    using (var unpackaged = store.OpenSubKey("NonPackaged"))
                        if (unpackaged != null)
                            foreach (var name in unpackaged.GetSubKeyNames())
                                if (name.IndexOf("antigravity", StringComparison.OrdinalIgnoreCase) >= 0 && IsMicKeyActive(unpackaged, name)) return true;
                }
            }
            catch { }
            return false;
        }

        static bool IsMicKeyActive(RegistryKey parent, string name)
        {
            using (var key = parent.OpenSubKey(name))
            {
                if (key == null) return false;
                object start = key.GetValue("LastUsedTimeStart"), stop = key.GetValue("LastUsedTimeStop");
                return start != null && stop != null && Convert.ToInt64(start) != 0 && Convert.ToInt64(stop) == 0;
            }
        }

        Rect FindComposerCardRect(IntPtr hwnd, System.Windows.Automation.Condition inputCond)
        {
            RECT wr;
            if (!GetWindowRect(hwnd, out wr)) return Rect.Empty;
            double winW = wr.Right - wr.Left;
            double winH = wr.Bottom - wr.Top;
            if (winW < 200 || winH < 200) return Rect.Empty;

            var winRect = new Rect(wr.Left, wr.Top, winW, winH);

            AutomationElement inputEl = null;
            if (cachedInputEl != null && cachedInputHwnd == hwnd && (probeCycle++ % 10 != 0))
            {
                try
                {
                    var cur = cachedInputEl.Current;
                    if (!cur.IsOffscreen && cur.IsEnabled && !cur.BoundingRectangle.IsEmpty)
                        inputEl = cachedInputEl;
                }
                catch { cachedInputEl = null; }
            }

            if (inputEl == null)
            {
                var win = AutomationElement.FromHandle(hwnd);
                if (win == null) return Rect.Empty;
                inputEl = FindChatInputElement(win, winRect, inputCond);
                cachedInputEl = inputEl;
                cachedInputHwnd = hwnd;
            }

            if (inputEl == null) return Rect.Empty;

            Rect inputRect;
            try { inputRect = inputEl.Current.BoundingRectangle; }
            catch { cachedInputEl = null; return Rect.Empty; }
            if (inputRect.IsEmpty) return Rect.Empty;

            return ResolveOuterComposerCard(inputEl, inputRect, winRect);
        }

        static AutomationElement FindChatInputElement(AutomationElement win, Rect winRect, System.Windows.Automation.Condition inputCond)
        {
            AutomationElement fallback = null;
            double fallbackScore = -1;

            var els = win.FindAll(TreeScope.Descendants, inputCond);
            foreach (AutomationElement el in els)
            {
                try
                {
                    var cur = el.Current;
                    if (cur.IsOffscreen || !cur.IsEnabled) continue;
                    Rect r = cur.BoundingRectangle;
                    if (r.IsEmpty || r.Width < 120 || r.Height < 18 || r.Height > winRect.Height * 0.5) continue;
                    if (r.Left < winRect.Left - 5 || r.Right > winRect.Right + 5) continue;

                    string name = (cur.Name ?? "").ToLowerInvariant();
                    if (name.Contains("message input")) return el;

                    bool isNamedPrompt = name.Contains("prompt") || name.Contains("ask anything") ||
                                         name.Contains("send a message") || name.Contains("reply") ||
                                         name.Contains("chat input");
                    if (isNamedPrompt && r.Top >= winRect.Top + winRect.Height * 0.40)
                    {
                        double score = r.Width * (0.5 + (r.Bottom - winRect.Top) / winRect.Height);
                        if (score > fallbackScore)
                        {
                            fallbackScore = score;
                            fallback = el;
                        }
                    }
                }
                catch { }
            }
            return fallback;
        }

        static readonly string[] AttachmentExtensions =
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".svg", ".pdf",
            ".cs", ".ts", ".tsx", ".js", ".jsx", ".py", ".md", ".json", ".txt",
            ".html", ".css", ".yml", ".yaml", ".xml", ".ps1", ".sh", ".csv"
        };

        internal static bool IsAttachmentLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            if (n.Contains("remove") || n.Contains("delete") || n.Contains("clear") ||
                n.Contains("detach") || n.Contains("dismiss") || n.Contains("attachment") ||
                n.Contains("attached") || n.Contains("screenshot"))
                return true;

            foreach (string ext in AttachmentExtensions)
                if (n.EndsWith(ext, StringComparison.Ordinal) || n.Contains(ext + " "))
                    return true;

            return false;
        }

        internal static bool IsExplicitProjectLabel(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            if (n == "no project" || n.StartsWith("no project", StringComparison.Ordinal) ||
                n.Contains("no project") || n.Contains("select project") ||
                n.Contains("choose project") || n.Contains("switch project") ||
                n.Contains("add project") || n.Contains("open project") ||
                n == "project" || n == "projects" || n == "workspace")
                return true;
            return false;
        }

        internal static bool IsProjectHeaderBand(
            Rect bandRect,
            Rect innerRect,
            Rect outerRect,
            Rect inputRect,
            bool hasAttachmentSignal,
            bool hasExplicitProjectSignal,
            bool hasInteractiveControl)
        {
            if (hasAttachmentSignal) return false;
            if (bandRect.IsEmpty || bandRect.Height < 16 || bandRect.Height > 56) return false;
            if (bandRect.Bottom > inputRect.Top + 4) return false;

            if (hasExplicitProjectSignal) return true;

            // Inner composer already wraps the bottom toolbar, and outer adds only a compact top header bar.
            bool innerHasBottomToolbar = innerRect.Bottom >= inputRect.Bottom + 16;
            bool topOnlyGrowth = (innerRect.Top - outerRect.Top) >= 18 &&
                                 (outerRect.Bottom - innerRect.Bottom) <= 14 &&
                                 (outerRect.Width - innerRect.Width) <= 24;
            return innerHasBottomToolbar && topOnlyGrowth && hasInteractiveControl && bandRect.Height <= 52;
        }

        static void InspectElementsForBand(
            AutomationElement root,
            ref bool hasAttachmentSignal,
            ref bool hasExplicitProjectSignal,
            ref bool hasInteractiveControl)
        {
            var walker = TreeWalker.RawViewWalker;
            var queue = new Queue<AutomationElement>();
            queue.Enqueue(root);
            int visited = 0;

            while (queue.Count > 0 && visited < 24)
            {
                AutomationElement el = queue.Dequeue();
                visited++;
                try
                {
                    var cur = el.Current;
                    if (cur.IsOffscreen) continue;

                    string name = cur.Name ?? "";
                    ControlType ct = cur.ControlType;
                    Rect r = cur.BoundingRectangle;

                    if (ct == ControlType.Image && !r.IsEmpty && r.Width >= 18 && r.Height >= 18)
                        hasAttachmentSignal = true;
                    else if (IsAttachmentLabel(name))
                        hasAttachmentSignal = true;

                    if (IsExplicitProjectLabel(name))
                        hasExplicitProjectSignal = true;

                    if (ct == ControlType.Button || ct == ControlType.ComboBox || ct == ControlType.MenuItem)
                        hasInteractiveControl = true;

                    AutomationElement child = walker.GetFirstChild(el);
                    int childCount = 0;
                    while (child != null && childCount < 10)
                    {
                        queue.Enqueue(child);
                        child = walker.GetNextSibling(child);
                        childCount++;
                    }
                }
                catch { }
            }
        }

        static bool ShouldStopBeforeProjectHeader(
            AutomationElement parent,
            Rect innerRect,
            Rect outerRect,
            Rect inputRect)
        {
            if (innerRect.Bottom < inputRect.Bottom + 16) return false;
            if ((innerRect.Top - outerRect.Top) < 18 || (outerRect.Bottom - innerRect.Bottom) > 14) return false;

            var walker = TreeWalker.RawViewWalker;
            Rect bandRect = Rect.Empty;
            bool hasAttachmentSignal = false;
            bool hasExplicitProjectSignal = false;
            bool hasInteractiveControl = false;

            try
            {
                AutomationElement child = walker.GetFirstChild(parent);
                int checkedCount = 0;
                while (child != null && checkedCount < 12)
                {
                    checkedCount++;
                    try
                    {
                        var cur = child.Current;
                        Rect r = cur.BoundingRectangle;
                        if (!cur.IsOffscreen && !r.IsEmpty && r.Height >= 16 && r.Bottom <= innerRect.Top + 6)
                        {
                            bandRect = bandRect.IsEmpty ? r : Rect.Union(bandRect, r);
                            InspectElementsForBand(child, ref hasAttachmentSignal, ref hasExplicitProjectSignal, ref hasInteractiveControl);
                        }
                    }
                    catch { }
                    child = walker.GetNextSibling(child);
                }
            }
            catch { return false; }

            return IsProjectHeaderBand(
                bandRect,
                innerRect,
                outerRect,
                inputRect,
                hasAttachmentSignal,
                hasExplicitProjectSignal,
                hasInteractiveControl);
        }

        static Rect TrimDirectProjectHeader(AutomationElement cardEl, Rect cardRect, Rect inputRect)
        {
            if (cardEl == null || cardRect.IsEmpty) return cardRect;
            var walker = TreeWalker.RawViewWalker;
            AutomationElement firstBand = null;
            Rect firstRect = Rect.Empty;
            Rect nextRect = Rect.Empty;

            try
            {
                AutomationElement child = walker.GetFirstChild(cardEl);
                int checkedCount = 0;
                while (child != null && checkedCount < 12)
                {
                    checkedCount++;
                    try
                    {
                        var cur = child.Current;
                        Rect r = cur.BoundingRectangle;
                        if (!cur.IsOffscreen && !r.IsEmpty && r.Height >= 12)
                        {
                            if (firstBand == null)
                            {
                                firstBand = child;
                                firstRect = r;
                            }
                            else if (r.Top >= firstRect.Bottom - 4)
                            {
                                nextRect = r;
                                break;
                            }
                        }
                    }
                    catch { }
                    child = walker.GetNextSibling(child);
                }
            }
            catch { return cardRect; }

            if (firstBand == null || nextRect.IsEmpty) return cardRect;
            if (firstRect.Top > cardRect.Top + 14 || firstRect.Bottom > inputRect.Top + 4) return cardRect;
            if (firstRect.Height < 16 || firstRect.Height > 56) return cardRect;
            if (nextRect.Top > inputRect.Top + 6) return cardRect;

            bool hasAttachment = false, hasProject = false, hasInteractive = false;
            InspectElementsForBand(firstBand, ref hasAttachment, ref hasProject, ref hasInteractive);

            bool compactNamedProjectHeader = hasInteractive && firstRect.Height <= 46 &&
                                             (cardRect.Bottom - nextRect.Top) >= inputRect.Height + 16;
            if (!hasAttachment && (hasProject || compactNamedProjectHeader))
            {
                double newTop = (nextRect.Top - firstRect.Bottom) > 4
                    ? Math.Max(firstRect.Bottom + 1, nextRect.Top - 4)
                    : nextRect.Top;
                if (newTop > cardRect.Top && newTop <= inputRect.Top + 2 && (cardRect.Bottom - newTop) >= inputRect.Height + 16)
                    return new Rect(cardRect.Left, newTop, cardRect.Width, cardRect.Bottom - newTop);
            }

            return cardRect;
        }

        static Rect ResolveOuterComposerCard(AutomationElement inputEl, Rect inputRect, Rect winRect)
        {
            var walker = TreeWalker.RawViewWalker;
            Rect card = Rect.Empty;
            AutomationElement cardEl = null;
            AutomationElement cur = inputEl;

            for (int depth = 0; depth < 9 && cur != null; depth++)
            {
                AutomationElement parent;
                try { parent = walker.GetParent(cur); }
                catch { break; }
                if (parent == null) break;

                Rect pr;
                try { pr = parent.Current.BoundingRectangle; }
                catch { break; }

                if (pr.IsEmpty)
                {
                    cur = parent;
                    continue;
                }
                if (pr.Left > inputRect.Left + 4 || pr.Right < inputRect.Right - 4)
                {
                    cur = parent;
                    continue;
                }
                if (pr.Top > inputRect.Top + 4 || pr.Bottom < inputRect.Bottom - 4)
                {
                    cur = parent;
                    continue;
                }

                if (pr.Width > inputRect.Width + 140) break;
                if (pr.Height > winRect.Height * 0.62) break;
                if (pr.Top < winRect.Top + winRect.Height * 0.20) break;
                if (pr.Bottom > inputRect.Bottom + 110) break;

                if (!card.IsEmpty && ShouldStopBeforeProjectHeader(parent, card, pr, inputRect))
                    break;

                cur = parent;

                if (pr.Height >= inputRect.Height + 16 || pr.Width >= inputRect.Width + 8)
                {
                    card = pr;
                    cardEl = parent;
                }
            }

            if (!card.IsEmpty)
            {
                card = TrimDirectProjectHeader(cardEl, card, inputRect);
                double clampedLeft = Math.Max(winRect.Left + 8, card.Left);
                double clampedTop = Math.Max(winRect.Top + 36, card.Top);
                return new Rect(
                    clampedLeft,
                    clampedTop,
                    Math.Min(winRect.Right - 8 - clampedLeft, card.Width),
                    Math.Max(20, card.Bottom - clampedTop));
            }

            double left = Math.Max(winRect.Left + 12, inputRect.Left - 16);
            double top = Math.Max(winRect.Top + 40, inputRect.Top - 22);
            double right = Math.Min(winRect.Right - 14, inputRect.Right + Math.Min(36, winRect.Width * 0.03));
            return new Rect(left, top, Math.Max(80, right - left), inputRect.Height + 36);
        }

        internal static Rect ComputeAppSpotPx(Rect chatBoxPx)
        {
            double rightInset = Math.Min(28.0, Math.Max(14.0, chatBoxPx.Width * 0.12));
            double spotW = 48.0;
            double spotH = 40.0;
            return new Rect(chatBoxPx.Right - rightInset - spotW, chatBoxPx.Top - spotH + 4.0, spotW, spotH);
        }

        internal static Rect ComputeCliSpotPx(Rect winPx, string cliPlacement)
        {
            string mode = string.IsNullOrWhiteSpace(cliPlacement) ? "bottom-right" : cliPlacement.Trim().ToLowerInvariant();
            double spotW = 48.0;
            double spotH = 40.0;
            double x = mode.EndsWith("-left", StringComparison.Ordinal) ? (winPx.Left + 22.0)
                : mode.EndsWith("-center", StringComparison.Ordinal) ? (winPx.Left + (winPx.Width - spotW) * 0.5)
                : (winPx.Right - 22.0 - spotW);
            double y = mode.StartsWith("top-", StringComparison.Ordinal)
                ? (winPx.Top - spotH + 4.0)
                : (winPx.Bottom - 20.0 - spotH);
            return new Rect(x, y, spotW, spotH);
        }

        internal static bool IsSpotOccludedByWindows(Rect spotPx, IEnumerable<Rect> aboveWindowRects)
        {
            if (spotPx.IsEmpty || spotPx.Width <= 0 || spotPx.Height <= 0) return false;
            double spotArea = spotPx.Width * spotPx.Height;
            double cx = spotPx.Left + spotPx.Width * 0.5;
            double cy = spotPx.Top + spotPx.Height * 0.5;

            foreach (Rect r in aboveWindowRects)
            {
                if (r.IsEmpty || r.Width <= 4 || r.Height <= 4) continue;
                if (cx >= r.Left && cx <= r.Right && cy >= r.Top && cy <= r.Bottom)
                    return true;

                Rect inter = Rect.Intersect(spotPx, r);
                if (!inter.IsEmpty && (inter.Width * inter.Height) >= spotArea * 0.35)
                    return true;
            }
            return false;
        }

        bool IsSpotUncoveredInZOrder(IntPtr targetHwnd, Rect spotPx)
        {
            if (targetHwnd == IntPtr.Zero || spotPx.IsEmpty) return false;
            var aboveRects = new List<Rect>();
            IntPtr cur = targetHwnd;
            int safety = 0;

            while (safety++ < 256)
            {
                cur = GetWindow(cur, GW_HWNDPREV);
                if (cur == IntPtr.Zero) break;
                if (!IsWindowVisible(cur) || IsIconic(cur)) continue;

                uint pid;
                GetWindowThreadProcessId(cur, out pid);
                if (pid == selfPid) continue;

                int ex = GetWindowLong(cur, GWL_EXSTYLE);
                if ((ex & WS_EX_TRANSPARENT) != 0 && (ex & WS_EX_LAYERED) != 0) continue;
                if (IsWindowCloaked(cur)) continue;

                RECT r;
                if (!GetVisualWindowRect(cur, out r)) continue;
                int w = r.Right - r.Left;
                int h = r.Bottom - r.Top;
                if (w <= 4 || h <= 4) continue;

                aboveRects.Add(new Rect(r.Left, r.Top, w, h));
            }

            return !IsSpotOccludedByWindows(spotPx, aboveRects);
        }

        static bool IsWindowCloaked(IntPtr hwnd)
        {
            try
            {
                int cloaked;
                return DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out cloaked, Marshal.SizeOf(typeof(int))) == 0 && cloaked != 0;
            }
            catch { return false; }
        }

        static bool GetVisualWindowRect(IntPtr hwnd, out RECT rect)
        {
            try
            {
                if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out rect, Marshal.SizeOf(typeof(RECT))) == 0 &&
                    rect.Right > rect.Left && rect.Bottom > rect.Top)
                    return true;
            }
            catch { }
            return GetWindowRect(hwnd, out rect);
        }

        // Zero-dependency Windows Core Audio peak meter to distinguish Antigravity speaking vs user speaking in Voice Mode
        static class AudioMeter
        {
            static readonly Guid CLSID_MMDeviceEnumerator = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            static readonly Guid IID_IAudioMeterInformation = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");

            [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IMMDeviceEnumerator
            {
                int NotImpl1();
                [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
            }

            [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IMMDevice
            {
                [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
            }

            [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
            interface IAudioMeterInformation
            {
                [PreserveSig] int GetPeakValue(out float pfPeak);
            }

            public static float GetSystemOutputPeak()
            {
                try
                {
                    Type t = Type.GetTypeFromCLSID(CLSID_MMDeviceEnumerator);
                    if (t == null) return 0f;
                    var enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(t);
                    IMMDevice dev;
                    if (enumerator.GetDefaultAudioEndpoint(0, 1, out dev) != 0 || dev == null) return 0f;
                    object obj;
                    Guid iid = IID_IAudioMeterInformation;
                    if (dev.Activate(ref iid, 1, IntPtr.Zero, out obj) != 0 || obj == null) return 0f;
                    float peak;
                    ((IAudioMeterInformation)obj).GetPeakValue(out peak);
                    return peak;
                }
                catch { return 0f; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct PROCESSENTRY32
        {
            public uint dwSize, cntUsage, th32ProcessID;
            public IntPtr th32DefaultHeapID;
            public uint th32ModuleID, cntThreads, th32ParentProcessID;
            public int pcPriClassBase;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeFile;
        }

        const uint GW_HWNDPREV = 3;
        const uint GW_OWNER = 4;
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        const int WS_EX_LAYERED = 0x00080000;
        const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        const int DWMWA_CLOAKED = 14;
        const uint TH32CS_SNAPPROCESS = 0x00000002;
        static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);
        [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc fn, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr hParent, EnumWindowsProc fn, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);
        [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr hObject);
    }
}
