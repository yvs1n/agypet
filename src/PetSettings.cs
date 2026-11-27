using System;
using System.IO;
#if NET8_0_OR_GREATER
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
#else
using System.Web.Script.Serialization;
using Microsoft.Win32;
#endif

namespace AgyPet
{
    public class PetSettings
    {
        public static readonly string DataDir = ResolveDataDir();
        static readonly string FilePath = Path.Combine(DataDir, "settings.json");

        static string ResolveDataDir()
        {
#if NET8_0_OR_GREATER
            if (OperatingSystem.IsMacOS())
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgyPet");
#endif
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgyPet");
        }
#if !NET8_0_OR_GREATER
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "AgyPet";
#endif

        // "classic" (Official Antigravity colors) | "dark" (New Dark Monochrome Remake)
        public string Skin { get; set; }

        // "pin-chat" (pins to top-right of Antigravity App / IDE chat, docks to corner when minimized)
        // "free" (stays wherever you drag it on screen)
        public string PlacementMode { get; set; }

        // "top-right" | "top-left" | "bottom-right" | "bottom-center" | "bottom-left"
        // | "taskbar-left" | "taskbar-center" | "taskbar-right" — where the mascot rests when Antigravity is away/minimized
        public string MinimizedCorner { get; set; }

        public struct LocationOption
        {
            public readonly string Id, Label;
            public LocationOption(string id, string label) { Id = id; Label = label; }
        }

        public static readonly LocationOption[] AwayLocations =
        {
            new LocationOption("top-right", "Top-Right"),
            new LocationOption("top-left", "Top-Left"),
            new LocationOption("bottom-right", "Bottom-Right (Above Taskbar)"),
            new LocationOption("bottom-center", "Bottom-Middle (Above Taskbar)"),
            new LocationOption("bottom-left", "Bottom-Left (Above Taskbar)"),
            new LocationOption("taskbar-left", "Taskbar · Left"),
            new LocationOption("taskbar-center", "Taskbar · Middle"),
            new LocationOption("taskbar-right", "Taskbar · Right"),
        };

        // "bottom-right" | "bottom-center" | "bottom-left" | "top-right" | "top-left" | "off"
        public string CliPlacement { get; set; }

        public static readonly LocationOption[] CliLocations =
        {
            new LocationOption("bottom-right", "Bottom-Right (CLI Prompt)"),
            new LocationOption("bottom-center", "Bottom-Middle (Prompt Center)"),
            new LocationOption("bottom-left", "Bottom-Left (Above Prompt)"),
            new LocationOption("top-right", "Top-Right (Window Edge)"),
            new LocationOption("top-left", "Top-Left (Window Edge)"),
            new LocationOption("off", "Off (Corner / Taskbar Only)"),
        };

        public const double MinPixelSize = 2.4;
        public const double MaxPixelSize = 7.2;
        public const double DefaultPixelSize = 4.2;

        // Legacy bucket ("small" | "medium" | "large") synced cleanly alongside precise PixelSize
        public string Size { get; set; }
        public double PixelSize { get; set; }

        // Custom offset relative to Antigravity window right/top when dragged in pin-chat mode
        public bool HasCustomChatOffset { get; set; }
        public double ChatOffsetFromRight { get; set; }
        public double ChatOffsetFromTop { get; set; }

        // Custom screen position when dragged in free mode (or while minimized)
        public bool HasCustomFreePos { get; set; }
        public double FreeX { get; set; }
        public double FreeY { get; set; }

        public bool ShowHoverCard { get; set; }
        public bool HideWhenInactive { get; set; }
        public bool HideWhenMinimized { get; set; }
        public bool PinAboveTaskbar { get; set; }
        public bool LockPosition { get; set; }
        public bool StartWithWindows { get; set; }
        public bool AutoUpdate { get; set; }

        public static string DefaultMinimizedCornerFor(bool isMacOs)
        {
            return isMacOs ? "bottom-center" : "top-right";
        }

        public static string DefaultMinimizedCorner()
        {
#if NET8_0_OR_GREATER
            return DefaultMinimizedCornerFor(OperatingSystem.IsMacOS());
#else
            return "top-right";
#endif
        }

        public PetSettings()
        {
            Skin = MascotCore.ClassicSkin;
            PlacementMode = "pin-chat";
            MinimizedCorner = DefaultMinimizedCorner();
            CliPlacement = "bottom-right";
            Size = "medium";
            PixelSize = DefaultPixelSize;
            HasCustomChatOffset = false;
            ChatOffsetFromRight = 92;
            ChatOffsetFromTop = 64;
            HasCustomFreePos = false;
            FreeX = -1;
            FreeY = -1;
            ShowHoverCard = true;
            HideWhenInactive = false;
            HideWhenMinimized = false;
            PinAboveTaskbar = false;
            LockPosition = false;
            StartWithWindows = false;
            AutoUpdate = true;
        }

        public static double PixelSizeFor(string size)
        {
            if (size == "small") return 3.2;
            if (size == "large") return 5.2;
            return DefaultPixelSize;
        }

        public static double ClampPixelSize(double px)
        {
            if (double.IsNaN(px) || double.IsInfinity(px) || px <= 0) return DefaultPixelSize;
            double rounded = Math.Round(px, 1);
            return Math.Max(MinPixelSize, Math.Min(MaxPixelSize, rounded));
        }

        public static string BucketForPixelSize(double px)
        {
            double c = ClampPixelSize(px);
            if (c < 3.6) return "small";
            if (c > 4.8) return "large";
            return "medium";
        }

        public static string FormatSizeLabel(double px)
        {
            double c = ClampPixelSize(px);
            int pct = (int)Math.Round(c / DefaultPixelSize * 100.0);
            return string.Format("{0}% ({1:0.0} px)", pct, c);
        }

