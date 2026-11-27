using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;

namespace AgyPet
{
    // Avalonia port of Mascot.cs sharing 100% of MascotCore pixel art, moods, leg-hop dance,
    // working/idle multi-press wince, cursor-following Idle gaze, and dynamic 2..8 session orbs.
    public class MascotView : Canvas
    {
        public const string ClassicSkin = MascotCore.ClassicSkin;
        public const string DarkSkin = MascotCore.DarkSkin;

        static readonly IBrush EyeDark = PetTheme.FrozenRgb(0x0B, 0x12, 0x20);
        static readonly IBrush EyeWhite = PetTheme.FrozenRgb(0xF8, 0xFA, 0xFC);
        static readonly IBrush EyeSoftSilver = PetTheme.FrozenRgb(0xCB, 0xD5, 0xE1);
        static readonly IBrush Cloud = PetTheme.FrozenRgb(0xE2, 0xE8, 0xF0);
        static readonly IBrush CloudDim = PetTheme.FrozenRgb(0x64, 0x74, 0x8B);
        static readonly IBrush Steel = PetTheme.FrozenRgb(0x94, 0xA3, 0xB8);
        static readonly IBrush SteelDark = PetTheme.FrozenRgb(0x3F, 0x4A, 0x5C);
        static readonly IBrush DarkCase = PetTheme.FrozenRgb(0x27, 0x27, 0x2A);
        static readonly IBrush DarkScreen = PetTheme.FrozenRgb(0x18, 0x18, 0x1B);
        static readonly IBrush ScreenGlow = PetTheme.FrozenRgb(0xE2, 0xE8, 0xF0);
        static readonly IBrush GlassLit = PetTheme.FrozenRgb(0x38, 0xBD, 0xF8);
        static readonly IBrush Spark = PetTheme.FrozenRgb(0xFF, 0xD2, 0x4A);
        static readonly IBrush SparkHot = PetTheme.FrozenRgb(0xFF, 0xF6, 0xD6);
        static readonly IBrush Amber = PetTheme.FrozenRgb(0xF5, 0x9E, 0x0B);
        static readonly IBrush Blue = PetTheme.FrozenRgb(0x38, 0xBD, 0xF8);
        static readonly IBrush Paper = PetTheme.FrozenRgb(0x0F, 0x17, 0x2A);

        static readonly IBrush AgyBlueBrush = PetTheme.FrozenRgb(MascotCore.AgyBlueCore.R, MascotCore.AgyBlueCore.G, MascotCore.AgyBlueCore.B);
        static readonly IBrush AgyTealBrush = PetTheme.FrozenRgb(MascotCore.AgyTeal.R, MascotCore.AgyTeal.G, MascotCore.AgyTeal.B);
        static readonly IBrush AgyGoldBrush = PetTheme.FrozenRgb(MascotCore.AgyGold.R, MascotCore.AgyGold.G, MascotCore.AgyGold.B);
        static readonly IBrush AgyCoralBrush = PetTheme.FrozenRgb(MascotCore.AgyCoral.R, MascotCore.AgyCoral.G, MascotCore.AgyCoral.B);
        static readonly IBrush AgyEmeraldBrush = PetTheme.FrozenRgb(MascotCore.AgyLime.R, MascotCore.AgyLime.G, MascotCore.AgyLime.B);
        static readonly IBrush AgyVioletBrush = PetTheme.FrozenRgb(MascotCore.AgyViolet.R, MascotCore.AgyViolet.G, MascotCore.AgyViolet.B);
        static readonly IBrush AgyOrbitDim = PetTheme.FrozenRgb(0x94, 0xA3, 0xB8);
        static readonly IBrush AgyShadowBrush = PetTheme.FrozenArgb(0x88, 0x07, 0x07, 0x09);
        static readonly IBrush ErrorRedBrush = PetTheme.FrozenRgb(0xEF, 0x44, 0x44);
        static readonly IBrush MonoBright = PetTheme.FrozenRgb(0xF4, 0xF4, 0xF5);
        static readonly IBrush MonoMid = PetTheme.FrozenRgb(0xA1, 0xA1, 0xAA);
        static readonly IBrush MonoDim = PetTheme.FrozenRgb(0x52, 0x52, 0x5B);

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
        int hurtTicks, pressTicks, danceTicks, clickStreak;
        DateTime lastClickUtc = DateTime.MinValue;
        MascotMood lastClickMood = MascotMood.Idle;
        double lookX, lookY, lookTargetX, lookTargetY;
        int activeSessionCount = 3;
        bool showHeadphones;
        bool isPlayingMusic;

