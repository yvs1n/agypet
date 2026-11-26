using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AgyPet
{
    // Lightweight native dark-glass feedback dialog for AgyPet.exe.
    // Lets users pick a category, write feedback, save locally, copy to clipboard,
    // or open a pre-filled GitHub issue / yassinr.me in their browser.
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
            diagnosticSummary = string.Format("Skin: {0} | Size: {1} | OS: {2}", skin, size, Environment.OSVersion.VersionString);

            Title = "AgyPet Feedback";
            Width = 420;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            var outer = new Border
            {
                Background = BrushHex(0x11, 0x13, 0x18),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(20, 18, 20, 18),
            };

            var stack = new StackPanel();

            // Draggable Header + Close button
            var header = new DockPanel { Margin = new Thickness(0, 0, 0, 12), Cursor = Cursors.SizeAll };
            header.MouseLeftButtonDown += (s, e) => { try { DragMove(); } catch { } };

            var closeBtn = new Button
            {
                Content = "×",
                Width = 26,
                Height = 26,
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 16,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(closeBtn, "Close feedback dialog");
            closeBtn.Click += (s, e) => Close();
            DockPanel.SetDock(closeBtn, Dock.Right);
            header.Children.Add(closeBtn);

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = "Send Feedback",
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = "Ideas, reaction requests, or bugs for AgyPet",
                Foreground = BrushHex(0x94, 0xA3, 0xB8),
                FontSize = 12,
                Margin = new Thickness(0, 2, 0, 0),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });
            header.Children.Add(titleStack);
            stack.Children.Add(header);

            // Category
            stack.Children.Add(new TextBlock
            {
                Text = "Category",
                Foreground = BrushHex(0xCB, 0xD5, 0xE1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            categoryBox = new ComboBox
            {
                FontSize = 13,
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 0, 0, 12),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            };
            AutomationProperties.SetName(categoryBox, "Feedback category");
            categoryBox.Items.Add("Reaction Idea");
            categoryBox.Items.Add("Feature Request");
            categoryBox.Items.Add("Bug Report");
            categoryBox.Items.Add("General Feedback");
            categoryBox.SelectedIndex = 0;
            stack.Children.Add(categoryBox);

            // Message
            stack.Children.Add(new TextBlock
            {
                Text = "Message",
                Foreground = BrushHex(0xCB, 0xD5, 0xE1),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 5),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            messageBox = new TextBox
            {
                Height = 110,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Background = BrushHex(0x18, 0x1B, 0x23),
                Foreground = BrushHex(0xF8, 0xFA, 0xFC),
                CaretBrush = BrushHex(0xF8, 0xFA, 0xFC),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 8, 10, 8),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            };
            AutomationProperties.SetName(messageBox, "Feedback message");
            stack.Children.Add(messageBox);

            stack.Children.Add(new TextBlock
            {
                Text = diagnosticSummary,
                Foreground = BrushHex(0x64, 0x74, 0x8B),
                FontSize = 11,
                Margin = new Thickness(0, 7, 0, 12),
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            });

            // Action Buttons
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

            var copyBtn = new Button
            {
                Content = "Copy & Visit yassinr.me",
                Padding = new Thickness(12, 7, 12, 7),
                Margin = new Thickness(0, 0, 8, 0),
                Background = BrushHex(0x1E, 0x22, 0x2C),
                Foreground = BrushHex(0xE2, 0xE8, 0xF0),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(1),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            };
            copyBtn.Click += (s, e) => SubmitFeedback(openGitHub: false);

            var sendBtn = new Button
            {
                Content = "Submit via GitHub",
                Padding = new Thickness(14, 7, 14, 7),
                Background = BrushHex(0x1E, 0x78, 0xFF),
                Foreground = BrushHex(0xFF, 0xFF, 0xFF),
                BorderThickness = new Thickness(0),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            };
            sendBtn.Click += (s, e) => SubmitFeedback(openGitHub: true);

            btnRow.Children.Add(copyBtn);
            btnRow.Children.Add(sendBtn);
            stack.Children.Add(btnRow);

            statusText = new TextBlock
            {
                Text = "",
                Foreground = BrushHex(0x4E, 0xC4, 0x46),
                FontSize = 11.5,
                Margin = new Thickness(0, 8, 0, 0),
                Visibility = Visibility.Collapsed,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"),
            };
            stack.Children.Add(statusText);

            outer.Child = stack;
            Content = outer;
            PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    Close();
                    e.Handled = true;
                }
                else if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    SubmitFeedback(openGitHub: true);
                    e.Handled = true;
                }
            };
            Loaded += (s, e) => messageBox.Focus();
        }

        void SubmitFeedback(bool openGitHub)
        {
            string cat = categoryBox.SelectedItem != null ? categoryBox.SelectedItem.ToString() : "General";
            string msg = (messageBox.Text ?? "").Trim();
            if (msg.Length == 0)
            {
                statusText.Text = "Please enter a short message first.";
                statusText.Foreground = BrushHex(0xF5, 0x9E, 0x0B);
                statusText.Visibility = Visibility.Visible;
                return;
            }

            string formatted = string.Format("[AgyPet {0}]\r\n{1}\r\n\r\n-- {2}", cat, msg, diagnosticSummary);

            try
            {
                Directory.CreateDirectory(PetSettings.DataDir);
                string logPath = Path.Combine(PetSettings.DataDir, "feedback.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("s") + "\r\n" + formatted + "\r\n---\r\n");
            }
            catch { }

            try { Clipboard.SetText(formatted); } catch { }

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

            statusText.Foreground = BrushHex(0x4E, 0xC4, 0x46);
            statusText.Visibility = Visibility.Visible;
        }

        static Brush BrushHex(byte r, byte g, byte b)
        {
            var br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }
    }
}
