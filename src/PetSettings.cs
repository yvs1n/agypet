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

        // "top-right" | "top-left" — where the mascot rests when Antigravity is minimized
        public string MinimizedCorner { get; set; }

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
        public bool StartWithWindows { get; set; }
        public bool AutoUpdate { get; set; }

        public PetSettings()
        {
            Skin = Mascot.ClassicSkin;
            PlacementMode = "pin-chat";
            MinimizedCorner = "top-right";
            Size = "medium";
            HasCustomChatOffset = false;
            ChatOffsetFromRight = 92;
            ChatOffsetFromTop = 64;
            HasCustomFreePos = false;
            FreeX = -1;
            FreeY = -1;
            ShowHoverCard = true;
            StartWithWindows = false;
            AutoUpdate = true;
        }

        public double PixelSize
        {
            get
            {
                if (Size == "small") return 3.2;
                if (Size == "large") return 5.2;
                return 4.2;
            }
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

        public void Normalize()
        {
            Skin = Mascot.NormalizeSkin(Skin);
            PlacementMode = PlacementMode == "free" ? "free" : "pin-chat";
            MinimizedCorner = MinimizedCorner == "top-left" ? "top-left" : "top-right";
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
