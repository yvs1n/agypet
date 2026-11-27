using System;
using System.IO;
using System.Windows.Media;

namespace AgyPet.Tests
{
    static class AgyPetTests
    {
        static int passed, failed;

        [STAThread]
        static int Main()
        {
            Run("Skin normalization defaults to classic and accepts dark aliases", () =>
            {
                AssertEq(Mascot.ClassicSkin, Mascot.NormalizeSkin("classic"));
                AssertEq(Mascot.ClassicSkin, Mascot.NormalizeSkin(""));
                AssertEq(Mascot.ClassicSkin, Mascot.NormalizeSkin("unknown"));
                AssertEq(Mascot.DarkSkin, Mascot.NormalizeSkin("dark"));
                AssertEq(Mascot.DarkSkin, Mascot.NormalizeSkin("obsidian"));
                AssertEq(Mascot.DarkSkin, Mascot.NormalizeSkin("monochrome"));
            });

            Run("Official Antigravity palette stays vibrant blue in core and green/red at shoulders", () =>
            {
                Color core = Mascot.OfficialArchPixelColor(0.50, 0.72, false, false, false);
                AssertTrue(core.B > 210 && core.B > core.R + 120, "Core should be vivid Antigravity blue");

                Color leftShoulder = Mascot.OfficialArchPixelColor(0.22, 0.34, false, false, false);
                AssertTrue(leftShoulder.G > leftShoulder.R && leftShoulder.G > 140, "Left shoulder should be fresh green");

                Color rightCrest = Mascot.OfficialArchPixelColor(0.66, 0.18, false, false, false);
                AssertTrue(rightCrest.R > 190 && rightCrest.R > rightCrest.B + 80, "Right crest should be warm orange-red");
            });

            Run("Dark Remake palette stays monochrome with brighter top rim", () =>
            {
                Color top = Mascot.DarkArchPixelColor(0.50, 0.05, false, true, false, false);
                Color leg = Mascot.DarkArchPixelColor(0.15, 0.90, false, false, false, false);
                AssertTrue(Math.Abs(top.R - top.B) <= 16, "Dark remake top should be neutral monochrome");
                AssertTrue(top.R > leg.R + 30, "Top edge should be brighter than legs for crisp contrast");
            });

            Run("Transcript parser extracts update_steps progress, update_focus, and tool mood", () =>
            {
                string[] lines =
                {
                    "{\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"update_focus\",\"args\":{\"focus\":\"Building overlay window\"}},{\"name\":\"update_steps\",\"args\":{\"steps\":\"[{\\\"title\\\":\\\"Step 1\\\",\\\"status\\\":\\\"done\\\"},{\\\"title\\\":\\\"Step 2\\\",\\\"status\\\":\\\"done\\\"},{\\\"title\\\":\\\"Step 3\\\",\\\"status\\\":\\\"in_progress\\\"},{\\\"title\\\":\\\"Step 4\\\",\\\"status\\\":\\\"pending\\\"}]\"}},{\"name\":\"edit_file\",\"args\":{\"Path\":\"c:/proj/src/PetWindow.cs\"}}]}"
                };
                var info = ActivityMonitor.ParseTranscriptLines(lines);
                AssertEq(MascotMood.Working, info.Mood);
                AssertEq("Editing PetWindow.cs", info.ActionLabel);
                AssertEq("Building overlay window", info.FocusDetail);
                AssertTrue(info.HasProgress, "Expected progress to be parsed");
                AssertEq(2, info.StepsDone);
                AssertEq(4, info.StepsTotal);
                AssertEq(1, info.StepsInProgress);
                AssertEq(50, info.PercentDone);
                var info2 = ActivityMonitor.ParseTranscriptLines(new[]
                {
                    "{\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"edit_file\",\"args\":{\"Path\":\"src/Mascot.cs\"}}]}",
                    "{\"type\":\"GENERIC\",\"status\":\"DONE\",\"content\":\"Made 1 replacement in src/Mascot.cs\"}",
                    "{\"type\":\"SYSTEM_MESSAGE\",\"status\":\"DONE\",\"content\":\"Reminder\"}"
                });
                AssertEq(MascotMood.Working, info2.Mood);
                AssertEq("Editing Mascot.cs", info2.ActionLabel);

                var info3 = ActivityMonitor.ParseTranscriptLines(new[]
                {
                    "{\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"read_file\",\"args\":{\"Path\":\"src/PetWindow.cs\"}}]}",
                    "{\"type\":\"GENERIC\",\"status\":\"DONE\",\"content\":\"Showing lines 1-100\"}"
                });
                AssertEq(MascotMood.Reading, info3.Mood);
                AssertEq("Reading PetWindow.cs", info3.ActionLabel);
            });

            Run("Task.md fallback computes exact done and left percentages", () =>
            {
                string tmp = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(tmp, "# Tasks\n- [x] One\n- [x] Two\n- [ ] Three\n- [ ] Four\n");
                    var info = new ActivityMonitor.TranscriptInfo();
                    ActivityMonitor.ParseTaskMarkdown(tmp, info);
                    AssertTrue(info.HasProgress, "Expected task.md progress");
                    AssertEq(50, info.PercentDone);
                    AssertEq(50, info.PercentLeft);
                    AssertEq(2, info.StepsDone);
                    AssertEq(4, info.StepsTotal);
                }
                finally { try { File.Delete(tmp); } catch { } }
            });

            Run("Tool-to-mood mapping covers terminal, browsing, reading, searching, asking, and working", () =>
            {
                AssertEq(MascotMood.Terminal, ActivityMonitor.MoodForTool("run_command"));
                AssertEq(MascotMood.Browsing, ActivityMonitor.MoodForTool("browser"));
                AssertEq(MascotMood.Reading, ActivityMonitor.MoodForTool("read_file"));
                AssertEq(MascotMood.Searching, ActivityMonitor.MoodForTool("search_web"));
                AssertEq(MascotMood.Question, ActivityMonitor.MoodForTool("ask_question"));
                AssertEq(MascotMood.Working, ActivityMonitor.MoodForTool("edit_file"));
                AssertEq(MascotMood.Delegating, ActivityMonitor.MoodForTool("invoke_subagent"));
                AssertEq(MascotMood.Delegating, ActivityMonitor.MoodForTool("run_workflow"));
            });

