using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AgyPet
{
    // Pixel-art Antigravity companion supporting two official skins:
    //  1) "classic" — refined CLI pixel-art arch in official Antigravity logo colors
    //                 (yellow-green upper-left, fiery orange-red upper-right, vivid blue core & legs, violet right shoulder)
    //  2) "dark"    — sleek new monochrome obsidian/graphite Antigravity remake with luminous white eyes
    public class Mascot : Canvas
    {
        public const string ClassicSkin = MascotCore.ClassicSkin;
        public const string DarkSkin = MascotCore.DarkSkin;

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
        static readonly Brush ErrorRedBrush = Frozen(Color.FromRgb(0xEF, 0x44, 0x44));

        // Dark Monochrome Remake palette (sampled from the new dark Antigravity remake logo)
        static readonly Color DarkTop = Color.FromRgb(0x44, 0x44, 0x4A);
        static readonly Color DarkMid = Color.FromRgb(0x2C, 0x2C, 0x31);
        static readonly Color DarkLeg = Color.FromRgb(0x18, 0x18, 0x1B);
        static readonly Color DarkRim = Color.FromRgb(0x6E, 0x6E, 0x78);
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

        // Alternating one-leg hop masks preserving 100% exact Idle body colors per pixel
        static readonly string[] AgyDanceLeftMask =
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

        static readonly string[] AgyDanceRightMask =
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

        // Compact rounded capsule for Sleeping
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
        readonly Canvas archDanceLeftLayer = new Canvas();
        readonly Canvas archDanceRightLayer = new Canvas();
        readonly Canvas archCompactLayer = new Canvas();
        readonly Canvas gear = new Canvas();
        readonly Canvas air = new Canvas();
        readonly DispatcherTimer timer;
        readonly Random rnd = new Random();

        readonly ScaleTransform recoilTransform = new ScaleTransform(1.0, 1.0);
        int tick, blinkAt, blinkLeft, glanceAt, glance;
        int hurtTicks;
        int pressTicks;
        int danceTicks;
        int clickStreak;
        DateTime lastClickUtc = DateTime.MinValue;
        MascotMood lastClickMood = MascotMood.Idle;
        double lookX, lookY, lookTargetX, lookTargetY;
        int activeSessionCount = 3;

        public MascotMood Mood { get; set; }
        public int ActiveSessionCount
        {
            get { return activeSessionCount; }
            set { activeSessionCount = value; }
        }
        public string SkinName { get; private set; }
        public bool IsHurt { get { return hurtTicks > 0; } }
        public bool IsPressed { get { return pressTicks > 0; } }
        public bool IsDancing { get { return danceTicks > 0; } }

        public Mascot(double pixelSize, string skin = ClassicSkin)
        {
            px = pixelSize;
            SkinName = NormalizeSkin(skin);
            ClipToBounds = false;
            sprite.RenderTransformOrigin = new Point(0.5, 0.85);
            sprite.RenderTransform = recoilTransform;
            air.IsHitTestVisible = false;
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

        // Subtle tactile press wince (waking from sleep or rapid continuous multi-press while working): "> <" eyes + squish.
        public void Poke()
        {
            if (Mood == MascotMood.Sleeping) return;
            pressTicks = 0;
            danceTicks = 0;
            hurtTicks = 6;
            Step(false);
        }

        // Crisp normal single-press tactile squish without changing eyes/expression or staying stuck in multi-press.
        public void Press()
        {
            if (Mood == MascotMood.Sleeping) return;
            hurtTicks = 0;
            pressTicks = 4;
            Step(false);
        }

        public void CancelHurt()
        {
            hurtTicks = 0;
            pressTicks = 0;
            recoilTransform.ScaleX = 1.0;
            recoilTransform.ScaleY = 1.0;
        }

        // Single press -> crisp tactile Press() squish;
        // Continuous rapid multi-press (3+ within ~1.1s per gap) -> Dance in Idle, Annoyed Poke() while working,
        // and resets streak immediately so the next single click afterward performs a normal Press().
        public void RegisterClick()
        {
            RegisterClickAt(DateTime.UtcNow);
        }

        internal void RegisterClickAt(DateTime nowUtc)
        {
            if (Mood == MascotMood.Sleeping)
            {
                clickStreak = 0;
                pressTicks = 0;
                hurtTicks = 0;
                danceTicks = 0;
                return;
            }

            if (lastClickUtc == DateTime.MinValue || (nowUtc - lastClickUtc).TotalSeconds > 1.1 || lastClickMood != Mood)
                clickStreak = 0;

            clickStreak++;
            lastClickUtc = nowUtc;
            lastClickMood = Mood;

            if (clickStreak >= 3)
            {
                clickStreak = 0;
                if (Mood == MascotMood.Idle)
                    Dance();
                else
                    Poke();
            }
            else
            {
                Press();
            }
        }

        // Playful alternating leg-hop dance on click (Idle only)
        public void Dance()
        {
            if (Mood != MascotMood.Idle || hurtTicks > 0) return;
            pressTicks = 0;
            danceTicks = 24;
            Step(false);
        }

        public static void NormalizeLookOffset(double dx, double dy, out double nx, out double ny)
        {
            nx = Math.Max(-1.0, Math.Min(1.0, Math.Tanh(dx / 220.0)));
            ny = Math.Max(-1.0, Math.Min(1.0, Math.Tanh(dy / 160.0)));
        }

        public void SetLookOffset(double nx, double ny)
        {
            lookTargetX = Math.Max(-1.0, Math.Min(1.0, nx));
            lookTargetY = Math.Max(-1.0, Math.Min(1.0, ny));
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
            archDanceLeftLayer.Children.Clear();
            archDanceRightLayer.Children.Clear();
            archCompactLayer.Children.Clear();
            gear.Children.Clear();
            air.Children.Clear();

            RenderPixelMask(archNormalLayer, AgyArchMask, false);
            RenderPixelMask(archDanceLeftLayer, AgyDanceLeftMask, false);
            RenderPixelMask(archDanceRightLayer, AgyDanceRightMask, false);
            RenderPixelMask(archCompactLayer, AgyCompactMask, true);
            archDanceLeftLayer.Visibility = Visibility.Collapsed;
            archDanceRightLayer.Visibility = Visibility.Collapsed;
            archCompactLayer.Visibility = Visibility.Collapsed;
            sprite.Children.Add(archNormalLayer);
            sprite.Children.Add(archDanceLeftLayer);
            sprite.Children.Add(archDanceRightLayer);
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

        public static Color OfficialArchPixelColor(double u, double v, bool compact, bool topEdge, bool innerArch)
        {
            var r = MascotCore.OfficialArchPixel(u, v, compact, topEdge, innerArch);
            return Color.FromRgb(r.R, r.G, r.B);
        }

        public static Color DarkArchPixelColor(double u, double v, bool compact, bool topEdge, bool sideEdge, bool bottomEdge)
        {
            var r = MascotCore.DarkArchPixel(u, v, compact, topEdge, sideEdge, bottomEdge);
            return Color.FromRgb(r.R, r.G, r.B);
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

        void Step(bool advanceTick = true)
        {
            if (advanceTick) tick++;
            gear.Children.Clear();
            air.Children.Clear();
            var mood = Mood;
            if (mood != MascotMood.Idle)
                danceTicks = 0;
            if (mood == MascotMood.Sleeping)
            {
                pressTicks = 0;
                hurtTicks = 0;
                clickStreak = 0;
            }
            bool hurt = mood != MascotMood.Sleeping && hurtTicks > 0;
            if (advanceTick && hurtTicks > 0) hurtTicks--;
            bool pressed = mood != MascotMood.Sleeping && pressTicks > 0 && !hurt;
            if (advanceTick && pressTicks > 0) pressTicks--;
            bool dancing = mood == MascotMood.Idle && danceTicks > 0 && !hurt;
            if (advanceTick && danceTicks > 0) danceTicks--;

            int hopStep = (tick / 3) % 4; // 0: left foot planted, 1: hop peak, 2: right foot planted, 3: hop peak
            double danceBob = dancing ? ((hopStep == 1 || hopStep == 3) ? -0.48 * px : -0.1 * px) : 0;
            double doneHop = mood == MascotMood.Done ? -Math.Round(Math.Abs(Math.Sin(tick * 0.45)) * 1.1) * px : 0;
            double baseBob = dancing ? danceBob : doneHop;
            bool wiggles = mood == MascotMood.Alert || mood == MascotMood.Question || mood == MascotMood.Error;

            double pressDip = pressed && pressTicks >= 2 ? px * 0.3 : 0;
            double y = hurt && hurtTicks >= 4 ? baseBob + px * 0.45 : baseBob + pressDip;
            SetTop(sprite, y);

            bool typing = mood == MascotMood.Working && tick % 90 < 72;
            double danceShift = dancing ? (hopStep == 0 ? -0.18 * px : hopStep == 2 ? 0.18 * px : 0) : 0;
            double errorShake = mood == MascotMood.Error && tick % 6 < 2 ? (tick % 2 == 0 ? -0.4 : 0.4) * px : 0;
            double x = hurt && hurtTicks >= 4 ? ((hurtTicks % 2 == 0 ? -0.28 : 0.28) * px)
                : mood == MascotMood.Error ? errorShake
                : (wiggles && tick % 12 < 4 ? (tick % 2 == 0 ? -px * 0.4 : px * 0.4) : danceShift);
            SetLeft(sprite, x);

            // Subtle tactile micro-squish on press / wince
            if (hurt && hurtTicks >= 4)
            {
                recoilTransform.ScaleX = 1.05;
                recoilTransform.ScaleY = 0.94;
            }
            else if (pressed && pressTicks >= 2)
            {
                recoilTransform.ScaleX = 1.06;
                recoilTransform.ScaleY = 0.92;
            }
            else if (recoilTransform.ScaleX != 1.0 || recoilTransform.ScaleY != 1.0)
            {
                recoilTransform.ScaleX = 1.0;
                recoilTransform.ScaleY = 1.0;
            }

            if (advanceTick)
            {
                if (--blinkAt <= 0) { blinkLeft = 2; blinkAt = rnd.Next(30, 70); }
                if (--glanceAt <= 0)
                {
                    glance = glance == 0 ? (rnd.Next(2) == 0 ? -1 : 1) : 0;
                    glanceAt = glance == 0 ? rnd.Next(40, 100) : rnd.Next(12, 25);
                }
            }
            bool closed = blinkLeft > 0;
            if (advanceTick && blinkLeft > 0) blinkLeft--;

            lookX += (lookTargetX - lookX) * 0.35;
            lookY += (lookTargetY - lookY) * 0.35;

            bool compact = mood == MascotMood.Sleeping;
            bool leftHop = !compact && dancing && hopStep == 0;
            bool rightHop = !compact && dancing && hopStep == 2;
            archCompactLayer.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
            archDanceLeftLayer.Visibility = leftHop ? Visibility.Visible : Visibility.Collapsed;
            archDanceRightLayer.Visibility = rightHop ? Visibility.Visible : Visibility.Collapsed;
            archNormalLayer.Visibility = (!compact && !leftHop && !rightHop) ? Visibility.Visible : Visibility.Collapsed;

            if (mood == MascotMood.Idle || mood == MascotMood.Listening || mood == MascotMood.Speaking ||
                mood == MascotMood.Searching || mood == MascotMood.Browsing || mood == MascotMood.Background)
            {
                if (dancing) DrawDanceArms(hopStep);
                else DrawRestingArms();
            }

            double ex = mood == MascotMood.Idle ? (lookX * 0.48 + glance * 0.08) : 0;
            double ey = mood == MascotMood.Idle ? (lookY * 0.34) : 0;

            // 1. Always render the active status's props & arms so the current status stays calm and intact
            switch (mood)
            {
                case MascotMood.Idle:
                    if (!hurt) DrawEyes(3.9 + ex, 3.2 + ey, 6.3 + ex, 3.2 + ey, closed);
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
                    if (!hurt) DrawThinkingEyes();
                    DrawThinkingArms();
                    DrawThoughtCloud();
                    break;
                case MascotMood.Speaking:
                    if (!hurt) { DrawEyes(3.9, 3.1, 6.3, 3.1, false); DrawMouth(); }
                    else DrawSpeechDotsOnly();
                    break;
                case MascotMood.Typing:
                    if (!hurt) DrawEyes(3.9, 3.3, 6.3, 3.3, closed);
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
                    if (!hurt) DrawConfusedEyes();
                    DrawConfusedArms();
                    DrawQuestion();
                    break;
                case MascotMood.Done:
                    if (!hurt) DrawHappyEyes();
                    DrawArmsUp();
                    Dot(air, AgyShadowBrush, 2.5, 8.7, 6.0, 0.4);
                    DrawCelebrationConfetti();
                    break;
                case MascotMood.Error:
                    if (!hurt) DrawErrorGlitchFace();
                    DrawArmsUp();
                    DrawErrorLightning();
                    break;
            }

            // Subtle "> <" wince eyes on rapid clicks
            if (hurt)
                DrawHurtOverlay(compact);
        }

        // Samples the exact pixel color of the Idle arch body at (x, y) so arms and moving limbs match seamlessly.
        public Color BodyColorAt(double x, double y)
        {
            double u = Math.Max(0.0, Math.Min(1.0, (x - 0.5) / 9.5));
            double v = Math.Max(0.0, Math.Min(1.0, y / 8.5));
            return IsDark
                ? DarkArchPixelColor(u, v, false, false, true, false)
                : OfficialArchPixelColor(u, v, false, false, false);
        }

        Brush BodyBrushAt(double x, double y)
        {
            return Frozen(BodyColorAt(x, y));
        }

        Brush EyePrimary { get { return IsDark ? EyeWhite : EyeDark; } }
        Brush EyeGlint { get { return IsDark ? EyeSoftSilver : EyeWhite; } }
        Brush ArmLeftUpper { get { return BodyBrushAt(2.0, 4.8); } }
        Brush ArmLeftLower { get { return BodyBrushAt(2.0, 5.4); } }
        Brush ArmRightUpper { get { return BodyBrushAt(9.0, 4.8); } }
        Brush ArmRightLower { get { return BodyBrushAt(9.0, 5.4); } }

        void DrawRestingArms()
        {
            Dot(gear, ArmLeftUpper, 1.5, 4.8, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.1, 5.4, 0.5, 0.5);
            Dot(gear, ArmRightUpper, 9.0, 4.8, 0.5, 1.0);
            Dot(gear, ArmRightLower, 9.4, 5.4, 0.5, 0.5);
        }

        void DrawDanceArms(int hopStep)
        {
            bool leftHigh = hopStep == 0 || hopStep == 3;
            Dot(gear, ArmLeftUpper, 1.4, leftHigh ? 4.2 : 4.9, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.0, leftHigh ? 4.0 : 5.5, 0.5, 0.5);
            Dot(gear, ArmRightUpper, 9.1, !leftHigh ? 4.2 : 4.9, 0.5, 1.0);
            Dot(gear, ArmRightLower, 9.5, !leftHigh ? 4.0 : 5.5, 0.5, 0.5);
        }

        void DrawArmsUp()
        {
            Dot(gear, BodyBrushAt(2.0, 3.8), 1.1, 3.7, 0.5, 1.0);
            Dot(gear, BodyBrushAt(2.0, 4.4), 1.6, 4.4, 0.5, 0.7);
            Dot(gear, BodyBrushAt(9.0, 3.8), 9.4, 3.7, 0.5, 1.0);
            Dot(gear, BodyBrushAt(9.0, 4.4), 8.9, 4.4, 0.5, 0.7);
        }

        void DrawEyes(double lx, double ly, double rx, double ry, bool closed, bool shortEyes = false)
        {
            if (closed) { DrawShutEyes(lx, ly + 0.4, rx, ry + 0.4); return; }
            double h = shortEyes ? 0.5 : 0.9;
            double glintSize = shortEyes ? 0.28 : 0.4;
            double pupilSize = shortEyes ? 0.25 : 0.35;
            double pupilY = shortEyes ? (h - pupilSize) : 0.35;
            Dot(gear, EyePrimary, lx, ly, 0.85, h);
            Dot(gear, EyePrimary, rx, ry, 0.85, h);
            if (!IsDark)
            {
                Dot(gear, EyeGlint, lx + 0.42, ly + 0.04, glintSize, glintSize);
                Dot(gear, EyeGlint, rx + 0.42, ry + 0.04, glintSize, glintSize);
            }
            else
            {
                // Subtle pupil depth cleanly bounded inside luminous white eyes on Dark Remake
                Dot(gear, EyeDark, lx + 0.45, ly + pupilY, pupilSize, pupilSize);
                Dot(gear, EyeDark, rx + 0.45, ry + pupilY, pupilSize, pupilSize);
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

        void DrawThinkingEyes()
        {
            // Pondering upward gaze with slightly raised brow
            Dot(gear, EyePrimary, 3.9, 2.75, 0.85, 0.55);
            Dot(gear, EyePrimary, 6.3, 2.75, 0.85, 0.8);
            if (!IsDark)
            {
                Dot(gear, EyeWhite, 4.35, 2.75, 0.35, 0.35);
                Dot(gear, EyeWhite, 6.75, 2.75, 0.35, 0.35);
            }
        }

        void DrawThinkingArms()
        {
            Dot(gear, ArmLeftUpper, 1.5, 4.8, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.1, 5.4, 0.5, 0.5);
            Dot(gear, BodyBrushAt(8.5, 4.6), 8.6, 4.6, 0.55, 0.85);
            Dot(gear, BodyBrushAt(8.5, 4.2), 7.6, 4.2, 1.05, 0.5);
        }

        void DrawStartledEyes()
        {
            Dot(gear, EyeWhite, 3.6, 2.9, 1.25, 1.25);
            Dot(gear, EyeDark, 3.95, 3.25, 0.55, 0.55);
            Dot(gear, EyeWhite, 6.1, 2.9, 1.25, 1.25);
            Dot(gear, EyeDark, 6.45, 3.25, 0.55, 0.55);
        }

        void DrawConfusedEyes()
        {
            Dot(gear, EyePrimary, 3.7, 3.2, 0.85, 0.85);
            if (!IsDark) Dot(gear, EyeWhite, 4.05, 3.2, 0.35, 0.35);
            Dot(gear, EyeWhite, 6.0, 2.8, 1.4, 1.4);
            Dot(gear, EyeDark, 6.45, 3.25, 0.55, 0.55);
        }

        void DrawConfusedArms()
        {
            Dot(gear, ArmLeftLower, 1.5, 4.8, 0.5, 0.5);
            Dot(gear, ArmLeftUpper, 1.1, 5.3, 0.5, 0.5);
            Dot(gear, ArmLeftLower, 1.5, 5.8, 0.5, 0.5);
            Dot(gear, BodyBrushAt(9.0, 4.7), 9.0, 4.7, 0.5, 0.6);
            Dot(gear, BodyBrushAt(9.0, 3.7), 9.5, 3.7, 0.5, 1.1);
            Dot(gear, BodyBrushAt(9.0, 3.3), 8.8, 3.3, 0.7, 0.5);
        }

        // ---- Scenes & Props ------------------------------------------------

        void DrawIdleSparkle()
        {
            int phase = tick % 24;
            if (phase >= 18) return;
            double sx = 8.9, sy = 1.3;
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
            Dot(air, cloudRim, 8.4, 0.6, 0.5, 0.5);
            Dot(air, cloudRim, 9.1, -0.2, 0.75, 0.75);
            Dot(air, cloudRim, 8.7, -2.6, 4.8, 2.2);
            Dot(air, cloudBg, 9.1, -2.25, 4.0, 1.5);

            Brush[] sparks = IsDark
                ? new[] { MonoBright, MonoMid, MonoBright }
                : new[] { AgyGoldBrush, AgyEmeraldBrush, GlassLit };
            for (int d = 0; d < 3; d++)
            {
                bool lit = d == pulse;
                Dot(air, lit ? sparks[d] : (IsDark ? MonoDim : CloudDim), 9.6 + d * 1.15, lit ? -1.85 : -1.65, 0.7, 0.7);
            }
        }

        void DrawCelebrationConfetti()
        {
            double liftA = tick % 8 < 4 ? -0.25 : 0.15;
            double liftB = tick % 8 < 4 ? 0.15 : -0.25;
            Brush c1 = IsDark ? MonoBright : AgyCoralBrush;
            Brush c2 = IsDark ? MonoMid : AgyEmeraldBrush;
            Brush c3 = IsDark ? MonoBright : AgyGoldBrush;
            Brush c4 = IsDark ? MonoMid : GlassLit;
            Brush c5 = IsDark ? MonoDim : AgyBlueBrush;

            Dot(air, c1, 0.2, 1.2 + liftA, 0.45, 0.85);
            Dot(air, c1, 0.65, 1.6 + liftA, 0.45, 0.45);
            Dot(air, c2, 1.3, -0.7 + liftB, 1.0, 0.36);
            Dot(air, c2, 1.6, -1.0 + liftB, 0.36, 1.0);
            Dot(air, c3, 3.1, -1.4 + liftA, 0.45, 0.45);
            Dot(air, c3, 3.55, -1.0 + liftA, 0.45, 0.45);
            Dot(air, c4, 5.4, -1.6 + liftB, 0.55, 0.55);
            Dot(air, c5, 7.2, -1.1 + liftB, 0.45, 0.45);
            Dot(air, c5, 7.65, -1.5 + liftB, 0.45, 0.45);
            Dot(air, c3, 8.7, -0.6 + liftA, 0.9, 0.36);
            Dot(air, c3, 9.0, -0.9 + liftA, 0.36, 0.9);
            Dot(air, c1, 9.9, 0.8 + liftB, 0.45, 0.8);
            Dot(air, c1, 10.3, 1.2 + liftB, 0.45, 0.45);
        }

        void DrawLaptop(bool typing)
        {
            double ox = -4.2;
            Dot(air, IsDark ? MonoMid : Steel, ox, 1.8, 5.2, 6.2);
            Dot(air, IsDark ? DarkScreen : ScreenGlow, ox + 0.85, 2.5, 3.5, 4.5);
            Dot(air, IsDark ? DarkCase : SteelDark, ox, 8.0, 6.4, 0.9);

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
            double gx = 3.5 + Math.Round(Math.Sin(tick * 0.14) * 2.8);
            Brush rim = IsDark ? MonoBright : GlassLit;
            Brush handle = IsDark ? MonoMid : AgyBlueBrush;
            Brush glint = IsDark ? MonoMid : EyeWhite;
            Shape(air, rim, gx, -2.4,
                ".###.",
                "#...#",
                "#...#",
                ".###.");
            Dot(air, handle, gx + 4, 1.4);
            Dot(air, handle, gx + 5, 2.3);
            if (tick % 8 < 4) Dot(air, glint, gx + 2, -0.9, 1, 1);
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
                double h = 3.6 - i * 0.85;
                double top = 4.5 - h / 2;
                Dot(air, i == t ? hot : cool, -0.4 - i * 0.95, top, 0.75, h);
                Dot(air, i == t ? hot : cool, 10.6 + i * 0.95, top, 0.75, h);
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
            if (t >= 1) Dot(air, IsDark ? MonoBright : EyeWhite, 10.3, 3.8);
            if (t >= 2) Dot(air, IsDark ? MonoMid : GlassLit, 11.4, 2.9);
            if (t >= 3) Dot(air, IsDark ? MonoDim : Blue, 12.5, 2.0);
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

            // Bouncing typing dots bubble snug on top-right
            double bx = 8.7, by = -0.9;
            Dot(air, IsDark ? DarkCase : Paper, bx, by, 4.2, 2.0);
            for (int d = 0; d < 3; d++)
            {
                double lift = ((tick / 3 + d) % 3 == 0) ? -0.3 : 0;
                Brush dotCol = d == 0 ? (IsDark ? MonoBright : AgyGoldBrush)
                    : d == 1 ? (IsDark ? MonoMid : AgyEmeraldBrush)
                    : (IsDark ? MonoBright : GlassLit);
                Dot(air, dotCol, bx + 0.65 + d * 1.15, by + 0.72 + lift, 0.62, 0.62);
            }
        }

        // Terminal: dark shell window with ">_" prompt and streaming log lines
        void DrawTerminal()
        {
            double tx = -4.3;
            Dot(air, IsDark ? MonoMid : SteelDark, tx, 1.7, 5.4, 6.4);
            Dot(air, DarkScreen, tx + 0.45, 2.2, 4.5, 5.4);
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
            Dot(air, IsDark ? MonoMid : Cloud, tx + 1.9, 3.7, 1.2 + (step % 3) * 0.55, 0.45);
            Dot(air, IsDark ? MonoDim : AgySkyBrush, tx + 0.9, 5.1, 2.6, 0.42);
            Dot(air, IsDark ? MonoMid : AgyGoldBrush, tx + 0.9, 6.0, 1.5 + ((step + 1) % 2) * 1.0, 0.42);
            if (tick % 6 < 3) Dot(air, prompt, tx + 3.6, 6.0, 0.6, 0.6);

            Dot(air, ArmLeftUpper, 0.6, 5.0, 1.6, 0.85);
        }

        // Browsing: floating pixel globe / viewport with scanning sweep
        void DrawBrowserGlobe()
        {
            double gx = 8.6, gy = -1.0;
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

        public static int ResolveOrbCount(int activeSessions)
        {
            if (activeSessions <= 0) return 3;
            return Math.Min(8, activeSessions);
        }

        // Multitasking: juggling one glowing task orb per active session (2..8) tightly overhead
        void DrawMultitaskingJuggle()
        {
            // Raised juggling arms alternating
            bool phase = tick % 6 < 3;
            Dot(gear, ArmLeftUpper, 1.0, phase ? 3.5 : 4.2, 0.55, 1.0);
            Dot(gear, ArmLeftLower, 1.5, 4.4, 0.5, 0.6);
            Dot(gear, ArmRightLower, 9.45, phase ? 4.2 : 3.5, 0.55, 1.0);
            Dot(gear, ArmRightUpper, 8.95, 4.4, 0.5, 0.6);

            int count = Math.Max(2, ResolveOrbCount(activeSessionCount));
            double orbSize = count >= 6 ? 0.9 : 1.05;
            Brush[] orbColors = IsDark
                ? new[] { MonoBright, MonoMid }
                : new[] { AgyGoldBrush, AgyEmeraldBrush, GlassLit, AgyVioletBrush, AgyCoralBrush, AgyBlueBrush, AgyTealBrush, EyeWhite };

            for (int i = 0; i < count; i++)
            {
                double a = tick * 0.24 + i * (Math.PI * 2.0 / count);
                double ox = 5.1 + Math.Cos(a) * 3.9;
                double oy = -0.4 + Math.Sin(a) * 1.55;
                Dot(air, orbColors[i % orbColors.Length], ox, oy, orbSize, orbSize);
                // Trailing motion pixel
                double pa = a - 0.36;
                Dot(air, IsDark ? MonoDim : AgyOrbitDim, 5.2 + Math.Cos(pa) * 3.9, -0.32 + Math.Sin(pa) * 1.55, 0.45, 0.45);
            }
        }

        // Background: chat finished, live server / daemon still humming calmly beside mascot
        void DrawBackgroundServer()
        {
            double sx = 9.0, sy = 0.6;
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
            Dot(air, ArmRightUpper, 8.7, 4.2, 1.1, 0.65);

            double nx = 9.5;
            Brush frame = IsDark ? DarkCase : Paper;
            Brush slot = IsDark ? MonoMid : SteelDark;
            for (int row = 0; row < 2; row++)
            {
                double ny = row == 0 ? 0.1 : 3.4;
                Dot(air, slot, nx, ny, 4.1, 2.6);
                Dot(air, frame, nx + 0.35, ny + 0.35, 3.4, 1.9);
                bool active = (tick / 4 % 2) == row;
                Brush badge = row == 0
                    ? (IsDark ? MonoBright : AgyGoldBrush)
                    : (IsDark ? MonoMid : AgyVioletBrush);
                Dot(air, badge, nx + 0.7, ny + 0.7, 0.65, 0.65);
                Dot(air, active ? (IsDark ? MonoBright : GlassLit) : (IsDark ? MonoDim : CloudDim), nx + 1.6, ny + 0.8, 1.5, 0.45);
                Dot(air, IsDark ? MonoDim : AgyEmeraldBrush, nx + 0.7, ny + 1.55, 2.1, 0.38);
            }

            // Animated packet dot travelling from hand toward active subagent card
            int step = tick % 6;
            double frac = step / 5.0;
            bool upper = (tick / 6) % 2 == 0;
            double pxP = 8.9 + frac * 0.8;
            double pyP = 4.2 + (upper ? -2.6 : -0.1) * frac;
            Dot(air, IsDark ? MonoBright : AgyGoldBrush, pxP, pyP, 0.65, 0.65);
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
            Shape(air, bolt, 4.3, -2.7,
                "..##",
                ".##.",
                "###.",
                ".##.",
                ".#..");
            Dot(air, core, 5.2, -1.1, 0.45, 0.45);
        }

        void DrawDanceNotes()
        {
            int t = tick / 3 % 4;
            Brush c = IsDark ? MonoBright : AgyGoldBrush;
            Dot(air, c, -0.2, 0.6 - t * 0.3, 0.48, 1.2);
            Dot(air, c, -0.65, 1.5 - t * 0.3, 0.65, 0.48);
            Dot(air, IsDark ? MonoMid : GlassLit, 10.2, 0.2 - ((t + 2) % 4) * 0.3, 0.48, 1.2);
            Dot(air, IsDark ? MonoMid : GlassLit, 9.75, 1.1 - ((t + 2) % 4) * 0.3, 0.65, 0.48);
        }

        void DrawZzz()
        {
            int t = tick / 10 % 4;
            if (t >= 1) Shape(air, IsDark ? MonoDim : CloudDim, 8.6, -1.1, "###", "..#", ".#.", "###");
            if (t >= 2) Shape(air, IsDark ? MonoBright : GlassLit, 11.4, -2.5, "####", "..#.", ".#..", "####");
        }

        void DrawBang()
        {
            if (tick % 10 < 2) return;
            Brush b = IsDark ? MonoBright : Amber;
            Dot(air, b, 5.0, -2.3, 1, 2.2);
            Dot(air, b, 5.0, 0.3, 1, 0.85);
        }

        void DrawQuestion()
        {
            if (tick % 16 < 2) return;
            Brush b = IsDark ? MonoBright : GlassLit;
            Shape(air, b, 4.0, -2.3,
                ".##.",
                "...#",
                "..#.");
            Dot(air, b, 6.0, 0.5);
        }

        static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
