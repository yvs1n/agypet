using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace AgyPet
{
    public class ActivitySnapshot
    {
        public MascotMood Mood = MascotMood.Idle;
        public TurnSentiment Sentiment = TurnSentiment.Neutral;
        public string SessionId = "";
        public string SessionTitle = "Antigravity";
        public string Source = "Antigravity";
        public string ActionLabel = "At rest · Ready";
        public string FocusDetail = "";
        public bool IsBusy;
        public bool HasBackgroundTask;
        public bool QuotaLimited;
        public bool HasError;
        public bool HasProgress;
        public int PercentDone;
        public int PercentLeft = 100;
        public int StepsDone;
        public int StepsTotal;
        public int StepsInProgress;
        public DateTime UpdatedAt = DateTime.MinValue;
    }

    // Watches Antigravity App, Antigravity IDE, and Antigravity CLI sessions natively via
    // Windows' built-in winsqlite3.dll + transcript.jsonl + task.md + FileSystemWatcher wakeup.
    // Supports:
    //  - Active-chat matching (reacts specifically to whichever conversation you have open!)
    //  - Multi-session tracking (all simultaneous active sessions for hover card & Multitasking mood)
    //  - Background daemon / live server detection (not_fully_idle == 1 after turn completes)
    //  - Prompt sentiment (Happy / Sad / Angry), subagent delegation, errors, and per-model quota sleep
    public class ActivityMonitor
    {
        static readonly string Home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        static readonly SourceDef[] Sources =
        {
            new SourceDef("Antigravity", Path.Combine(Home, ".gemini", "antigravity", "conversation_summaries.db"), Path.Combine(Home, ".gemini", "antigravity", "brain"), new[] { "Antigravity", "antigravity" }),
            new SourceDef("Antigravity IDE", Path.Combine(Home, ".gemini", "antigravity-ide", "conversation_summaries.db"), Path.Combine(Home, ".gemini", "antigravity-ide", "brain"), new[] { "Antigravity IDE", "antigravity-ide", "Antigravity", "antigravity" }),
            new SourceDef("Antigravity CLI", Path.Combine(Home, ".gemini", "antigravity-cli", "conversation_summaries.db"), Path.Combine(Home, ".gemini", "antigravity-cli", "brain"), new[] { "agy" }),
        };

        class SourceDef
        {
            public readonly string Name, DbPath, BrainRoot;
            public readonly string[] ProcessNames;
            public SourceDef(string name, string db, string brain, string[] procs)
            {
                Name = name; DbPath = db; BrainRoot = brain; ProcessNames = procs;
            }
        }

        const int TailBytes = 768 * 1024;
        static readonly TimeSpan DoneHoldDuration = TimeSpan.FromSeconds(18);
        static readonly TimeSpan SleepAfterIdle = TimeSpan.FromMinutes(6);
        static readonly TimeSpan StaleBusyCutoff = TimeSpan.FromMinutes(20);

        volatile ActivitySnapshot latest = new ActivitySnapshot();
        volatile ActivitySnapshot focusedSession = new ActivitySnapshot();
        volatile List<ActivitySnapshot> activeSessions = new List<ActivitySnapshot>();
        volatile string preferredChatTitle = "";

        readonly HashSet<string> prevBusyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly AutoResetEvent wakeEvent = new AutoResetEvent(false);
        readonly List<FileSystemWatcher> watchers = new List<FileSystemWatcher>();
        ActivitySnapshot lastCompletedSnapshot;
        DateTime lastDoneAt = DateTime.MinValue;
        DateTime lastActiveAt = DateTime.Now;
        bool running;

        public ActivitySnapshot Latest { get { return latest; } }
        public ActivitySnapshot FocusedSession { get { return focusedSession; } }
        public List<ActivitySnapshot> ActiveSessions { get { return activeSessions; } }
        public string PreferredChatTitle
        {
            get { return preferredChatTitle; }
            set { preferredChatTitle = value ?? ""; }
        }
        public event Action Updated;

        public void Start()
        {
            if (running) return;
            running = true;
            InitWatchers();
            new Thread(Loop)
            {
                IsBackground = true,
                Name = "AgyPet ActivityMonitor",
                Priority = ThreadPriority.BelowNormal
            }.Start();
        }

        void InitWatchers()
        {
            foreach (var src in Sources)
            {
                try
                {
                    if (Directory.Exists(src.BrainRoot))
                    {
                        var bw = new FileSystemWatcher(src.BrainRoot)
                        {
                            IncludeSubdirectories = true,
                            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                            EnableRaisingEvents = true
                        };
                        bw.Changed += (s, e) => wakeEvent.Set();
                        bw.Created += (s, e) => wakeEvent.Set();
                        watchers.Add(bw);
                    }
                    string dbDir = Path.GetDirectoryName(src.DbPath);
                    string dbFile = Path.GetFileName(src.DbPath);
                    if (!string.IsNullOrEmpty(dbDir) && Directory.Exists(dbDir))
                    {
                        var dw = new FileSystemWatcher(dbDir, dbFile + "*")
                        {
                            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                            EnableRaisingEvents = true
                        };
                        dw.Changed += (s, e) => wakeEvent.Set();
                        watchers.Add(dw);
                    }
                }
                catch { }
            }
        }

        void Loop()
        {
            while (running)
            {
                try
                {
                    PollOnce();
                    var handler = Updated;
                    if (handler != null) handler();
                }
                catch { }
                // Wait up to 750ms, or wake immediately when Antigravity writes a transcript/DB update
                if (wakeEvent.WaitOne(750))
                    Thread.Sleep(60);
            }
        }

        void PollOnce()
        {
            var allRows = new List<SessionRow>();
            var busyRows = new List<SessionRow>();
            var bgRows = new List<SessionRow>();
            SessionRow bestRecent = null;
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DateTime now = DateTime.Now;

            foreach (var src in Sources)
            {
                if (!File.Exists(src.DbPath)) continue;
                bool hostRunning = AnyProcessAlive(src.ProcessNames);
                var rows = QueryRecentSessions(src.Name, src.DbPath, src.BrainRoot);
                foreach (var r in rows)
                {
                    if (seenIds.Contains(r.Id)) continue;
                    seenIds.Add(r.Id);

                    bool activeFile = IsSessionActuallyActive(r, hostRunning, now);
                    bool runningTurn = r.RawRunning && activeFile;
                    bool backgroundDaemon = !runningTurn && r.NotFullyIdle && hostRunning;

                    r.IsBusy = runningTurn;
                    r.HasBackgroundTask = backgroundDaemon;
                    allRows.Add(r);

                    if (runningTurn) busyRows.Add(r);
                    else if (backgroundDaemon) bgRows.Add(r);

                    if (bestRecent == null || r.Updated > bestRecent.Updated)
                        bestRecent = r;
                }
            }

            busyRows.Sort((a, b) => b.Updated.CompareTo(a.Updated));

            // 1. Build global active sessions list (for hover card & minimized Multitasking)
            var builtBusy = new List<ActivitySnapshot>(busyRows.Count);
            if (busyRows.Count > 0)
            {
                prevBusyIds.Clear();
                foreach (var r in busyRows)
                {
                    prevBusyIds.Add(r.Id);
                    builtBusy.Add(BuildSnapshotForRow(r, now, false));
                }
                lastActiveAt = now;
                lastCompletedSnapshot = builtBusy[0];
            }
            else if (prevBusyIds.Count > 0)
            {
                prevBusyIds.Clear();
                lastDoneAt = now;
                lastActiveAt = now;
            }

            activeSessions = builtBusy;

            // Global latest snapshot (used when minimized / away)
            if (builtBusy.Count > 0)
            {
                latest = builtBusy[0];
            }
            else if (bgRows.Count > 0)
            {
                latest = BuildSnapshotForRow(bgRows[0], now, false);
            }
            else if (bestRecent != null)
            {
                bool showDone = (now - lastDoneAt) < DoneHoldDuration;
                latest = BuildSnapshotForRow(bestRecent, now, showDone);
            }
            else
            {
                var idle = new ActivitySnapshot();
                if ((now - lastActiveAt) > SleepAfterIdle)
                {
                    idle.Mood = MascotMood.Sleeping;
                    idle.ActionLabel = "Sleeping · Waiting for Antigravity";
                }
                latest = idle;
            }

            // 2. Match the SPECIFIC conversation currently open in Antigravity!
            string wantTitle = preferredChatTitle;
            SessionRow matched = FindSessionByTitle(allRows, wantTitle);
            if (matched != null)
            {
                bool showDone = !matched.IsBusy && !matched.HasBackgroundTask && (now - lastDoneAt) < DoneHoldDuration &&
                                lastCompletedSnapshot != null && matched.Id == lastCompletedSnapshot.SessionId;
                focusedSession = BuildSnapshotForRow(matched, now, showDone);
            }
            else
            {
                focusedSession = latest;
            }
        }

        static SessionRow FindSessionByTitle(List<SessionRow> rows, string title)
        {
            if (string.IsNullOrWhiteSpace(title) || rows.Count == 0) return null;
            string want = title.Trim().ToLowerInvariant();
            SessionRow partial = null;
            foreach (var r in rows)
            {
                string rt = (r.Title ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(rt)) continue;
                if (rt == want) return r;
                if (partial == null && (rt.Contains(want) || want.Contains(rt)))
                    partial = r;
            }
            return partial;
        }

        ActivitySnapshot BuildSnapshotForRow(SessionRow r, DateTime now, bool celebrateDone)
        {
            TranscriptInfo info = InspectSessionFiles(r.BrainRoot, r.Id);
            var snap = new ActivitySnapshot
            {
                SessionId = r.Id,
                SessionTitle = string.IsNullOrWhiteSpace(r.Title) ? r.Source : r.Title,
                Source = r.Source,
                Sentiment = info.Sentiment,
                IsBusy = r.IsBusy,
                HasBackgroundTask = r.HasBackgroundTask,
                QuotaLimited = info.QuotaLimited,
                HasError = info.HasError,
                UpdatedAt = r.Updated,
                FocusDetail = info.FocusDetail,
                HasProgress = info.HasProgress,
                PercentDone = info.PercentDone,
                PercentLeft = info.PercentLeft,
                StepsDone = info.StepsDone,
                StepsTotal = info.StepsTotal,
                StepsInProgress = info.StepsInProgress,
            };

            if (r.IsBusy)
            {
                snap.Mood = info.Mood == MascotMood.Done ? MascotMood.Thinking : info.Mood;
                snap.ActionLabel = !string.IsNullOrEmpty(info.ActionLabel) ? info.ActionLabel : MoodDefaultLabel(snap.Mood);
                return snap;
            }

            if (info.QuotaLimited)
            {
                snap.Sentiment = TurnSentiment.Neutral;
                snap.Mood = MascotMood.Sleeping;
                snap.ActionLabel = "Quota limit · Sleeping";
                snap.FocusDetail = "Switch model to wake immediately";
                return snap;
            }

            if (r.HasBackgroundTask)
            {
                snap.Mood = MascotMood.Background;
                snap.ActionLabel = "Done · Background task running";
                snap.FocusDetail = !string.IsNullOrEmpty(info.FocusDetail) ? info.FocusDetail : "Live server or background process active";
                if (!snap.HasProgress)
                {
                    snap.HasProgress = true;
                    snap.PercentDone = 100;
                    snap.PercentLeft = 0;
                }
                return snap;
            }

            bool recentEnd = r.Updated > DateTime.MinValue && (now - r.Updated) < TimeSpan.FromSeconds(14);
            if ((celebrateDone || recentEnd) && info.HasError)
            {
                snap.Sentiment = TurnSentiment.Neutral;
                snap.Mood = MascotMood.Error;
                snap.ActionLabel = "Turn hit an error";
                return snap;
            }

            bool justFinished = celebrateDone || (recentEnd && info.Mood == MascotMood.Done);
            if (justFinished)
            {
                int total = info.StepsTotal > 0 ? info.StepsTotal : 1;
                bool praise = info.Sentiment == TurnSentiment.Happy;
                snap.Mood = praise ? MascotMood.Happy : MascotMood.Done;
                snap.ActionLabel = praise ? "Happy to help!" : "Task complete!";
                snap.FocusDetail = !string.IsNullOrEmpty(info.FocusDetail) ? info.FocusDetail : "All steps finished";
                snap.HasProgress = true;
                snap.PercentDone = 100;
                snap.PercentLeft = 0;
                snap.StepsDone = total;
                snap.StepsTotal = total;
                return snap;
            }

            // Sustained Angry/Sad turn mood cools down when the turn is no longer busy
            snap.Sentiment = TurnSentiment.Neutral;
            bool sleeping = r.Updated > DateTime.MinValue && (now - r.Updated) > SleepAfterIdle;
            snap.Mood = sleeping ? MascotMood.Sleeping : MascotMood.Idle;
            snap.ActionLabel = sleeping ? "Sleeping · Quiet" : "At rest · Ready";
            if (string.IsNullOrEmpty(snap.FocusDetail)) snap.FocusDetail = snap.SessionTitle;
            return snap;
        }

        static bool AnyProcessAlive(string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    var procs = Process.GetProcessesByName(name);
                    if (procs != null && procs.Length > 0)
                    {
                        foreach (var p in procs) p.Dispose();
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        static bool IsSessionActuallyActive(SessionRow r, bool hostRunning, DateTime now)
        {
            string transcript = Path.Combine(r.BrainRoot, r.Id, ".system_generated", "logs", "transcript.jsonl");
            DateTime fileWrite = DateTime.MinValue;
            try
            {
                if (File.Exists(transcript))
                    fileWrite = File.GetLastWriteTime(transcript);
            }
            catch { }

            DateTime newest = r.Updated > fileWrite ? r.Updated : fileWrite;
            if (!hostRunning && (newest == DateTime.MinValue || (now - newest) > TimeSpan.FromMinutes(2)))
                return false;
            if (newest > DateTime.MinValue && (now - newest) > StaleBusyCutoff)
                return false;
            return true;
        }

        static TranscriptInfo InspectSessionFiles(string brainRoot, string sessionId)
        {
            string brainDir = Path.Combine(brainRoot, sessionId);
            string transcriptPath = Path.Combine(brainDir, ".system_generated", "logs", "transcript.jsonl");
            TranscriptInfo info = ParseTranscriptTail(transcriptPath);
            if (!info.HasProgress)
            {
                string taskMd = Path.Combine(brainDir, "task.md");
                if (File.Exists(taskMd))
                    ParseTaskMarkdown(taskMd, info);
            }
            return info;
        }

        // ---- Transcript & Accurate Progress Parsing ----

        public class TranscriptInfo
        {
            public MascotMood Mood = MascotMood.Thinking;
            public TurnSentiment Sentiment = TurnSentiment.Neutral;
            public string ActionLabel = "";
            public string FocusDetail = "";
            public bool QuotaLimited;
            public bool HasError;
            public bool HasProgress;
            public int PercentDone;
            public int PercentLeft = 100;
            public int StepsDone;
            public int StepsTotal;
            public int StepsInProgress;
        }

        static readonly Regex CodeBlockRe = new Regex("```[\\s\\S]*?```|`[^`\\n]*`", RegexOptions.Compiled);
        static readonly Regex UrlRe = new Regex(@"https?://\S+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex PathRe = new Regex(@"(?:^|\s)(?:~?/|(?:\./|\.\./))[A-Za-z0-9._/@-]+", RegexOptions.Compiled);
        static readonly Regex AngryWordRe = new Regex(@"\b(wtf|wth|fu+ck+\w*|fck+\w*|fuk+\w*|sh[i1]+t+\w*|bullsh[i1]+t+\w*|b[i1]tch\w*|crap+\w*|ffs|stfu|gtfo|piss\s+off|shut\s+(?:the\s+\w+\s+|your\s+mouth\s+|it\s+)?up|still\s+(?:broken|not\s+working|failing)|broken\s+again|nothing\s+works|ugh+|argh+|are\s+you\s+(?:kidding|serious))\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex SadWordRe = new Regex(@"\b(stupid(?:er|est)?(?!-simple)|dumb(?:er|est)?(?!\s+terminal)|useless|worthless|hopeless|incompetent|clueless|idiot(?:ic)?|moron(?:ic)?|pathetic|brain\s*dead|hate\s+(?:u|you)|(?:u|you)\s+suck)\b|\b(?:you(?:'re|\s+are)|this\s+(?:is|code\s+is)|that(?:'s|\s+is))\s+(?:so\s+|complete\s+|total\s+)?(?:trash|garbage|rubbish|horrible|terrible|awful)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex ThanksRe = new Regex(@"\b(thanks|thank\s+you|thx|great\s+(?:job|work)|nice\s+(?:job|work|one)|good\s+(?:job|bot)|well\s+done|love\s+(?:it|this|you)|you\s+rock)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex PraiseRe = new Regex(@"\b(awesome|amazing|perfect|brilliant|excellent|nice|great|beautiful)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex NegPraiseRe = new Regex(@"\b(no\s+thanks|thanks\s+for\s+nothing|not\s+(?:great|nice|good|awesome|amazing|perfect|helpful))\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex QuotaErrRe = new Regex(@"reached.*quota|quota\s*(?:limit|exhaust\w*|exceed\w*)|(?:rate|usage|5[-\s]?h(?:ou)?r|weekly)\s*limit|resume\s+using\s+this\s+model|resource[_\s-]?exhausted|\b429\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex NonShoutTokenRe = new Regex(@"[_=/\\<>\{\}\[\]\d]|::", RegexOptions.Compiled);

        public static TurnSentiment DetectSentiment(string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return TurnSentiment.Neutral;
            string cleaned = CodeBlockRe.Replace(rawText, " ");
            cleaned = UrlRe.Replace(cleaned, " ");
            cleaned = PathRe.Replace(cleaned, " ").Trim();
            if (cleaned.Length == 0) return TurnSentiment.Neutral;

            if (AngryWordRe.IsMatch(cleaned) || IsShouting(cleaned))
                return TurnSentiment.Angry;
            if (SadWordRe.IsMatch(cleaned))
                return TurnSentiment.Sad;
            if (!NegPraiseRe.IsMatch(cleaned))
            {
                if (ThanksRe.IsMatch(cleaned)) return TurnSentiment.Happy;
                int words = cleaned.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                if (words <= 4 && PraiseRe.IsMatch(cleaned)) return TurnSentiment.Happy;
            }
            return TurnSentiment.Neutral;
        }

        static bool IsShouting(string text)
        {
            var tokens = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return false;
            string first = tokens[0].Trim('(', ')', '.', ',', ';', ':', '!', '?', '"', '\'').ToUpperInvariant();
            if (first == "SELECT" || first == "INSERT" || first == "UPDATE" || first == "DELETE" || first == "CREATE" || first == "ALTER" || first == "DROP")
                return false;
            int alphaWords = 0, upperLetters = 0, totalLetters = 0;
            foreach (string tok in tokens)
            {
                if (NonShoutTokenRe.IsMatch(tok)) continue;
                bool hasLetter = false;
                foreach (char ch in tok)
                {
                    if (char.IsLetter(ch))
                    {
                        hasLetter = true;
                        totalLetters++;
                        if (char.IsUpper(ch)) upperLetters++;
                    }
                }
                if (hasLetter) alphaWords++;
            }
            return alphaWords >= 3 && totalLetters >= 12 && (upperLetters * 10 > totalLetters * 8);
        }

        public static bool IsQuotaErrorText(string text)
        {
            return !string.IsNullOrEmpty(text) && QuotaErrRe.IsMatch(text);
        }

        public static TranscriptInfo ParseTranscriptTail(string filePath)
        {
            var info = new TranscriptInfo();
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return info;

            string[] lines;
            try
            {
                using (var f = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    long start = Math.Max(0, f.Length - TailBytes);
                    f.Seek(start, SeekOrigin.Begin);
                    var buf = new byte[f.Length - start];
                    int got = f.Read(buf, 0, buf.Length);
                    lines = Encoding.UTF8.GetString(buf, 0, got).Split('\n');
                }
            }
            catch { return info; }

            return ParseTranscriptLines(lines);
        }

        public static TranscriptInfo ParseTranscriptLines(string[] lines)
        {
            var info = new TranscriptInfo();
            if (lines == null || lines.Length == 0) return info;

            bool foundMood = false;
            bool foundSteps = false;
            bool foundFocus = false;
            bool foundSentiment = false;
            bool sawTurnOutcome = false;

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                if (foundMood && foundSentiment && line.IndexOf("update_steps", StringComparison.Ordinal) < 0 && line.IndexOf("update_focus", StringComparison.Ordinal) < 0)
                    continue;

                Dictionary<string, object> d;
                try { d = Json.ParseObject(line); } catch { continue; }
                if (d == null) continue;

                string type = Json.Str(d, "type");
                if (type == "PLANNER_RESPONSE")
                {
                    string status = Json.Str(d, "status");
                    string errText = Json.Str(d, "error");
                    if (!sawTurnOutcome)
                    {
                        sawTurnOutcome = true;
                        if (IsQuotaErrorText(errText) || IsQuotaErrorText(Json.Str(d, "content")))
                            info.QuotaLimited = true;
                        else if (status == "ERROR" || !string.IsNullOrEmpty(errText))
                            info.HasError = true;
                    }

                    object toolCallsObj;
                    if (d.TryGetValue("tool_calls", out toolCallsObj) && toolCallsObj is IEnumerable && !(toolCallsObj is string))
                    {
                        var calls = new List<Dictionary<string, object>>();
                        foreach (object o in (IEnumerable)toolCallsObj)
                        {
                            var tc = o as Dictionary<string, object>;
                            if (tc != null) calls.Add(tc);
                        }

                        for (int j = calls.Count - 1; j >= 0; j--)
                        {
                            var tc = calls[j];
                            string name = Json.Str(tc, "name");
                            var args = Json.Obj(tc, "args");

                            if (!foundSteps && name == "update_steps" && args != null)
                            {
                                if (TryApplyStepsArg(args, info))
                                    foundSteps = true;
                            }
                            else if (!foundFocus && name == "update_focus" && args != null)
                            {
                                string focus = Unquote(Json.Str(args, "focus"));
                                if (!string.IsNullOrWhiteSpace(focus))
                                {
                                    info.FocusDetail = focus;
                                    foundFocus = true;
                                }
                            }
                            else if (!foundMood && !string.IsNullOrEmpty(name) && name != "update_focus" && name != "update_steps")
                            {
                                info.Mood = MoodForTool(name);
                                info.ActionLabel = DescribeTool(name, args);
                                foundMood = true;
                            }
                        }
                    }

                    if (!foundMood)
                    {
                        string content = Json.Str(d, "content");
                        if (status == "DONE")
                        {
                            info.Mood = MascotMood.Done;
                            info.ActionLabel = "Task complete!";
                        }
                        else if (!string.IsNullOrWhiteSpace(content))
                        {
                            info.Mood = MascotMood.Typing;
                            info.ActionLabel = "Writing response...";
                        }
                        else
                        {
                            info.Mood = MascotMood.Thinking;
                            info.ActionLabel = "Thinking...";
                        }
                        foundMood = true;
                    }
                }
                else if (type == "USER_INPUT")
                {
                    if (!foundSentiment)
                    {
                        info.Sentiment = DetectSentiment(Json.Str(d, "content"));
                        foundSentiment = true;
                    }
                    if (!foundMood)
                    {
                        info.Mood = MascotMood.Thinking;
                        info.ActionLabel = "Thinking...";
                        foundMood = true;
                    }
                }
                else if (type == "SYSTEM_MESSAGE")
                {
                    if (!sawTurnOutcome)
                    {
                        string sysContent = Json.Str(d, "content");
                        if (IsQuotaErrorText(sysContent))
                        {
                            info.QuotaLimited = true;
                            sawTurnOutcome = true;
                        }
                    }
                }
                else if (!foundMood && type == "GENERIC")
                {
                    info.Mood = MascotMood.Thinking;
                    info.ActionLabel = "Thinking...";
                    foundMood = true;
                }

                if (foundMood && foundSteps && foundFocus && foundSentiment) break;
            }

            return info;
        }

        static bool TryApplyStepsArg(Dictionary<string, object> args, TranscriptInfo info)
        {
            object stepsObj;
            if (!args.TryGetValue("steps", out stepsObj) || stepsObj == null) return false;

            IEnumerable stepsList = stepsObj as IEnumerable;
            if (stepsObj is string)
            {
                string raw = Unquote((string)stepsObj);
                try { stepsList = Json.Parse(raw) as IEnumerable; }
                catch { return false; }
            }
            if (stepsList == null) return false;

            int total = 0, done = 0, inProgress = 0;
            string currentTitle = "";
            foreach (object item in stepsList)
            {
                var step = item as Dictionary<string, object>;
                if (step == null) continue;
                total++;
                string st = Json.Str(step, "status").ToLowerInvariant();
                string title = Json.Str(step, "title");
                if (st == "done" || st == "skipped") done++;
                else if (st == "in_progress")
                {
                    inProgress++;
                    if (string.IsNullOrEmpty(currentTitle)) currentTitle = title;
                }
            }

            if (total <= 0) return false;
            info.HasProgress = true;
            info.StepsDone = done;
            info.StepsTotal = total;
            info.StepsInProgress = inProgress;

            int pct = done >= total ? 100 : (int)Math.Round(done * 100.0 / total);
            info.PercentDone = Math.Max(0, Math.Min(100, pct));
            info.PercentLeft = 100 - info.PercentDone;
            if (string.IsNullOrEmpty(info.FocusDetail) && !string.IsNullOrEmpty(currentTitle))
                info.FocusDetail = currentTitle;
            return true;
        }

        public static void ParseTaskMarkdown(string filePath, TranscriptInfo info)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                int total = 0, done = 0, active = 0;
                string activeTitle = "";
                foreach (string raw in lines)
                {
                    string s = raw.TrimStart();
                    if (s.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase)) { total++; done++; }
                    else if (s.StartsWith("- [/] ", StringComparison.Ordinal))
                    {
                        total++; active++;
                        if (string.IsNullOrEmpty(activeTitle)) activeTitle = s.Substring(6).Trim();
                    }
                    else if (s.StartsWith("- [ ] ", StringComparison.Ordinal)) { total++; }
                }
                if (total <= 0) return;
                info.HasProgress = true;
                info.StepsDone = done;
                info.StepsTotal = total;
                info.StepsInProgress = active;
                int pct = done >= total ? 100 : (int)Math.Round(done * 100.0 / total);
                info.PercentDone = Math.Max(0, Math.Min(100, pct));
                info.PercentLeft = 100 - info.PercentDone;
                if (string.IsNullOrEmpty(info.FocusDetail) && !string.IsNullOrEmpty(activeTitle))
                    info.FocusDetail = activeTitle;
            }
            catch { }
        }

        public static MascotMood MoodForTool(string tool)
        {
            if (string.IsNullOrEmpty(tool)) return MascotMood.Working;
            string lower = tool.ToLowerInvariant();
            if (lower == "invoke_subagent" || lower == "swarm" || lower == "run_workflow" || lower == "define_subagent")
                return MascotMood.Delegating;
            if (lower == "run_command" || lower.Contains("terminal") || lower.Contains("shell") || lower.Contains("exec"))
                return MascotMood.Terminal;
            if (lower.Contains("browser") || lower.Contains("navigate") || lower.Contains("screenshot") || lower.Contains("click") || lower.Contains("devtools"))
                return MascotMood.Browsing;
            if (lower.Contains("search") || lower.Contains("fetch") || lower.Contains("marketplace"))
                return MascotMood.Searching;
            if (lower.Contains("read") || lower.Contains("glob") || lower.Contains("grep") || lower.Contains("list") || lower.Contains("resource"))
                return MascotMood.Reading;
            if (lower.Contains("question") || lower.Contains("ask"))
                return MascotMood.Question;
            if (lower.Contains("alert") || lower.Contains("error"))
                return MascotMood.Alert;
            if (lower.Contains("edit") || lower.Contains("write"))
                return MascotMood.Working;
            return MascotMood.Working;
        }

        public static string DescribeTool(string tool, Dictionary<string, object> args)
        {
            string lower = (tool ?? "").ToLowerInvariant();
            string path = args != null ? Unquote(Json.Str(args, "Path")) : "";
            string file = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : "";

            if (lower == "read_file") return !string.IsNullOrEmpty(file) ? "Reading " + file : "Reading files";
            if (lower == "edit_file") return !string.IsNullOrEmpty(file) ? "Editing " + file : "Editing code";
            if (lower == "write_file") return !string.IsNullOrEmpty(file) ? "Writing " + file : "Writing file";
            if (lower == "run_command")
            {
                string summary = args != null ? Unquote(Json.Str(args, "toolSummary")) : "";
                return !string.IsNullOrEmpty(summary) ? summary : "Running terminal command";
            }
            if (lower.Contains("browser") || lower.Contains("navigate")) return "Browsing web page";
            if (lower.Contains("search")) return "Searching...";
            if (lower.Contains("question") || lower.Contains("ask")) return "Waiting for your answer";
            if (lower.Contains("subagent")) return "Coordinating subagent";
            return MoodDefaultLabel(MoodForTool(tool));
        }

        public static string MoodDefaultLabel(MascotMood mood)
        {
            switch (mood)
            {
                case MascotMood.Idle: return "At rest · Ready";
                case MascotMood.Sleeping: return "Sleeping · Quiet";
                case MascotMood.Listening: return "Listening to you";
                case MascotMood.Thinking: return "Thinking...";
                case MascotMood.Speaking: return "Speaking";
                case MascotMood.Typing: return "Typing...";
                case MascotMood.Searching: return "Searching...";
                case MascotMood.Reading: return "Reading code";
                case MascotMood.Working: return "Editing files";
                case MascotMood.Terminal: return "Running command";
                case MascotMood.Browsing: return "Browsing live site";
                case MascotMood.Delegating: return "Delegating to subagents";
                case MascotMood.Multitasking: return "Multitasking sessions";
                case MascotMood.Background: return "Done · Server running";
                case MascotMood.Alert: return "Needs approval";
                case MascotMood.Question: return "Asking a question";
                case MascotMood.Done: return "Task complete!";
                case MascotMood.Happy: return "Happy to help!";
                case MascotMood.Error: return "Step hit an error";
                default: return "Working...";
            }
        }

        static string Unquote(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Trim();
            if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
            {
                try { var parsed = Json.Parse(s) as string; if (parsed != null) return parsed; } catch { }
                return s.Substring(1, s.Length - 2);
            }
            return s;
        }

        class SessionRow
        {
            public string Source, BrainRoot, Id, Title;
            public bool RawRunning, NotFullyIdle;
            public bool IsBusy, HasBackgroundTask;
            public DateTime Updated;
        }

        static List<SessionRow> QueryRecentSessions(string source, string dbPath, string brainRoot)
        {
            var list = new List<SessionRow>();
            IntPtr db;
            byte[] pathBytes = Encoding.UTF8.GetBytes(dbPath + "\0");
            if (SqliteNative.Open(pathBytes, out db, SqliteNative.SQLITE_OPEN_READONLY, IntPtr.Zero) != SqliteNative.SQLITE_OK)
                return list;

            try
            {
                string query = "SELECT conversation_id, title, status, not_fully_idle, last_modified_time " +
                               "FROM conversation_summaries WHERE killed = 0 ORDER BY last_modified_time DESC LIMIT 20;\0";
                byte[] sqlBytes = Encoding.UTF8.GetBytes(query);
                IntPtr stmt;
                if (SqliteNative.Prepare(db, sqlBytes, sqlBytes.Length, out stmt, IntPtr.Zero) != SqliteNative.SQLITE_OK)
                    return list;

                try
                {
                    while (SqliteNative.Step(stmt) == SqliteNative.SQLITE_ROW)
                    {
                        string id = SqliteNative.Utf8String(SqliteNative.ColumnText(stmt, 0));
                        if (string.IsNullOrEmpty(id)) continue;
                        string title = SqliteNative.Utf8String(SqliteNative.ColumnText(stmt, 1));
                        string rawStatus = SqliteNative.Utf8String(SqliteNative.ColumnText(stmt, 2));
                        int notIdle = SqliteNative.ColumnInt(stmt, 3);
                        string lastModStr = SqliteNative.Utf8String(SqliteNative.ColumnText(stmt, 4));

                        DateTime updated = DateTime.MinValue;
                        if (!string.IsNullOrEmpty(lastModStr))
                        {
                            DateTime parsed;
                            if (DateTime.TryParse(lastModStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out parsed))
                                updated = parsed.ToLocalTime();
                        }

                        list.Add(new SessionRow
                        {
                            Source = source,
                            BrainRoot = brainRoot,
                            Id = id,
                            Title = title,
                            RawRunning = rawStatus == "CASCADE_RUN_STATUS_RUNNING",
                            NotFullyIdle = notIdle == 1,
                            Updated = updated
                        });
                    }
                }
                finally { SqliteNative.Finalize(stmt); }
            }
            finally { SqliteNative.Close(db); }

            return list;
        }

        static class SqliteNative
        {
            [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_open_v2", CallingConvention = CallingConvention.Cdecl)]
            public static extern int Open(byte[] filename, out IntPtr db, int flags, IntPtr vfs);
            [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_prepare_v2", CallingConvention = CallingConvention.Cdecl)]
            public static extern int Prepare(IntPtr db, byte[] sql, int numBytes, out IntPtr stmt, IntPtr pzTail);
            [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_step", CallingConvention = CallingConvention.Cdecl)]
            public static extern int Step(IntPtr stmt);
            [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_text", CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr ColumnText(IntPtr stmt, int col);
            [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_column_int", CallingConvention = CallingConvention.Cdecl)]
            public static extern int ColumnInt(IntPtr stmt, int col);
            [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_finalize", CallingConvention = CallingConvention.Cdecl)]
            public static extern int Finalize(IntPtr stmt);
            [DllImport("winsqlite3.dll", EntryPoint = "sqlite3_close", CallingConvention = CallingConvention.Cdecl)]
            public static extern int Close(IntPtr db);

            public const int SQLITE_OK = 0;
            public const int SQLITE_ROW = 100;
            public const int SQLITE_OPEN_READONLY = 1;

            public static string Utf8String(IntPtr ptr)
            {
                if (ptr == IntPtr.Zero) return "";
                int len = 0;
                while (Marshal.ReadByte(ptr, len) != 0) len++;
                if (len == 0) return "";
                byte[] bytes = new byte[len];
                Marshal.Copy(ptr, bytes, 0, len);
                return Encoding.UTF8.GetString(bytes);
            }
        }
    }
}