            Run("CLI terminal and placement detection identify terminals cleanly without false positives", () =>
            {
                AssertEq("bottom-right", PetSettings.NormalizeCliPlacement(null));
                AssertEq("bottom-center", PetSettings.NormalizeCliPlacement("bottom-center"));
                AssertEq("off", PetSettings.NormalizeCliPlacement("off"));

                AssertTrue(WindowTracker.IsCliProcessName("agy"), "agy.exe must match CLI process");
                AssertTrue(!WindowTracker.IsCliProcessName("chrome"), "chrome must not match CLI");
                AssertTrue(WindowTracker.IsTerminalProcessName("WindowsTerminal"), "WindowsTerminal must match terminal hosts");
                AssertTrue(WindowTracker.IsTerminalProcessName("pwsh.exe"), "pwsh.exe must match terminal hosts");
                AssertTrue(!WindowTracker.IsTerminalProcessName("Antigravity"), "Antigravity GUI must not match terminal filter");

                var win = new System.Windows.Rect(160, 120, 960, 620);
                var work = new System.Windows.Rect(0, 0, 1920, 1032);
                foreach (double px in new[] { 3.2, 4.2, 5.2 })
                {
                    double sw = 11.0 * px, sh = 9.0 * px;
                    var br = PetWindow.PlaceOnCliWindow("bottom-right", win, work, sw, sh, 32, 24);
                    var bc = PetWindow.PlaceOnCliWindow("bottom-center", win, work, sw, sh, 32, 24);
                    var bl = PetWindow.PlaceOnCliWindow("bottom-left", win, work, sw, sh, 32, 24);
                    var tr = PetWindow.PlaceOnCliWindow("top-right", win, work, sw, sh, 32, 24);
                    var tl = PetWindow.PlaceOnCliWindow("top-left", win, work, sw, sh, 32, 24);

                    AssertTrue(Math.Abs((br.X + 32 + sw) - (win.Right - 22.0)) < 0.01, "CLI bottom-right X aligns right inset");
                    AssertTrue(Math.Abs((br.Y + 24 + sh) - (win.Bottom - 20.0)) < 0.01, "CLI bottom-right rests above prompt");
                    AssertTrue(Math.Abs((bc.X + 32 + sw * 0.5) - (win.Left + win.Width * 0.5)) < 0.01, "CLI bottom-center centers horizontally");
                    AssertTrue(Math.Abs((bl.X + 32) - (win.Left + 22.0)) < 0.01, "CLI bottom-left aligns left inset");
                    AssertTrue(Math.Abs((tr.Y + 24 + sh) - (win.Top + 2.0)) < 0.01, "CLI top-right rests cleanly on window top edge");
                    AssertTrue(Math.Abs(tl.X - bl.X) < 0.01, "CLI top-left X aligns with bottom-left X");
                }
            });

