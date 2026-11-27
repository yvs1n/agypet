using System;

namespace AgyPet
{
    public enum MascotMood
    {
        Idle,
        Sleeping,
        Listening,
        Thinking,
        Speaking,
        Typing,
        Searching,
        Reading,
        Working,
        Terminal,
        Browsing,
        Delegating,
        Multitasking,
        Background,
        Alert,
        Question,
        Done,
        Error
    }

    public struct RgbColor
    {
        public readonly byte R, G, B;
        public RgbColor(byte r, byte g, byte b) { R = r; G = g; B = b; }
    }

    // Pure framework-independent Antigravity mascot palette, pixel masks, click-streak & gaze helpers
    // shared identically across Windows WPF (Mascot.cs) and macOS/Linux Avalonia (MascotView.cs).
    public static class MascotCore
    {
        public const string ClassicSkin = "classic";
        public const string DarkSkin = "dark";

        public static readonly RgbColor AgyBlueCore = new RgbColor(0x1E, 0x78, 0xFF);
        public static readonly RgbColor AgyBlueLeg = new RgbColor(0x2C, 0x86, 0xFF);
        public static readonly RgbColor AgyGold = new RgbColor(0xF8, 0xC6, 0x18);
        public static readonly RgbColor AgyLime = new RgbColor(0x4E, 0xC4, 0x46);
        public static readonly RgbColor AgyTeal = new RgbColor(0x1F, 0xA8, 0xD8);
        public static readonly RgbColor AgyOrange = new RgbColor(0xFA, 0x74, 0x20);
        public static readonly RgbColor AgyCoral = new RgbColor(0xEF, 0x44, 0x3A);
        public static readonly RgbColor AgyViolet = new RgbColor(0x7B, 0x61, 0xEC);

        public static readonly RgbColor DarkTop = new RgbColor(0x44, 0x44, 0x4A);
        public static readonly RgbColor DarkMid = new RgbColor(0x2C, 0x2C, 0x31);
        public static readonly RgbColor DarkLeg = new RgbColor(0x18, 0x18, 0x1B);
        public static readonly RgbColor DarkRim = new RgbColor(0x6E, 0x6E, 0x78);

        // 20 x 18 Antigravity Arch silhouette
        public static readonly string[] AgyArchMask =
        {
            "........####........",
            ".......######.......",
            "......########......",
            "......########......",
            ".....##########.....",
            ".....##########.....",
            ".....##########.....",
            "....############....",
            "....############....",
            "....############....",
            "....####....####....",
            "...####......####...",
            "...###........###...",
            "...###........###...",
            "..###..........###..",
            ".###............###.",
            ".##..............##.",
            "##................##",
        };

        // Alternating one-leg hop masks preserving 100% exact Idle body colors per pixel
        public static readonly string[] AgyDanceLeftMask =
        {
            "........####........",
            ".......######.......",
            "......########......",
            "......########......",
            ".....##########.....",
            ".....##########.....",
            ".....##########.....",
            "....############....",
            "....############....",
            "....############....",
            "....####....####....",
            "...####......####...",
            "...###........###...",
            "...###........###...",
            "..###..........####.",
            ".###...........###..",
            ".##.................",
            "##..................",
        };

        public static readonly string[] AgyDanceRightMask =
        {
            "........####........",
            ".......######.......",
            "......########......",
            "......########......",
            ".....##########.....",
            ".....##########.....",
            ".....##########.....",
            "....############....",
            "....############....",
            "....############....",
            "....####....####....",
            "...####......####...",
            "...###........###...",
            "...###........###...",
            ".####..........###..",
            "..###...........###.",
            ".................##.",
            "..................##",
        };

        // Editable 22 x 10 pixel-art headphones mask (0.5px grid, centered at x = 5.5):
        //  'O' = headband frame & outer cup shell, 'C' = inner ear cushion, 'A' = glowing skin accent stripe
        public static readonly string[] HeadphonesMask =
        {
            ".......OOOOOOOO.......",
            "......O........O......",
            ".....O..........O.....",
            ".....O..........O.....",
            "....OC..........CO....",
            "...OAC..........CAO...",
            "...OAC..........CAO...",
            "...OAC..........CAO...",
            "...OAC..........CAO...",
            "....OC..........CO....",
        };

        // Compact rounded capsule for Sleeping
        public static readonly string[] AgyCompactMask =
        {
            "....................",
            "....................",
            "....................",
            "....................",
            "......########......",
            ".....##########.....",
            "....############....",
            "....############....",
            "....############....",
            "....############....",
            "....############....",
            "....############....",
            ".....##########.....",
            "......########......",
            "....................",
            "....................",
            "....................",
            "....................",
        };

        public static string NormalizeSkin(string skin)
        {
            if (string.Equals(skin, DarkSkin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skin, "obsidian", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skin, "monochrome", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skin, "antigravity-dark", StringComparison.OrdinalIgnoreCase))
                return DarkSkin;
            return ClassicSkin;
        }

        public static void NormalizeLookOffset(double dx, double dy, out double nx, out double ny)
        {
            nx = Math.Max(-1.0, Math.Min(1.0, Math.Tanh(dx / 220.0)));
            ny = Math.Max(-1.0, Math.Min(1.0, Math.Tanh(dy / 160.0)));
        }

        public static int ResolveOrbCount(int activeSessionCount)
        {
            if (activeSessionCount <= 0) return 3;
            return Math.Max(2, Math.Min(8, activeSessionCount));
        }

