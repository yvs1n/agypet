using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace AgyPet
{
    public struct ScreenRectDip
    {
        public readonly double Left, Top, Width, Height;
        public double Right { get { return Left + Width; } }
        public double Bottom { get { return Top + Height; } }
        public bool IsEmpty { get { return Width <= 0 || Height <= 0; } }

        public ScreenRectDip(double left, double top, double width, double height)
        {
            Left = left; Top = top; Width = width; Height = height;
        }
    }

    // Native cross-platform tracker for Antigravity App / IDE / agy CLI windows & global cursor coordinates:
    //  - macOS: CoreGraphics CGWindowListCopyWindowInfo + CoreFoundation (zero Accessibility prompt required) + CGEventGetLocation
    //  - Linux X11/XWayland: libX11.so.6 _NET_CLIENT_LIST_STACKING / _NET_ACTIVE_WINDOW / _NET_WM_PID / XQueryPointer
    //  - Wayland fallback: Process + /proc/<pid>/cmdline inspection so sessions & moods stay live without X11
    public class PlatformWindowTracker
    {
        public bool IsOpen { get; private set; }
        public bool IsMinimized { get; private set; }
        public bool IsAppOpen { get; private set; }
        public bool IsAppMinimized { get; private set; }
        public bool HasActiveCli { get; private set; }
        public bool HasVisibleCli { get; private set; }
        public bool IsCliMinimized { get; private set; }
        public bool IsInFront { get; private set; }
        public bool IsCliInFront { get; private set; }
        public bool IsAnyAntigravityOpen { get { return IsAppOpen || HasActiveCli; } }
        public bool TrackCliWindows { get; set; }
        public volatile bool TrackMusicAudio;
        public bool IsMusicPlaying { get; private set; }
        public ScreenRectDip WindowBoundsDip { get; private set; }
        public string ActiveChatTitle { get; private set; }
        public string[] ActiveChatCandidates { get; private set; }

        readonly HashSet<int> appPids = new HashSet<int>();
        readonly HashSet<int> cliHostPids = new HashSet<int>();
        readonly HashSet<int> terminalPids = new HashSet<int>();
        bool hasCliProcess;
        DateTime pidsRefreshed = DateTime.MinValue;
        DateTime audioFirstSeenUtc = DateTime.MinValue;
        DateTime lastAudioActiveUtc = DateTime.MinValue;
        DateTime lastAudioProbeUtc = DateTime.MinValue;
        DateTime lastLinuxPactlUtc = DateTime.MinValue;
        bool musicPlayingState;
        volatile float latestAudioPeak;
        volatile bool audioProbeInFlight;
        readonly int selfPid = Environment.ProcessId;

        public static bool IsCliProcessName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim();
            if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
            return n.Equals("agy", StringComparison.OrdinalIgnoreCase)
                || n.Equals("agy-cli", StringComparison.OrdinalIgnoreCase)
                || n.Equals("antigravity-cli", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAntigravityName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim();
            if (n.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
            if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) n = n.Substring(0, n.Length - 4);
            return n.Equals("Antigravity", StringComparison.OrdinalIgnoreCase)
                || n.Equals("GoogleAntigravity", StringComparison.OrdinalIgnoreCase)
                || n.Equals("Google Antigravity", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Antigravity ", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Antigravity-", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsTerminalName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            switch (n)
            {
                case "terminal":
                case "iterm2":
                case "iterm":
                case "ghostty":
                case "warp":
                case "alacritty":
                case "kitty":
                case "wezterm":
                case "wezterm-gui":
                case "hyper":
                case "tabby":
                case "gnome-terminal":
                case "gnome-terminal-server":
                case "konsole":
                case "xfce4-terminal":
                case "mate-terminal":
                case "tilix":
                case "terminator":
                case "foot":
                case "ptyxis":
                case "kgx":
                case "xterm":
                case "rxvt":
                case "urxvt":
                    return true;
                default:
                    return false;
            }
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
                if (part.Length < 2 ||
                    part.Equals("Antigravity", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Antigravity IDE", StringComparison.OrdinalIgnoreCase) ||
                    part.Equals("Visual Studio Code", StringComparison.OrdinalIgnoreCase))
                    continue;
                bool dup = false;
                for (int i = 0; i < list.Count; i++)
                    if (string.Equals(list[i], part, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (!dup) list.Add(part);
            }
            return list.ToArray();
        }

        public void Poll(double dpiScale)
        {
            RefreshProcesses();
            HasActiveCli = hasCliProcess || cliHostPids.Count > 0;
            UpdateAudioState();

            if (OperatingSystem.IsMacOS())
            {
                PollMacOs();
                return;
            }
            if (OperatingSystem.IsLinux())
            {
                PollLinux(dpiScale > 0.1 ? dpiScale : 1.0);
                return;
            }

            // Fallback (when testing cross-platform runner on Windows)
            IsAppOpen = appPids.Count > 0;
            IsOpen = IsAppOpen || HasActiveCli;
            IsAppMinimized = false;
            IsCliMinimized = false;
            HasVisibleCli = HasActiveCli;
            IsInFront = false;
            IsCliInFront = false;
            WindowBoundsDip = default(ScreenRectDip);
            ActiveChatTitle = "";
            ActiveChatCandidates = new string[0];
        }

        public static bool TryGetCursorDip(double dpiScale, out double x, out double y)
        {
            x = 0;
            y = 0;
            try
            {
                if (OperatingSystem.IsMacOS())
                    return MacNative.TryGetCursorLocation(out x, out y);
                if (OperatingSystem.IsLinux())
                    return X11Native.TryGetCursorDip(dpiScale > 0.1 ? dpiScale : 1.0, out x, out y);
            }
            catch { }
            return false;
        }

        void RefreshProcesses()
        {
            if ((DateTime.UtcNow - pidsRefreshed).TotalMilliseconds < 1200) return;
            pidsRefreshed = DateTime.UtcNow;

            appPids.Clear();
            cliHostPids.Clear();
            terminalPids.Clear();
            hasCliProcess = false;

            var ppidMap = new Dictionary<int, int>();
            var procByPid = new Dictionary<int, string>();
            var agyPids = new List<int>();

            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.Id == selfPid) continue;
                        string pname = p.ProcessName ?? "";
                        procByPid[p.Id] = pname;
                        if (IsCliProcessName(pname))
                            agyPids.Add(p.Id);
                        else if (IsAntigravityName(pname))
                            appPids.Add(p.Id);
                        if (IsTerminalName(pname))
                            terminalPids.Add(p.Id);

                        if (OperatingSystem.IsLinux())
                        {
                            int ppid = ReadLinuxParentPid(p.Id);
                            if (ppid > 0) ppidMap[p.Id] = ppid;
                            if (pname.Equals("node", StringComparison.OrdinalIgnoreCase) && LinuxCmdContainsAgy(p.Id))
                                agyPids.Add(p.Id);
                        }
                    }
                    catch { }
                    finally { try { p.Dispose(); } catch { } }
                }

                hasCliProcess = agyPids.Count > 0;

                if (OperatingSystem.IsMacOS() && agyPids.Count > 0)
                    PopulateMacParents(ppidMap);

                foreach (int pid in agyPids)
                {
                    int cur = pid;
                    for (int hop = 0; hop < 8; hop++)
                    {
                        if (terminalPids.Contains(cur)) { cliHostPids.Add(cur); break; }
                        int next;
                        if (!ppidMap.TryGetValue(cur, out next) || next <= 1 || next == cur) break;
                        cur = next;
                    }
                    if (cliHostPids.Count == 0 && TrackCliWindows)
                    {
                        foreach (int t in terminalPids) cliHostPids.Add(t);
                    }
                }
            }
            catch { }
        }

        static int ReadLinuxParentPid(int pid)
        {
            try
            {
                string stat = File.ReadAllText("/proc/" + pid + "/stat");
                int close = stat.LastIndexOf(')');
                if (close < 0 || close + 2 >= stat.Length) return 0;
                string[] rest = stat.Substring(close + 2).Split(' ');
                int ppid;
                if (rest.Length >= 2 && int.TryParse(rest[1], out ppid)) return ppid;
            }
            catch { }
            return 0;
        }

        static bool LinuxCmdContainsAgy(int pid)
        {
            try
            {
                string cmd = File.ReadAllText("/proc/" + pid + "/cmdline").Replace('\0', ' ');
                return cmd.IndexOf("/agy", StringComparison.OrdinalIgnoreCase) >= 0
                    || cmd.IndexOf("agy-cli", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        static void PopulateMacParents(Dictionary<int, int> ppidMap)
        {
            try
            {
                var psi = new ProcessStartInfo("ps", "-eo pid=,ppid=")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return;
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(500);
                    foreach (string line in output.Split('\n'))
                    {
                        var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        int p, pp;
                        if (parts.Length >= 2 && int.TryParse(parts[0], out p) && int.TryParse(parts[1], out pp))
                            ppidMap[p] = pp;
                    }
                }
            }
            catch { }
        }

        void PollMacOs()
        {
            List<MacNative.WinInfo> wins = MacNative.GetOnScreenWindows();
            int frontIdx = -1;
            int frontAppIdx = -1;
            int frontCliIdx = -1;
            bool anyAppVisible = false;
            bool anyCliVisible = false;

            for (int i = 0; i < wins.Count; i++)
            {
                var w = wins[i];
                if (w.OwnerPid == selfPid || w.Layer != 0 || w.Bounds.Width < 120 || w.Bounds.Height < 100) continue;
                if (frontIdx < 0) frontIdx = i;

                bool isApp = appPids.Contains(w.OwnerPid) || IsAntigravityName(w.OwnerName);
                bool isCli = TrackCliWindows && (cliHostPids.Contains(w.OwnerPid) ||
                    (cliHostPids.Count > 0 && IsTerminalName(w.OwnerName)));

                if (isApp)
                {
                    anyAppVisible = true;
                    if (frontAppIdx < 0) frontAppIdx = i;
                }
                else if (isCli)
                {
                    anyCliVisible = true;
                    if (frontCliIdx < 0) frontCliIdx = i;
                }
            }

            IsAppOpen = appPids.Count > 0 || anyAppVisible;
            IsAppMinimized = IsAppOpen && !anyAppVisible;
            HasVisibleCli = anyCliVisible;
            IsCliMinimized = TrackCliWindows && HasActiveCli && !anyCliVisible;
            IsOpen = IsAppOpen || (TrackCliWindows && HasActiveCli);

            if (frontAppIdx >= 0 && frontIdx == frontAppIdx)
            {
                var w = wins[frontAppIdx];
                IsInFront = true;
                IsCliInFront = false;
                IsMinimized = false;
                WindowBoundsDip = w.Bounds;
                var cands = ParseWindowTitleCandidates(w.Title);
                ActiveChatCandidates = cands;
                ActiveChatTitle = cands.Length > 0 ? cands[0] : "";
                return;
            }

            if (frontCliIdx >= 0 && frontIdx == frontCliIdx)
            {
                var w = wins[frontCliIdx];
                IsInFront = true;
                IsCliInFront = true;
                IsMinimized = false;
                WindowBoundsDip = w.Bounds;
                ActiveChatTitle = "";
                ActiveChatCandidates = new string[0];
                return;
            }

            IsInFront = false;
            IsCliInFront = false;
            IsMinimized = IsAppMinimized || (!IsAppOpen && IsCliMinimized);
            WindowBoundsDip = default(ScreenRectDip);
            ActiveChatTitle = "";
            ActiveChatCandidates = new string[0];
        }

        void PollLinux(double dpiScale)
        {
            List<X11Native.XWinInfo> wins = X11Native.QueryWindows(dpiScale);
            if (wins == null)
            {
                // Pure Wayland or headless fallback: keep session tracking accurate via process checks
                IsAppOpen = appPids.Count > 0;
                IsAppMinimized = false;
                HasVisibleCli = HasActiveCli;
                IsCliMinimized = false;
                IsOpen = IsAppOpen || (TrackCliWindows && HasActiveCli);
                IsInFront = false;
                IsCliInFront = false;
                IsMinimized = false;
                WindowBoundsDip = default(ScreenRectDip);
                ActiveChatTitle = "";
                ActiveChatCandidates = new string[0];
                return;
            }

            bool anyAppVisible = false, anyAppHidden = false;
            bool anyCliVisible = false, anyCliHidden = false;
            X11Native.XWinInfo activeApp = null, activeCli = null;

            for (int i = 0; i < wins.Count; i++)
            {
                var w = wins[i];
                if (w.Pid == selfPid) continue;
                bool isApp = appPids.Contains(w.Pid) || (!string.IsNullOrEmpty(w.Title) && w.Title.EndsWith("Antigravity", StringComparison.OrdinalIgnoreCase));
                bool isCli = TrackCliWindows && cliHostPids.Contains(w.Pid);
                if (isApp)
                {
                    if (w.IsMinimized || w.Bounds.Width < 120) anyAppHidden = true;
                    else
                    {
                        anyAppVisible = true;
                        if (w.IsActive && activeApp == null) activeApp = w;
                    }
                }
                else if (isCli)
                {
                    if (w.IsMinimized || w.Bounds.Width < 120) anyCliHidden = true;
                    else
                    {
                        anyCliVisible = true;
                        if (w.IsActive && activeCli == null) activeCli = w;
                    }
                }
            }

            IsAppOpen = appPids.Count > 0 || anyAppVisible || anyAppHidden;
            IsAppMinimized = IsAppOpen && !anyAppVisible;
            HasVisibleCli = anyCliVisible;
            IsCliMinimized = TrackCliWindows && (HasActiveCli || anyCliHidden) && !anyCliVisible;
            IsOpen = IsAppOpen || (TrackCliWindows && HasActiveCli);

            if (activeApp != null)
            {
                IsInFront = true;
                IsCliInFront = false;
                IsMinimized = false;
                WindowBoundsDip = activeApp.Bounds;
                var cands = ParseWindowTitleCandidates(activeApp.Title);
                ActiveChatCandidates = cands;
                ActiveChatTitle = cands.Length > 0 ? cands[0] : "";
                return;
            }

            if (activeCli != null)
            {
                IsInFront = true;
                IsCliInFront = true;
                IsMinimized = false;
                WindowBoundsDip = activeCli.Bounds;
                ActiveChatTitle = "";
                ActiveChatCandidates = new string[0];
                return;
            }

            IsInFront = false;
            IsCliInFront = false;
            IsMinimized = IsAppMinimized || (!IsAppOpen && IsCliMinimized);
            WindowBoundsDip = default(ScreenRectDip);
            ActiveChatTitle = "";
            ActiveChatCandidates = new string[0];
        }

        static class MacNative
        {
            public class WinInfo
            {
                public int OwnerPid, Layer;
                public string OwnerName, Title;
                public ScreenRectDip Bounds;
            }

            const string CG = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";
            const string CF = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
            const uint kCGWindowListOptionOnScreenOnly = 1;
            const uint kCGWindowListExcludeDesktopElements = 16;
            const uint kCFStringEncodingUTF8 = 0x08000100;
            const int kCFNumberSInt32Type = 3;

            [StructLayout(LayoutKind.Sequential)]
            struct CGPoint { public double X, Y; }
            [StructLayout(LayoutKind.Sequential)]
            struct CGSize { public double Width, Height; }
            [StructLayout(LayoutKind.Sequential)]
            struct CGRect { public CGPoint Origin; public CGSize Size; }

            [DllImport(CG)] static extern IntPtr CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);
            [DllImport(CG)] static extern bool CGRectMakeWithDictionaryRepresentation(IntPtr dict, out CGRect rect);
            [DllImport(CG)] static extern IntPtr CGEventCreate(IntPtr source);
            [DllImport(CG)] static extern CGPoint CGEventGetLocation(IntPtr ev);

            [DllImport(CF)] static extern nint CFArrayGetCount(IntPtr theArray);
            [DllImport(CF)] static extern IntPtr CFArrayGetValueAtIndex(IntPtr theArray, nint idx);
            [DllImport(CF)] static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);
            [DllImport(CF)] static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);
            [DllImport(CF)] static extern bool CFStringGetCString(IntPtr theString, byte[] buffer, nint bufferSize, uint encoding);
            [DllImport(CF)] static extern bool CFNumberGetValue(IntPtr number, int theType, out int value);
            [DllImport(CF)] static extern void CFRelease(IntPtr cf);

            static readonly IntPtr KeyOwnerName = CfKey("kCGWindowOwnerName");
            static readonly IntPtr KeyOwnerPid = CfKey("kCGWindowOwnerPID");
            static readonly IntPtr KeyLayer = CfKey("kCGWindowLayer");
            static readonly IntPtr KeyBounds = CfKey("kCGWindowBounds");
            static readonly IntPtr KeyName = CfKey("kCGWindowName");

            static IntPtr CfKey(string name)
            {
                try { return OperatingSystem.IsMacOS() ? CFStringCreateWithCString(IntPtr.Zero, name, kCFStringEncodingUTF8) : IntPtr.Zero; }
                catch { return IntPtr.Zero; }
            }

            public static bool TryGetCursorLocation(out double x, out double y)
            {
                x = 0; y = 0;
                IntPtr ev = CGEventCreate(IntPtr.Zero);
                if (ev == IntPtr.Zero) return false;
                try
                {
                    CGPoint pt = CGEventGetLocation(ev);
                    x = pt.X;
                    y = pt.Y;
                    return true;
                }
                finally { CFRelease(ev); }
            }

            public static List<WinInfo> GetOnScreenWindows()
            {
                var list = new List<WinInfo>();
                IntPtr arr = IntPtr.Zero;
                try
                {
                    arr = CGWindowListCopyWindowInfo(kCGWindowListOptionOnScreenOnly | kCGWindowListExcludeDesktopElements, 0);
                    if (arr == IntPtr.Zero) return list;
                    nint count = CFArrayGetCount(arr);
                    byte[] buf = new byte[512];
                    for (nint i = 0; i < count; i++)
                    {
                        IntPtr dict = CFArrayGetValueAtIndex(arr, i);
                        if (dict == IntPtr.Zero) continue;
                        int pid = GetInt(dict, KeyOwnerPid);
                        int layer = GetInt(dict, KeyLayer);
                        string owner = GetStr(dict, KeyOwnerName, buf);
                        string title = GetStr(dict, KeyName, buf);
                        CGRect rect;
                        IntPtr boundsRef = CFDictionaryGetValue(dict, KeyBounds);
                        if (boundsRef == IntPtr.Zero || !CGRectMakeWithDictionaryRepresentation(boundsRef, out rect)) continue;
                        list.Add(new WinInfo
                        {
                            OwnerPid = pid,
                            Layer = layer,
                            OwnerName = owner,
                            Title = title,
                            Bounds = new ScreenRectDip(rect.Origin.X, rect.Origin.Y, rect.Size.Width, rect.Size.Height)
                        });
                    }
                }
                catch { }
                finally { if (arr != IntPtr.Zero) CFRelease(arr); }
                return list;
            }

            static int GetInt(IntPtr dict, IntPtr key)
            {
                if (key == IntPtr.Zero) return 0;
                IntPtr v = CFDictionaryGetValue(dict, key);
                int res;
                return v != IntPtr.Zero && CFNumberGetValue(v, kCFNumberSInt32Type, out res) ? res : 0;
            }

            static string GetStr(IntPtr dict, IntPtr key, byte[] buf)
            {
                if (key == IntPtr.Zero) return "";
                IntPtr v = CFDictionaryGetValue(dict, key);
                if (v == IntPtr.Zero || !CFStringGetCString(v, buf, buf.Length, kCFStringEncodingUTF8)) return "";
                int len = Array.IndexOf(buf, (byte)0);
                return Encoding.UTF8.GetString(buf, 0, len >= 0 ? len : buf.Length);
            }
        }

        static class X11Native
        {
            public class XWinInfo
            {
                public int Pid;
                public string Title;
                public bool IsActive, IsMinimized;
                public ScreenRectDip Bounds;
            }

            const string LibX11 = "libX11.so.6";

            [DllImport(LibX11)] static extern IntPtr XOpenDisplay(IntPtr display);
            [DllImport(LibX11)] static extern int XCloseDisplay(IntPtr display);
            [DllImport(LibX11)] static extern IntPtr XDefaultRootWindow(IntPtr display);
            [DllImport(LibX11)] static extern IntPtr XInternAtom(IntPtr display, string name, bool onlyIfExists);
            [DllImport(LibX11)] static extern int XFree(IntPtr data);
            [DllImport(LibX11)] static extern int XGetWindowProperty(
                IntPtr display, IntPtr w, IntPtr property, nint longOffset, nint longLength, bool delete,
                IntPtr reqType, out IntPtr actualType, out int actualFormat, out nuint nitems, out nuint bytesAfter, out IntPtr prop);
            [DllImport(LibX11)] static extern int XGetWindowAttributes(IntPtr display, IntPtr w, out XWindowAttributes attrs);
            [DllImport(LibX11)] static extern bool XTranslateCoordinates(
                IntPtr display, IntPtr srcW, IntPtr destW, int srcX, int srcY, out int destX, out int destY, out IntPtr child);
            [DllImport(LibX11)] static extern bool XQueryPointer(
                IntPtr display, IntPtr w, out IntPtr rootRet, out IntPtr childRet,
                out int rootX, out int rootY, out int winX, out int winY, out uint mask);

            [StructLayout(LayoutKind.Sequential)]
            struct XWindowAttributes
            {
                public int x, y, width, height, border_width, depth;
                public IntPtr visual, root;
                public int c_class, bit_gravity, win_gravity, backing_store;
                public nuint backing_planes, backing_pixel;
                public int save_under;
                public IntPtr colormap;
                public int map_installed, map_state;
                public nint all_event_masks, your_event_mask, do_not_propagate_mask;
                public int override_redirect;
                public IntPtr screen;
            }

            public static bool TryGetCursorDip(double dpiScale, out double x, out double y)
            {
                x = 0; y = 0;
                IntPtr dpy = IntPtr.Zero;
                try
                {
                    dpy = XOpenDisplay(IntPtr.Zero);
                    if (dpy == IntPtr.Zero) return false;
                    IntPtr root = XDefaultRootWindow(dpy);
                    IntPtr rr, cr;
                    int rx, ry, wx, wy;
                    uint mask;
                    if (!XQueryPointer(dpy, root, out rr, out cr, out rx, out ry, out wx, out wy, out mask))
                        return false;
                    x = rx / dpiScale;
                    y = ry / dpiScale;
                    return true;
                }
                catch { return false; }
                finally { if (dpy != IntPtr.Zero) try { XCloseDisplay(dpy); } catch { } }
            }

            public static List<XWinInfo> QueryWindows(double dpiScale)
            {
                IntPtr dpy = IntPtr.Zero;
                try
                {
                    dpy = XOpenDisplay(IntPtr.Zero);
                    if (dpy == IntPtr.Zero) return null;
                    IntPtr root = XDefaultRootWindow(dpy);

                    IntPtr aActive = XInternAtom(dpy, "_NET_ACTIVE_WINDOW", false);
                    IntPtr aList = XInternAtom(dpy, "_NET_CLIENT_LIST_STACKING", false);
                    IntPtr aPid = XInternAtom(dpy, "_NET_WM_PID", false);
                    IntPtr aName = XInternAtom(dpy, "_NET_WM_NAME", false);
                    IntPtr aWmName = XInternAtom(dpy, "WM_NAME", false);
                    IntPtr aState = XInternAtom(dpy, "_NET_WM_STATE", false);
                    IntPtr aHidden = XInternAtom(dpy, "_NET_WM_STATE_HIDDEN", false);

                    IntPtr activeWin = ReadSingleWindow(dpy, root, aActive);
                    IntPtr[] clients = ReadWindowArray(dpy, root, aList);
                    if (clients.Length == 0 && activeWin != IntPtr.Zero)
                        clients = new[] { activeWin };

                    var res = new List<XWinInfo>(clients.Length);
                    for (int i = 0; i < clients.Length; i++)
                    {
                        IntPtr w = clients[i];
                        int pid = ReadIntProp(dpy, w, aPid);
                        string title = ReadStringProp(dpy, w, aName);
                        if (string.IsNullOrEmpty(title)) title = ReadStringProp(dpy, w, aWmName);
                        bool hidden = HasAtomInProp(dpy, w, aState, aHidden);
                        XWindowAttributes attrs;
                        if (XGetWindowAttributes(dpy, w, out attrs) == 0) continue;
                        if (attrs.map_state != 2) hidden = true; // IsViewable == 2

                        int sx, sy;
                        IntPtr child;
                        if (!XTranslateCoordinates(dpy, w, root, 0, 0, out sx, out sy, out child))
                        {
                            sx = attrs.x;
                            sy = attrs.y;
                        }
                        res.Add(new XWinInfo
                        {
                            Pid = pid,
                            Title = title ?? "",
                            IsActive = w == activeWin,
                            IsMinimized = hidden,
                            Bounds = new ScreenRectDip(sx / dpiScale, sy / dpiScale, attrs.width / dpiScale, attrs.height / dpiScale)
                        });
                    }
                    return res;
                }
                catch { return null; }
                finally { if (dpy != IntPtr.Zero) try { XCloseDisplay(dpy); } catch { } }
            }

            static IntPtr ReadSingleWindow(IntPtr dpy, IntPtr w, IntPtr prop)
            {
                var arr = ReadWindowArray(dpy, w, prop);
                return arr.Length > 0 ? arr[0] : IntPtr.Zero;
            }

            static IntPtr[] ReadWindowArray(IntPtr dpy, IntPtr w, IntPtr prop)
            {
                IntPtr atype, data;
                int fmt;
                nuint n, after;
                if (XGetWindowProperty(dpy, w, prop, 0, 1024, false, IntPtr.Zero, out atype, out fmt, out n, out after, out data) != 0 || data == IntPtr.Zero)
                    return new IntPtr[0];
                try
                {
                    int count = (int)n;
                    var arr = new IntPtr[count];
                    int ptrSz = IntPtr.Size;
                    for (int i = 0; i < count; i++)
                    {
                        arr[i] = ptrSz == 8
                            ? new IntPtr(Marshal.ReadInt64(data, i * 8))
                            : new IntPtr(Marshal.ReadInt32(data, i * 4));
                    }
                    return arr;
                }
                finally { XFree(data); }
            }

            static int ReadIntProp(IntPtr dpy, IntPtr w, IntPtr prop)
            {
                IntPtr atype, data;
                int fmt;
                nuint n, after;
                if (XGetWindowProperty(dpy, w, prop, 0, 1, false, IntPtr.Zero, out atype, out fmt, out n, out after, out data) != 0 || data == IntPtr.Zero)
                    return 0;
                try { return n > 0 ? Marshal.ReadInt32(data) : 0; }
                finally { XFree(data); }
            }

            static string ReadStringProp(IntPtr dpy, IntPtr w, IntPtr prop)
            {
                IntPtr atype, data;
                int fmt;
                nuint n, after;
                if (XGetWindowProperty(dpy, w, prop, 0, 512, false, IntPtr.Zero, out atype, out fmt, out n, out after, out data) != 0 || data == IntPtr.Zero)
                    return "";
                try
                {
                    if (fmt != 8 || n == 0) return "";
                    int len = Math.Min((int)n, 1024);
                    byte[] bytes = new byte[len];
                    Marshal.Copy(data, bytes, 0, len);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally { XFree(data); }
            }

            static bool HasAtomInProp(IntPtr dpy, IntPtr w, IntPtr prop, IntPtr wantedAtom)
            {
                var atoms = ReadWindowArray(dpy, w, prop);
                for (int i = 0; i < atoms.Length; i++)
                    if (atoms[i] == wantedAtom) return true;
                return false;
            }
        }

        void UpdateAudioState()
        {
            if (!TrackMusicAudio)
            {
                audioFirstSeenUtc = DateTime.MinValue;
                lastAudioActiveUtc = DateTime.MinValue;
                musicPlayingState = false;
                latestAudioPeak = 0f;
                IsMusicPlaying = false;
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (!audioProbeInFlight && (now - lastAudioProbeUtc).TotalMilliseconds >= 240)
            {
                lastAudioProbeUtc = now;
                audioProbeInFlight = true;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { latestAudioPeak = ProbePlatformAudioPeak(); }
                    catch { latestAudioPeak = 0f; }
                    finally { audioProbeInFlight = false; }
                });
            }

            IsMusicPlaying = MascotCore.UpdateMusicPlaybackState(
                latestAudioPeak,
                false,
                now,
                ref audioFirstSeenUtc,
                ref lastAudioActiveUtc,
                ref musicPlayingState);
        }

        float ProbePlatformAudioPeak()
        {
            if (OperatingSystem.IsMacOS())
                return MacAudio.IsDefaultOutputRunning() ? 1.0f : 0.0f;
            if (OperatingSystem.IsLinux())
                return IsLinuxAudioPlaying() ? 1.0f : 0.0f;
            return 0.0f;
        }

        bool IsLinuxAudioPlaying()
        {
            try
            {
                if (Directory.Exists("/proc/asound"))
                {
                    foreach (string cardDir in Directory.GetDirectories("/proc/asound", "card*"))
                    {
                        foreach (string pcmDir in Directory.GetDirectories(cardDir, "pcm*p"))
                        {
                            foreach (string subDir in Directory.GetDirectories(pcmDir, "sub*"))
                            {
                                string statusFile = Path.Combine(subDir, "status");
                                if (File.Exists(statusFile) && IsAlsaStatusRunning(File.ReadAllText(statusFile)))
                                    return true;
                            }
                        }
                    }
                }
            }
            catch { }

            DateTime now = DateTime.UtcNow;
            if ((now - lastLinuxPactlUtc).TotalMilliseconds < 1000)
                return latestAudioPeak > 0.01f;
            lastLinuxPactlUtc = now;

            try
            {
                var psi = new ProcessStartInfo("pactl", "list sink-inputs")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var proc = Process.Start(psi))
                {
                    if (proc == null) return false;
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(350);
                    return IsPactlOutputPlaying(output);
                }
            }
            catch { return false; }
        }

        public static bool IsAlsaStatusRunning(string statusText)
        {
            if (string.IsNullOrEmpty(statusText)) return false;
            return statusText.IndexOf("state: RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsPactlOutputPlaying(string pactlOutput)
        {
            if (string.IsNullOrWhiteSpace(pactlOutput)) return false;
            string[] blocks = pactlOutput.Split(new[] { "Sink Input #" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < blocks.Length; i++)
            {
                string block = blocks[i];
                bool uncorked = block.IndexOf("Corked: no", StringComparison.OrdinalIgnoreCase) >= 0;
                bool muted = block.IndexOf("Mute: yes", StringComparison.OrdinalIgnoreCase) >= 0;
                if (uncorked && !muted)
                    return true;
            }
            return false;
        }

        static class MacAudio
        {
            const string CoreAudioLib = "/System/Library/Frameworks/CoreAudio.framework/CoreAudio";
            const uint kAudioObjectSystemObject = 1;
            const uint kAudioHardwarePropertyDefaultOutputDevice = 0x644F7574; // 'dOut'
            const uint kAudioDevicePropertyDeviceIsRunningSomewhere = 0x676F6E65; // 'gone'
            const uint kAudioObjectPropertyScopeGlobal = 0x676C6F62; // 'glob'
            const uint kAudioObjectPropertyScopeOutput = 0x6F757470; // 'outp'

            [StructLayout(LayoutKind.Sequential)]
            struct AudioObjectPropertyAddress
            {
                public uint mSelector;
                public uint mScope;
                public uint mElement;
            }

            [DllImport(CoreAudioLib)]
            static extern int AudioObjectGetPropertyData(
                uint inObjectID,
                ref AudioObjectPropertyAddress inAddress,
                uint inQualifierDataSize,
                IntPtr inQualifierData,
                ref uint ioDataSize,
                out uint outData);

            public static bool IsDefaultOutputRunning()
            {
                try
                {
                    var devAddr = new AudioObjectPropertyAddress
                    {
                        mSelector = kAudioHardwarePropertyDefaultOutputDevice,
                        mScope = kAudioObjectPropertyScopeGlobal,
                        mElement = 0,
                    };
                    uint size = 4;
                    uint devId;
                    if (AudioObjectGetPropertyData(kAudioObjectSystemObject, ref devAddr, 0, IntPtr.Zero, ref size, out devId) != 0 || devId == 0)
                        return false;

                    var runAddr = new AudioObjectPropertyAddress
                    {
                        mSelector = kAudioDevicePropertyDeviceIsRunningSomewhere,
                        mScope = kAudioObjectPropertyScopeOutput,
                        mElement = 0,
                    };
                    size = 4;
                    uint running;
                    if (AudioObjectGetPropertyData(devId, ref runAddr, 0, IntPtr.Zero, ref size, out running) != 0)
                    {
                        runAddr.mScope = kAudioObjectPropertyScopeGlobal;
                        size = 4;
                        if (AudioObjectGetPropertyData(devId, ref runAddr, 0, IntPtr.Zero, ref size, out running) != 0)
                            return false;
                    }
                    return running != 0;
                }
                catch { return false; }
            }
        }
    }
}
