using System;
using System.IO;
using AgyPet;

namespace AgyPet.CrossTests
{
    static class CrossTests
    {
        static int passed, failed;

        [STAThread]
        static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            Run("Shared MascotCore palette & masks render vibrant Classic & crisp Dark skins", () =>
            {
                AssertEq("classic", MascotCore.NormalizeSkin(null));
                AssertEq("dark", MascotCore.NormalizeSkin("obsidian"));
                var topCenter = MascotCore.OfficialArchPixel(0.42, 0.08, false, true, false);
                var bottomLeg = MascotCore.OfficialArchPixel(0.15, 0.90, false, false, false);
                AssertTrue(topCenter.R > 200 && topCenter.G > 110, "Official top should be gold/orange");
                AssertTrue(bottomLeg.B > 200 && bottomLeg.B > bottomLeg.R, "Official leg should be blue");

                var arm = MascotCore.BodyRgbAt(false, 1.5, 4.8);
                var expected = MascotCore.OfficialArchPixel((1.5 - 0.5) / 9.5, 4.8 / 8.5, false, false, false);
                AssertEq(expected.R, arm.R);
                AssertEq(expected.G, arm.G);
                AssertEq(expected.B, arm.B);

                var darkArm = MascotCore.BodyRgbAt(true, 1.5, 4.8);
                var expectedDark = MascotCore.DarkArchPixel((1.5 - 0.5) / 9.5, 4.8 / 8.5, false, false, true, false);
                AssertEq(expectedDark.R, darkArm.R);
                AssertEq(expectedDark.G, darkArm.G);
                AssertEq(expectedDark.B, darkArm.B);

                AssertTrue(!Equals(PetTheme.DotBrushForMood(MascotMood.Multitasking, true), PetTheme.DotBrushForMood(MascotMood.Idle, true)),
                    "Cross hover card dark dots must separate Multitasking from Idle");

                AssertEq(3, MascotCore.ResolveOrbCount(0));
                AssertEq(2, MascotCore.ResolveOrbCount(2));
                AssertEq(6, MascotCore.ResolveOrbCount(6));
                AssertEq(8, MascotCore.ResolveOrbCount(14));
            });

            Run("Procedural PNG icon encodes valid signature & RGBA dimensions without System.Drawing", () =>
            {
                byte[] png = PetTheme.RenderMascotPng(32, false);
                byte[] pngDark = PetTheme.RenderMascotPng(32, true);
                AssertTrue(png != null && png.Length > 200, "PNG output should be non-empty");
                AssertTrue(pngDark != null && pngDark.Length > 200, "Dark PNG output should be non-empty");
                AssertEq((byte)137, png[0]);
                AssertEq((byte)80, png[1]);
                AssertEq((byte)78, png[2]);
                AssertEq((byte)71, png[3]);
            });

            Run("Updater parses platform assets for Windows, macOS, and Linux arm64/x64", () =>
            {
                string json = "{\"tag_name\":\"v0.4.0\",\"draft\":false,\"prerelease\":false,\"assets\":[" +
                    "{\"name\":\"AgyPet.exe\",\"browser_download_url\":\"https://github.com/yvs1n/agypet/releases/download/v0.4.0/AgyPet.exe\",\"digest\":\"sha256:win1\"}," +
                    "{\"name\":\"AgyPet-macOS.dmg\",\"browser_download_url\":\"https://github.com/yvs1n/agypet/releases/download/v0.4.0/AgyPet-macOS.dmg\",\"digest\":\"sha256:mac1\"}," +
                    "{\"name\":\"AgyPet-linux-x64\",\"browser_download_url\":\"https://github.com/yvs1n/agypet/releases/download/v0.4.0/AgyPet-linux-x64\",\"digest\":\"sha256:linx1\"}," +
                    "{\"name\":\"AgyPet-linux-arm64\",\"browser_download_url\":\"https://github.com/yvs1n/agypet/releases/download/v0.4.0/AgyPet-linux-arm64\",\"digest\":\"sha256:lina1\"}" +
                    "]}";

                AssertEq("mac1", Updater.ParseRelease(json, "AgyPet-macOS.dmg").Sha256);
                AssertEq("linx1", Updater.ParseRelease(json, "AgyPet-linux-x64").Sha256);
                AssertEq("lina1", Updater.ParseRelease(json, "AgyPet-linux-arm64").Sha256);
                AssertEq("win1", Updater.ParseRelease(json, "AgyPet.exe").Sha256);
                AssertTrue(Updater.IsNewer("v0.4.0", new Version(0, 3, 8, 0)), "0.4.0 > 0.3.8");
            });

            Run("PlatformWindowTracker parses Antigravity titles, process names, and terminal hosts", () =>
            {
                AssertTrue(PlatformWindowTracker.IsAntigravityName("Antigravity"), "Antigravity app");
                AssertTrue(PlatformWindowTracker.IsAntigravityName("Antigravity.app"), "macOS .app name");
                AssertTrue(PlatformWindowTracker.IsTerminalName("iTerm2"), "macOS iTerm2");
                AssertTrue(PlatformWindowTracker.IsTerminalName("gnome-terminal-server"), "Linux GNOME terminal");
                AssertTrue(PlatformWindowTracker.IsTerminalName("Ghostty"), "Ghostty terminal");

                var cands = PlatformWindowTracker.ParseWindowTitleCandidates("Implement Cross Platform - Antigravity");
                AssertEq(1, cands.Length);
                AssertEq("Implement Cross Platform", cands[0]);
            });