        public MascotMood Mood { get; set; }
        public int ActiveSessionCount
        {
            get { return activeSessionCount; }
            set { activeSessionCount = value; }
        }
        public bool ShowHeadphones
        {
            get { return showHeadphones; }
            set { showHeadphones = value; }
        }
        public bool IsPlayingMusic
        {
            get { return isPlayingMusic; }
            set { isPlayingMusic = value; }
        }
        public string SkinName { get; private set; }
        public bool IsHurt { get { return hurtTicks > 0; } }
        public bool IsPressed { get { return pressTicks > 0; } }
        public bool IsDancing { get { return danceTicks > 0; } }
        internal Canvas GearLayer { get { return gear; } }
        internal Canvas AirLayer { get { return air; } }
        bool IsDark { get { return SkinName == DarkSkin; } }

        public MascotView(double pixelSize, string skin = ClassicSkin)
        {
            px = pixelSize;
            SkinName = MascotCore.NormalizeSkin(skin);
            ClipToBounds = false;
            sprite.RenderTransformOrigin = new RelativePoint(0.5, 0.85, RelativeUnit.Relative);
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
            AttachedToVisualTree += (s, e) => { Step(); timer.Start(); };
            DetachedFromVisualTree += (s, e) => timer.Stop();
            Step();
        }

        public void SetSkin(string skin)
        {
            string next = MascotCore.NormalizeSkin(skin);
            if (next == SkinName) return;
            SkinName = next;
            BuildSprite();
            Step();
        }

        public void Poke()
        {
            if (Mood == MascotMood.Sleeping) return;
            pressTicks = 0;
            danceTicks = 0;
            hurtTicks = 6;
            Step(false);
        }

        public void Press()
        {
            if (Mood == MascotMood.Sleeping) return;
            hurtTicks = 0;
            pressTicks = 4;
            Step(false);
        }

        public void Dance()
        {
            if (Mood != MascotMood.Idle || hurtTicks > 0) return;
            pressTicks = 0;
            danceTicks = 24;
            Step(false);
        }

        public void CancelHurt()
        {
            hurtTicks = 0;
            pressTicks = 0;
            recoilTransform.ScaleX = 1.0;
            recoilTransform.ScaleY = 1.0;
        }

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

        public void SetLookOffset(double nx, double ny)
        {
            lookTargetX = Math.Max(-1.0, Math.Min(1.0, nx));
            lookTargetY = Math.Max(-1.0, Math.Min(1.0, ny));
        }

        public void Snap(int atTick)
        {
            tick = atTick;
            blinkLeft = 0;
            glance = 0;
            Step();
        }

        public void RefreshVisuals()
        {
            Step(false);
        }

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

            RenderPixelMask(archNormalLayer, MascotCore.AgyArchMask, false);
            RenderPixelMask(archDanceLeftLayer, MascotCore.AgyDanceLeftMask, false);
            RenderPixelMask(archDanceRightLayer, MascotCore.AgyDanceRightMask, false);
            RenderPixelMask(archCompactLayer, MascotCore.AgyCompactMask, true);
            archDanceLeftLayer.IsVisible = false;
            archDanceRightLayer.IsVisible = false;
            archCompactLayer.IsVisible = false;

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
                    RgbColor col = IsDark
                        ? MascotCore.DarkArchPixel(u, v, compact, !hasTop, !hasLeft || !hasRight, !hasBottom)
                        : MascotCore.OfficialArchPixel(u, v, compact, !hasTop, !hasBottom && r >= 8 && r <= 12 && c >= 6 && c <= 13);