        // Smoothed audio playback detector shared across Windows (WASAPI) and macOS/Linux (CoreAudio/ALSA):
        // requires >= 350ms of audio before turning on and holds through quiet beats <= 2.4s; suppressed in Voice Mode.
        public static bool UpdateMusicPlaybackState(
            float peak,
            bool inVoiceMode,
            DateTime nowUtc,
            ref DateTime audioFirstSeenUtc,
            ref DateTime lastAudioActiveUtc,
            ref bool wasPlaying)
        {
            if (inVoiceMode)
            {
                audioFirstSeenUtc = DateTime.MinValue;
                lastAudioActiveUtc = DateTime.MinValue;
                wasPlaying = false;
                return false;
            }

            if (peak > 0.01f)
            {
                if (audioFirstSeenUtc == DateTime.MinValue ||
                    lastAudioActiveUtc == DateTime.MinValue ||
                    (nowUtc - lastAudioActiveUtc).TotalSeconds > 1.8)
                {
                    audioFirstSeenUtc = nowUtc;
                }
                lastAudioActiveUtc = nowUtc;
                if ((nowUtc - audioFirstSeenUtc).TotalMilliseconds >= 350)
                {
                    wasPlaying = true;
                    return true;
                }
                return wasPlaying;
            }

            if (wasPlaying && lastAudioActiveUtc != DateTime.MinValue &&
                (nowUtc - lastAudioActiveUtc).TotalSeconds <= 2.4)
            {
                return true;
            }

            if (lastAudioActiveUtc == DateTime.MinValue || (nowUtc - lastAudioActiveUtc).TotalSeconds > 2.4)
                audioFirstSeenUtc = DateTime.MinValue;

            wasPlaying = false;
            return false;
        }

        public static RgbColor BodyRgbAt(bool isDark, double x, double y)
        {
            double u = Math.Max(0.0, Math.Min(1.0, (x - 0.5) / 9.5));
            double v = Math.Max(0.0, Math.Min(1.0, y / 8.5));
            return isDark
                ? DarkArchPixel(u, v, false, false, true, false)
                : OfficialArchPixel(u, v, false, false, false);
        }

        public static RgbColor OfficialArchPixel(double u, double v, bool compact, bool topEdge, bool innerArch)
        {
            if (compact) v = Math.Max(0.04, Math.Min(0.96, (v - 0.16) / 0.68));

            RgbColor c = LerpRgb(AgyBlueCore, AgyBlueLeg, Math.Min(1.0, Math.Max(0.0, (v - 0.35) / 0.65)));
            double wGreen = Gauss(u, v, 0.23, 0.34, 0.18, 0.22);
            c = LerpRgb(c, AgyLime, Math.Min(1.0, wGreen * 1.15));

            double wTeal = Gauss(u, v, 0.20, 0.54, 0.14, 0.16);
            c = LerpRgb(c, AgyTeal, Math.Min(0.85, wTeal * 0.85));

            double wGold = Gauss(u, v, 0.40, 0.06, 0.17, 0.17);
            c = LerpRgb(c, AgyGold, Math.Min(1.0, wGold * 1.25));

            double wOrange = Gauss(u, v, 0.54, 0.08, 0.16, 0.16);
            c = LerpRgb(c, AgyOrange, Math.Min(1.0, wOrange * 1.2));

            double wCoral = Gauss(u, v, 0.65, 0.21, 0.16, 0.19);
            c = LerpRgb(c, AgyCoral, Math.Min(1.0, wCoral * 1.18));

            double wViolet = Gauss(u, v, 0.72, 0.44, 0.14, 0.16);
            c = LerpRgb(c, AgyViolet, Math.Min(0.85, wViolet * 0.90));

            if (topEdge && u < 0.60) c = ShiftLight(c, 0.08);
            else if (innerArch) c = ShiftLight(c, -0.14);
            return c;
        }

        public static RgbColor DarkArchPixel(double u, double v, bool compact, bool topEdge, bool sideEdge, bool bottomEdge)
        {
            if (compact) v = Math.Max(0.05, Math.Min(0.95, (v - 0.16) / 0.68));

            RgbColor c = v < 0.45
                ? LerpRgb(DarkTop, DarkMid, v / 0.45)
                : LerpRgb(DarkMid, DarkLeg, (v - 0.45) / 0.55);

            double crownGlow = Gauss(u, v, 0.52, 0.16, 0.22, 0.20);
            c = ShiftLight(c, crownGlow * 0.14);

            if (topEdge) c = LerpRgb(c, DarkRim, 0.58);
            else if (sideEdge) c = LerpRgb(c, DarkRim, 0.34);
            else if (bottomEdge && v < 0.72) c = ShiftLight(c, -0.18);

            return c;
        }

        static double Gauss(double u, double v, double mu, double mv, double su, double sv)
        {
            double du = (u - mu) / su;
            double dv = (v - mv) / sv;
            return Math.Exp(-0.5 * (du * du + dv * dv));
        }

        static RgbColor LerpRgb(RgbColor a, RgbColor b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return new RgbColor(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        static RgbColor ShiftLight(RgbColor c, double delta)
        {
            if (delta >= 0)
                return new RgbColor(
                    (byte)Math.Min(255, Math.Round(c.R + (255 - c.R) * delta)),
                    (byte)Math.Min(255, Math.Round(c.G + (255 - c.G) * delta)),
                    (byte)Math.Min(255, Math.Round(c.B + (255 - c.B) * delta)));
            double k = 1 + delta;
            return new RgbColor(
                (byte)Math.Max(0, Math.Round(c.R * k)),
                (byte)Math.Max(0, Math.Round(c.G * k)),
                (byte)Math.Max(0, Math.Round(c.B * k)));
        }
    }
}