            Run("Cross placement honors taskbar buffer sizing & bottom/dock corners", () =>
            {
                var screen = new ScreenRectDip(0, 0, 1440, 900);
                var work = new ScreenRectDip(0, 25, 1440, 827); // 48 DIP dock at bottom
                AssertEq("medium", PetWindow.ResolveEffectiveSize("small", "taskbar-right", false, false, true, false, work, screen));
                AssertEq("small", PetWindow.ResolveEffectiveSize("small", "taskbar-right", false, false, false, false, work, screen));
                AssertEq(
                    PetWindow.ResolveEffectivePixelSize(6.3, "taskbar-right", false, false, true, false, work, screen),
                    PetWindow.ResolveEffectivePixelSize(6.3, "taskbar-right", false, false, true, true, work, screen));

                double lx, ty;
                PetWindow.PlaceInCorner("bottom-right", work, screen, 46.2, 37.8, 36, 24, out lx, out ty);
                AssertEq(work.Bottom - 37.8 - 24, ty);
            });

            Run("macOS menu bar template icon, CLI detection, right-click disable, and default above-Dock placement", () =>
            {
                byte[] macIcon = PetTheme.RenderMacMenuBarPng(36);
                AssertTrue(macIcon != null && macIcon.Length > 200, "macOS menu bar PNG should be non-empty");
                AssertEq((byte)137, macIcon[0]);
                AssertEq((byte)80, macIcon[1]);
                AssertEq((byte)78, macIcon[2]);
                AssertEq((byte)71, macIcon[3]);

                AssertTrue(PlatformWindowTracker.IsCliProcessName("agy"), "agy CLI process");
                AssertTrue(PlatformWindowTracker.IsCliProcessName("agy-cli"), "agy-cli process");
                AssertTrue(PlatformWindowTracker.IsCliProcessName("antigravity-cli"), "antigravity-cli process");
                AssertTrue(!PlatformWindowTracker.IsCliProcessName("Antigravity"), "Antigravity GUI is not CLI process");

                AssertTrue(!PetWindow.ShouldAllowRightClickContextMenu(true), "macOS disables right-click context menu");
                AssertTrue(PetWindow.ShouldAllowRightClickContextMenu(false), "Non-macOS keeps right-click context menu");

                AssertEq("bottom-center", PetWindow.ResolveAwayCornerForPlatform(true, false, "top-right"));
                AssertEq("top-right", PetWindow.ResolveAwayCornerForPlatform(true, true, "top-right"));
                AssertEq("top-right", PetWindow.ResolveAwayCornerForPlatform(false, false, "top-right"));

                var screen = new ScreenRectDip(0, 0, 1440, 900);
                var work = new ScreenRectDip(0, 25, 1440, 827);
                double lx, ty;
                PetWindow.PlaceInCorner("bottom-center", work, screen, 46.2, 37.8, 36, 24, out lx, out ty);
                AssertEq(work.Left + (work.Width - (46.2 + 72)) * 0.5, lx);
                AssertEq(work.Bottom - 37.8 - 24, ty);
            });

            Run("Transcript parser extracts steps, focus, and moods in .NET 8 build cleanly", () =>
            {
                string[] lines =
                {
                    "{\"type\":\"PLANNER_RESPONSE\",\"tool_calls\":[{\"name\":\"update_focus\",\"args\":{\"focus\":\"Testing macOS & Linux\"}},{\"name\":\"update_steps\",\"args\":{\"steps\":\"[{\\\"title\\\":\\\"Build macOS\\\",\\\"status\\\":\\\"done\\\"},{\\\"title\\\":\\\"Test Linux\\\",\\\"status\\\":\\\"in_progress\\\"}]\"}},{\"name\":\"run_command\",\"args\":{\"CommandLine\":\"dotnet test\"}}]}"
                };
                var info = ActivityMonitor.ParseTranscriptLines(lines);
                AssertEq(MascotMood.Terminal, info.Mood);
                AssertEq(50, info.PercentDone);
                AssertEq(1, info.StepsInProgress);
                AssertEq("Testing macOS & Linux", info.FocusDetail);

                var s = new PetSettings();
                s.SetPixelSize(5.4);
                string raw = System.Text.Json.JsonSerializer.Serialize(s, PetSettingsJsonContext.Default.PetSettings);
                var back = System.Text.Json.JsonSerializer.Deserialize(raw, PetSettingsJsonContext.Default.PetSettings);
                back.Normalize();
                AssertEq(5.4, back.PixelSize);
                AssertEq("large", back.Size);

                // Submenu body factory creates distinct instances per hover so re-entering a submenu never crashes
                var firstSub = PetWindow.BuildFreshMenuBody(() => new Avalonia.Controls.Control[] { new Avalonia.Controls.TextBlock { Text = "A" } });
                var secondSub = PetWindow.BuildFreshMenuBody(() => new Avalonia.Controls.Control[] { new Avalonia.Controls.TextBlock { Text = "A" } });
                AssertTrue(!object.ReferenceEquals(firstSub, secondSub), "Each submenu open must produce a fresh detached body");
            });

            Console.WriteLine("Cross tests complete: " + passed + " passed, " + failed + " failed.");
            return failed == 0 ? 0 : 1;
        }

        static void Run(string name, Action body)
        {
            try { body(); passed++; Console.WriteLine("  PASS  " + name); }
            catch (Exception ex) { failed++; Console.WriteLine("  FAIL  " + name + " -> " + ex.Message); }
        }

        static void AssertTrue(bool cond, string msg) { if (!cond) throw new Exception(msg); }
        static void AssertEq<T>(T exp, T act)
        {
            if (!Equals(exp, act)) throw new Exception("Expected '" + exp + "' got '" + act + "'");
        }
    }
}
