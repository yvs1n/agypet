using System;
using System.IO;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace AgyPet
{
    public class PetSettings
    {
        public static readonly string DataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgyPet");
        static readonly string FilePath = Path.Combine(DataDir, "settings.json");
        const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValueName = "AgyPet";

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

        // "small" | "medium" | "large"
        public string Size { get; set; }

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

        public PetSettings()
        {
            Skin = Mascot.ClassicSkin;
            PlacementMode = "pin-chat";
            MinimizedCorner = "top-right";
            CliPlacement = "bottom-right";
            Size = "medium";
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
            return 4.2;
        }

        public double PixelSize
        {
            get { return PixelSizeFor(Size); }
        }

        public static PetSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var s = new JavaScriptSerializer().Deserialize<PetSettings>(File.ReadAllText(FilePath));
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
                File.WriteAllText(tmp, Json.Pretty(new JavaScriptSerializer().Serialize(this)));
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
            return "top-right";
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
            Skin = Mascot.NormalizeSkin(Skin);
            PlacementMode = PlacementMode == "free" ? "free" : "pin-chat";
            MinimizedCorner = NormalizeCorner(MinimizedCorner);
            CliPlacement = NormalizeCliPlacement(CliPlacement);
            if (Size != "small" && Size != "medium" && Size != "large") Size = "medium";
            if (double.IsNaN(ChatOffsetFromRight) || ChatOffsetFromRight < 8 || ChatOffsetFromRight > 1200) ChatOffsetFromRight = 92;
            if (double.IsNaN(ChatOffsetFromTop) || ChatOffsetFromTop < 8 || ChatOffsetFromTop > 1200) ChatOffsetFromTop = 64;
        }

        public static void ApplyStartup(bool enable, string exePath)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enable && !string.IsNullOrEmpty(exePath))
                        key.SetValue(RunValueName, "\"" + exePath + "\"");
                    else
                        key.DeleteValue(RunValueName, false);
                }
            }
            catch { }
        }
    }
}
