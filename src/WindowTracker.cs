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
    // Tracks Antigravity App and Antigravity IDE windows, active chat title,
    // outer composer card bounds, user typing state, dictation, and voice mode (listening vs speaking).
    class WindowTracker
    {
        const string MicStore = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
        const string MicOnButton = "Turn off microphone";
        const string MicOffButton = "Turn on microphone";

        public bool IsOpen { get; private set; }
        public bool IsMinimized { get; private set; }
        public bool IsInFront { get; private set; }
        public Rect WindowBoundsPx { get; private set; }
        public Rect ChatBoxBoundsPx { get; private set; }
        public IntPtr WindowHandle { get; private set; }
        public string ActiveChatTitle { get; private set; }

        public bool IsUserTyping { get; private set; }
        public bool InDictation { get; private set; }
        public bool InVoiceMode { get; private set; }
        public bool VoiceAgentSpeaking { get; private set; }

        HashSet<uint> targetPids = new HashSet<uint>();
        DateTime pidsRefreshed = DateTime.MinValue;
        readonly uint selfPid = (uint)Process.GetCurrentProcess().Id;

        volatile IntPtr probeHwnd = IntPtr.Zero;
        volatile bool probeActive;
        volatile bool probedVoice, probedDictation, probedAgentSpeaking;
        Rect probedChatBox = Rect.Empty;
        readonly object chatLock = new object();
        bool uiaStarted;

        AutomationElement cachedInputEl;
        IntPtr cachedInputHwnd = IntPtr.Zero;
        int probeCycle;
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
            string n = processName.Trim();
            if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(0, n.Length - 4);
            return n.Equals("Antigravity", StringComparison.OrdinalIgnoreCase)
                || n.Equals("GoogleAntigravity", StringComparison.OrdinalIgnoreCase)
                || n.Equals("Google Antigravity", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Antigravity ", StringComparison.OrdinalIgnoreCase)
                || n.StartsWith("Antigravity-", StringComparison.OrdinalIgnoreCase);
        }

        public void Poll()
        {
            if ((DateTime.Now - pidsRefreshed).TotalSeconds > 1.5)
            {
                var pids = new HashSet<uint>();
                try
                {
                    foreach (var p in Process.GetProcesses())
                    {
                        try
                        {
                            if (p.Id != selfPid && IsAntigravityProcessName(p.ProcessName))
                                pids.Add((uint)p.Id);
                        }
                        catch { }
                        finally { try { p.Dispose(); } catch { } }
                    }
                }
                catch { }
                targetPids = pids;
                pidsRefreshed = DateTime.Now;
            }

            IntPtr visible = IntPtr.Zero;
            IntPtr minimized = IntPtr.Zero;
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

                // Strictly match actual Antigravity App / IDE processes (never web browsers whose tab title mentions Antigravity).
                if (!targetPids.Contains(pid)) return true;

                if (IsIconic(h))
                {
                    if (minimized == IntPtr.Zero) minimized = h;
                }
                else
                {
                    if (h == fg || visible == IntPtr.Zero) visible = h;
                }
                return true;
            }, IntPtr.Zero);

            IsOpen = visible != IntPtr.Zero || minimized != IntPtr.Zero;
            IsMinimized = IsOpen && visible == IntPtr.Zero;
            WindowHandle = visible != IntPtr.Zero ? visible : minimized;

            bool fgTransient = fg == IntPtr.Zero;
            bool fgIsSelf = fgPid == selfPid;
            bool fgIsAgy = visible != IntPtr.Zero && (visible == fg || (fgPid != 0 && targetPids.Contains(fgPid) && !IsIconic(fg)));
            IsInFront = visible != IntPtr.Zero && (fgIsAgy || ((fgIsSelf || fgTransient) && IsInFront));

            if (visible != IntPtr.Zero)
            {
                RECT r;
                if (GetWindowRect(visible, out r) && r.Right > r.Left && r.Bottom > r.Top)
                    WindowBoundsPx = new Rect(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
                ActiveChatTitle = TitleFromWindow(visible) ?? "";
            }
            else
            {
                WindowBoundsPx = Rect.Empty;
                ActiveChatTitle = "";
            }

            probeHwnd = IsInFront ? visible : IntPtr.Zero;
            probeActive = IsInFront;

            lock (chatLock)
            {
                ChatBoxBoundsPx = IsInFront ? probedChatBox : Rect.Empty;
            }

            // Detect live user typing strictly while Antigravity's chat screen is active
            if (IsInFront && !ChatBoxBoundsPx.IsEmpty && AnyTypingKeyPressed())
                lastKeystrokeAt = DateTime.UtcNow;

            IsUserTyping = IsInFront && !ChatBoxBoundsPx.IsEmpty && (DateTime.UtcNow - lastKeystrokeAt).TotalSeconds < 1.6;
            InVoiceMode = IsInFront && probedVoice;
            InDictation = IsInFront && !probedVoice && probedDictation;
            VoiceAgentSpeaking = InVoiceMode && probedAgentSpeaking;
        }

        // Checks whether a typing key is currently down without installing any global keyboard hook.
        static bool AnyTypingKeyPressed()
        {
            // Ignore shortcut chords (Ctrl / Alt held)
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0 || (GetAsyncKeyState(0x12) & 0x8000) != 0)
                return false;

            // Letters A-Z (0x41..0x5A) and Digits 0-9 (0x30..0x39)
            for (int vk = 0x41; vk <= 0x5A; vk++)
                if ((GetAsyncKeyState(vk) & 0x8001) != 0) return true;
            for (int vk = 0x30; vk <= 0x39; vk++)
                if ((GetAsyncKeyState(vk) & 0x8001) != 0) return true;
            // Space (0x20), Backspace (0x08), OEM punctuation
            if ((GetAsyncKeyState(0x20) & 0x8001) != 0 || (GetAsyncKeyState(0x08) & 0x8001) != 0)
                return true;
            return false;
        }

        public static string ParseWindowTitle(string full)
        {
            if (string.IsNullOrWhiteSpace(full)) return null;
            string s = full.Trim();
            const string suffix = " - Antigravity";
            if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - suffix.Length).Trim();
            else if (s.Equals("Antigravity", StringComparison.OrdinalIgnoreCase))
                return null;
            int sep = s.IndexOf(" - ", StringComparison.Ordinal);
            if (sep > 0) s = s.Substring(0, sep).Trim();
            return s.Length == 0 || s.Equals("Antigravity", StringComparison.OrdinalIgnoreCase) ? null : s;
        }

        static string TitleFromWindow(IntPtr hwnd)
        {
            try
            {
                var sb = new StringBuilder(512);
                if (GetWindowText(hwnd, sb, sb.Capacity) <= 0) return null;
                return ParseWindowTitle(sb.ToString());
            }
            catch { return null; }
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

            while (true)
            {
                try
                {
                    IntPtr h = probeHwnd;
                    if (probeActive && h != IntPtr.Zero)
                    {
                        Rect found = FindComposerCardRect(h, inputCond);
                        lock (chatLock) { probedChatBox = found; }

                        if (probeCycle % 3 == 0)
                            ProbeVoiceAndDictation(h, voiceCond, dictationCond);
                    }
                    else
                    {
                        cachedInputEl = null;
                        cachedInputHwnd = IntPtr.Zero;
                        probedVoice = false;
                        probedDictation = false;
                        probedAgentSpeaking = false;
                    }
                }
                catch
                {
                    cachedInputEl = null;
                }
                Thread.Sleep(probeActive ? 140 : 600);
            }
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

        static Rect ResolveOuterComposerCard(AutomationElement inputEl, Rect inputRect, Rect winRect)
        {
            var walker = TreeWalker.RawViewWalker;
            Rect card = Rect.Empty;
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
                cur = parent;

                if (pr.IsEmpty) continue;
                if (pr.Left > inputRect.Left + 4 || pr.Right < inputRect.Right - 4) continue;
                if (pr.Top > inputRect.Top + 4 || pr.Bottom < inputRect.Bottom - 4) continue;

                if (pr.Width > inputRect.Width + 140) break;
                if (pr.Height > winRect.Height * 0.62) break;
                if (pr.Top < winRect.Top + winRect.Height * 0.20) break;
                if (pr.Bottom > inputRect.Bottom + 110) break;

                if (pr.Height >= inputRect.Height + 16 || pr.Width >= inputRect.Width + 8)
                    card = pr;
            }

            if (!card.IsEmpty)
            {
                return new Rect(
                    Math.Max(winRect.Left + 8, card.Left),
                    Math.Max(winRect.Top + 36, card.Top),
                    Math.Min(winRect.Right - 8 - Math.Max(winRect.Left + 8, card.Left), card.Width),
                    card.Height);
            }

            double left = Math.Max(winRect.Left + 12, inputRect.Left - 16);
            double top = Math.Max(winRect.Top + 40, inputRect.Top - 22);
            double right = Math.Min(winRect.Right - 14, inputRect.Right + Math.Min(36, winRect.Width * 0.03));
            return new Rect(left, top, Math.Max(80, right - left), inputRect.Height + 36);
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

        const uint GW_OWNER = 4;
        delegate bool EnumWindowsProc(IntPtr h, IntPtr l);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc fn, IntPtr l);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
    }
}