            Run("Window title parser and Antigravity process filter reject browsers and unrelated apps", () =>
            {
                AssertEq("Build AgyPet", WindowTracker.ParseWindowTitle("Build AgyPet - AgyDock - Antigravity"));
                AssertEq("Quick Fix", WindowTracker.ParseWindowTitle("Quick Fix - Antigravity"));
                AssertEq((string)null, WindowTracker.ParseWindowTitle("Antigravity"));

                var cands = WindowTracker.ParseWindowTitleCandidates("PetWindow.cs - AgyDock - Antigravity");
                AssertEq(2, cands.Length);
                AssertEq("PetWindow.cs", cands[0]);
                AssertEq("AgyDock", cands[1]);

                AssertTrue(WindowTracker.IsAntigravityProcessName("Antigravity"), "Antigravity.exe must match");
                AssertTrue(WindowTracker.IsAntigravityProcessName("Antigravity IDE"), "Antigravity IDE must match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("chrome"), "Chrome must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("msedge"), "Edge must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("firefox"), "Firefox must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("AgyPet"), "Self must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("hp-one-agent-service"), "Unrelated agent service must never match");
            });

            Run("Per-chat matching tracks focused chat across truncated UIA headers, CLI isolation, and New Chat", () =>
            {
                string[] titles = { "Anti-Gravity Mascot Add-on Implementation", "Brag Video", "CLI Session" };
                string[] sources = { "Antigravity", "Antigravity", "Antigravity CLI" };

                AssertEq("s0", ActivityMonitor.MatchSessionIdForTest(titles, sources, new[] { "Anti-Gravity Mascot Add-on Imp…", "PetWindow.cs" }, "", true));
                AssertEq("s1", ActivityMonitor.MatchSessionIdForTest(titles, sources, new[] { "Brag Video" }, "", true));
                AssertEq("s2", ActivityMonitor.MatchSessionIdForTest(titles, sources, new[] { "Unknown Terminal" }, "Antigravity CLI", false));
                // Exact match on a later candidate must beat an earlier generic substring hit
                AssertEq("s1", ActivityMonitor.MatchSessionIdForTest(titles, sources, new[] { "Mascot", "Brag Video" }, "", true));
                AssertTrue(ActivityMonitor.IsExplicitNewChat(new[] { "New conversation" }), "Explicit New conversation header should stay Idle");
                AssertTrue(!ActivityMonitor.IsExplicitNewChat(new[] { "Brag Video" }), "Named chat should not match New Chat");

                var actives = new[]
                {
                    new ActivitySnapshot { SessionId = "s0", Mood = MascotMood.Thinking, IsBusy = true },
                    new ActivitySnapshot { SessionId = "s1", Mood = MascotMood.Terminal, IsBusy = true },
                };
                AssertEq(MascotMood.Idle, PetWindow.ResolveMood(false, MascotMood.Idle, true, false, true, false, false, false, false, new ActivitySnapshot { SessionId = "s2", Mood = MascotMood.Idle }, actives, actives[0]));
                AssertEq(MascotMood.Terminal, PetWindow.ResolveMood(false, MascotMood.Idle, true, false, true, false, false, false, false, actives[1], actives, actives[0]));
                AssertEq(MascotMood.Multitasking, PetWindow.ResolveMood(false, MascotMood.Idle, true, true, true, false, false, false, false, actives[1], actives, actives[0]));
                AssertEq(MascotMood.Multitasking, PetWindow.ResolveMood(false, MascotMood.Idle, true, false, false, false, false, false, false, actives[0], actives, actives[0]));
                AssertEq(MascotMood.Multitasking, PetWindow.ResolveMood(false, MascotMood.Idle, false, false, true, false, false, false, false, actives[0], actives, actives[0]));
            });

            Run("Chatbox placement adapts visual right & feet cleanly across Small, Medium, and Large", () =>
            {
                var box = new System.Windows.Rect(400, 700, 620, 140);
                foreach (double px in new[] { 3.2, 4.2, 5.2 })
                {
                    double sw = 11.0 * px, sh = 9.0 * px;
                    var pt = PetWindow.PlaceAboveChatBox(box, sw, sh, 32, 24);
                    double visualRight = pt.X + 32 + sw;
                    double visualBottom = pt.Y + 24 + sh;
                    AssertTrue(Math.Abs(visualRight - (box.Right - 28.0)) < 0.01, "Right foot should stay inset 28px clear of rounded top-right chatbox corner");
                    AssertTrue(Math.Abs(visualBottom - (box.Top + 1.0)) < 0.01, "Feet should rest cleanly on top border of chatbox");
                }
            });

            Run("Away placement supports top/bottom corners and left/middle/right taskbar locations across all sizes", () =>
            {
                AssertEq("top-right", PetSettings.NormalizeCorner(null));
                AssertEq("top-left", PetSettings.NormalizeCorner("top-left"));
                AssertEq("bottom-right", PetSettings.NormalizeCorner("bottom-right"));
                AssertEq("bottom-center", PetSettings.NormalizeCorner("bottom-center"));
                AssertEq("bottom-left", PetSettings.NormalizeCorner("bottom-left"));
                AssertEq("taskbar-left", PetSettings.NormalizeCorner("taskbar-left"));
                AssertEq("taskbar-center", PetSettings.NormalizeCorner("taskbar-center"));
                AssertEq("taskbar-right", PetSettings.NormalizeCorner("taskbar-right"));

                var work = new System.Windows.Rect(0, 0, 1920, 1032);
                var screen = new System.Windows.Rect(0, 0, 1920, 1080);
                foreach (double px in new[] { 3.2, 4.2, 5.2 })
                {
                    double sw = 11.0 * px, sh = 9.0 * px;
                    var br = PetWindow.PlaceInCorner("bottom-right", work, screen, sw, sh, 32, 24);
                    var bc = PetWindow.PlaceInCorner("bottom-center", work, screen, sw, sh, 32, 24);
                    var bl = PetWindow.PlaceInCorner("bottom-left", work, screen, sw, sh, 32, 24);
                    var tl = PetWindow.PlaceInCorner("top-left", work, screen, sw, sh, 32, 24);
                    var tr = PetWindow.PlaceInCorner("top-right", work, screen, sw, sh, 32, 24);

                    var tbl = PetWindow.PlaceInCorner("taskbar-left", work, screen, sw, sh, 32, 24);
                    var tbc = PetWindow.PlaceInCorner("taskbar-center", work, screen, sw, sh, 32, 24);
                    var tbr = PetWindow.PlaceInCorner("taskbar-right", work, screen, sw, sh, 32, 24);

                    AssertTrue(Math.Abs((br.Y + 24 + sh) - work.Bottom) < 0.01, "Bottom-right feet should rest directly above taskbar");
                    AssertTrue(Math.Abs((bc.Y + 24 + sh) - work.Bottom) < 0.01, "Bottom-middle feet should rest directly above taskbar");
                    AssertTrue(Math.Abs((bl.Y + 24 + sh) - work.Bottom) < 0.01, "Bottom-left feet should rest directly above taskbar");
                    AssertTrue(Math.Abs(br.X - tr.X) < 0.01, "Bottom-right X should align with Top-Right X");
                    AssertTrue(Math.Abs(bl.X - tl.X) < 0.01, "Bottom-left X should align with Top-Left X");
                    AssertTrue(Math.Abs((bc.X + 32 + sw * 0.5) - (work.Left + work.Width * 0.5)) < 0.01, "Bottom-middle should center horizontally");

                    // On-taskbar placements sit centered vertically inside [work.Bottom .. screen.Bottom]
                    double expectedCenterY = work.Bottom + (screen.Bottom - work.Bottom) * 0.5;
                    AssertTrue(Math.Abs((tbl.Y + 24 + sh * 0.5) - expectedCenterY) < 0.01, "Taskbar-left should center vertically on taskbar");
                    AssertTrue(Math.Abs((tbc.Y + 24 + sh * 0.5) - expectedCenterY) < 0.01, "Taskbar-center should center vertically on taskbar");
                    AssertTrue(Math.Abs((tbr.Y + 24 + sh * 0.5) - expectedCenterY) < 0.01, "Taskbar-right should center vertically on taskbar");
                    AssertTrue(Math.Abs(tbc.X - bc.X) < 0.01, "Taskbar-center X should match bottom-center X");
                }
            });

            Run("Single press squishes normally, 3+ rapid clicks dance in Idle or wince in Working, then reset cleanly", () =>
            {
                var m = new Mascot(4.0, Mascot.ClassicSkin) { Mood = MascotMood.Idle };
                m.Poke();
                AssertTrue(m.IsHurt, "Mascot should enter hurt state immediately when poked");
                AssertEq(MascotMood.Idle, m.Mood);
                m.CancelHurt();
                AssertTrue(!m.IsHurt, "CancelHurt should clear hurt state on drag");

                var t0 = new DateTime(2026, 11, 27, 1, 0, 0, DateTimeKind.Utc);
                m.RegisterClickAt(t0);
                AssertTrue(m.IsPressed && !m.IsDancing && !m.IsHurt, "1st click should play normal press squish");
                m.RegisterClickAt(t0.AddMilliseconds(250));
                AssertTrue(m.IsPressed && !m.IsDancing && !m.IsHurt, "2nd rapid click still plays normal press squish");
                m.RegisterClickAt(t0.AddMilliseconds(500));
                AssertTrue(m.IsDancing && !m.IsHurt && !m.IsPressed, "3rd continuous rapid click in Idle triggers leg-hop dance");
                // Very next single click immediately after multi-press must play normal Press, NOT re-trigger multi-press
                m.RegisterClickAt(t0.AddMilliseconds(800));
                AssertTrue(m.IsPressed && !m.IsHurt, "Single click right after multi-press resets to normal single-press squish");

                // While Working: 1-2 clicks play subtle press squish, 3+ rapid clicks trigger annoyed wince, then reset cleanly
                m.Mood = MascotMood.Working;
                m.Snap(1);
                AssertTrue(!m.IsDancing, "Transitioning to Working clears active leg-hop dance");
                m.RegisterClickAt(t0.AddSeconds(3));
                AssertTrue(m.IsPressed && !m.IsDancing && !m.IsHurt, "Single click while Working plays subtle press squish");
                m.RegisterClickAt(t0.AddSeconds(3.2));
                AssertTrue(m.IsPressed && !m.IsDancing && !m.IsHurt, "Second click while Working stays calm press squish");
                m.RegisterClickAt(t0.AddSeconds(3.4));
                AssertTrue(m.IsHurt && !m.IsDancing, "3rd rapid click while Working winces");
                m.RegisterClickAt(t0.AddSeconds(3.7));
                AssertTrue(m.IsPressed && !m.IsHurt, "Single click right after Working multi-press plays normal press, not stuck wince");
                m.CancelHurt();
                m.Dance();
                AssertTrue(!m.IsDancing, "Dance remains ignored outside Idle");

                var leftArmSample = m.BodyColorAt(1.5, 4.8);
                AssertEq(Mascot.OfficialArchPixelColor((1.5 - 0.5) / 9.5, 4.8 / 8.5, false, false, false), leftArmSample);

                double nx, ny;
                Mascot.NormalizeLookOffset(0, 0, out nx, out ny);
                AssertEq(0.0, nx);
                AssertEq(0.0, ny);
                Mascot.NormalizeLookOffset(5000, -5000, out nx, out ny);
                AssertTrue(nx > 0.95 && nx <= 1.0 && ny < -0.95 && ny >= -1.0, "Gaze should normalize cleanly within [-1, 1]");

                // Cursor gaze in Idle keeps outer eye sockets fixed while shifting pupils smoothly inside them
                m.Mood = MascotMood.Idle;
                m.SetLookOffset(-1.0, 0.0);
                for (int i = 0; i < 12; i++) m.Snap(1);
                double sockLeftL = System.Windows.Controls.Canvas.GetLeft(m.GearLayer.Children[4]);
                double pupilLeftL = System.Windows.Controls.Canvas.GetLeft(m.GearLayer.Children[6]);
                m.SetLookOffset(1.0, 0.0);
                for (int i = 0; i < 12; i++) m.Snap(1);
                double sockLeftR = System.Windows.Controls.Canvas.GetLeft(m.GearLayer.Children[4]);
                double pupilLeftR = System.Windows.Controls.Canvas.GetLeft(m.GearLayer.Children[6]);
                AssertTrue(Math.Abs(sockLeftL - sockLeftR) < 0.001, "Idle eye socket must stay anchored instead of sliding across face");
                AssertTrue(pupilLeftR > pupilLeftL + 0.5, "Idle pupil must shift right inside eye socket when cursor moves right");

                AssertEq(3, Mascot.ResolveOrbCount(0));
                AssertEq(2, Mascot.ResolveOrbCount(2));
                AssertEq(4, Mascot.ResolveOrbCount(4));
                AssertEq(8, Mascot.ResolveOrbCount(12));
            });

            Run("Dark skin shares identical multi-session, hover dot, normal-press, and multi-press logic with Classic", () =>
            {
                var baseTime = new DateTime(2026, 11, 27, 2, 0, 0, DateTimeKind.Utc);
                foreach (string skin in new[] { Mascot.ClassicSkin, Mascot.DarkSkin })
                {
                    var pet = new Mascot(4.2, skin)
                    {
                        Mood = MascotMood.Idle,
                        ActiveSessionCount = 5,
                    };
                    pet.Snap(1);

                    // Idle: 1-2 single presses -> normal press; 3 continuous rapid presses -> dance; next click -> normal press
                    pet.RegisterClickAt(baseTime);
                    AssertTrue(pet.IsPressed && !pet.IsDancing && !pet.IsHurt, skin + ": 1st click in Idle should press");
                    pet.RegisterClickAt(baseTime.AddMilliseconds(250));
                    AssertTrue(pet.IsPressed && !pet.IsDancing && !pet.IsHurt, skin + ": 2nd click in Idle should press");
                    pet.RegisterClickAt(baseTime.AddMilliseconds(500));
                    AssertTrue(pet.IsDancing && !pet.IsHurt, skin + ": 3rd rapid click in Idle should dance");
                    pet.RegisterClickAt(baseTime.AddMilliseconds(750));
                    AssertTrue(pet.IsPressed && !pet.IsHurt, skin + ": click after dance trigger resets cleanly to normal press");

                    // Multitasking: 1-2 clicks -> normal press; 3 continuous rapid clicks -> annoyed wince; next click -> normal press
                    pet.CancelHurt();
                    pet.Mood = MascotMood.Multitasking;
                    pet.Snap(2);
                    pet.RegisterClickAt(baseTime.AddSeconds(4));
                    AssertTrue(pet.IsPressed && !pet.IsDancing && !pet.IsHurt, skin + ": single click while Multitasking should normal-press");
                    pet.RegisterClickAt(baseTime.AddSeconds(4.2));
                    AssertTrue(pet.IsPressed && !pet.IsDancing && !pet.IsHurt, skin + ": two clicks while Multitasking should normal-press");
                    pet.RegisterClickAt(baseTime.AddSeconds(4.4));
                    AssertTrue(pet.IsHurt && !pet.IsDancing, skin + ": 3rd rapid click while Multitasking should wince");
                    pet.RegisterClickAt(baseTime.AddSeconds(4.6));
                    AssertTrue(pet.IsPressed && !pet.IsHurt, skin + ": single click after wince resets cleanly to normal press");

                    // Switching skin mid-session preserves mood and session orb count
                    string other = skin == Mascot.DarkSkin ? Mascot.ClassicSkin : Mascot.DarkSkin;
                    pet.SetSkin(other);
                    AssertEq(other, pet.SkinName);
                    AssertEq(MascotMood.Multitasking, pet.Mood);
                    AssertEq(5, pet.ActiveSessionCount);
                }

                var darkPet = new Mascot(4.2, Mascot.DarkSkin);
                var darkArm = darkPet.BodyColorAt(1.5, 4.8);
                AssertEq(Mascot.DarkArchPixelColor((1.5 - 0.5) / 9.5, 4.8 / 8.5, false, false, true, false), darkArm);

                var darkBusyDot = PetWindow.DotBrushForMood(MascotMood.Multitasking, true);
                var darkIdleDot = PetWindow.DotBrushForMood(MascotMood.Idle, true);
                AssertTrue(darkBusyDot != darkIdleDot, "Dark hover card must visually separate active sessions from idle");
            });

            Run("Nudge wakes a sleeping mascot and resets the idle sleep clock", () =>
            {
                var now = new DateTime(2026, 11, 26, 12, 0, 0);
                var oldActivity = now.AddMinutes(-10);
                AssertTrue(ActivityMonitor.ShouldSleep(oldActivity, DateTime.MinValue, now), "Should sleep after >6 minutes idle without nudge");
                AssertTrue(!ActivityMonitor.ShouldSleep(oldActivity, now.AddSeconds(-30), now), "Recent nudge should keep mascot awake");
                AssertTrue(ActivityMonitor.ShouldSleep(oldActivity, now.AddMinutes(-7), now), "Should return to sleep >6 minutes after last nudge");
                AssertTrue(!ActivityMonitor.ShouldSleep(DateTime.MinValue, DateTime.MinValue, now), "Undated activity should stay awake");

                var sleepingSnap = new ActivitySnapshot { Mood = MascotMood.Sleeping, ActionLabel = "Sleeping · Quiet" };
                var awakeSnap = ActivityMonitor.WakeSnapshot(sleepingSnap, now);
                AssertEq(MascotMood.Idle, awakeSnap.Mood);
                AssertEq("At rest · Ready", awakeSnap.ActionLabel);

                var m = new Mascot(4.0, Mascot.ClassicSkin) { Mood = MascotMood.Sleeping };
                var mon = new ActivityMonitor();
                mon.WakeUpAt(now);
                m.Mood = awakeSnap.Mood;
                m.Poke();
                AssertEq(MascotMood.Idle, m.Mood);
                AssertTrue(m.IsHurt, "Waking nudge should give brief tactile feedback while staying awake");
            });

            Run("Updater parses GitHub latest release and compares semantic versions", () =>
            {
                string json = "{\"tag_name\":\"v0.2.0\",\"draft\":false,\"prerelease\":false,\"assets\":[{\"name\":\"AgyPet.exe\",\"browser_download_url\":\"https://github.com/yvs1n/agypet/releases/download/v0.2.0/AgyPet.exe\",\"digest\":\"sha256:abc123\"}]}";
                var rel = Updater.ParseRelease(json);
                AssertTrue(rel != null, "Expected valid release");
                AssertEq("v0.2.0", rel.Tag);
                AssertEq("abc123", rel.Sha256);
                AssertTrue(Updater.IsNewer("v0.2.0", new Version(0, 1, 0, 0)), "0.2.0 should be newer than 0.1.0");
                AssertTrue(!Updater.IsNewer("v0.1.0", new Version(0, 1, 0, 0)), "0.1.0 should not be newer than 0.1.0");
            });

            Run("Auto-hide visibility handles inactive, minimized, CLI fallback, preview, and active menus", () =>
            {
                var defaults = new PetSettings();
                AssertTrue(!defaults.HideWhenInactive, "HideWhenInactive should default to false");
                AssertTrue(!defaults.HideWhenMinimized, "HideWhenMinimized should default to false");

                AssertTrue(!PetWindow.ShouldHideMascot(false, false, false, false, false, false, false, false, false), "Stay visible when both options are off");
                AssertTrue(PetWindow.ShouldHideMascot(true, false, false, false, false, false, false, false, false), "Hide when inactive and neither app nor CLI is running");
                AssertTrue(!PetWindow.ShouldHideMascot(true, false, true, false, false, false, false, false, false), "Stay visible when Antigravity app is open");
                AssertTrue(!PetWindow.ShouldHideMascot(true, false, false, false, true, false, false, false, false), "Stay visible when agy CLI is running");

                AssertTrue(PetWindow.ShouldHideMascot(false, true, true, true, false, false, false, false, false), "Hide when app is minimized and no CLI terminal is visible");
                AssertTrue(!PetWindow.ShouldHideMascot(false, true, true, true, true, true, false, false, false), "Stay visible when app is minimized but CLI terminal is visible");
                AssertTrue(!PetWindow.ShouldHideMascot(false, true, true, false, false, false, false, false, false), "Stay visible when app is open and unminimized");
                AssertTrue(PetWindow.ShouldHideMascot(false, true, false, false, true, false, true, false, false), "Hide when only CLI terminal exists and it is minimized");

                AssertTrue(!PetWindow.ShouldHideMascot(true, true, false, true, false, false, false, true, false), "Preview mode must keep mascot visible");
                AssertTrue(!PetWindow.ShouldHideMascot(true, true, false, true, false, false, false, false, true), "Open menu must keep mascot visible");
            });

            Run("Pin above taskbar and lock position settings resolve corners and custom coords cleanly", () =>
            {
                var defaults = new PetSettings();
                AssertTrue(!defaults.PinAboveTaskbar, "PinAboveTaskbar should default to false");
                AssertTrue(!defaults.LockPosition, "LockPosition should default to false");

                AssertTrue(!PetSettings.IsBottomOrTaskbarCorner("top-right"), "top-right is not above taskbar");
                AssertTrue(PetSettings.IsBottomOrTaskbarCorner("bottom-right"), "bottom-right is above taskbar");
                AssertTrue(PetSettings.IsBottomOrTaskbarCorner("bottom-center"), "bottom-center is above taskbar");
                AssertTrue(PetSettings.IsBottomOrTaskbarCorner("taskbar-right"), "taskbar-right is a taskbar placement");

                AssertEq("bottom-right", PetSettings.ResolvePinnedCorner("top-right", true));
                AssertEq("bottom-left", PetSettings.ResolvePinnedCorner("bottom-left", true));
                AssertEq("taskbar-center", PetSettings.ResolvePinnedCorner("taskbar-center", true));
                AssertEq("top-right", PetSettings.ResolvePinnedCorner("top-right", false));

                var work = new System.Windows.Rect(0, 0, 1920, 1032);
                var screen = new System.Windows.Rect(0, 0, 1920, 1080);
                double sw = 11.0 * 4.2, sh = 9.0 * 4.2;

                var pinnedDefault = PetWindow.ComputePinnedAwayPosition("top-right", true, false, 0, 0, work, screen, sw, sh, 32, 24);
                var expectedBottomRight = PetWindow.PlaceInCorner("bottom-right", work, screen, sw, sh, 32, 24);
                AssertEq(expectedBottomRight.X, pinnedDefault.X);
                AssertEq(expectedBottomRight.Y, pinnedDefault.Y);

                var pinnedCustom = PetWindow.ComputePinnedAwayPosition("bottom-right", true, true, 1234.5, 950.0, work, screen, sw, sh, 32, 24);
                AssertEq(1234.5, pinnedCustom.X);
                AssertEq(950.0, pinnedCustom.Y);
            });

            Run("Composer header detection excludes project tab while keeping attachments inside box", () =>
            {
                AssertTrue(WindowTracker.IsExplicitProjectLabel("No Project"), "No Project label must be recognized");
                AssertTrue(WindowTracker.IsExplicitProjectLabel("No Project v"), "No Project dropdown text must be recognized");
                AssertTrue(!WindowTracker.IsExplicitProjectLabel("PetWindow.cs"), "File name must not be treated as project tab");

                AssertTrue(WindowTracker.IsAttachmentLabel("Remove screenshot.png"), "Remove button must be treated as attachment");
                AssertTrue(WindowTracker.IsAttachmentLabel("image.png"), "Image file must be treated as attachment");
                AssertTrue(!WindowTracker.IsAttachmentLabel("No Project"), "No Project must not be treated as attachment");

                var input = new System.Windows.Rect(420, 760, 560, 42);
                var inner = new System.Windows.Rect(404, 738, 592, 114); // wraps input + bottom toolbar (bottom=852 >= 760+16)
                var outer = new System.Windows.Rect(400, 696, 600, 160); // adds 42px project bar on top
                var band = new System.Windows.Rect(404, 698, 592, 38);

                AssertTrue(WindowTracker.IsProjectHeaderBand(band, inner, outer, input, false, true, true),
                    "Should exclude explicit No Project header bar above inner composer");
                AssertTrue(WindowTracker.IsProjectHeaderBand(band, inner, outer, input, false, false, true),
                    "Should exclude compact interactive project bar above inner composer with bottom toolbar");
                AssertTrue(!WindowTracker.IsProjectHeaderBand(band, inner, outer, input, true, false, true),
                    "Must keep band when attachment signal (thumbnail/remove/file) is present");
            });

            Run("Taskbar placement auto-sizes mascot with buffer space, picks empty gaps, and keeps hover box in taskbar", () =>
            {
                var screen = new System.Windows.Rect(0, 0, 1920, 1080);
                var tallBarWork = new System.Windows.Rect(0, 0, 1920, 1024); // 56 DIP tall taskbar (large + buffer)
                var win11Work = new System.Windows.Rect(0, 0, 1920, 1032);   // 48 DIP taskbar (medium + buffer)
                var win10Work = new System.Windows.Rect(0, 0, 1920, 1040);   // 40 DIP taskbar (small + buffer)

                // Tall taskbar (56 DIP): big enough for large with >= 6 DIP buffer, only outside app
                AssertEq("large", PetWindow.ResolveEffectiveSize("small", "taskbar-right", false, false, true, false, tallBarWork, screen));
                AssertEq("small", PetWindow.ResolveEffectiveSize("small", "taskbar-right", false, false, false, false, tallBarWork, screen));

                // Continuous slider sizing clamps [2.4..7.2], formats cleanly, and respects taskbar buffer
                AssertEq(2.4, PetSettings.ClampPixelSize(1.0));
                AssertEq(7.2, PetSettings.ClampPixelSize(12.0));
                AssertEq(4.7, PetSettings.ClampPixelSize(4.74));
                var customSettings = new PetSettings();
                customSettings.SetPixelSize(6.3);
                AssertEq(6.3, customSettings.PixelSize);
                AssertEq("large", customSettings.Size);
                AssertEq("150% (6.3 px)", PetSettings.FormatSizeLabel(6.3));
                AssertEq(6.3, PetWindow.ResolveEffectivePixelSize(6.3, "taskbar-right", false, false, false, false, win11Work, screen));
                AssertTrue(PetWindow.ResolveEffectivePixelSize(6.3, "taskbar-right", false, false, true, false, win11Work, screen) <= 4.61,
                    "Oversized slider value should smoothly scale to keep 6 DIP buffer inside 48 DIP taskbar");
                AssertEq(
                    PetWindow.ResolveEffectivePixelSize(6.3, "taskbar-right", false, false, true, false, win11Work, screen),
                    PetWindow.ResolveEffectivePixelSize(6.3, "taskbar-right", false, false, true, true, win11Work, screen));
                AssertEq("medium", PetWindow.ResolveEffectiveSize("large", "taskbar-right", false, false, true, true, win11Work, screen));

                // Standard 48 DIP Win11 taskbar: avoids edge-to-edge large, uses medium with comfortable buffer outside app
                AssertEq("medium", PetWindow.ResolveEffectiveSize("small", "taskbar-right", false, false, true, false, win11Work, screen));
                AssertEq("medium", PetWindow.ResolveEffectiveSize("large", "taskbar-center", false, false, true, false, win11Work, screen));
                AssertEq("large", PetWindow.ResolveEffectiveSize("large", "taskbar-center", false, false, false, false, win11Work, screen));
                AssertEq("small", PetWindow.ResolveEffectiveSize("small", "taskbar-right", true, false, false, false, win11Work, screen));

                // 40 DIP Win10 taskbar: uses small so vertical buffer isn't edge-to-edge, restores user size inside app
                AssertEq("small", PetWindow.ResolveEffectiveSize("large", "taskbar-left", false, false, true, false, win10Work, screen));
                AssertEq("large", PetWindow.ResolveEffectiveSize("large", "taskbar-left", false, false, false, false, win10Work, screen));

                // Non-taskbar placements and custom dragged positions keep user selection
                AssertEq("small", PetWindow.ResolveEffectiveSize("small", "bottom-right", false, false, true, false, tallBarWork, screen));
                AssertEq("small", PetWindow.ResolveEffectiveSize("small", "taskbar-right", false, true, true, false, tallBarWork, screen));

                double spriteW = 11.0 * 4.2;
                double spriteH = 9.0 * 4.2;

                // Centered Win11 taskbar: Widgets [0..140], Centered Apps [700..1220], Clock/Tray [1660..1920]
                var centeredItems = new[]
                {
                    new TaskbarSpan(0, 140),
                    new TaskbarSpan(700, 1220),
                    new TaskbarSpan(1660, 1920),
                };
                TaskbarSpan gapL, gapR, gapC;
                double xL = PetWindow.ResolveTaskbarVisualLeft("taskbar-left", win11Work, centeredItems, spriteW, out gapL);
                double xR = PetWindow.ResolveTaskbarVisualLeft("taskbar-right", win11Work, centeredItems, spriteW, out gapR);
                double xC = PetWindow.ResolveTaskbarVisualLeft("taskbar-center", win11Work, centeredItems, spriteW, out gapC);

                AssertTrue(xL >= 148.0 && xL + spriteW <= 692.0, "taskbar-left must sit in empty gap after Widgets and clear of centered apps");
                AssertTrue(xR >= 1228.0 && xR + spriteW <= 1652.0, "taskbar-right must sit in empty gap clear of centered apps and clock/tray");
                AssertTrue(xC + spriteW <= 692.0 || xC >= 1228.0, "taskbar-center must dodge centered app cluster instead of overlapping icons");

                // Left-aligned taskbar: Start + App Icons [0..480], Clock/Tray [1660..1920]
                var leftItems = new[]
                {
                    new TaskbarSpan(0, 480),
                    new TaskbarSpan(1660, 1920),
                };
                TaskbarSpan gapLeftAlignedL, gapLeftAlignedR;
                double lxL = PetWindow.ResolveTaskbarVisualLeft("taskbar-left", win11Work, leftItems, spriteW, out gapLeftAlignedL);
                double lxR = PetWindow.ResolveTaskbarVisualLeft("taskbar-right", win11Work, leftItems, spriteW, out gapLeftAlignedR);
                AssertTrue(lxL >= 488.0 && lxL < 540.0, "Left-aligned taskbar-left should sit immediately after app icons");
                AssertTrue(lxR + spriteW <= 1652.0 && lxR + spriteW > 1600.0, "Left-aligned taskbar-right should sit just clear of clock/tray");

                // Hover popup centers horizontally over mascot's visual midpoint in taskbar & bottom corners
                var tbRightPt = PetWindow.PlaceInTaskbar("taskbar-right", win11Work, screen, centeredItems, spriteW, spriteH, 32, 24, out gapR);
                var cardPos = PetWindow.ComputePopupScreenPos(tbRightPt.X, tbRightPt.Y, 32, 24, spriteW, spriteH, 280.0, 108.0, win11Work, true, false);
                double popupCenterX = cardPos.X + 140.0;
                double mascotCenterX = (tbRightPt.X + 32.0) + spriteW * 0.5;
                AssertTrue(Math.Abs(popupCenterX - mascotCenterX) < 0.01, "Hover card should sit dead center over taskbar mascot");
                AssertTrue(Math.Abs((cardPos.Y + 108.0) - (tbRightPt.Y + 24.0)) < 0.01, "Hover card bottom should sit right above mascot head");

                // Near screen edges clamps cleanly inside workArea without overflowing
                var cardEdge = PetWindow.ComputePopupScreenPos(-24.0, tbRightPt.Y, 32, 24, spriteW, spriteH, 280.0, 108.0, win11Work, true, false);
                AssertTrue(cardEdge.X >= win11Work.Left + 4.0 - 0.01, "Centered hover card should clamp cleanly inside left work area");

                // WPF size slider ControlTemplate must seal and apply cleanly without FrameworkElementFactory Track errors
                Exception staErr = null;
                var t = new System.Threading.Thread(() =>
                {
                    try
                    {
                        var slider = new System.Windows.Controls.Slider
                        {
                            Minimum = PetSettings.MinPixelSize,
                            Maximum = PetSettings.MaxPixelSize,
                            Value = PetSettings.DefaultPixelSize,
                            Template = PetWindow.CreateWebsiteSliderTemplate(),
                        };
                        if (!slider.ApplyTemplate())
                            throw new Exception("Slider.ApplyTemplate returned false");
                    }
                    catch (Exception ex) { staErr = ex; }
                });
                t.SetApartmentState(System.Threading.ApartmentState.STA);
                t.Start();
                t.Join();
                if (staErr != null) throw staErr;
            });

            Run("Startup registry helper detects path changes and formats quoted command", () =>
            {
                string exeA = @"C:\Users\User\Downloads\AgyPet.exe";
                string exeB = @"C:\Apps\AgyPet\AgyPet.exe";
                AssertEq("\"" + exeA + "\"", PetSettings.FormatStartupCommand(exeA));
                AssertTrue(!PetSettings.ShouldUpdateStartupValue("\"" + exeA + "\"", exeA), "Identical path should not require registry update");
                AssertTrue(!PetSettings.ShouldUpdateStartupValue("\"" + exeA.ToLowerInvariant() + "\"", exeA), "Case-only difference on Windows should match");
                AssertTrue(PetSettings.ShouldUpdateStartupValue("\"" + exeA + "\"", exeB), "Launching from a new exe path should trigger startup update");
                AssertTrue(PetSettings.ShouldUpdateStartupValue(null, exeB), "Missing startup value should trigger update");
                AssertTrue(!PetSettings.ShouldUpdateStartupValue(null, ""), "Empty exe path must not update startup");
            });

            Run("Headphones option draws headgear and music detector smooths playback with floating notes", () =>
            {
                var defaults = new PetSettings();
                AssertTrue(!defaults.Headphones, "Headphones should default to false");

                DateTime first = DateTime.MinValue, last = DateTime.MinValue;
                bool playing = false;
                var t0 = new DateTime(2026, 11, 27, 17, 0, 0, DateTimeKind.Utc);

                // Brief single notification ding (< 350ms) should not trigger music notes
                AssertTrue(!WindowTracker.UpdateMusicPlaybackState(0.25f, false, t0, ref first, ref last, ref playing),
                    "Single brief audio spike must not immediately turn on music notes");
                // Sustained audio >= 350ms turns on music notes
                AssertTrue(WindowTracker.UpdateMusicPlaybackState(0.20f, false, t0.AddMilliseconds(450), ref first, ref last, ref playing),
                    "Sustained audio playback should enable music notes");
                // Holds through short quiet beats (<= 2.4s)
                AssertTrue(WindowTracker.UpdateMusicPlaybackState(0.001f, false, t0.AddSeconds(1.8), ref first, ref last, ref playing),
                    "Should hold music state through brief quiet passages");
                // Turns off after silence (> 2.4s)
                AssertTrue(!WindowTracker.UpdateMusicPlaybackState(0.0f, false, t0.AddSeconds(3.2), ref first, ref last, ref playing),
                    "Should turn off music notes after sustained silence");
                // Suppressed while Voice Mode is active
                WindowTracker.UpdateMusicPlaybackState(0.3f, false, t0.AddSeconds(4.0), ref first, ref last, ref playing);
                WindowTracker.UpdateMusicPlaybackState(0.3f, false, t0.AddSeconds(4.5), ref first, ref last, ref playing);
                AssertTrue(!WindowTracker.UpdateMusicPlaybackState(0.3f, true, t0.AddSeconds(4.8), ref first, ref last, ref playing),
                    "Voice Mode must suppress music notes");

                foreach (string skin in new[] { Mascot.ClassicSkin, Mascot.DarkSkin })
                {
                    var m = new Mascot(4.2, skin) { Mood = MascotMood.Idle, ShowHeadphones = false, IsPlayingMusic = false };
                    m.Snap(1);
                    int baseGear = m.GearLayer.Children.Count;
                    int baseAir = m.AirLayer.Children.Count;

                    m.ShowHeadphones = true;
                    m.Snap(2);
                    AssertEq(baseGear, m.GearLayer.Children.Count);
                    AssertEq(baseAir, m.AirLayer.Children.Count);

                    m.IsPlayingMusic = true;
                    m.Snap(3);
                    AssertTrue(m.GearLayer.Children.Count > baseGear, skin + ": headphones should render on gear layer when music plays");
                    AssertTrue(m.AirLayer.Children.Count > baseAir, skin + ": music playback should draw floating notes in air layer");

                    // Sleeping compact posture also keeps headphones cleanly
                    m.Mood = MascotMood.Sleeping;
                    m.Snap(4);
                    AssertTrue(m.GearLayer.Children.Count > 4, skin + ": sleeping compact posture should still draw headphones");
                }
            });

            Run("Z-order spot visibility keeps Agy perched when tray or non-overlapping windows are open", () =>
            {
                var chatBox = new System.Windows.Rect(1000, 740, 820, 140);
                var appSpot = WindowTracker.ComputeAppSpotPx(chatBox);

                // Taskbar at bottom and a browser on the left do NOT cover the chatbox perch spot
                var nonCovering = new[]
                {
                    new System.Windows.Rect(0, 1032, 1920, 48),   // Windows taskbar
                    new System.Windows.Rect(1620, 860, 280, 168), // System tray popup in bottom-right corner below chatbox
                    new System.Windows.Rect(0, 0, 960, 1032),     // Side-by-side window on left half
                };
                AssertTrue(!WindowTracker.IsSpotOccludedByWindows(appSpot, nonCovering),
                    "Taskbar, non-covering tray popup, and left-half window must not occlude right-side chatbox spot");

                // A window covering the chatbox spot DOES occlude it
                var covering = new[]
                {
                    new System.Windows.Rect(900, 620, 950, 300),
                };
                AssertTrue(WindowTracker.IsSpotOccludedByWindows(appSpot, covering),
                    "Window covering the chatbox perch spot must occlude it");
            });

            Run("Clicking/petting squishes mascot from bottom-center feet anchor without moving legs", () =>
            {
                var m = new Mascot(4.2, Mascot.ClassicSkin) { Mood = MascotMood.Idle };
                m.Snap(1);
                AssertEq(11.0 * 4.2, m.SpriteLayer.Width);
                AssertEq(9.0 * 4.2, m.SpriteLayer.Height);
                AssertEq(5.5 * 4.2, m.RecoilTransform.CenterX);
                AssertEq(9.0 * 4.2, m.RecoilTransform.CenterY);

                m.Press();
                AssertTrue(m.IsPressed, "Press should activate press state");
                AssertTrue(m.RecoilTransform.ScaleY < 1.0 && m.RecoilTransform.ScaleX > 1.0,
                    "Press should squish vertically and widen slightly horizontally");
                AssertTrue(Math.Abs(System.Windows.Controls.Canvas.GetTop(m.SpriteLayer)) < 0.001,
                    "Idle Press must not shift sprite Canvas Top vertically");

                var feetBefore = new System.Windows.Point(5.5 * 4.2, 9.0 * 4.2);
                var feetAfter = m.RecoilTransform.Transform(feetBefore);
                var headAfter = m.RecoilTransform.Transform(new System.Windows.Point(5.5 * 4.2, 0.0));
                AssertTrue(Math.Abs(feetAfter.Y - feetBefore.Y) < 0.001,
                    "Feet bottom pivot must stay fixed at 0 vertical shift during squish");
                AssertTrue(headAfter.Y > 0.3 * 4.2,
                    "Head top must dip downward when petting squish is active");

                // Dance uses the two 3-rect eighth-notes (6 air children total) just like speaker active notes
                m.Dance();
                AssertTrue(m.IsDancing, "Dance should activate dancing state in Idle");
                AssertEq(6, m.AirLayer.Children.Count);
            });

            Console.WriteLine("Tests complete: " + passed + " passed, " + failed + " failed.");
            return failed == 0 ? 0 : 1;
        }

        static void Run(string name, Action body)
        {
            try { body(); passed++; Console.WriteLine("  PASS  " + name); }
            catch (Exception e) { failed++; Console.WriteLine("  FAIL  " + name + " -> " + e.Message); }
        }

        static void AssertEq<T>(T expected, T actual)
        {
            if (!Equals(expected, actual))
                throw new Exception("Expected '" + expected + "' but got '" + actual + "'");
        }

        static void AssertTrue(bool cond, string msg)
        {
            if (!cond) throw new Exception(msg);
        }
    }
}
