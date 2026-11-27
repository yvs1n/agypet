using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AgyPet
{
    // Website-themed dark feedback modal for macOS, Linux, and cross-platform builds.
    class FeedbackWindow : Window
    {
        readonly ComboBox categoryBox;
        readonly TextBox messageBox;
        readonly TextBlock statusText;
        readonly string diagnosticSummary;

        static FeedbackWindow activeInstance;

        public static void ShowDialogOnce(string skin, string size)
        {
            if (activeInstance != null)
            {
                activeInstance.Activate();
                return;
            }
            var win = new FeedbackWindow(skin, size);
            activeInstance = win;
            win.Closed += (s, e) => { if (activeInstance == win) activeInstance = null; };
            win.Show();
        }

        FeedbackWindow(string skin, string size)
        {
            diagnosticSummary = string.Format("Skin: {0} | Size: {1} | OS: {2} ({3})",
                skin, size, RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture);

            Title = "AgyPet Feedback";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SystemDecorations = SystemDecorations.None;
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
            Background = Brushes.Transparent;
            Topmost = true;
            CanResize = false;
            Icon = PetTheme.CreateMascotWindowIcon();

            var outer = new Border
            {
                Background = PetTheme.BgRaised,
                BorderBrush = PetTheme.BorderStrong,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(20, 18, 20, 18),
            };

            var stack = new StackPanel { Spacing = 0 };

            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Cursor = new Cursor(StandardCursorType.SizeAll) };
            header.PointerPressed += (s, e) => { try { BeginMoveDrag(e); } catch { } };

            var closeBtn = new Button
            {
                Content = "×",
                Width = 26,
                Height = 26,
                Foreground = PetTheme.InkMuted,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0),
            };
            closeBtn.Click += (s, e) => Close();
            DockPanel.SetDock(closeBtn, Dock.Right);
            header.Children.Add(closeBtn);

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "Send Feedback",
                Foreground = PetTheme.InkPrimary,
                FontSize = 16,
                FontWeight = FontWeight.Bold,
                FontFamily = PetTheme.UiFont,
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "Ideas, reaction requests, or bugs for AgyPet",
                Foreground = PetTheme.InkMuted,
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                FontFamily = PetTheme.UiFont,
            });
            header.Children.Add(titleStack);
            stack.Children.Add(header);

            stack.Children.Add(new TextBlock
            {
                Text = "Category",
                Foreground = PetTheme.InkSecondary,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                FontFamily = PetTheme.UiFont,
            });

            categoryBox = new ComboBox
            {
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                FontFamily = PetTheme.UiFont,
                ItemsSource = new[] { "Reaction Idea", "Feature Request", "Bug Report", "General Feedback" },
                SelectedIndex = 0,
            };
            stack.Children.Add(categoryBox);

            stack.Children.Add(new TextBlock
            {
                Text = "Message",
                Foreground = PetTheme.InkSecondary,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                FontFamily = PetTheme.UiFont,
            });

            messageBox = new TextBox
            {
                Height = 110,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Background = PetTheme.BgCanvas,
                Foreground = PetTheme.InkPrimary,
                CaretBrush = PetTheme.InkPrimary,
                BorderBrush = PetTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13,
                FontFamily = PetTheme.UiFont,
            };
            stack.Children.Add(messageBox);

            stack.Children.Add(new TextBlock
            {
                Text = diagnosticSummary,
                Foreground = PetTheme.InkMuted,
                FontSize = 11,
                Margin = new Thickness(0, 7, 0, 12),
                FontFamily = PetTheme.UiFont,
                TextWrapping = TextWrapping.Wrap,
            });

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };

            var copyBtn = new Button
            {
                Content = "Copy & Visit yassinr.me",
                Padding = new Thickness(12, 7, 12, 7),
                Background = PetTheme.BgElevated,
                Foreground = PetTheme.InkSecondary,
                BorderBrush = PetTheme.BorderSubtle,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(9),
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold,
                Cursor = new Cursor(StandardCursorType.Hand),
                FontFamily = PetTheme.UiFont,
            };
            copyBtn.Click += (s, e) => SubmitFeedback(openGitHub: false);

            var sendBtn = new Button
            {
                Content = "Submit via GitHub",
                Padding = new Thickness(14, 7, 14, 7),
                Background = PetTheme.AccentBlue,
                Foreground = PetTheme.InkPrimary,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(9),
                FontSize = 12.5,
                FontWeight = FontWeight.SemiBold,
                Cursor = new Cursor(StandardCursorType.Hand),
                FontFamily = PetTheme.UiFont,
            };
            sendBtn.Click += (s, e) => SubmitFeedback(openGitHub: true);

            btnRow.Children.Add(copyBtn);
            btnRow.Children.Add(sendBtn);
            stack.Children.Add(btnRow);

            statusText = new TextBlock
            {
                Text = "",
                Foreground = PetTheme.AccentEmerald,
                FontSize = 11.5,
                Margin = new Thickness(0, 8, 0, 0),
                IsVisible = false,
                FontFamily = PetTheme.UiFont,
            };
            stack.Children.Add(statusText);

            outer.Child = stack;
            Content = outer;
            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control)
                {
                    SubmitFeedback(openGitHub: true);
                    e.Handled = true;
                }
            };
            Opened += (s, e) => messageBox.Focus();
        }

        async void SubmitFeedback(bool openGitHub)
        {
            string cat = categoryBox.SelectedItem != null ? categoryBox.SelectedItem.ToString() : "General";
            string msg = (messageBox.Text ?? "").Trim();
            if (msg.Length == 0)
            {
                statusText.Text = "Please enter a short message first.";
                statusText.Foreground = PetTheme.AccentAmber;
                statusText.IsVisible = true;
                return;
            }

            string formatted = string.Format("[AgyPet {0}]\n{1}\n\n-- {2}", cat, msg, diagnosticSummary);

            try
            {
                Directory.CreateDirectory(PetSettings.DataDir);
                string logPath = Path.Combine(PetSettings.DataDir, "feedback.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("s") + "\n" + formatted + "\n---\n");
            }
            catch { }

            try
            {
                if (Clipboard != null)
                    await Clipboard.SetTextAsync(formatted);
            }
            catch { }

            if (openGitHub)
            {
                string label = cat.IndexOf("Bug", StringComparison.OrdinalIgnoreCase) >= 0 ? "bug"
                    : (cat.IndexOf("Idea", StringComparison.OrdinalIgnoreCase) >= 0 || cat.IndexOf("Feature", StringComparison.OrdinalIgnoreCase) >= 0 ? "enhancement" : "");
                string title = Uri.EscapeDataString("[AgyPet " + cat + "] " + (msg.Length > 54 ? msg.Substring(0, 54) + "..." : msg));
                string body = Uri.EscapeDataString(msg + "\n\n---\n" + diagnosticSummary);
                string labelParam = string.IsNullOrEmpty(label) ? "" : "&labels=" + label;
                PetWindow.OpenUrl("https://github.com/yvs1n/agypet/issues/new?title=" + title + "&body=" + body + labelParam);
                statusText.Text = "Opened GitHub issue & saved locally.";
            }
            else
            {
                PetWindow.OpenUrl("https://yassinr.me");
                statusText.Text = "Copied feedback to clipboard & opened yassinr.me.";
            }

            statusText.Foreground = PetTheme.AccentEmerald;
            statusText.IsVisible = true;
        }
    }
}