        public void SetPixelSize(double px)
        {
            PixelSize = ClampPixelSize(px);
            Size = BucketForPixelSize(PixelSize);
        }

        public static PetSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string raw = File.ReadAllText(FilePath);
#if NET8_0_OR_GREATER
                    var s = JsonSerializer.Deserialize(raw, PetSettingsJsonContext.Default.PetSettings);
#else
                    var s = new JavaScriptSerializer().Deserialize<PetSettings>(raw);
#endif
                    if (s != null) { s.Normalize(); return s; }
                }
            }
            catch { }
            return new PetSettings();
        }

        public void Save()
        {
            try
            {
                Normalize();
                Directory.CreateDirectory(DataDir);
                string tmp = FilePath + ".tmp";
#if NET8_0_OR_GREATER
                string text = JsonSerializer.Serialize(this, PetSettingsJsonContext.Default.PetSettings);
#else
                string text = Json.Pretty(new JavaScriptSerializer().Serialize(this));
#endif
                File.WriteAllText(tmp, text);
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            catch { }
        }

        public static bool IsBottomOrTaskbarCorner(string corner)
        {
            string c = NormalizeCorner(corner);
            return c.StartsWith("bottom-", StringComparison.Ordinal) ||
                   c.StartsWith("taskbar-", StringComparison.Ordinal);
        }

        public static string ResolvePinnedCorner(string corner, bool pinAboveTaskbar)
        {
            string normalized = NormalizeCorner(corner);
            if (pinAboveTaskbar && !IsBottomOrTaskbarCorner(normalized))
                return "bottom-right";
            return normalized;
        }

        public static string NormalizeCorner(string corner)
        {
            if (corner != null)
            {
                foreach (var opt in AwayLocations)
                    if (string.Equals(corner, opt.Id, StringComparison.OrdinalIgnoreCase))
                        return opt.Id;
            }
            return DefaultMinimizedCorner();
        }

        public static string NormalizeCliPlacement(string placement)
        {
            if (placement != null)
            {
                foreach (var opt in CliLocations)
                    if (string.Equals(placement, opt.Id, StringComparison.OrdinalIgnoreCase))
                        return opt.Id;
            }
            return "bottom-right";
        }

        public void Normalize()
        {
            Skin = MascotCore.NormalizeSkin(Skin);
            PlacementMode = PlacementMode == "free" ? "free" : "pin-chat";
            MinimizedCorner = NormalizeCorner(MinimizedCorner);
            CliPlacement = NormalizeCliPlacement(CliPlacement);
            if (Size != "small" && Size != "medium" && Size != "large") Size = "medium";
            if (double.IsNaN(PixelSize) || PixelSize <= 0)
                PixelSize = PixelSizeFor(Size);
            else
                PixelSize = ClampPixelSize(PixelSize);
            Size = BucketForPixelSize(PixelSize);
            if (double.IsNaN(ChatOffsetFromRight) || ChatOffsetFromRight < 8 || ChatOffsetFromRight > 1200) ChatOffsetFromRight = 92;
            if (double.IsNaN(ChatOffsetFromTop) || ChatOffsetFromTop < 8 || ChatOffsetFromTop > 1200) ChatOffsetFromTop = 64;
        }

        public static void ApplyStartup(bool enable, string exePath)
        {
            try
            {
#if NET8_0_OR_GREATER
                if (OperatingSystem.IsMacOS())
                {
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string plist = Path.Combine(home, "Library", "LaunchAgents", "me.yassinr.agypet.plist");
                    if (enable && !string.IsNullOrEmpty(exePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(plist));
                        string escaped = exePath.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
                        File.WriteAllText(plist,
                            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                            "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                            "<plist version=\"1.0\">\n<dict>\n" +
                            "  <key>Label</key><string>me.yassinr.agypet</string>\n" +
                            "  <key>ProgramArguments</key><array><string>" + escaped + "</string></array>\n" +
                            "  <key>RunAtLoad</key><true/>\n" +
                            "</dict>\n</plist>\n");
                    }
                    else if (File.Exists(plist))
                    {
                        File.Delete(plist);
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    string cfg = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                    string desktop = Path.Combine(cfg, "autostart", "agypet.desktop");
                    if (enable && !string.IsNullOrEmpty(exePath))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(desktop));
                        File.WriteAllText(desktop,
                            "[Desktop Entry]\n" +
                            "Type=Application\n" +
                            "Name=AgyPet\n" +
                            "Comment=Desktop companion for Antigravity\n" +
                            "Exec=\"" + exePath + "\"\n" +
                            "Terminal=false\n" +
                            "X-GNOME-Autostart-enabled=true\n");
                    }
                    else if (File.Exists(desktop))
                    {
                        File.Delete(desktop);
                    }
                }
                else if (OperatingSystem.IsWindows())
                {
                    string args = enable && !string.IsNullOrEmpty(exePath)
                        ? @"add ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v AgyPet /t REG_SZ /d ""\""" + exePath + @"\"""" /f"
                        : @"delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Run"" /v AgyPet /f";
                    Process.Start(new ProcessStartInfo("reg.exe", args) { CreateNoWindow = true, UseShellExecute = false });
                }
#else
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable && !string.IsNullOrEmpty(exePath))
                        key.SetValue(RunValueName, "\"" + exePath + "\"");
                    else
                        key.DeleteValue(RunValueName, false);
                }
#endif
            }
            catch { }
        }
    }

#if NET8_0_OR_GREATER
    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(PetSettings))]
    internal partial class PetSettingsJsonContext : JsonSerializerContext
    {
    }
#endif
}
