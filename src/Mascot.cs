using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AgyPet
{
    public enum TurnSentiment
    {
        Neutral,
        Happy,
        Sad,
        Angry
    }

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
        Happy,
        Error
    }

    // Pixel-art Antigravity companion supporting two official skins:
    //  1) "classic" — refined CLI pixel-art arch in official Antigravity logo colors
    //                 (yellow-green upper-left, fiery orange-red upper-right, vivid blue core & legs, violet right shoulder)
    //  2) "dark"    — sleek new monochrome obsidian/graphite Antigravity remake with luminous white eyes
    public class Mascot : Canvas
    {
        public const string ClassicSkin = "classic";
        public const string DarkSkin = "dark";

        // Shared neutral & eye brushes
        static readonly Brush EyeDark = Frozen(Color.FromRgb(0x0B, 0x12, 0x20));
        static readonly Brush EyeWhite = Frozen(Color.FromRgb(0xF8, 0xFA, 0xFC));
        static readonly Brush EyeSoftSilver = Frozen(Color.FromRgb(0xCB, 0xD5, 0xE1));
        static readonly Brush Cloud = Frozen(Color.FromRgb(0xE2, 0xE8, 0xF0));
        static readonly Brush CloudDim = Frozen(Color.FromRgb(0x64, 0x74, 0x8B));
        static readonly Brush Steel = Frozen(Color.FromRgb(0x94, 0xA3, 0xB8));
        static readonly Brush SteelDark = Frozen(Color.FromRgb(0x3F, 0x4A, 0x5C));
        static readonly Brush DarkCase = Frozen(Color.FromRgb(0x27, 0x27, 0x2A));
        static readonly Brush DarkScreen = Frozen(Color.FromRgb(0x18, 0x18, 0x1B));
        static readonly Brush ScreenGlow = Frozen(Color.FromRgb(0xE2, 0xE8, 0xF0));
        static readonly Brush GlassLit = Frozen(Color.FromRgb(0x38, 0xBD, 0xF8));
        static readonly Brush Spark = Frozen(Color.FromRgb(0xFF, 0xD2, 0x4A));
        static readonly Brush SparkHot = Frozen(Color.FromRgb(0xFF, 0xF6, 0xD6));
        static readonly Brush Amber = Frozen(Color.FromRgb(0xF5, 0x9E, 0x0B));
        static readonly Brush Blue = Frozen(Color.FromRgb(0x38, 0xBD, 0xF8));
        static readonly Brush Paper = Frozen(Color.FromRgb(0x0F, 0x17, 0x2A));
        static readonly Brush MonoPaper = Frozen(Color.FromRgb(0x18, 0x18, 0x1B));

        // Official Antigravity Logo colors (sampled directly from the main Antigravity logo)
        static readonly Color AgyBlueCore = Color.FromRgb(0x1E, 0x78, 0xFF);
        static readonly Color AgyBlueLeg = Color.FromRgb(0x2C, 0x86, 0xFF);
        static readonly Color AgyGold = Color.FromRgb(0xF8, 0xC6, 0x18);
        static readonly Color AgyLime = Color.FromRgb(0x4E, 0xC4, 0x46);
        static readonly Color AgyTeal = Color.FromRgb(0x1F, 0xA8, 0xD8);
        static readonly Color AgyOrange = Color.FromRgb(0xFA, 0x74, 0x20);
        static readonly Color AgyCoral = Color.FromRgb(0xEF, 0x44, 0x3A);
        static readonly Color AgyViolet = Color.FromRgb(0x7B, 0x61, 0xEC);

        static readonly Brush AgyBlueBrush = Frozen(AgyBlueCore);
        static readonly Brush AgySkyBrush = Frozen(AgyBlueLeg);
        static readonly Brush AgyTealBrush = Frozen(AgyTeal);
        static readonly Brush AgyGoldBrush = Frozen(AgyGold);
        static readonly Brush AgyCoralBrush = Frozen(AgyCoral);
        static readonly Brush AgyEmeraldBrush = Frozen(AgyLime);
        static readonly Brush AgyVioletBrush = Frozen(AgyViolet);
        static readonly Brush AgyOrbitDim = Frozen(Color.FromRgb(0x94, 0xA3, 0xB8));
        static readonly Brush AgyShadowBrush = Frozen(Color.FromArgb(0x88, 0x07, 0x07, 0x09));
        static readonly Brush BlushBrush = Frozen(Color.FromArgb(0xD8, 0xFB, 0x71, 0x85));
        static readonly Brush TearBrush = Frozen(Color.FromRgb(0x38, 0xBD, 0xF8));
        static readonly Brush SteamBright = Frozen(Color.FromRgb(0xFB, 0x71, 0x85));
        static readonly Brush ErrorRedBrush = Frozen(Color.FromRgb(0xEF, 0x44, 0x44));

        // Dark Monochrome Remake palette (sampled from the new dark Antigravity remake logo)
        static readonly Color DarkTop = Color.FromRgb(0x44, 0x44, 0x4A);
        static readonly Color DarkMid = Color.FromRgb(0x2C, 0x2C, 0x31);
        static readonly Color DarkLeg = Color.FromRgb(0x18, 0x18, 0x1B);
        static readonly Color DarkRim = Color.FromRgb(0x6E, 0x6E, 0x78);
        static readonly Brush DarkArmLight = Frozen(Color.FromRgb(0x52, 0x52, 0x5B));
        static readonly Brush DarkArmMid = Frozen(Color.FromRgb(0x3A, 0x3A, 0x40));
        static readonly Brush MonoBright = Frozen(Color.FromRgb(0xF4, 0xF4, 0xF5));
        static readonly Brush MonoMid = Frozen(Color.FromRgb(0xA1, 0xA1, 0xAA));
        static readonly Brush MonoDim = Frozen(Color.FromRgb(0x52, 0x52, 0x5B));

        // 20 x 18 Antigravity Arch silhouette (refined from CLI pixel logo + official arch curvature)
        static readonly string[] AgyArchMask =
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

        // Compact rounded capsule for Thinking (atomic orbital state) and Sleeping
        static readonly string[] AgyCompactMask =
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

        readonly double px;
        readonly Canvas sprite = new Canvas();
        readonly Canvas archNormalLayer = new Canvas();
        readonly Canvas archCompactLayer = new Canvas();
        readonly Canvas gear = new Canvas();
        readonly Canvas air = new Canvas();
        readonly DispatcherTimer timer;
        readonly Random rnd = new Random();

        readonly ScaleTransform recoilTransform = new ScaleTransform(1.0, 1.0);
        int tick, blinkAt, blinkLeft, glanceAt, glance;
        int hurtTicks;
        int danceTicks;

        public MascotMood Mood { get; set; }
        public TurnSentiment Sentiment { get; set; }
        public string SkinName { get; private set; }
        public bool IsHurt { get { return hurtTicks > 0; } }
        public bool IsDancing { get { return danceTicks > 0; } }

        public Mascot(double pixelSize, string skin = ClassicSkin)
        {
            px = pixelSize;
            SkinName = NormalizeSkin(skin);
            ClipToBounds = false;
            sprite.RenderTransformOrigin = new Point(0.5, 0.85);
            sprite.RenderTransform = recoilTransform;
            Children.Add(sprite);
            Children.Add(air);

            BuildSprite();

            blinkAt = rnd.Next(25, 60);
            glanceAt = rnd.Next(40, 90);
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            timer.Tick += (s, e) => Step();
            timer.Start();
            Loaded += (s, e) => { Step(); timer.Start(); };
            Unloaded += (s, e) => timer.Stop();
            Step();
        }

        public static string NormalizeSkin(string skin)
        {
            if (string.Equals(skin, DarkSkin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skin, "obsidian", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skin, "monochrome", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(skin, "antigravity-dark", StringComparison.OrdinalIgnoreCase))
                return DarkSkin;
            return ClassicSkin;
        }

        public void SetSkin(string skin)
        {
            string next = NormalizeSkin(skin);
            if (next == SkinName) return;
            SkinName = next;
            BuildSprite();
            Step();
        }

        public void Snap(int atTick)
        {
            tick = atTick;
            blinkLeft = 0;
            glance = 0;
            Step();
        }

        // Subtle tactile press wince: keeps the current mood scene intact with a brief micro-squish and "> <" eyes.
        public void Poke()
        {
            hurtTicks = 6;
            Step();
        }

        public void CancelHurt()
        {
            hurtTicks = 0;
            recoilTransform.ScaleX = 1.0;
            recoilTransform.ScaleY = 1.0;
        }

        // Playful dance groove on double-click when calm
        public void Dance()
        {
            if (hurtTicks > 0 || Sentiment == TurnSentiment.Angry || Sentiment == TurnSentiment.Sad) return;
            danceTicks = 22;
            Step();
        }

        internal void StepFrame()
        {
            Step();
        }

        bool IsDark { get { return SkinName == DarkSkin; } }

        void BuildSprite()
        {
            Width = 11.0 * px;
            Height = 9.0 * px;

            sprite.Children.Clear();
            archNormalLayer.Children.Clear();
            archCompactLayer.Children.Clear();
            gear.Children.Clear();
            air.Children.Clear();

            RenderPixelMask(archNormalLayer, AgyArchMask, false);
            RenderPixelMask(archCompactLayer, AgyCompactMask, true);
            archCompactLayer.Visibility = Visibility.Collapsed;
            sprite.Children.Add(archNormalLayer);
            sprite.Children.Add(archCompactLayer);
            sprite.Children.Add(gear);
        }

        void RenderPixelMask(Canvas layer, string[] mask, bool compact)
        {
            for (int r = 0; r < mask.Length; r++)
            {
                string row = mask[r];
                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] != '#') continue;
                    double x = 0.5 + c * 0.5;
                    double y = r * 0.5;
                    bool hasLeft = c > 0 && row[c - 1] == '#';
                    bool hasRight = c + 1 < row.Length && row[c + 1] == '#';
                    bool hasTop = r > 0 && mask[r - 1][c] == '#';
                    bool hasBottom = r + 1 < mask.Length && mask[r + 1][c] == '#';

                    double u = c / 19.0;
                    double v = r / 17.0;
                    Color col = IsDark
                        ? DarkArchPixelColor(u, v, compact, !hasTop, !hasLeft || !hasRight, !hasBottom)
                        : OfficialArchPixelColor(u, v, compact, !hasTop, !hasBottom && r >= 8 && r <= 12 && c >= 6 && c <= 13);

                    double bleedX = hasRight ? 0.03 : 0.0;
                    double bleedY = hasBottom ? 0.03 : 0.0;
                    Dot(layer, Frozen(col), x, y, 0.5 + bleedX, 0.5 + bleedY);
                }
            }
        }

        // Spatial radial color model matching the official Antigravity logo:
        // vivid royal blue core & legs, bright yellow-gold top-left, fresh green left shoulder,
        // fiery orange-red top-right crest, and crisp violet-blue right shoulder — zero muddy center!
        public static Color OfficialArchPixelColor(double u, double v, bool compact, bool topEdge, bool innerArch)
        {
            if (compact) v = Math.Max(0.04, Math.Min(0.96, (v - 0.16) / 0.68));

            // Base luminous Google/Antigravity blue
            Color c = LerpColor(AgyBlueCore, AgyBlueLeg, Math.Min(1.0, Math.Max(0.0, (v - 0.35) / 0.65)));

            // 1. Lime/Emerald Green along upper-left shoulder
            double wGreen = Gauss(u, v, 0.23, 0.34, 0.18, 0.22);
            c = LerpColor(c, AgyLime, Math.Min(1.0, wGreen * 1.15));

            // 2. Aqua-Teal transition along mid-left leg
            double wTeal = Gauss(u, v, 0.20, 0.54, 0.14, 0.16);
            c = LerpColor(c, AgyTeal, Math.Min(0.85, wTeal * 0.85));

            // 3. Bright Yellow-Gold at top-left crest
            double wGold = Gauss(u, v, 0.40, 0.06, 0.17, 0.17);
            c = LerpColor(c, AgyGold, Math.Min(1.0, wGold * 1.25));

            // 4. Warm Orange at top crest center-right
            double wOrange = Gauss(u, v, 0.54, 0.08, 0.16, 0.16);
            c = LerpColor(c, AgyOrange, Math.Min(1.0, wOrange * 1.2));

            // 5. Vivid Coral-Red along upper-right crest & shoulder
            double wCoral = Gauss(u, v, 0.65, 0.21, 0.16, 0.19);
            c = LerpColor(c, AgyCoral, Math.Min(1.0, wCoral * 1.18));

            // 6. Violet-Indigo bridge on right mid-shoulder
            double wViolet = Gauss(u, v, 0.72, 0.44, 0.14, 0.16);
            c = LerpColor(c, AgyViolet, Math.Min(0.85, wViolet * 0.90));

            if (topEdge && u < 0.60) c = ShiftLight(c, 0.08);
            else if (innerArch) c = ShiftLight(c, -0.14);
            return c;
        }

        // Sleek monochrome obsidian/graphite gradient inspired by the new Dark Remake logo,
        // with a crisp subtle silver-graphite rim so it reads cleanly on both dark & light themes.
        public static Color DarkArchPixelColor(double u, double v, bool compact, bool topEdge, bool sideEdge, bool bottomEdge)
        {
            if (compact) v = Math.Max(0.05, Math.Min(0.95, (v - 0.16) / 0.68));

            // Soft directional sheen from top-center-right down into deep charcoal legs
            Color c = v < 0.45
                ? LerpColor(DarkTop, DarkMid, v / 0.45)
                : LerpColor(DarkMid, DarkLeg, (v - 0.45) / 0.55);

            // Subtle specular highlight on the upper arch crown
            double crownGlow = Gauss(u, v, 0.52, 0.16, 0.22, 0.20);
            c = ShiftLight(c, crownGlow * 0.14);

            // Crisp rim edge so the monochrome silhouette stands out against dark IDE/chat backgrounds
            if (topEdge) c = LerpColor(c, DarkRim, 0.58);
            else if (sideEdge) c = LerpColor(c, DarkRim, 0.34);
            else if (bottomEdge && v < 0.72) c = ShiftLight(c, -0.18);

            return c;
        }

        static double Gauss(double u, double v, double mu, double mv, double su, double sv)
        {
            double du = (u - mu) / su;
            double dv = (v - mv) / sv;
            return Math.Exp(-0.5 * (du * du + dv * dv));
        }

        static Color LerpColor(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromRgb(
                (byte)Math.Round(a.R + (b.R - a.R) * t),
                (byte)Math.Round(a.G + (b.G - a.G) * t),
                (byte)Math.Round(a.B + (b.B - a.B) * t));
        }

        static Color ShiftLight(Color c, double delta)
        {
            if (delta >= 0)
                return Color.FromRgb(
                    (byte)Math.Min(255, Math.Round(c.R + (255 - c.R) * delta)),
                    (byte)Math.Min(255, Math.Round(c.G + (255 - c.G) * delta)),
                    (byte)Math.Min(255, Math.Round(c.B + (255 - c.B) * delta)));
            double k = 1 + delta;
            return Color.FromRgb(
                (byte)Math.Max(0, Math.Round(c.R * k)),
                (byte)Math.Max(0, Math.Round(c.G * k)),
                (byte)Math.Max(0, Math.Round(c.B * k)));
        }

        void Dot(Canvas layer, Brush fill, double x, double y, double w = 1, double h = 1)
        {
            var r = new Rectangle { Fill = fill, Width = w * px, Height = h * px };
            SetLeft(r, x * px);
            SetTop(r, y * px);
            layer.Children.Add(r);
        }

        void Shape(Canvas layer, Brush fill, double x, double y, params string[] rows)
        {
            for (int row = 0; row < rows.Length; row++)
                for (int col = 0; col < rows[row].Length; col++)
                    if (rows[row][col] == '#') Dot(layer, fill, x + col, y + row);
        }

        void Step()
        {
            tick++;
            gear.Children.Clear();
            air.Children.Clear();
            var mood = Mood;
            bool hurt = hurtTicks > 0;
            if (hurtTicks > 0) hurtTicks--;
            bool dancing = danceTicks > 0;
            if (danceTicks > 0) danceTicks--;

            bool showSentiment = (Sentiment == TurnSentiment.Angry || Sentiment == TurnSentiment.Sad) &&
                                 mood != MascotMood.Sleeping && mood != MascotMood.Done &&
                                 mood != MascotMood.Happy && mood != MascotMood.Error;

            bool hops = dancing || mood == MascotMood.Done || mood == MascotMood.Happy;
            bool wiggles = mood == MascotMood.Alert || mood == MascotMood.Question || mood == MascotMood.Error;

            double baseBob = hops ? -Math.Round(Math.Abs(Math.Sin(tick * 0.55)) * 1.6) * px : 0;
            double y = hurt && hurtTicks >= 4 ? baseBob + px * 0.45 : baseBob;
            SetTop(sprite, y);

            bool typing = mood == MascotMood.Working && tick % 90 < 72;
            double danceSway = dancing ? Math.Sin(tick * 0.7) * 1.4 * px : 0;
            double errorShake = mood == MascotMood.Error && tick % 6 < 2 ? (tick % 2 == 0 ? -0.45 : 0.45) * px : 0;
            double x = hurt && hurtTicks >= 4 ? ((hurtTicks % 2 == 0 ? -0.3 : 0.3) * px)
                : mood == MascotMood.Error ? errorShake
                : (wiggles && tick % 12 < 4 ? (tick % 2 == 0 ? -px * 0.5 : px * 0.5) : danceSway);
            SetLeft(sprite, x);

            // Subtle tactile micro-squish on press (no exaggerated bounce or sparks)
            if (hurt && hurtTicks >= 4)
            {
                recoilTransform.ScaleX = 1.05;
                recoilTransform.ScaleY = 0.94;
            }
            else if (recoilTransform.ScaleX != 1.0 || recoilTransform.ScaleY != 1.0)
            {
                recoilTransform.ScaleX = 1.0;
                recoilTransform.ScaleY = 1.0;
            }

            if (--blinkAt <= 0) { blinkLeft = 2; blinkAt = rnd.Next(30, 70); }
            bool closed = blinkLeft-- > 0;

            if (--glanceAt <= 0)
            {
                glance = glance == 0 ? (rnd.Next(2) == 0 ? -1 : 1) : 0;
                glanceAt = glance == 0 ? rnd.Next(40, 100) : rnd.Next(12, 25);
            }

            bool compact = mood == MascotMood.Sleeping;
            archNormalLayer.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            archCompactLayer.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;

            if (mood == MascotMood.Idle || mood == MascotMood.Listening || mood == MascotMood.Speaking ||
                mood == MascotMood.Searching || mood == MascotMood.Browsing || mood == MascotMood.Background)
                DrawRestingArms();

            double ex = mood == MascotMood.Idle ? glance * 0.35 : 0;

            // 1. Always render the active status's props & arms so the current status stays calm and intact
            switch (mood)
            {
                case MascotMood.Idle:
                    if (!hurt) DrawEyes(3.9 + ex, 3.2, 6.3 + ex, 3.2, closed);
                    if (dancing) DrawDanceNotes();
                    else DrawIdleSparkle();
                    break;
                case MascotMood.Sleeping:
                    if (!hurt) DrawShutEyes(4.0, 4.3, 6.2, 4.3);
                    DrawZzz();
                    break;
                case MascotMood.Listening:
                    if (!hurt) DrawEyes(3.9, 3.1, 6.3, 3.1, false);
                    DrawWaves();
                    break;
                case MascotMood.Thinking:
                    if (!hurt) DrawThinkingFace();
                    DrawThoughtCloud();
                    break;
                case MascotMood.Speaking:
                    if (!hurt) { DrawEyes(3.9, 3.1, 6.3, 3.1, false); DrawMouth(); }
                    else DrawSpeechDotsOnly();
                    break;
                case MascotMood.Typing:
                    if (!hurt) DrawEyes(3.7, 3.3, 6.1, 3.3, false, shortEyes: true);
                    DrawTypingScene();
                    break;
                case MascotMood.Searching:
                    double sx = Math.Round(Math.Sin(tick * 0.14)) * 0.35;
                    if (!hurt) DrawEyes(3.9 + sx, 3.2, 6.3 + sx, 3.2, false);
                    DrawMagnifier();
                    break;
                case MascotMood.Reading:
                    if (!hurt) DrawEyes(3.9, 3.1, 6.3, 3.1, false, shortEyes: true);
                    DrawBook();
                    break;
                case MascotMood.Working:
                    if (!hurt) DrawWorkingFace();
                    DrawLaptop(typing);
                    break;
                case MascotMood.Terminal:
                    if (!hurt) DrawWorkingFace();
                    DrawTerminal();
                    break;
                case MascotMood.Browsing:
                    double bx = Math.Round(Math.Sin(tick * 0.16)) * 0.35;
                    if (!hurt) DrawEyes(3.9 + bx, 3.1, 6.3 + bx, 3.1, false);
                    DrawBrowserGlobe();
                    break;
                case MascotMood.Delegating:
                    if (!hurt) DrawEyes(4.1, 3.1, 6.5, 3.1, false);
                    DrawDelegatingDispatch();
                    break;
                case MascotMood.Multitasking:
                    double mx = (tick / 5 % 2 == 0) ? -0.45 : 0.45;
                    if (!hurt) DrawEyes(3.9 + mx, 3.1, 6.3 + mx, 3.1, false);
                    DrawMultitaskingJuggle();
                    break;
                case MascotMood.Background:
                    if (!hurt) DrawHappyEyes();
                    DrawBackgroundServer();
                    break;
                case MascotMood.Alert:
                    if (!hurt) DrawStartledEyes();
                    DrawArmsUp();
                    DrawBang();
                    break;
                case MascotMood.Question:
                    if (!hurt) DrawConfusedFace();
                    DrawQuestion();
                    break;
                case MascotMood.Done:
                    if (!hurt) DrawHappyEyes();
                    DrawArmsUp();
                    Dot(air, AgyShadowBrush, 2.5, 8.7, 6.0, 0.4);
                    DrawCelebrationConfetti();
                    break;
                case MascotMood.Happy:
                    if (!hurt) DrawHappyEyes();
                    DrawArmsUp();
                    Dot(air, AgyShadowBrush, 2.5, 8.7, 6.0, 0.4);
                    DrawHappyAurora();
                    break;
                case MascotMood.Error:
                    if (!hurt) DrawErrorGlitchFace();
                    DrawArmsUp();
                    DrawErrorLightning();
                    break;
            }

            // 2. Original AgyPet sentiment cues (steam vents for Angry, shimmering tear for Sad — no body recolor)
            if (showSentiment && !hurt)
                DrawSentimentEffects(Sentiment == TurnSentiment.Angry);

            // 3. Subtle "> <" wince eyes on left-click poke above chatbox
            if (hurt)
                DrawHurtOverlay(compact);
        }

        // ---- Face & arm helpers (auto-adapted for Classic vs Dark Remake) ----

        Brush EyePrimary { get { return IsDark ? EyeWhite : EyeDark; } }
        Brush EyeGlint { get { return IsDark ? EyeSoftSilver : EyeWhite; } }
        Brush ArmLeftUpper { get { return IsDark ? DarkArmLight : AgyTealBrush; } }
        Brush ArmLeftLower { get { return IsDark ? DarkArmMid : AgyBlueBrush; } }
        Brush ArmRightUpper { get { return IsDark ? DarkArmLight : AgyVioletBrush; } }
        Brush ArmRightLower { get { return IsDark ? DarkArmMid : AgySkyBrush; } }

        void DrawRestingArms()
        {
            Dot(gear, ArmLeftUpper, 1.5, 4.8, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.1, 5.4, 0.5, 0.5);
            Dot(gear, ArmRightUpper, 9.0, 4.8, 0.5, 1.0);
            Dot(gear, ArmRightLower, 9.4, 5.4, 0.5, 0.5);
        }

        void DrawArmsUp()
        {
            Dot(gear, ArmLeftUpper, 1.1, 3.7, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.6, 4.4, 0.5, 0.7);
            Dot(gear, ArmRightLower, 9.4, 3.7, 0.5, 1.0);
            Dot(gear, ArmRightUpper, 8.9, 4.4, 0.5, 0.7);
        }

        void DrawEyes(double lx, double ly, double rx, double ry, bool closed, bool shortEyes = false)
        {
            if (closed) { DrawShutEyes(lx, ly + 0.4, rx, ry + 0.4); return; }
            double h = shortEyes ? 0.5 : 0.9;
            Dot(gear, EyePrimary, lx, ly, 0.85, h);
            Dot(gear, EyePrimary, rx, ry, 0.85, h);
            if (!IsDark)
            {
                Dot(gear, EyeGlint, lx + 0.4, ly, 0.4, 0.4);
                Dot(gear, EyeGlint, rx + 0.4, ry, 0.4, 0.4);
            }
            else
            {
                // Subtle pupil depth inside luminous white eyes on Dark Remake
                Dot(gear, EyeDark, lx + 0.45, ly + 0.35, 0.35, 0.35);
                Dot(gear, EyeDark, rx + 0.45, ry + 0.35, 0.35, 0.35);
            }
        }

        void DrawShutEyes(double lx, double ly, double rx, double ry)
        {
            Dot(gear, EyePrimary, lx, ly, 0.9, 0.35);
            Dot(gear, EyePrimary, rx, ry, 0.9, 0.35);
        }

        void DrawHappyEyes()
        {
            Dot(gear, EyePrimary, 3.7, 3.4, 0.4, 0.4); Dot(gear, EyePrimary, 4.1, 3.0, 0.5, 0.4); Dot(gear, EyePrimary, 4.6, 3.4, 0.4, 0.4);
            Dot(gear, EyePrimary, 6.0, 3.4, 0.4, 0.4); Dot(gear, EyePrimary, 6.4, 3.0, 0.5, 0.4); Dot(gear, EyePrimary, 6.9, 3.4, 0.4, 0.4);
        }

        void DrawWorkingFace()
        {
            Dot(gear, EyePrimary, 3.6, 3.4, 0.85, 0.75);
            Dot(gear, EyePrimary, 6.0, 3.4, 0.85, 0.75);
            if (!IsDark)
            {
                Dot(gear, EyeWhite, 3.6, 3.4, 0.4, 0.4);
                Dot(gear, EyeWhite, 6.0, 3.4, 0.4, 0.4);
            }
            else
            {
                Dot(gear, EyeDark, 3.6, 3.5, 0.35, 0.35);
                Dot(gear, EyeDark, 6.0, 3.5, 0.35, 0.35);
            }
            Dot(gear, EyePrimary, 3.3, 2.7, 0.75, 0.3);
            Dot(gear, EyePrimary, 4.0, 2.9, 0.55, 0.3);
            Dot(gear, EyePrimary, 5.8, 2.9, 0.55, 0.3);
            Dot(gear, EyePrimary, 6.3, 2.7, 0.75, 0.3);
        }

        void DrawThinkingFace()
        {
            // Pondering upward gaze with slightly raised brow & resting chin hand
            Dot(gear, EyePrimary, 3.9, 2.75, 0.85, 0.55);
            Dot(gear, EyePrimary, 6.3, 2.75, 0.85, 0.8);
            if (!IsDark)
            {
                Dot(gear, EyeWhite, 4.35, 2.75, 0.35, 0.35);
                Dot(gear, EyeWhite, 6.75, 2.75, 0.35, 0.35);
            }
            Dot(gear, ArmLeftUpper, 1.5, 4.8, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.1, 5.4, 0.5, 0.5);
            Dot(gear, ArmRightUpper, 8.6, 4.6, 0.55, 0.85);
            Dot(gear, ArmRightLower, 7.6, 4.2, 1.05, 0.5);
        }

        void DrawStartledEyes()
        {
            Dot(gear, EyeWhite, 3.6, 2.9, 1.25, 1.25);
            Dot(gear, EyeDark, 3.95, 3.25, 0.55, 0.55);
            Dot(gear, EyeWhite, 6.1, 2.9, 1.25, 1.25);
            Dot(gear, EyeDark, 6.45, 3.25, 0.55, 0.55);
        }

        void DrawConfusedFace()
        {
            Dot(gear, EyePrimary, 3.7, 3.2, 0.85, 0.85);
            if (!IsDark) Dot(gear, EyeWhite, 4.05, 3.2, 0.35, 0.35);
            Dot(gear, EyeWhite, 6.0, 2.8, 1.4, 1.4);
            Dot(gear, EyeDark, 6.45, 3.25, 0.55, 0.55);
            Dot(gear, ArmLeftLower, 1.5, 4.8, 0.5, 0.5);
            Dot(gear, ArmLeftUpper, 1.1, 5.3, 0.5, 0.5);
            Dot(gear, ArmLeftLower, 1.5, 5.8, 0.5, 0.5);
            Dot(gear, ArmRightUpper, 9.0, 4.7, 0.5, 0.6);
            Dot(gear, ArmRightLower, 9.5, 3.7, 0.5, 1.1);
            Dot(gear, ArmRightLower, 8.8, 3.3, 0.7, 0.5);
        }

        // ---- Scenes & Props ------------------------------------------------

        void DrawIdleSparkle()
        {
            int phase = tick % 24;
            if (phase >= 18) return;
            double sx = 9.8, sy = 1.0;
            Brush core = IsDark ? MonoBright : (phase < 8 ? SparkHot : EyeWhite);
            Brush rays = IsDark ? MonoMid : Cloud;
            Dot(air, core, sx, sy, 0.5, 0.5);
            if (phase < 12)
            {
                Dot(air, rays, sx - 0.5, sy, 0.5, 0.5);
                Dot(air, rays, sx + 0.5, sy, 0.5, 0.5);
                Dot(air, rays, sx, sy - 0.5, 0.5, 0.5);
                Dot(air, rays, sx, sy + 0.5, 0.5, 0.5);
            }
        }

        // Thinking: trailing neural bubbles + glowing Antigravity thought cloud (zero orbital rings)
        void DrawThoughtCloud()
        {
            int pulse = tick / 3 % 3;
            Brush cloudBg = IsDark ? DarkCase : Paper;
            Brush cloudRim = IsDark ? MonoMid : AgyBlueBrush;
            Dot(air, cloudRim, 9.0, 0.3, 0.55, 0.55);
            Dot(air, cloudRim, 10.1, -0.7, 0.8, 0.8);
            Dot(air, cloudRim, 9.6, -3.5, 5.3, 2.4);
            Dot(air, cloudBg, 10.0, -3.1, 4.5, 1.6);

            Brush[] sparks = IsDark
                ? new[] { MonoBright, MonoMid, MonoBright }
                : new[] { AgyGoldBrush, AgyEmeraldBrush, GlassLit };
            for (int d = 0; d < 3; d++)
            {
                bool lit = d == pulse;
                Dot(air, lit ? sparks[d] : (IsDark ? MonoDim : CloudDim), 10.6 + d * 1.25, lit ? -2.65 : -2.4, 0.75, 0.75);
            }
        }

        void DrawCelebrationConfetti()
        {
            double liftA = tick % 8 < 4 ? -0.3 : 0.2;
            double liftB = tick % 8 < 4 ? 0.2 : -0.3;
            Brush c1 = IsDark ? MonoBright : AgyCoralBrush;
            Brush c2 = IsDark ? MonoMid : AgyEmeraldBrush;
            Brush c3 = IsDark ? MonoBright : AgyGoldBrush;
            Brush c4 = IsDark ? MonoMid : GlassLit;
            Brush c5 = IsDark ? MonoDim : AgyBlueBrush;

            Dot(air, c1, -0.2, 1.0 + liftA, 0.45, 0.9);
            Dot(air, c1, 0.25, 1.45 + liftA, 0.45, 0.45);
            Dot(air, c2, 0.8, -1.2 + liftB, 1.1, 0.38);
            Dot(air, c2, 1.15, -1.55 + liftB, 0.38, 1.1);
            Dot(air, c3, 2.8, -2.1 + liftA, 0.45, 0.45);
            Dot(air, c3, 3.25, -1.65 + liftA, 0.45, 0.45);
            Dot(air, c4, 5.4, -2.3 + liftB, 0.55, 0.55);
            Dot(air, c5, 7.4, -1.7 + liftB, 0.45, 0.45);
            Dot(air, c5, 7.85, -2.15 + liftB, 0.45, 0.45);
            Dot(air, c3, 9.2, -1.0 + liftA, 0.95, 0.38);
            Dot(air, c3, 9.5, -1.3 + liftA, 0.38, 0.95);
            Dot(air, c1, 10.6, 0.6 + liftB, 0.45, 0.85);
            Dot(air, c1, 11.05, 1.05 + liftB, 0.45, 0.45);
        }

        void DrawLaptop(bool typing)
        {
            double ox = -5.5;
            Dot(air, IsDark ? MonoMid : Steel, ox, 1.8, 5.5, 6.2);
            Dot(air, IsDark ? DarkScreen : ScreenGlow, ox + 0.9, 2.5, 3.7, 4.5);
            Dot(air, IsDark ? DarkCase : SteelDark, ox, 8.0, 7.2, 0.9);

            int written = tick / 5;
            Brush[] colors = IsDark
                ? new[] { MonoBright, MonoMid, MonoDim, MonoBright }
                : new[] { AgyBlueBrush, AgyCoralBrush, AgyEmeraldBrush, AgyGoldBrush };
            for (int row = 0; row < 4; row++)
            {
                int line = written - 3 + row;
                if (line < 0) continue;
                int seed = line * 7 + 3;
                int indent = seed % 3 == 0 ? 1 : 0;
                double width = Math.Min(1 + seed % 3.5, 3.5 - indent);
                if (row == 3 && typing) width = Math.Min(width, 1 + tick % 4 % Math.Max(1, (int)width));
                Dot(air, colors[seed % colors.Length], ox + 1.0 + indent * 0.8, 2.8 + row * 0.95, width, 0.45);
            }
            if (typing && tick % 8 < 4) Dot(air, IsDark ? MonoBright : GlassLit, ox + 1.2, 6.3, 0.45, 0.45);

            if (typing)
            {
                Brush sp1 = IsDark ? MonoBright : (tick % 6 < 3 ? AgyGoldBrush : AgyCoralBrush);
                Brush sp2 = IsDark ? MonoMid : AgyCoralBrush;
                Dot(air, sp1, ox + 3.8, 0.4 - (tick % 4) * 0.3, 0.45, 0.45);
                Dot(air, sp2, ox + 4.7, -0.2 + (tick % 4) * 0.2, 0.45, 0.45);
            }

            int tap = tick / 2 % 2;
            Dot(air, ArmLeftUpper, 0.4, 5.0, 2.0, 0.9);
            Dot(air, ArmLeftLower, 0.4, 5.9, 0.9, typing && tap == 0 ? 1.8 : 0.9);
            Dot(air, ArmLeftLower, 1.4, 5.9, 0.9, typing && tap == 1 ? 1.8 : 0.9);
        }

        void DrawMagnifier()
        {
            double gx = 3.5 + Math.Round(Math.Sin(tick * 0.14) * 4);
            Brush rim = IsDark ? MonoBright : GlassLit;
            Brush handle = IsDark ? MonoMid : AgyBlueBrush;
            Brush glint = IsDark ? MonoMid : EyeWhite;
            Shape(air, rim, gx, -3.6,
                ".###.",
                "#...#",
                "#...#",
                ".###.");
            Dot(air, handle, gx + 4, 0.4);
            Dot(air, handle, gx + 5, 1.4);
            if (tick % 8 < 4) Dot(air, glint, gx + 2, -2.1, 1, 1);
        }

        void DrawBook()
        {
            double bx = 0.5;
            Dot(gear, IsDark ? MonoPaper : Paper, bx, 4.3, 5, 3);
            Dot(gear, IsDark ? MonoPaper : Paper, bx + 5, 4.3, 5, 3);
            Dot(gear, IsDark ? MonoMid : AgyBlueBrush, bx + 4.75, 4.3, 0.5, 3);
            Brush line1 = IsDark ? MonoBright : AgyGoldBrush;
            Brush line2 = IsDark ? MonoMid : AgyEmeraldBrush;
            for (int line = 0; line < 2; line++)
            {
                Dot(gear, line1, bx + 1, 5.0 + line, 3, 0.4);
                Dot(gear, line2, bx + 6, 5.0 + line, 3, 0.4);
            }
            if (tick % 50 < 5) Dot(gear, MonoBright, bx + 2 + tick % 50, 3.9, 1, 3.4);
        }

        void DrawWaves()
        {
            int t = tick / 3 % 3;
            Brush hot = IsDark ? MonoBright : GlassLit;
            Brush cool = IsDark ? MonoMid : AgyBlueBrush;
            for (int i = 0; i <= t; i++)
            {
                double h = 3.8 - i * 0.9;
                double top = 4.5 - h / 2;
                Dot(air, i == t ? hot : cool, -1.2 - i * 1.15, top, 0.8, h);
                Dot(air, i == t ? hot : cool, 11.2 + i * 1.15, top, 0.8, h);
            }
        }

        void DrawMouth()
        {
            double open = tick % 4 < 2 ? 0.9 : 0.4;
            Dot(gear, EyePrimary, 4.7, 4.4, 1.6, open);
            DrawSpeechDotsOnly();
        }

        void DrawSpeechDotsOnly()
        {
            int t = tick / 3 % 4;
            if (t >= 1) Dot(air, IsDark ? MonoBright : EyeWhite, 11.4, 4);
            if (t >= 2) Dot(air, IsDark ? MonoMid : GlassLit, 12.7, 3);
            if (t >= 3) Dot(air, IsDark ? MonoDim : Blue, 14.0, 2);
        }

        void DrawHurtOverlay(bool compact)
        {
            double ey = compact ? 3.85 : 3.05;
            // Subtle "> <" wince eyes
            Dot(gear, EyePrimary, 3.65, ey, 0.4, 0.35);
            Dot(gear, EyePrimary, 4.05, ey + 0.32, 0.45, 0.35);
            Dot(gear, EyePrimary, 3.65, ey + 0.64, 0.4, 0.35);

            Dot(gear, EyePrimary, 6.75, ey, 0.4, 0.35);
            Dot(gear, EyePrimary, 6.3, ey + 0.32, 0.45, 0.35);
            Dot(gear, EyePrimary, 6.75, ey + 0.64, 0.4, 0.35);
        }

        // ---- 5 New Antigravity-Native Reaction Scenes ----

        // Typing: animated mini keyboard under paws + bouncing "..." speech bubble
        void DrawTypingScene()
        {
            Brush board = IsDark ? DarkCase : SteelDark;
            Brush key = IsDark ? MonoMid : Cloud;
            Brush activeKey = IsDark ? MonoBright : GlassLit;
            Dot(gear, board, 1.4, 6.2, 8.2, 1.4);
            int tap = tick / 2 % 4;
            for (int i = 0; i < 4; i++)
            {
                bool down = i == tap;
                Dot(gear, down ? activeKey : key, 2.1 + i * 1.8, down ? 6.65 : 6.45, 1.1, 0.55);
            }
            // Little paws tapping alternately
            bool leftDown = tick % 4 < 2;
            Dot(gear, ArmLeftUpper, 1.6, leftDown ? 5.3 : 4.8, 0.65, 0.9);
            Dot(gear, ArmRightUpper, 8.7, leftDown ? 4.8 : 5.3, 0.65, 0.9);

            // Bouncing typing dots bubble on top-right
            double bx = 9.8, by = -1.6;
            Dot(air, IsDark ? DarkCase : Paper, bx, by, 4.4, 2.1);
            for (int d = 0; d < 3; d++)
            {
                double lift = ((tick / 3 + d) % 3 == 0) ? -0.35 : 0;
                Brush dotCol = d == 0 ? (IsDark ? MonoBright : AgyGoldBrush)
                    : d == 1 ? (IsDark ? MonoMid : AgyEmeraldBrush)
                    : (IsDark ? MonoBright : GlassLit);
                Dot(air, dotCol, bx + 0.7 + d * 1.2, by + 0.8 + lift, 0.65, 0.65);
            }
        }

        // Terminal: dark shell window with ">_" prompt and streaming log lines
        void DrawTerminal()
        {
            double tx = -5.6;
            Dot(air, IsDark ? MonoMid : SteelDark, tx, 1.7, 5.8, 6.4);
            Dot(air, DarkScreen, tx + 0.45, 2.2, 4.9, 5.4);
            // Tiny green/amber header dots
            Dot(air, IsDark ? MonoBright : AgyCoralBrush, tx + 0.75, 2.45, 0.45, 0.45);
            Dot(air, IsDark ? MonoMid : AgyGoldBrush, tx + 1.4, 2.45, 0.45, 0.45);
            Dot(air, IsDark ? MonoDim : AgyEmeraldBrush, tx + 2.05, 2.45, 0.45, 0.45);
            // Prompt chevron ">"
            Brush prompt = IsDark ? MonoBright : AgyEmeraldBrush;
            Dot(air, prompt, tx + 0.85, 3.5, 0.4, 0.4);
            Dot(air, prompt, tx + 1.25, 3.85, 0.4, 0.4);
            Dot(air, prompt, tx + 0.85, 4.2, 0.4, 0.4);
            // Scrolling output lines + blinking cursor
            int step = tick / 4;
            Dot(air, IsDark ? MonoMid : Cloud, tx + 2.0, 3.7, 1.4 + (step % 3) * 0.6, 0.45);
            Dot(air, IsDark ? MonoDim : AgySkyBrush, tx + 0.9, 5.1, 2.8, 0.42);
            Dot(air, IsDark ? MonoMid : AgyGoldBrush, tx + 0.9, 6.0, 1.6 + ((step + 1) % 2) * 1.1, 0.42);
            if (tick % 6 < 3) Dot(air, prompt, tx + 3.8, 6.0, 0.6, 0.6);

            Dot(air, ArmLeftUpper, 0.4, 5.0, 1.8, 0.85);
        }

        // Browsing: floating pixel globe / viewport with scanning sweep
        void DrawBrowserGlobe()
        {
            double gx = 9.4, gy = -1.8;
            Brush ring = IsDark ? MonoBright : GlassLit;
            Brush lat = IsDark ? MonoMid : AgyBlueBrush;
            Shape(air, ring, gx, gy,
                ".###.",
                "#...#",
                "#...#",
                ".###.");
            Dot(air, lat, gx + 1, gy + 1.5, 3, 0.45);
            double sweep = 1.2 + Math.Round(Math.Sin(tick * 0.25) * 0.9);
            Dot(air, IsDark ? MonoBright : AgyGoldBrush, gx + sweep, gy + 0.5, 0.65, 3.0);
        }

        // Multitasking: juggling 3 glowing task orbs above head when 2+ sessions run while minimized!
        void DrawMultitaskingJuggle()
        {
            // Raised juggling arms alternating
            bool phase = tick % 6 < 3;
            Dot(gear, ArmLeftUpper, 1.0, phase ? 3.5 : 4.2, 0.55, 1.0);
            Dot(gear, ArmLeftLower, 1.5, 4.4, 0.5, 0.6);
            Dot(gear, ArmRightLower, 9.45, phase ? 4.2 : 3.5, 0.55, 1.0);
            Dot(gear, ArmRightUpper, 8.95, 4.4, 0.5, 0.6);

            Brush[] orbColors = IsDark
                ? new[] { MonoBright, MonoMid, MonoBright }
                : new[] { AgyGoldBrush, AgyEmeraldBrush, GlassLit };
            for (int i = 0; i < 3; i++)
            {
                double a = tick * 0.24 + i * (Math.PI * 2.0 / 3.0);
                double ox = 5.2 + Math.Cos(a) * 4.8;
                double oy = -0.8 + Math.Sin(a) * 2.2;
                Dot(air, orbColors[i], ox, oy, 1.05, 1.05);
                // Trailing motion pixel
                double pa = a - 0.38;
                Dot(air, IsDark ? MonoDim : AgyOrbitDim, 5.3 + Math.Cos(pa) * 4.8, -0.7 + Math.Sin(pa) * 2.2, 0.48, 0.48);
            }
        }

        // Background: chat finished, live server / daemon still humming calmly beside mascot
        void DrawBackgroundServer()
        {
            double sx = 9.8, sy = 0.2;
            Dot(air, IsDark ? DarkCase : Paper, sx, sy, 3.8, 4.6);
            Dot(air, IsDark ? MonoMid : SteelDark, sx + 0.35, sy + 0.4, 3.1, 1.5);
            Dot(air, IsDark ? MonoMid : SteelDark, sx + 0.35, sy + 2.5, 3.1, 1.5);
            // Pulsing live-server LEDs
            bool beat = tick % 10 < 5;
            Dot(air, beat ? (IsDark ? MonoBright : AgyEmeraldBrush) : (IsDark ? MonoDim : AgyTealBrush), sx + 0.8, sy + 0.85, 0.65, 0.65);
            Dot(air, !beat ? (IsDark ? MonoBright : GlassLit) : (IsDark ? MonoDim : AgyBlueBrush), sx + 0.8, sy + 2.95, 0.65, 0.65);
            Dot(air, IsDark ? MonoBright : Cloud, sx + 1.9, sy + 1.0, 1.2, 0.35);
            Dot(air, IsDark ? MonoMid : Cloud, sx + 1.9, sy + 3.1, 1.2, 0.35);
        }

        // Delegating: streaming dispatch packets into twin subagent worker cards
        void DrawDelegatingDispatch()
        {
            Dot(gear, ArmLeftUpper, 1.5, 4.8, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.1, 5.4, 0.5, 0.5);
            Dot(air, ArmRightUpper, 8.9, 4.2, 1.35, 0.65);

            double nx = 10.5;
            Brush frame = IsDark ? DarkCase : Paper;
            Brush slot = IsDark ? MonoMid : SteelDark;
            for (int row = 0; row < 2; row++)
            {
                double ny = row == 0 ? -0.4 : 3.2;
                Dot(air, slot, nx, ny, 4.2, 2.7);
                Dot(air, frame, nx + 0.35, ny + 0.35, 3.5, 2.0);
                bool active = (tick / 4 % 2) == row;
                Brush badge = row == 0
                    ? (IsDark ? MonoBright : AgyGoldBrush)
                    : (IsDark ? MonoMid : AgyVioletBrush);
                Dot(air, badge, nx + 0.7, ny + 0.75, 0.65, 0.65);
                Dot(air, active ? (IsDark ? MonoBright : GlassLit) : (IsDark ? MonoDim : CloudDim), nx + 1.65, ny + 0.85, 1.6, 0.45);
                Dot(air, IsDark ? MonoDim : AgyEmeraldBrush, nx + 0.7, ny + 1.65, 2.2, 0.38);
            }

            // Animated packet dot travelling from hand toward active subagent card
            int step = tick % 6;
            double frac = step / 5.0;
            bool upper = (tick / 6) % 2 == 0;
            double pxP = 9.4 + frac * 1.1;
            double pyP = 4.2 + (upper ? -2.9 : -0.1) * frac;
            Dot(air, IsDark ? MonoBright : AgyGoldBrush, pxP, pyP, 0.65, 0.65);
        }

        // Happy: rosy blush cheeks + twinkling 4-point Antigravity Aurora stars
        void DrawHappyAurora()
        {
            if (!IsDark)
            {
                Dot(gear, BlushBrush, 2.8, 4.15, 0.9, 0.55);
                Dot(gear, BlushBrush, 7.2, 4.15, 0.9, 0.55);
            }
            int phase = tick / 3;
            double liftA = -((phase) % 4) * 0.45;
            double liftB = -((phase + 2) % 4) * 0.45;
            Brush s1 = IsDark ? MonoBright : AgyGoldBrush;
            Brush s2 = IsDark ? MonoMid : AgyEmeraldBrush;
            Brush s3 = IsDark ? MonoBright : GlassLit;

            Shape(air, s1, -2.0, -0.4 + liftA,
                ".#.",
                "###",
                ".#.");
            Shape(air, s2, 10.6, -1.0 + liftB,
                ".#.",
                "###",
                ".#.");
            Dot(air, s3, 4.9, -2.2 + liftA * 0.5, 0.8, 0.8);
        }

        // Error: glitch squint eyes + flashing Coral Red lightning bolt
        void DrawErrorGlitchFace()
        {
            Brush col = IsDark ? MonoBright : EyeWhite;
            // Left dizzy ">", Right dizzy "<"
            Dot(gear, col, 3.55, 2.95, 0.45, 0.35);
            Dot(gear, col, 4.0, 3.3, 0.45, 0.35);
            Dot(gear, col, 3.55, 3.65, 0.45, 0.35);

            Dot(gear, col, 6.85, 2.95, 0.45, 0.35);
            Dot(gear, col, 6.4, 3.3, 0.45, 0.35);
            Dot(gear, col, 6.85, 3.65, 0.45, 0.35);
        }

        void DrawErrorLightning()
        {
            if (tick % 8 < 2) return;
            Brush bolt = IsDark ? MonoBright : ErrorRedBrush;
            Brush core = IsDark ? MonoMid : Spark;
            Shape(air, bolt, 4.2, -3.7,
                "..##",
                ".##.",
                "###.",
                ".##.",
                ".#..");
            Dot(air, core, 5.1, -2.1, 0.45, 0.45);
        }

        // Original AgyPet sentiment cues (angry steam vents or sad teardrop; preserves body skin)
        void DrawSentimentEffects(bool angry)
        {
            if (angry)
            {
                // Furrowed sharp brow bar
                Dot(gear, EyePrimary, 3.4, 2.4, 1.35, 0.35);
                Dot(gear, EyePrimary, 6.1, 2.4, 1.35, 0.35);
                // Dual animated steam puffs above left/right shoulders
                int p = (tick / 3) % 4;
                double rise = -p * 0.55;
                Brush steam = IsDark ? MonoBright : SteamBright;
                Dot(air, steam, 2.2, -0.6 + rise, 0.9, 0.7);
                Dot(air, IsDark ? MonoMid : AgyGoldBrush, 1.8, -1.3 + rise, 0.65, 0.55);
                Dot(air, steam, 7.9, -0.8 + rise, 0.9, 0.7);
                Dot(air, IsDark ? MonoMid : AgyGoldBrush, 8.4, -1.5 + rise, 0.65, 0.55);
            }
            else
            {
                // Drooping outer brows + gentle falling cyan pixel teardrop
                Dot(gear, EyePrimary, 3.8, 2.45, 0.75, 0.3);
                Dot(gear, EyePrimary, 3.25, 2.75, 0.6, 0.3);
                Dot(gear, EyePrimary, 6.3, 2.45, 0.75, 0.3);
                Dot(gear, EyePrimary, 6.95, 2.75, 0.6, 0.3);
                double fall = ((tick / 3) % 5) * 0.55;
                Dot(gear, IsDark ? MonoBright : TearBrush, 3.45, 4.2 + fall, 0.55, 0.75);
            }
        }

        void DrawDanceNotes()
        {
            int t = tick / 3 % 4;
            Brush c = IsDark ? MonoBright : AgyGoldBrush;
            Dot(air, c, -1.2, 0.2 - t * 0.4, 0.5, 1.3);
            Dot(air, c, -1.7, 1.2 - t * 0.4, 0.7, 0.5);
            Dot(air, IsDark ? MonoMid : GlassLit, 11.4, -0.4 - ((t + 2) % 4) * 0.4, 0.5, 1.3);
            Dot(air, IsDark ? MonoMid : GlassLit, 10.9, 0.6 - ((t + 2) % 4) * 0.4, 0.7, 0.5);
        }

        void DrawZzz()
        {
            int t = tick / 10 % 4;
            if (t >= 1) Shape(air, IsDark ? MonoDim : CloudDim, 9.4, -2, "###", "..#", ".#.", "###");
            if (t >= 2) Shape(air, IsDark ? MonoBright : GlassLit, 12.8, -4, "####", "..#.", ".#..", "####");
        }

        void DrawBang()
        {
            if (tick % 10 < 2) return;
            Brush b = IsDark ? MonoBright : Amber;
            Dot(air, b, 5.0, -3.2, 1, 2.5);
            Dot(air, b, 5.0, -0.3, 1, 0.9);
        }

        void DrawQuestion()
        {
            if (tick % 16 < 2) return;
            Brush b = IsDark ? MonoBright : GlassLit;
            Shape(air, b, 4.0, -3.2,
                ".##.",
                "...#",
                "..#.");
            Dot(air, b, 6.0, 0.1);
        }

        static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
