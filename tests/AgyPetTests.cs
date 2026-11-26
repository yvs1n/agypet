using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
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
                AssertEq(50, info.PercentLeft);
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

                AssertTrue(WindowTracker.IsAntigravityProcessName("Antigravity"), "Antigravity.exe must match");
                AssertTrue(WindowTracker.IsAntigravityProcessName("Antigravity IDE"), "Antigravity IDE must match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("chrome"), "Chrome must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("msedge"), "Edge must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("firefox"), "Firefox must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("AgyPet"), "Self must never match");
                AssertTrue(!WindowTracker.IsAntigravityProcessName("hp-one-agent-service"), "Unrelated agent service must never match");
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
                    AssertTrue(Math.Abs(visualRight - (box.Right - 14.0)) < 0.01, "Right edge should stay inset 14px inside chatbox");
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

            Run("Poke easter egg triggers hurt state while preserving active Mood", () =>
            {
                var m = new Mascot(4.0, Mascot.ClassicSkin) { Mood = MascotMood.Thinking };
                m.Poke();
                AssertTrue(m.IsHurt, "Mascot should enter hurt state immediately when poked");
                AssertEq(MascotMood.Thinking, m.Mood);
                m.CancelHurt();
                AssertTrue(!m.IsHurt, "CancelHurt should clear hurt state on drag");
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

            Run("Context menu hit-testing and outside-click dismissal handle main menu, submenus, and focus loss", () =>
            {
                var rects = new List<Rect>
                {
                    new Rect(100, 100, 220, 320),
                    new Rect(320, 140, 180, 160)
                };
                AssertTrue(PetWindow.IsPointInsideAnyRect(150, 200, rects), "Point inside main menu should be treated as inside");
                AssertTrue(PetWindow.IsPointInsideAnyRect(380, 200, rects), "Point inside open submenu should be treated as inside");
                AssertTrue(!PetWindow.IsPointInsideAnyRect(50, 50, rects), "Point outside menu and submenu should be outside");
                AssertTrue(!PetWindow.IsPointInsideAnyRect(150, 200, null), "Null rect list should return false");
                AssertTrue(!PetWindow.IsPointInsideAnyRect(150, 200, new List<Rect>()), "Empty rect list should return false");

                AssertTrue(!PetWindow.ShouldDismissMenu(true, true, false, false, false, false), "Clicking inside menu or unmeasured popup should not dismiss");
                AssertTrue(!PetWindow.ShouldDismissMenu(false, false, false, false, false, false), "Idle cursor outside menu without click or focus loss should keep menu open");
                AssertTrue(PetWindow.ShouldDismissMenu(false, true, false, false, false, false), "Left-click outside menu must dismiss");
                AssertTrue(PetWindow.ShouldDismissMenu(false, false, true, false, false, false), "Right-click outside menu must dismiss");
                AssertTrue(PetWindow.ShouldDismissMenu(false, false, false, true, false, false), "Middle-click outside menu must dismiss");
                AssertTrue(PetWindow.ShouldDismissMenu(true, false, false, false, true, false), "Escape key must dismiss menu");
                AssertTrue(PetWindow.ShouldDismissMenu(true, false, false, false, false, true), "Foreground moving to another app must dismiss menu");
            });

            Run("Taskbar Z-order guard keeps mascot topmost while allowing own popups above it", () =>
            {
                IntPtr topmost = new IntPtr(-1);
                IntPtr tray = new IntPtr(1234);
                IntPtr ownPopup = new IntPtr(5678);

                AssertTrue(PetWindow.ShouldForceTopmostOnPosChanging(true, 0, tray, 999, 100), "External Z-order demotion while docked on taskbar must be forced back to HWND_TOPMOST");
                AssertTrue(PetWindow.ShouldForceTopmostOnPosChanging(true, 0, IntPtr.Zero, 0, 100), "HWND_TOP demotion while docked on taskbar must be forced back to HWND_TOPMOST");
                AssertTrue(!PetWindow.ShouldForceTopmostOnPosChanging(false, 0, tray, 999, 100), "Do not override Z-order when not docked on taskbar");
                AssertTrue(!PetWindow.ShouldForceTopmostOnPosChanging(true, 0x0004, tray, 999, 100), "Do not override when SWP_NOZORDER is set");
                AssertTrue(!PetWindow.ShouldForceTopmostOnPosChanging(true, 0, topmost, 0, 100), "Already HWND_TOPMOST needs no override");
                AssertTrue(!PetWindow.ShouldForceTopmostOnPosChanging(true, 0, ownPopup, 100, 100), "Own popups (context menu / hover card) must stay allowed above mascot");
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
