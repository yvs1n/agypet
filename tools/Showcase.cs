using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AgyPet.Tools
{
    static class Showcase
    {
        [STAThread]
        static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : "docs";
            Directory.CreateDirectory(outDir);

            RenderSheet(Path.Combine(outDir, "showcase-classic.png"), Mascot.ClassicSkin,
                "AgyPet · Classic Antigravity Skin",
                "Refined CLI pixel arch in official Antigravity logo colors — all 19 live reactions + sentiment mood", 0);

            RenderSheet(Path.Combine(outDir, "showcase-dark.png"), Mascot.DarkSkin,
                "AgyPet · Dark Remake Skin",
                "Monochrome obsidian & silver remake with luminous eyes — all 19 live reactions + sentiment mood", 0);

            string siteDir = args.Length > 1 ? args[1] : Path.GetFullPath(Path.Combine(outDir, "..", "site"));
            Directory.CreateDirectory(siteDir);
            RenderOgScreen(Path.Combine(siteDir, "og-screen.png"), 0);
            RenderRedditCover(Path.Combine(outDir, "reddit-cover.png"));
            RenderIconPng(Path.Combine(siteDir, "apple-touch-icon.png"), 180);
            RenderIconPng(Path.Combine(siteDir, "favicon-32x32.png"), 32);

            string gifDir = Environment.GetEnvironmentVariable("AGYPET_GIF_FRAMES");
            if (!string.IsNullOrEmpty(gifDir))
            {
                Directory.CreateDirectory(gifDir);
                const int FrameCount = 24;
                for (int f = 0; f < FrameCount; f++)
                {
                    int tick = f * 2;
                    RenderOgScreen(Path.Combine(gifDir, string.Format("preview_{0:D2}.png", f)), tick);
                    RenderSheet(Path.Combine(gifDir, string.Format("classic_{0:D2}.png", f)), Mascot.ClassicSkin,
                        "AgyPet · Classic Antigravity Skin",
                        "Refined CLI pixel arch in official Antigravity logo colors — 19 live reactions + turn sentiment", tick);
                    RenderSheet(Path.Combine(gifDir, string.Format("dark_{0:D2}.png", f)), Mascot.DarkSkin,
                        "AgyPet · Dark Remake Skin",
                        "Monochrome obsidian & silver remake with luminous eyes — 19 live reactions + turn sentiment", tick);
                }
                Console.WriteLine("Rendered " + FrameCount + " animation frame sets to " + gifDir);
            }

            Console.WriteLine("Rendered showcases, og-screen.png, and icon PNGs to " + outDir + " & " + siteDir);
            return 0;
        }

        static void RenderIconPng(string outPath, int size)
        {
            var root = new Border
            {
                Width = size,
                Height = size,
                CornerRadius = new CornerRadius(size * 0.22),
                Background = BrushHex(0x0B, 0x0D, 0x12),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(Math.Max(1, size / 64.0)),
            };
            double px = size * 0.065;
            var mascot = new Mascot(px, Mascot.ClassicSkin) { Mood = MascotMood.Idle };
            mascot.Snap(4);
            mascot.HorizontalAlignment = HorizontalAlignment.Center;
            mascot.VerticalAlignment = VerticalAlignment.Center;
            root.Child = mascot;

            root.Measure(new Size(size, size));
            root.Arrange(new Rect(0, 0, size, size));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(outPath)) enc.Save(fs);
        }

        static void RenderOgScreen(string outPath, int frameTick)
        {
            const int W = 1200, H = 630;
            var root = new Grid { Width = W, Height = H, Background = BrushHex(0x0B, 0x0D, 0x12) };

            // Subtle ambient dot-grid & top glow
            var glow = new Border
            {
                Background = new RadialGradientBrush(Color.FromArgb(0x36, 0x25, 0x75, 0xFC), Colors.Transparent)
                {
                    Center = new Point(0.65, 0.22),
                    GradientOrigin = new Point(0.65, 0.22),
                    RadiusX = 0.6,
                    RadiusY = 0.7,
                },
            };
            root.Children.Add(glow);

            var main = new Grid { Margin = new Thickness(56, 48, 56, 44) };
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.08, GridUnitType.Star) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) });
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });

            // Left column: Brand + Headline + Tags + Credits
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(15),
                Background = new SolidColorBrush(Color.FromArgb(0x22, 0x38, 0xBD, 0xF8)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0x38, 0xBD, 0xF8)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14, 6, 14, 6),
                Margin = new Thickness(0, 0, 0, 20),
                Child = new TextBlock
                {
                    Text = "AgyPet · Windows Companion for Google Antigravity",
                    Foreground = BrushHex(0x93, 0xC5, 0xFD),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            };
            left.Children.Add(badge);

            left.Children.Add(new TextBlock
            {
                Text = "A live pixel companion\ninside Antigravity.",
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                FontSize = 46,
                FontWeight = FontWeights.Bold,
                LineHeight = 54,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            left.Children.Add(new TextBlock
            {
                Text = "19 live reactions, turn moods, voice sync, taskbar corner docking, and multi-session tracking.",
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 19,
                LineHeight = 28,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 18, 0, 28),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            var skinsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 26) };
            var cMascot = new Mascot(5.2, Mascot.ClassicSkin) { Mood = MascotMood.Multitasking };
            cMascot.Snap(6 + frameTick);
            var dMascot = new Mascot(5.2, Mascot.DarkSkin) { Mood = MascotMood.Thinking };
            dMascot.Snap(12 + frameTick);
            skinsRow.Children.Add(MakeOgChip("Classic Official", cMascot));
            skinsRow.Children.Add(new Border { Width = 14 });
            skinsRow.Children.Add(MakeOgChip("Dark Remake", dMascot));
            left.Children.Add(skinsRow);

            left.Children.Add(new TextBlock
            {
                Text = "Made by Yassin Ragab (yassinr.me) · Built with Antigravity",
                Foreground = BrushHex(0xCB, 0xD5, 0xE1),
                FontSize = 14.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            Grid.SetColumn(left, 0);
            main.Children.Add(left);

            // Right column: Simulated Antigravity Chatbox + Perching Mascot + Progress Hover Card
            var rightStage = new Grid { VerticalAlignment = VerticalAlignment.Center };

            var cardFrame = new Border
            {
                Background = BrushHex(0x13, 0x16, 0x1E),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(24),
                Height = 470,
            };

            var stageStack = new Grid();
            stageStack.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            stageStack.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Top: Hover Progress Card + Simulated Chat Thread
            var topArea = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var hover = BuildHoverCardPreviewTile(Mascot.ClassicSkin);
            hover.HorizontalAlignment = HorizontalAlignment.Left;
            hover.Margin = new Thickness(0, 0, 0, 18);
            topArea.Children.Add(hover);

            var userBubble = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = BrushHex(0x1E, 0x22, 0x2D),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 9, 14, 9),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new TextBlock
                {
                    Text = "Track my active chats, typing, and voice mode.",
                    Foreground = BrushHex(0xE2, 0xE8, 0xF0),
                    FontSize = 13.5,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            };
            topArea.Children.Add(userBubble);

            topArea.Children.Add(new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = BrushHex(0x16, 0x19, 0x22),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 9, 14, 9),
                Child = new TextBlock
                {
                    Text = "Perching above the chatbox with 19 live reactions.",
                    Foreground = BrushHex(0x94, 0xA3, 0xB8),
                    FontSize = 13.5,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            });

            Grid.SetRow(topArea, 0);
            stageStack.Children.Add(topArea);

            // Bottom: Simulated Chat Composer with Classic Mascot perched on top-right
            var composerWrap = new Grid { VerticalAlignment = VerticalAlignment.Bottom };
            var composer = new Border
            {
                Background = BrushHex(0x1B, 0x1F, 0x2A),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(18, 16, 18, 14),
                Margin = new Thickness(0, 44, 0, 0),
            };
            var compStack = new StackPanel();
            compStack.Children.Add(new TextBlock
            {
                Text = "Ask anything, @ to mention, / for workflows...",
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 15,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            compStack.Children.Add(new TextBlock
            {
                Text = "Antigravity 2.0 · Live Chatbox Perch",
                Foreground = BrushHex(0x64, 0x74, 0x8B),
                FontSize = 12,
                Margin = new Thickness(0, 18, 0, 0),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            composer.Child = compStack;
            composerWrap.Children.Add(composer);

            var perchMood = frameTick >= 24 ? MascotMood.Happy : MascotMood.Typing;
            var perchMascot = new Mascot(5.8, Mascot.ClassicSkin) { Mood = perchMood };
            perchMascot.Snap(6 + frameTick);
            perchMascot.HorizontalAlignment = HorizontalAlignment.Right;
            perchMascot.VerticalAlignment = VerticalAlignment.Top;
            perchMascot.Margin = new Thickness(0, -10, 22, 0);
            composerWrap.Children.Add(perchMascot);

            Grid.SetRow(composerWrap, 1);
            stageStack.Children.Add(composerWrap);
            cardFrame.Child = stageStack;
            rightStage.Children.Add(cardFrame);

            Grid.SetColumn(rightStage, 2);
            main.Children.Add(rightStage);
            root.Children.Add(main);

            root.Measure(new Size(W, H));
            root.Arrange(new Rect(0, 0, W, H));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(outPath)) enc.Save(fs);
        }

        static Border MakeOgChip(string label, Mascot m)
        {
            var inner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var host = new Grid { Width = 72, Height = 58, VerticalAlignment = VerticalAlignment.Center };
            m.HorizontalAlignment = HorizontalAlignment.Center;
            m.VerticalAlignment = VerticalAlignment.Center;
            host.Children.Add(m);
            inner.Children.Add(host);
            inner.Children.Add(new TextBlock
            {
                Text = label,
                Foreground = BrushHex(0xE2, 0xE8, 0xF0),
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            return new Border
            {
                Background = BrushHex(0x13, 0x16, 0x1E),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(10, 6, 14, 6),
                Child = inner,
            };
        }

        class ShowcaseItem
        {
            public MascotMood Mood;
            public TurnSentiment Sentiment;
            public string Title, Sub;
            public int BaseTick;
            public ShowcaseItem(MascotMood mood, string title, string sub, int tick, TurnSentiment sentiment = TurnSentiment.Neutral)
            {
                Mood = mood; Title = title; Sub = sub; BaseTick = tick; Sentiment = sentiment;
            }
        }

        static ShowcaseItem[] All20ShowcaseItems(int animTick)
        {
            var sent20 = ((animTick / 24) % 2 == 1) ? TurnSentiment.Sad : TurnSentiment.Angry;
            return new[]
            {
                new ShowcaseItem(MascotMood.Typing, "Typing", "Agent or you typing", 6),
                new ShowcaseItem(MascotMood.Thinking, "Thinking", "Planning steps", 12),
                new ShowcaseItem(MascotMood.Working, "Working", "Editing code", 14),
                new ShowcaseItem(MascotMood.Terminal, "Terminal", "Running commands", 10),
                new ShowcaseItem(MascotMood.Browsing, "Browsing", "Live browser steps", 8),
                new ShowcaseItem(MascotMood.Delegating, "Delegating", "Subagents & swarms", 10),
                new ShowcaseItem(MascotMood.Reading, "Reading", "Scanning files", 6),
                new ShowcaseItem(MascotMood.Searching, "Searching", "Docs & web", 11),
                new ShowcaseItem(MascotMood.Listening, "Listening", "Voice & dictation", 8),
                new ShowcaseItem(MascotMood.Speaking, "Speaking", "Voice reply aloud", 9),
                new ShowcaseItem(MascotMood.Multitasking, "Multitasking", "2+ active chats", 14),
                new ShowcaseItem(MascotMood.Background, "Background", "Live server", 10),
                new ShowcaseItem(MascotMood.Alert, "Alert", "Needs approval", 5),
                new ShowcaseItem(MascotMood.Question, "Question", "Waiting choice", 7),
                new ShowcaseItem(MascotMood.Done, "Done", "Task complete", 4),
                new ShowcaseItem(MascotMood.Happy, "Happy", "User praise!", 6),
                new ShowcaseItem(MascotMood.Error, "Error", "Step hit error", 4),
                new ShowcaseItem(MascotMood.Idle, "Idle", "Ready at rest", 4),
                new ShowcaseItem(MascotMood.Sleeping, "Sleeping", "Idle / Quota sleep", 24),
                new ShowcaseItem(MascotMood.Working, "Angry / Sad", "Reacts to tone", 8, sent20),
            };
        }

        static void RenderRedditCover(string outPath)
        {
            const int W = 1400, H = 880;
            var bg = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1),
            };
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0x0B, 0x0D, 0x12), 0.0));
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0x0F, 0x15, 0x24), 0.55));
            bg.GradientStops.Add(new GradientStop(Color.FromRgb(0x0B, 0x0D, 0x12), 1.0));

            var root = new Border { Width = W, Height = H, Background = bg, Padding = new Thickness(34, 30, 34, 28) };
            var page = new StackPanel();

            // ---- Top Row: Brand / Headline / Skins (Left) + Simulated Antigravity Perch (Right) ----
            var topGrid = new Grid { Margin = new Thickness(0, 0, 0, 24) };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(640) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            left.Children.Add(new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                BorderBrush = BrushHex(0x25, 0x75, 0xFC),
                Background = BrushHex(0x11, 0x1B, 0x33),
                Padding = new Thickness(13, 5, 13, 5),
                Margin = new Thickness(0, 0, 0, 14),
                Child = new TextBlock
                {
                    Text = "AgyPet · Windows Companion for Google Antigravity",
                    Foreground = BrushHex(0x60, 0xA5, 0xFA),
                    FontSize = 12.5,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            });

            left.Children.Add(new TextBlock
            {
                Text = "Live pixel companion\ninside Antigravity.",
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                FontSize = 40,
                FontWeight = FontWeights.Bold,
                LineHeight = 46,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            left.Children.Add(new TextBlock
            {
                Text = "Zero-dependency Windows overlay (~150 KB): 19 reactions, Angry/Sad turn moods, taskbar corner docking, voice sync, and multi-session tracking.",
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 15.5,
                LineHeight = 22,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 12, 0, 18),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            var skinsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            var classicMini = new Mascot(3.6, Mascot.ClassicSkin) { Mood = MascotMood.Done };
            classicMini.Snap(4);
            var darkMini = new Mascot(3.6, Mascot.DarkSkin) { Mood = MascotMood.Thinking };
            darkMini.Snap(12);
            skinsRow.Children.Add(MakeOgChip("Classic Official Skin", classicMini));
            var darkChip = MakeOgChip("Dark Remake Skin", darkMini);
            darkChip.Margin = new Thickness(12, 0, 0, 0);
            skinsRow.Children.Add(darkChip);
            left.Children.Add(skinsRow);

            left.Children.Add(new TextBlock
            {
                Text = "agypet.pages.dev   ·   github.com/yvs1n/agypet   ·   Open Source (MIT)",
                Foreground = BrushHex(0xCB, 0xD5, 0xE1),
                FontSize = 13.5,
                FontWeight = FontWeights.SemiBold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            Grid.SetColumn(left, 0);
            topGrid.Children.Add(left);

            // Right: Simulated Antigravity 2.0 Chat Stage
            var cardFrame = new Border
            {
                Background = BrushHex(0x13, 0x16, 0x1E),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(22),
                Height = 382,
            };

            var stageGrid = new Grid();
            stageGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            stageGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var topStage = new StackPanel { VerticalAlignment = VerticalAlignment.Top };
            var hover = BuildHoverCardPreviewTile(Mascot.ClassicSkin);
            hover.HorizontalAlignment = HorizontalAlignment.Left;
            hover.Margin = new Thickness(0, 0, 0, 12);
            topStage.Children.Add(hover);

            var userBubble = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = BrushHex(0x1E, 0x22, 0x2D),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(13, 7, 13, 7),
                Margin = new Thickness(0, 0, 0, 8),
                Child = new TextBlock
                {
                    Text = "Track my active chats, typing, and voice mode.",
                    Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                    FontSize = 12.5,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            };
            topStage.Children.Add(userBubble);

            var agentBubble = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = BrushHex(0x15, 0x1A, 0x26),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(13, 7, 13, 7),
                Child = new TextBlock
                {
                    Text = "Perched cleanly above your composer with zero side-panel clutter.",
                    Foreground = BrushHex(0xCB, 0xD5, 0xE1),
                    FontSize = 12.5,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            };
            topStage.Children.Add(agentBubble);
            Grid.SetRow(topStage, 0);
            stageGrid.Children.Add(topStage);

            var composerWrap = new Grid { VerticalAlignment = VerticalAlignment.Bottom };
            var composer = new Border
            {
                Background = BrushHex(0x1B, 0x1F, 0x2A),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x32, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 13, 16, 11),
                Margin = new Thickness(0, 34, 0, 0),
            };
            var compStack = new StackPanel();
            compStack.Children.Add(new TextBlock
            {
                Text = "Ask anything, @ to mention, / for workflows...",
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 13.5,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            compStack.Children.Add(new TextBlock
            {
                Text = "Antigravity 2.0 · Live Chatbox Perch  (Small · Medium · Large)",
                Foreground = BrushHex(0x64, 0x74, 0x8B),
                FontSize = 11,
                Margin = new Thickness(0, 12, 0, 0),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            composer.Child = compStack;
            composerWrap.Children.Add(composer);

            var perchMascot = new Mascot(5.2, Mascot.ClassicSkin) { Mood = MascotMood.Typing };
            perchMascot.Snap(6);
            perchMascot.HorizontalAlignment = HorizontalAlignment.Right;
            perchMascot.VerticalAlignment = VerticalAlignment.Top;
            perchMascot.Margin = new Thickness(0, -10, 20, 0);
            composerWrap.Children.Add(perchMascot);

            Grid.SetRow(composerWrap, 1);
            stageGrid.Children.Add(composerWrap);
            cardFrame.Child = stageGrid;
            Grid.SetColumn(cardFrame, 2);
            topGrid.Children.Add(cardFrame);
            page.Children.Add(topGrid);

            // ---- Bottom Band: All 19 Live Reactions + Sentiment Mood (10 x 2) ----
            var bandHdr = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            bandHdr.Children.Add(new TextBlock
            {
                Text = "ALL 19 LIVE REACTIONS + TURN SENTIMENT",
                Foreground = BrushHex(0xE2, 0xE8, 0xF0),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            bandHdr.Children.Add(new TextBlock
            {
                Text = "Includes Delegating · Happy · Error · Quota Sleep · Angry/Sad Tints · Taskbar Corners",
                Foreground = BrushHex(0x88, 0x92, 0xA6),
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            page.Children.Add(bandHdr);

            var moods = All20ShowcaseItems(0);
            var grid = new Grid();
            for (int c = 0; c < 10; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 2; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(148) });

            for (int i = 0; i < moods.Length; i++)
            {
                var m = moods[i];
                var mascot = new Mascot(3.3, Mascot.ClassicSkin) { Mood = m.Mood, Sentiment = m.Sentiment };
                mascot.Snap(m.BaseTick);

                var host = new Grid { Height = 56, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                host.Children.Add(mascot);

                var tileStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                tileStack.Children.Add(host);
                tileStack.Children.Add(new TextBlock
                {
                    Text = m.Title,
                    Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 7, 0, 1),
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });
                tileStack.Children.Add(new TextBlock
                {
                    Text = m.Sub,
                    Foreground = BrushHex(0x88, 0x92, 0xA6),
                    FontSize = 10,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });

                var tile = new Border
                {
                    Background = BrushHex(0x13, 0x16, 0x1E),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(11),
                    Margin = new Thickness(4),
                    Child = tileStack,
                };
                Grid.SetRow(tile, i / 10);
                Grid.SetColumn(tile, i % 10);
                grid.Children.Add(tile);
            }
            page.Children.Add(grid);

            var footerRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footerRow.Children.Add(new TextBlock
            {
                Text = "Made by Yassin Ragab (yassinr.me) for Antigravity · Built with Antigravity",
                Foreground = BrushHex(0x64, 0x74, 0x8B),
                FontSize = 11.5,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            footerRow.Children.Add(new TextBlock
            {
                Text = "Interactive demo & Windows download: agypet.pages.dev",
                Foreground = BrushHex(0x60, 0xA5, 0xFA),
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Right,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            page.Children.Add(footerRow);

            root.Child = page;
            root.Measure(new Size(W, H));
            root.Arrange(new Rect(0, 0, W, H));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(outPath)) enc.Save(fs);
        }

        static void RenderSheet(string outPath, string skin, string title, string subtitle, int frameTick)
        {
            const int W = 1300, H = 748;
            var root = new Border
            {
                Width = W,
                Height = H,
                Background = BrushHex(0x0B, 0x0D, 0x12),
                Padding = new Thickness(30, 24, 30, 24),
            };

            var stack = new StackPanel();
            var header = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                FontSize = 23,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 13,
                Margin = new Thickness(0, 3, 0, 0),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            header.Children.Add(titleStack);

            var pill = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(14),
                BorderThickness = new Thickness(1),
                BorderBrush = skin == Mascot.DarkSkin ? BrushHex(0x52, 0x52, 0x5B) : BrushHex(0x25, 0x75, 0xFC),
                Background = skin == Mascot.DarkSkin ? BrushHex(0x18, 0x18, 0x1B) : BrushHex(0x12, 0x1C, 0x33),
                Padding = new Thickness(14, 6, 14, 6),
                Child = new TextBlock
                {
                    Text = skin == Mascot.DarkSkin ? "DARK REMAKE · 19 REACTIONS" : "OFFICIAL COLORS · 19 REACTIONS",
                    Foreground = skin == Mascot.DarkSkin ? BrushHex(0xE4, 0xE4, 0xE7) : BrushHex(0x60, 0xA5, 0xFA),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            };
            header.Children.Add(pill);
            stack.Children.Add(header);

            var grid = new Grid();
            for (int c = 0; c < 5; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 4; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(154) });

            var moods = All20ShowcaseItems(frameTick);

            for (int i = 0; i < moods.Length; i++)
            {
                var m = moods[i];
                var mascot = new Mascot(4.2, skin) { Mood = m.Mood, Sentiment = m.Sentiment };
                mascot.Snap(m.BaseTick + frameTick);

                var mascotHost = new Grid { Height = 68, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                mascotHost.Children.Add(mascot);

                var cardInner = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                cardInner.Children.Add(mascotHost);
                cardInner.Children.Add(new TextBlock
                {
                    Text = m.Title,
                    Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                    FontSize = 13.5,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 7, 0, 2),
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });
                cardInner.Children.Add(new TextBlock
                {
                    Text = m.Sub,
                    Foreground = BrushHex(0x88, 0x92, 0xA6),
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });

                var tile = new Border
                {
                    Background = BrushHex(0x13, 0x16, 0x1E),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(13),
                    Margin = new Thickness(5),
                    Child = cardInner,
                };
                Grid.SetRow(tile, i / 5);
                Grid.SetColumn(tile, i % 5);
                grid.Children.Add(tile);
            }

            stack.Children.Add(grid);
            root.Child = stack;

            root.Measure(new Size(W, H));
            root.Arrange(new Rect(0, 0, W, H));
            root.UpdateLayout();

            var rtb = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(root);

            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(outPath)) enc.Save(fs);
        }

        static Border BuildHoverCardPreviewTile(string skin)
        {
            var card = new Border
            {
                Width = 224,
                Background = BrushHex(0x0E, 0x10, 0x16),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(11),
                Padding = new Thickness(12, 10, 12, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var s = new StackPanel();
            s.Children.Add(new TextBlock
            {
                Text = "HOVER STATUS CARD",
                Foreground = BrushHex(0x64, 0x74, 0x8B),
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            s.Children.Add(new TextBlock
            {
                Text = "Editing PetWindow.cs",
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 2),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            s.Children.Add(new TextBlock
            {
                Text = "Building standalone overlay",
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 11,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            var fill = new Border
            {
                Height = 5,
                Width = 132,
                HorizontalAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(3),
                Background = skin == Mascot.DarkSkin
                    ? (Brush)BrushHex(0xE4, 0xE4, 0xE7)
                    : new LinearGradientBrush(Color.FromRgb(0x34, 0xA8, 0x53), Color.FromRgb(0x25, 0x75, 0xFC), 0),
            };
            var track = new Border
            {
                Height = 5,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(0, 8, 0, 5),
                Child = fill,
            };
            s.Children.Add(track);
            s.Children.Add(new TextBlock
            {
                Text = "67% done · 33% left · 4/6 steps",
                Foreground = BrushHex(0xCB, 0xD5, 0xE1),
                FontSize = 10.5,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            card.Child = s;

            return new Border
            {
                Background = BrushHex(0x13, 0x16, 0x1E),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Margin = new Thickness(7),
                Child = card,
            };
        }

        static SolidColorBrush BrushHex(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }
    }
}
