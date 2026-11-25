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
                "Refined CLI pixel arch in official Antigravity logo colors — all 11 live reactions");

            RenderSheet(Path.Combine(outDir, "showcase-dark.png"), Mascot.DarkSkin,
                "AgyPet · Dark Remake Skin",
                "Monochrome obsidian & silver remake with luminous eyes — all 11 live reactions");

            string siteDir = args.Length > 1 ? args[1] : Path.GetFullPath(Path.Combine(outDir, "..", "site"));
            Directory.CreateDirectory(siteDir);
            RenderOgScreen(Path.Combine(siteDir, "og-screen.png"));
            RenderIconPng(Path.Combine(siteDir, "apple-touch-icon.png"), 180);
            RenderIconPng(Path.Combine(siteDir, "favicon-32x32.png"), 32);

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

        static void RenderOgScreen(string outPath)
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
                Text = "16 live reactions, per-chat & voice sync, adaptive chatbox perch, and scrollable multi-session tracking.",
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 19,
                LineHeight = 28,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 18, 0, 28),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            var skinsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 26) };
            var cMascot = new Mascot(5.2, Mascot.ClassicSkin) { Mood = MascotMood.Multitasking };
            cMascot.Snap(6);
            var dMascot = new Mascot(5.2, Mascot.DarkSkin) { Mood = MascotMood.Thinking };
            dMascot.Snap(12);
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

            var agentBubble = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = BrushHex(0x16, 0x19, 0x22),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14, 9, 14, 9),
                Child = new TextBlock
                {
                    Text = "Perching above the chatbox with 16 live reactions.",
                    Foreground = BrushHex(0x94, 0xA3, 0xB8),
                    FontSize = 13.5,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            };
            topArea.Children.Add(agentBubble);

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

            var perchMascot = new Mascot(5.8, Mascot.ClassicSkin) { Mood = MascotMood.Typing };
            perchMascot.Snap(6);
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

        static void RenderSheet(string outPath, string skin, string title, string subtitle)
        {
            const int W = 1200, H = 680;
            var root = new Border
            {
                Width = W,
                Height = H,
                Background = BrushHex(0x0B, 0x0D, 0x12),
                Padding = new Thickness(32, 28, 32, 28),
            };

            var stack = new StackPanel();
            var header = new Grid { Margin = new Thickness(0, 0, 0, 22) };
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = subtitle,
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 13.5,
                Margin = new Thickness(0, 4, 0, 0),
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
                    Text = skin == Mascot.DarkSkin ? "DARK REMAKE" : "OFFICIAL COLORS",
                    Foreground = skin == Mascot.DarkSkin ? BrushHex(0xE4, 0xE4, 0xE7) : BrushHex(0x60, 0xA5, 0xFA),
                    FontSize = 11.5,
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                },
            };
            header.Children.Add(pill);
            stack.Children.Add(header);

            var grid = new Grid();
            for (int c = 0; c < 4; c++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int r = 0; r < 3; r++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(182) });

            var moods = new[]
            {
                Tuple.Create(MascotMood.Idle, "Idle", "At rest · Ready", 4),
                Tuple.Create(MascotMood.Sleeping, "Sleeping", "Idle · Quiet", 24),
                Tuple.Create(MascotMood.Listening, "Listening", "Hearing you", 8),
                Tuple.Create(MascotMood.Thinking, "Thinking", "Planning answer", 12),
                Tuple.Create(MascotMood.Speaking, "Speaking", "Reading aloud", 9),
                Tuple.Create(MascotMood.Searching, "Searching", "Looking up", 11),
                Tuple.Create(MascotMood.Reading, "Reading", "Scanning code", 6),
                Tuple.Create(MascotMood.Working, "Working", "Editing & running", 14),
                Tuple.Create(MascotMood.Alert, "Alert", "Needs approval", 5),
                Tuple.Create(MascotMood.Question, "Question", "Asking a choice", 7),
                Tuple.Create(MascotMood.Done, "Done", "Task complete", 4),
            };

            for (int i = 0; i < moods.Length; i++)
            {
                var m = moods[i];
                var mascot = new Mascot(5.0, skin) { Mood = m.Item1 };
                mascot.Snap(m.Item4);

                var mascotHost = new Grid { Height = 82, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                mascotHost.Children.Add(mascot);

                var cardInner = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                cardInner.Children.Add(mascotHost);
                cardInner.Children.Add(new TextBlock
                {
                    Text = m.Item2,
                    Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                    FontSize = 14.5,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 2),
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });
                cardInner.Children.Add(new TextBlock
                {
                    Text = m.Item3,
                    Foreground = BrushHex(0x88, 0x92, 0xA6),
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
                });

                var tile = new Border
                {
                    Background = BrushHex(0x13, 0x16, 0x1E),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(0x1E, 0xFF, 0xFF, 0xFF)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(14),
                    Margin = new Thickness(7),
                    Child = cardInner,
                };
                Grid.SetRow(tile, i / 4);
                Grid.SetColumn(tile, i % 4);
                grid.Children.Add(tile);
            }

            // 12th tile: Hover Progress Card preview
            var progressTile = BuildHoverCardPreviewTile(skin);
            Grid.SetRow(progressTile, 2);
            Grid.SetColumn(progressTile, 3);
            grid.Children.Add(progressTile);

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