                    double bleedX = hasRight ? 0.03 : 0.0;
                    double bleedY = hasBottom ? 0.03 : 0.0;
                    Dot(layer, PetTheme.FrozenRgb(col.R, col.G, col.B), x, y, 0.5 + bleedX, 0.5 + bleedY);
                }
            }
        }

        void Dot(Canvas layer, IBrush fill, double x, double y, double w = 1, double h = 1)
        {
            var r = new Rectangle { Fill = fill, Width = w * px, Height = h * px };
            SetLeft(r, x * px);
            SetTop(r, y * px);
            layer.Children.Add(r);
        }

        void Shape(Canvas layer, IBrush fill, double x, double y, params string[] rows)
        {
            for (int r = 0; r < rows.Length; r++)
                for (int c = 0; c < rows[r].Length; c++)
                    if (rows[r][c] == '#')
                        Dot(layer, fill, x + c, y + r);
        }

        IBrush BodyBrushAt(double x, double y)
        {
            var c = MascotCore.BodyRgbAt(IsDark, x, y);
            return PetTheme.FrozenRgb(c.R, c.G, c.B);
        }

        IBrush EyePrimary { get { return IsDark ? EyeWhite : EyeDark; } }
        IBrush EyeGlint { get { return IsDark ? EyeSoftSilver : EyeWhite; } }
        IBrush ArmLeftUpper { get { return BodyBrushAt(2.0, 4.8); } }
        IBrush ArmLeftLower { get { return BodyBrushAt(2.0, 5.4); } }
        IBrush ArmRightUpper { get { return BodyBrushAt(9.0, 4.8); } }
        IBrush ArmRightLower { get { return BodyBrushAt(9.0, 5.4); } }

        public void Step(bool advanceTick = true)
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

            int hopStep = (tick / 3) % 4;
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
            archCompactLayer.IsVisible = compact;
            archDanceLeftLayer.IsVisible = leftHop;
            archDanceRightLayer.IsVisible = rightHop;
            archNormalLayer.IsVisible = !compact && !leftHop && !rightHop;

            if (mood == MascotMood.Idle || mood == MascotMood.Listening || mood == MascotMood.Speaking ||
                mood == MascotMood.Searching || mood == MascotMood.Browsing || mood == MascotMood.Background)
            {
                if (dancing) DrawDanceArms(hopStep);
                else DrawRestingArms();
            }

            double gx = Math.Max(-1.0, Math.Min(1.0, lookX + glance * 0.22));
            double gy = Math.Max(-1.0, Math.Min(1.0, lookY));

            switch (mood)
            {
                case MascotMood.Idle:
                    if (!hurt) DrawEyes(3.9, 3.2, 6.3, 3.2, closed, gx, gy);
                    if (dancing) DrawDanceNotes();
                    else DrawIdleSparkle();
                    break;
                case MascotMood.Sleeping:
                    if (!hurt) DrawShutEyes(4.0, 4.3, 6.2, 4.3);
                    DrawZzz();
                    break;
                case MascotMood.Listening:
                    if (!hurt) DrawEyes(3.9, 3.1, 6.3, 3.1, false, lookX * 0.65, lookY * 0.55);
                    DrawWaves();
                    break;
                case MascotMood.Thinking:
                    if (!hurt) DrawThinkingEyes(Math.Sin(tick * 0.08) * 0.78, -0.72);
                    DrawThinkingArms();
                    DrawThoughtCloud();
                    break;
                case MascotMood.Speaking:
                    if (!hurt) DrawSpeakingFace(closed, Math.Sin(tick * 0.14) * 0.5, -0.18 * Math.Abs(Math.Sin(tick * 0.09)));
                    DrawSpeechBubbles();
                    break;
                case MascotMood.Typing:
                    if (!hurt) DrawEyes(3.9, 3.3, 6.3, 3.3, closed, Math.Sin(tick * 0.15) * 0.55, 0.35);
                    DrawTypingScene();
                    break;
                case MascotMood.Searching:
                    if (!hurt) DrawEyes(3.9, 3.2, 6.3, 3.2, false, Math.Sin(tick * 0.14) * 0.9, -0.35);
                    DrawMagnifier();
                    break;
                case MascotMood.Reading:
                    double readSweep = -0.85 + ((tick % 16) / 15.0) * 1.7;
                    if (!hurt) DrawEyes(3.9, 3.1, 6.3, 3.1, false, readSweep, 0.65, shortEyes: true);
                    DrawBook();
                    break;
                case MascotMood.Working:
                    if (!hurt) DrawFocusedWorkingFace(-0.72 + Math.Sin(tick * 0.22) * 0.28, Math.Sin(tick * 0.11) * 0.45);
                    DrawLaptop(typing);
                    break;
                case MascotMood.Terminal:
                    if (!hurt) DrawFocusedWorkingFace(-0.72 + Math.Sin(tick * 0.22) * 0.28, Math.Sin(tick * 0.11) * 0.45);
                    DrawTerminal();
                    break;
                case MascotMood.Browsing:
                    if (!hurt) DrawEyes(3.9, 3.1, 6.3, 3.1, false, 0.25 + Math.Sin(tick * 0.16) * 0.65, -0.3 + Math.Cos(tick * 0.22) * 0.3);
                    DrawGlobe();
                    break;
                case MascotMood.Delegating:
                    if (!hurt) DrawEyes(3.9, 3.1, 6.3, 3.1, false, 0.75 + Math.Sin(tick * 0.2) * 0.25, Math.Sin(tick * 0.15) * 0.25);
                    DrawDelegatingDispatch();
                    break;
                case MascotMood.Multitasking:
                    if (!hurt) DrawEyes(3.9, 3.1, 6.3, 3.1, false, Math.Sin(tick * 0.32), -0.75 + Math.Cos(tick * 0.32) * 0.25);
                    DrawMultitaskingJuggle();
                    break;
                case MascotMood.Background:
                    if (!hurt) DrawHappyEyes();
                    DrawBackgroundServer();
                    break;
                case MascotMood.Alert:
                    if (!hurt) DrawStartledEyes(Math.Sin(tick * 0.65), Math.Cos(tick * 0.45) * 0.65);
                    DrawArmsUp();
                    DrawExclamation();
                    break;
                case MascotMood.Question:
                    if (!hurt) DrawConfusedEyes(Math.Sin(tick * 0.12) * 0.7, -0.45 + Math.Cos(tick * 0.12) * 0.35);
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

            if (showHeadphones && isPlayingMusic)
            {
                DrawHeadphones(compact);
                if (!(mood == MascotMood.Idle && dancing))
                    DrawMusicNotes();
            }

            if (hurt)
                DrawHurtOverlay(compact);
        }

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

        static double ClampUnit(double v) { return Math.Max(-1.0, Math.Min(1.0, v)); }

        void DrawPupilDot(double ex, double ey, double ew, double eh, double gx, double gy, double pSize, double padX, double padY, IBrush pupilBrush)
        {
            double tx = Math.Max(0.0, (ew - pSize) * 0.5 - padX);
            double ty = Math.Max(0.0, (eh - pSize) * 0.5 - padY);
            double pxOff = (ew - pSize) * 0.5 + ClampUnit(gx) * tx;
            double pyOff = (eh - pSize) * 0.5 + ClampUnit(gy) * ty;
            Dot(gear, pupilBrush, ex + pxOff, ey + pyOff, pSize, pSize);
        }

        void DrawEyes(double lx, double ly, double rx, double ry, bool closed, double gx = 0, double gy = 0, bool shortEyes = false)
        {
            if (closed) { DrawShutEyes(lx, ly + 0.4, rx, ry + 0.4); return; }
            const double ew = 0.90;
            double eh = shortEyes ? 0.55 : 0.92;
            double pSize = shortEyes ? 0.28 : 0.36;
            double padY = shortEyes ? 0.05 : 0.06;
            IBrush pupilBrush = IsDark ? EyeDark : EyeGlint;

            Dot(gear, EyePrimary, lx, ly, ew, eh);
            Dot(gear, EyePrimary, rx, ry, ew, eh);
            DrawPupilDot(lx, ly, ew, eh, gx, gy, pSize, 0.06, padY, pupilBrush);
            DrawPupilDot(rx, ry, ew, eh, gx, gy, pSize, 0.06, padY, pupilBrush);
        }

        void DrawHurtOverlay(bool compact)
        {
            double ey = compact ? 3.85 : 3.05;
            Dot(gear, EyePrimary, 3.65, ey, 0.4, 0.35);
            Dot(gear, EyePrimary, 4.05, ey + 0.32, 0.45, 0.35);
            Dot(gear, EyePrimary, 3.65, ey + 0.64, 0.4, 0.35);
            Dot(gear, EyePrimary, 6.75, ey, 0.4, 0.35);
            Dot(gear, EyePrimary, 6.3, ey + 0.32, 0.45, 0.35);
            Dot(gear, EyePrimary, 6.75, ey + 0.64, 0.4, 0.35);
        }

        void DrawShutEyes(double lx, double ly, double rx, double ry)
        {
            Dot(gear, EyePrimary, lx, ly, 0.9, 0.35);
            Dot(gear, EyePrimary, rx, ry, 0.9, 0.35);
        }

        void DrawFocusedWorkingFace(double gx, double gy)
        {
            DrawEyes(3.6, 3.4, 6.0, 3.4, false, gx, gy, shortEyes: true);
        }

        void DrawHappyEyes()
        {
            Dot(gear, EyePrimary, 3.7, 3.4, 0.4, 0.4);
            Dot(gear, EyePrimary, 4.1, 3.0, 0.5, 0.4);
            Dot(gear, EyePrimary, 4.6, 3.4, 0.4, 0.4);
            Dot(gear, EyePrimary, 6.0, 3.4, 0.4, 0.4);
            Dot(gear, EyePrimary, 6.4, 3.0, 0.5, 0.4);
            Dot(gear, EyePrimary, 6.9, 3.4, 0.4, 0.4);
        }

        void DrawSpeakingFace(bool closed, double gx, double gy)
        {
            DrawEyes(3.9, 3.1, 6.3, 3.1, closed, gx, gy);
            bool wide = tick % 4 < 2;
            Dot(gear, EyePrimary, wide ? 5.12 : 5.19, wide ? 4.24 : 4.34, wide ? 0.86 : 0.72, wide ? 0.42 : 0.24);
        }

        void DrawErrorGlitchFace()
        {
            Dot(gear, EyeWhite, 3.55, 2.95, 0.45, 0.35);
            Dot(gear, EyeWhite, 4.0, 3.3, 0.45, 0.35);
            Dot(gear, EyeWhite, 3.55, 3.65, 0.45, 0.35);
            Dot(gear, EyeWhite, 6.85, 2.95, 0.45, 0.35);
            Dot(gear, EyeWhite, 6.4, 3.3, 0.45, 0.35);
            Dot(gear, EyeWhite, 6.85, 3.65, 0.45, 0.35);
        }

        void DrawThinkingEyes(double gx, double gy)
        {
            Dot(gear, EyePrimary, 3.9, 2.75, 0.88, 0.60);
            Dot(gear, EyePrimary, 6.3, 2.75, 0.88, 0.84);
            IBrush pupilBrush = IsDark ? EyeDark : EyeWhite;
            DrawPupilDot(3.9, 2.75, 0.88, 0.60, gx, gy, 0.30, 0.06, 0.05, pupilBrush);
            DrawPupilDot(6.3, 2.75, 0.88, 0.84, gx, gy, 0.34, 0.06, 0.06, pupilBrush);
        }

        void DrawThinkingArms()
        {
            Dot(gear, ArmLeftUpper, 1.5, 4.8, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.1, 5.4, 0.5, 0.5);
            Dot(gear, BodyBrushAt(8.5, 4.6), 8.6, 4.6, 0.55, 0.85);
            Dot(gear, BodyBrushAt(8.5, 4.2), 7.6, 4.2, 1.05, 0.5);
        }

        void DrawStartledEyes(double gx, double gy)
        {
            Dot(gear, EyeWhite, 3.6, 2.9, 1.25, 1.25);
            Dot(gear, EyeWhite, 6.1, 2.9, 1.25, 1.25);
            DrawPupilDot(3.6, 2.9, 1.25, 1.25, gx, gy, 0.55, 0.08, 0.08, EyeDark);
            DrawPupilDot(6.1, 2.9, 1.25, 1.25, gx, gy, 0.55, 0.08, 0.08, EyeDark);
        }

        void DrawConfusedEyes(double gx, double gy)
        {
            Dot(gear, EyePrimary, 3.7, 3.2, 0.88, 0.88);
            DrawPupilDot(3.7, 3.2, 0.88, 0.88, gx, gy, 0.34, 0.06, 0.06, IsDark ? EyeDark : EyeWhite);
            Dot(gear, EyeWhite, 6.0, 2.8, 1.4, 1.4);
            DrawPupilDot(6.0, 2.8, 1.4, 1.4, gx, gy, 0.55, 0.10, 0.10, EyeDark);
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

        void DrawIdleSparkle()
        {
            int phase = tick % 24;
            if (phase >= 18) return;
            double sx = 8.9, sy = 1.3;
            IBrush core = IsDark ? MonoBright : (phase < 8 ? SparkHot : EyeWhite);
            IBrush rays = IsDark ? MonoMid : Cloud;
            Dot(air, core, sx, sy, 0.5, 0.5);
            if (phase < 12)
            {
                Dot(air, rays, sx - 0.5, sy, 0.5, 0.5);
                Dot(air, rays, sx + 0.5, sy, 0.5, 0.5);
                Dot(air, rays, sx, sy - 0.5, 0.5, 0.5);
                Dot(air, rays, sx, sy + 0.5, 0.5, 0.5);
            }
        }

        void DrawThoughtCloud()
        {
            int pulse = tick / 3 % 3;
            IBrush cloudBg = IsDark ? DarkCase : Paper;
            IBrush cloudRim = IsDark ? MonoMid : AgyBlueBrush;
            Dot(air, cloudRim, 8.4, 0.6, 0.5, 0.5);
            Dot(air, cloudRim, 9.1, -0.2, 0.75, 0.75);
            Dot(air, cloudRim, 8.7, -2.6, 4.8, 2.2);
            Dot(air, cloudBg, 9.1, -2.25, 4.0, 1.5);

            IBrush[] sparks = IsDark
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
            IBrush c1 = IsDark ? MonoBright : AgyCoralBrush;
            IBrush c2 = IsDark ? MonoMid : AgyEmeraldBrush;
            IBrush c3 = IsDark ? MonoBright : AgyGoldBrush;
            IBrush c4 = IsDark ? MonoMid : GlassLit;
            IBrush c5 = IsDark ? MonoDim : AgyBlueBrush;

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
            double mx = -4.2;
            Dot(air, IsDark ? MonoMid : Steel, mx, 1.8, 5.0, 6.0);
            Dot(air, IsDark ? DarkScreen : ScreenGlow, mx + 0.8, 2.5, 3.4, 4.4);
            Dot(air, IsDark ? DarkCase : SteelDark, mx, 7.8, 6.2, 0.9);

            Dot(air, IsDark ? MonoBright : AgyBlueBrush, mx + 1.1, 3.0, 2.4, 0.45);
            Dot(air, IsDark ? MonoMid : AgyCoralBrush, mx + 1.5, 4.0, 2.0, 0.45);
            Dot(air, IsDark ? MonoBright : AgyEmeraldBrush, mx + 1.1, 5.0, 1.2 + (tick % 3) * 0.55, 0.45);
            Dot(gear, BodyBrushAt(2.0, 5.0), 0.6, 5.0, 1.6, 0.85);
        }

        void DrawTerminal()
        {
            double tx = -4.3;
            Dot(air, IsDark ? MonoMid : SteelDark, tx, 1.7, 5.2, 6.2);
            Dot(air, DarkScreen, tx + 0.45, 2.2, 4.3, 5.2);

            Dot(air, IsDark ? MonoBright : AgyCoralBrush, tx + 0.75, 2.45, 0.45, 0.45);
            Dot(air, IsDark ? MonoMid : AgyGoldBrush, tx + 1.4, 2.45, 0.45, 0.45);
            Dot(air, IsDark ? MonoDim : AgyEmeraldBrush, tx + 2.05, 2.45, 0.45, 0.45);

            IBrush prompt = IsDark ? MonoBright : AgyEmeraldBrush;
            Dot(air, prompt, tx + 0.85, 3.5, 0.4, 0.4);
            Dot(air, prompt, tx + 1.25, 3.85, 0.4, 0.4);
            Dot(air, prompt, tx + 0.85, 4.2, 0.4, 0.4);

            double cmdLen = 1.2 + (tick / 4 % 3) * 0.55;
            Dot(air, IsDark ? MonoMid : ScreenGlow, tx + 1.9, 3.7, cmdLen, 0.45);
            Dot(air, IsDark ? MonoDim : AgyTealBrush, tx + 0.9, 5.0, 2.5, 0.42);
            Dot(air, IsDark ? MonoMid : ScreenGlow, tx + 0.9, 5.9, 2.1, 0.42);

            if (tick % 6 < 3)
                Dot(air, prompt, tx + 3.5, 5.9, 0.6, 0.6);

            Dot(gear, BodyBrushAt(2.0, 5.0), 0.6, 5.0, 1.6, 0.85);
        }

        void DrawBook()
        {
            Dot(gear, DarkScreen, 0.5, 4.3, 10.0, 3.0);
            Dot(gear, IsDark ? MonoMid : AgyBlueBrush, 5.25, 4.3, 0.5, 3.0);
            IBrush pageA = IsDark ? MonoBright : AgyGoldBrush;
            IBrush pageB = IsDark ? MonoMid : AgyEmeraldBrush;
            Dot(gear, pageA, 1.5, 5.0, 3.0, 0.4);
            Dot(gear, pageB, 6.5, 5.0, 3.0, 0.4);
            Dot(gear, pageA, 1.5, 6.0, 3.0, 0.4);
            Dot(gear, pageB, 6.5, 6.0, 3.0, 0.4);
        }

        void DrawMagnifier()
        {
            double shift = Math.Round(Math.Sin(tick * 0.14) * 2.8);
            double gx = 3.5 + shift, gy = -2.4;
            Shape(air, IsDark ? MonoBright : Blue, gx, gy, ".###.", "#...#", "#...#", ".###.");
            Dot(air, IsDark ? MonoMid : AgyBlueBrush, gx + 4, gy + 3.8, 0.9, 0.9);
            Dot(air, IsDark ? MonoDim : AgyTealBrush, gx + 4.7, gy + 4.5, 0.8, 0.8);
        }

        void DrawGlobe()
        {
            double gx = 8.6, gy = -1.0;
            Shape(air, IsDark ? MonoBright : Blue, gx, gy, ".###.", "#...#", "#...#", ".###.");
            Dot(air, IsDark ? MonoMid : AgyBlueBrush, gx + 1, gy + 1.5, 3.0, 0.45);
            double bandW = 1.2 + Math.Round(Math.Sin(tick * 0.25) * 0.9);
            Dot(air, IsDark ? MonoBright : AgyGoldBrush, gx + bandW, gy + 0.5, 0.65, 3.0);
        }

        void DrawWaves()
        {
            int ring = tick / 3 % 3;
            for (int i = 0; i <= ring; i++)
            {
                double h = 3.6 - i * 0.85, top = 4.5 - h / 2;
                IBrush col = i == ring ? (IsDark ? MonoBright : Blue) : (IsDark ? MonoMid : AgyBlueBrush);
                Dot(air, col, -0.4 - i * 0.95, top, 0.75, h);
                Dot(air, col, 10.6 + i * 0.95, top, 0.75, h);
            }
        }

        void DrawSpeechBubbles()
        {
            int p = tick / 3 % 4;
            if (p >= 1) Dot(air, IsDark ? MonoBright : EyeWhite, 10.3, 3.8, 0.9, 0.9);
            if (p >= 2) Dot(air, IsDark ? MonoMid : Blue, 11.4, 2.9, 0.9, 0.9);
            if (p >= 3) Dot(air, IsDark ? MonoDim : AgyVioletBrush, 12.5, 2.0, 0.9, 0.9);
        }

        void DrawTypingScene()
        {
            double ky = 6.2;
            Dot(gear, IsDark ? DarkCase : SteelDark, 1.4, ky, 8.2, 1.4);
            int activeKey = tick / 2 % 4;
            for (int i = 0; i < 4; i++)
            {
                bool pressed = i == activeKey;
                IBrush keyCol = pressed ? (IsDark ? MonoBright : Blue) : (IsDark ? MonoMid : Cloud);
                Dot(gear, keyCol, 2.1 + i * 1.8, ky + (pressed ? 0.45 : 0.25), 1.1, 0.55);
            }
            bool leftDown = tick % 4 < 2;
            double leftY = leftDown ? 5.3 : 4.8;
            double rightY = leftDown ? 4.8 : 5.3;
            Dot(gear, BodyBrushAt(2.0, leftY), 1.6, leftY, 0.65, 0.9);
            Dot(gear, BodyBrushAt(9.0, rightY), 8.7, rightY, 0.65, 0.9);

            Dot(air, IsDark ? DarkCase : Paper, 8.7, -0.9, 4.2, 2.0);
            for (int d = 0; d < 3; d++)
            {
                bool up = (tick / 3 + d) % 3 == 0;
                IBrush dc = d == 0 ? (IsDark ? MonoBright : AgyGoldBrush) : (d == 1 ? (IsDark ? MonoMid : AgyEmeraldBrush) : (IsDark ? MonoBright : Blue));
                Dot(air, dc, 9.35 + d * 1.15, up ? -0.45 : -0.18, 0.62, 0.62);
            }
        }

        void DrawDelegatingDispatch()
        {
            Dot(gear, ArmLeftUpper, 1.5, 4.8, 0.5, 1.0);
            Dot(gear, ArmLeftLower, 1.1, 5.4, 0.5, 0.5);
            Dot(gear, BodyBrushAt(9.0, 4.2), 8.7, 4.2, 1.1, 0.65);

            IBrush cardFrame = IsDark ? DarkCase : Paper;
            IBrush cardSlot = IsDark ? MonoMid : SteelDark;
            for (int row = 0; row < 2; row++)
            {
                double ny = row == 0 ? 0.1 : 3.4;
                Dot(air, cardSlot, 9.5, ny, 4.1, 2.6);
                Dot(air, cardFrame, 9.85, ny + 0.35, 3.4, 1.9);
                bool active = (tick / 4) % 2 == row;
                IBrush badge = row == 0 ? (IsDark ? MonoBright : AgyGoldBrush) : (IsDark ? MonoMid : AgyVioletBrush);
                Dot(air, badge, 10.2, ny + 0.7, 0.65, 0.65);
                Dot(air, active ? (IsDark ? MonoBright : GlassLit) : (IsDark ? MonoDim : CloudDim), 11.1, ny + 0.8, 1.5, 0.45);
                Dot(air, IsDark ? MonoDim : AgyEmeraldBrush, 10.2, ny + 1.55, 2.1, 0.38);
            }

            double frac = (tick % 6) / 5.0;
            bool upper = (tick / 6) % 2 == 0;
            Dot(air, IsDark ? MonoBright : AgyGoldBrush, 8.9 + frac * 0.8, 4.2 + (upper ? -2.6 : -0.1) * frac, 0.65, 0.65);
        }

        void DrawMultitaskingJuggle()
        {
            bool phase = tick % 6 < 3;
            double ly = phase ? 3.5 : 4.2;
            double ry = phase ? 4.2 : 3.5;
            Dot(gear, BodyBrushAt(2.0, ly), 1.0, ly, 0.55, 1.0);
            Dot(gear, BodyBrushAt(2.0, 4.4), 1.5, 4.4, 0.5, 0.6);
            Dot(gear, BodyBrushAt(9.0, ry), 9.45, ry, 0.55, 1.0);
            Dot(gear, BodyBrushAt(9.0, 4.4), 8.95, 4.4, 0.5, 0.6);

            IBrush[] orbPalette = IsDark
                ? new[] { MonoBright, MonoMid }
                : new[] { AgyGoldBrush, AgyEmeraldBrush, Blue, AgyVioletBrush, AgyCoralBrush, AgyBlueBrush, AgyTealBrush, EyeWhite };

            int orbCount = MascotCore.ResolveOrbCount(activeSessionCount);
            double orbSize = orbCount >= 6 ? 0.9 : 1.05;
            for (int i = 0; i < orbCount; i++)
            {
                double a = tick * 0.24 + i * (Math.PI * 2.0 / orbCount);
                double ox = 5.1 + Math.Cos(a) * 3.9;
                double oy = -0.4 + Math.Sin(a) * 1.55;
                Dot(air, orbPalette[i % orbPalette.Length], ox, oy, orbSize, orbSize);
                double pa = a - 0.36;
                Dot(air, IsDark ? MonoDim : AgyOrbitDim, 5.2 + Math.Cos(pa) * 3.9, -0.32 + Math.Sin(pa) * 1.55, 0.45, 0.45);
            }
        }

        void DrawBackgroundServer()
        {
            bool beat = tick % 10 < 5;
            double rx = 9.0;
            Dot(air, IsDark ? DarkCase : Paper, rx, 0.6, 3.8, 4.6);
            Dot(air, IsDark ? MonoDim : SteelDark, rx + 0.35, 1.0, 3.1, 1.5);
            Dot(air, IsDark ? MonoDim : SteelDark, rx + 0.35, 3.1, 3.1, 1.5);

            IBrush led1 = beat ? (IsDark ? MonoBright : AgyEmeraldBrush) : (IsDark ? MonoMid : AgyTealBrush);
            IBrush led2 = !beat ? (IsDark ? MonoBright : GlassLit) : (IsDark ? MonoMid : AgyBlueBrush);
            Dot(air, led1, rx + 0.8, 1.45, 0.65, 0.65);
            Dot(air, led2, rx + 0.8, 3.55, 0.65, 0.65);
            Dot(air, IsDark ? MonoBright : Cloud, rx + 1.9, 1.6, 1.2, 0.35);
            Dot(air, IsDark ? MonoMid : Cloud, rx + 1.9, 3.7, 1.2, 0.35);
        }

        void DrawZzz()
        {
            int p = tick / 10 % 4;
            if (p >= 1) Shape(air, IsDark ? MonoDim : CloudDim, 8.6, -1.1, "###", "..#", ".#.", "###");
            if (p >= 2) Shape(air, IsDark ? MonoBright : Blue, 11.4, -2.5, "###", "..#", ".#.", "###");
        }

        void DrawExclamation()
        {
            if (tick % 10 < 2) return;
            Dot(air, IsDark ? MonoBright : Amber, 5.0, -2.3, 1, 2.2);
            Dot(air, IsDark ? MonoBright : Amber, 5.0, 0.3, 1, 0.8);
        }

        void DrawQuestion()
        {
            if (tick % 16 < 2) return;
            Shape(air, IsDark ? MonoBright : Blue, 4.0, -2.3, ".##.", "...#", "..#.");
            Dot(air, IsDark ? MonoBright : Blue, 6.0, 0.5, 0.9, 0.9);
        }

        void DrawErrorLightning()
        {
            if (tick % 8 < 2) return;
            Shape(air, IsDark ? MonoBright : ErrorRedBrush, 4.3, -2.7, "..##", ".##.", "###.", ".##.", ".#..");
            Dot(air, IsDark ? MonoMid : Spark, 5.2, -1.1, 0.45, 0.45);
        }

        void DrawDanceNotes()
        {
            int n = tick / 3 % 4;
            Dot(air, IsDark ? MonoBright : AgyGoldBrush, -0.2, 0.6 - n * 0.3, 0.48, 1.2);
            Dot(air, IsDark ? MonoBright : AgyGoldBrush, -0.65, 1.5 - n * 0.3, 0.65, 0.48);
            Dot(air, IsDark ? MonoMid : Blue, 10.2, 0.2 - (n + 2) % 4 * 0.3, 0.48, 1.2);
            Dot(air, IsDark ? MonoMid : Blue, 9.75, 1.1 - (n + 2) % 4 * 0.3, 0.65, 0.48);
        }

        void DrawHeadphones(bool compact)
        {
            double yBase = compact ? 1.4 : -0.45;
            IBrush frameCol = IsDark ? MonoMid : Steel;
            IBrush cushionCol = IsDark ? DarkScreen : Paper;
            IBrush accentCol = IsDark ? MonoBright : GlassLit;

            var mask = MascotCore.HeadphonesMask;
            for (int r = 0; r < mask.Length; r++)
            {
                string row = mask[r];
                for (int c = 0; c < row.Length; c++)
                {
                    char ch = row[c];
                    if (ch == '.') continue;
                    IBrush fill = ch == 'A' ? accentCol : (ch == 'C' ? cushionCol : frameCol);
                    bool hasRight = c + 1 < row.Length && row[c + 1] == ch;
                    bool hasBottom = r + 1 < mask.Length && mask[r + 1][c] == ch;
                    Dot(gear, fill, c * 0.5, yBase + r * 0.5, 0.5 + (hasRight ? 0.03 : 0.0), 0.5 + (hasBottom ? 0.03 : 0.0));
                }
            }
        }

        void DrawMusicNotes()
        {
            int t1 = tick / 3 % 6;
            int t2 = (tick / 3 + 3) % 6;
            double lift1 = (t1 <= 3 ? t1 : 6 - t1) * 0.28;
            double lift2 = (t2 <= 3 ? t2 : 6 - t2) * 0.28;

            IBrush c1 = IsDark ? MonoBright : GlassLit;
            IBrush c2 = IsDark ? MonoMid : AgyGoldBrush;

            double lx = 0.2;
            double ly = -0.3 - lift1;
            Dot(air, c1, lx, ly, 1.75, 0.38);
            Dot(air, c1, lx, ly + 0.38, 0.38, 0.95);
            Dot(air, c1, lx + 1.37, ly + 0.38, 0.38, 0.95);
            Dot(air, c1, lx - 0.32, ly + 1.1, 0.72, 0.46);
            Dot(air, c1, lx + 1.05, ly + 1.1, 0.72, 0.46);

            double rx = 9.5;
            double ry = -0.5 - lift2;
            Dot(air, c2, rx, ry, 0.38, 1.25);
            Dot(air, c2, rx + 0.38, ry, 0.55, 0.4);
            Dot(air, c2, rx - 0.32, ry + 1.0, 0.72, 0.46);
        }
    }
}
