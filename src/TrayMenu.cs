using System;
using System.Drawing;
using System.Windows.Forms;

namespace AgyPet
{
    class TrayMenu : IDisposable
    {
        readonly NotifyIcon icon;
        readonly PetWindow window;
        readonly PetSettings settings;
        readonly Updater updater;
        ToolStripMenuItem updateItem;
        ToolStripMenuItem locationMenu;
        ToolStripMenuItem cliLocationMenu;
        ToolStripMenuItem hideInactiveItem;
        ToolStripMenuItem hideMinimizedItem;

        public TrayMenu(PetWindow window, PetSettings settings, Updater updater)
        {
            this.window = window;
            this.settings = settings;
            this.updater = updater;

            var menu = BuildMenu();
            menu.Opening += (s, e) => window.Dispatcher.BeginInvoke(new Action(() =>
            {
                window.SetExternalMenuOpen(true);
                RefreshUpdateLabel();
                RefreshCornerChecks();
            }));
            menu.Closed += (s, e) => window.Dispatcher.BeginInvoke(new Action(() => window.SetExternalMenuOpen(false)));

            icon = new NotifyIcon
            {
                Text = "AgyPet — Antigravity Companion",
                Icon = CreateTrayIcon(),
                Visible = true,
                ContextMenuStrip = menu,
            };
            icon.DoubleClick += (s, e) => window.Dispatcher.BeginInvoke(new Action(window.ResetPosition));
            if (updater != null) updater.Changed += () => window.Dispatcher.BeginInvoke(new Action(RefreshUpdateLabel));
        }

        void RefreshUpdateLabel()
        {
            if (updateItem == null || updater == null) return;
            updateItem.Text = updater.UpdateReady
                ? "Install Update (" + updater.ReadyTag + ")"
                : "Check for Updates (" + updater.Status + ")";
        }

        void RefreshCornerChecks()
        {
            if (locationMenu != null)
            {
                bool custom = settings.HasCustomFreePos;
                foreach (ToolStripItem item in locationMenu.DropDownItems)
                {
                    var mi = item as ToolStripMenuItem;
                    if (mi != null && mi.Tag is string)
                        mi.Checked = !custom && settings.MinimizedCorner == (string)mi.Tag;
                }
            }
            if (cliLocationMenu != null)
            {
                foreach (ToolStripItem item in cliLocationMenu.DropDownItems)
                {
                    var mi = item as ToolStripMenuItem;
                    if (mi != null && mi.Tag is string)
                        mi.Checked = settings.CliPlacement == (string)mi.Tag;
                }
            }
            if (hideInactiveItem != null)
                hideInactiveItem.Checked = settings.HideWhenInactive;
            if (hideMinimizedItem != null)
                hideMinimizedItem.Checked = settings.HideWhenMinimized;
        }

        ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("Skin: Classic (Official Colors)", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => window.ApplySkin(Mascot.ClassicSkin))));
            menu.Items.Add("Skin: Dark Remake (Monochrome)", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => window.ApplySkin(Mascot.DarkSkin))));
            locationMenu = new ToolStripMenuItem("Location When Away / Minimized");
            bool addedSep = false;
            foreach (var opt in PetSettings.AwayLocations)
            {
                if (!addedSep && opt.Id.StartsWith("taskbar-", StringComparison.Ordinal))
                {
                    locationMenu.DropDownItems.Add(new ToolStripSeparator());
                    addedSep = true;
                }
                string value = opt.Id;
                var mi = new ToolStripMenuItem(opt.Label, null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => window.SetMinimizedCorner(value))))
                {
                    Tag = value
                };
                locationMenu.DropDownItems.Add(mi);
            }
            menu.Items.Add(locationMenu);

            cliLocationMenu = new ToolStripMenuItem("CLI Terminal Placement");
            foreach (var opt in PetSettings.CliLocations)
            {
                string value = opt.Id;
                var mi = new ToolStripMenuItem(opt.Label, null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => window.SetCliPlacement(value))))
                {
                    Tag = value
                };
                cliLocationMenu.DropDownItems.Add(mi);
            }
            menu.Items.Add(cliLocationMenu);

            var autoHideMenu = new ToolStripMenuItem("Auto-Hide");
            hideInactiveItem = new ToolStripMenuItem("Hide When Inactive (App & CLI Closed)", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => window.SetHideWhenInactive(!settings.HideWhenInactive))));
            hideMinimizedItem = new ToolStripMenuItem("Hide When Minimized", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => window.SetHideWhenMinimized(!settings.HideWhenMinimized))));
            autoHideMenu.DropDownItems.AddRange(new ToolStripItem[] { hideInactiveItem, hideMinimizedItem });
            menu.Items.Add(autoHideMenu);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Reset Position", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(window.ResetPosition)));
            updateItem = new ToolStripMenuItem("Check for Updates", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => { if (updater != null) updater.CheckNow(); })));
            RefreshUpdateLabel();
            menu.Items.Add(updateItem);
            menu.Items.Add("Send Feedback (GitHub Issue)...", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => FeedbackWindow.ShowDialogOnce(settings.Skin, settings.Size))));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Made by Yassin Ragab (yassinr.me)", null, (s, e) => PetWindow.OpenUrl("https://yassinr.me"));
            menu.Items.Add("GitHub: yvs1n/agypet", null, (s, e) => PetWindow.OpenUrl("https://github.com/yvs1n/agypet"));
            menu.Items.Add("For Antigravity, built with Antigravity", null, (s, e) => PetWindow.OpenUrl("https://antigravity.google"));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit AgyPet", null, (s, e) => window.Dispatcher.BeginInvoke(new Action(() => System.Windows.Application.Current.Shutdown())));
            return menu;
        }

        static Icon CreateTrayIcon()
        {
            using (var bmp = new Bitmap(16, 16))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                string[] mask =
                {
                    "......####......",
                    ".....######.....",
                    "....########....",
                    "....########....",
                    "...##########...",
                    "...##########...",
                    "..############..",
                    "..############..",
                    "..###......###..",
                    "..##........##..",
                    ".###........###.",
                    ".##..........##.",
                    "##............##",
                    "#..............#"
                };
                for (int r = 0; r < mask.Length; r++)
                {
                    for (int c = 0; c < 16; c++)
                    {
                        if (mask[r][c] != '#') continue;
                        Color col;
                        if (r <= 2) col = c < 7 ? Color.FromArgb(0xF8, 0xC6, 0x18) : (c < 10 ? Color.FromArgb(0xFA, 0x74, 0x20) : Color.FromArgb(0xEF, 0x44, 0x3A));
                        else if (r <= 6 && c < 7) col = Color.FromArgb(0x4E, 0xC4, 0x46);
                        else if (r <= 6 && c >= 9) col = Color.FromArgb(0x7B, 0x61, 0xEC);
                        else col = Color.FromArgb(0x1E, 0x78, 0xFF);
                        bmp.SetPixel(c, r + 1, col);
                    }
                }
                bmp.SetPixel(5, 6, Color.FromArgb(0x0B, 0x12, 0x20));
                bmp.SetPixel(10, 6, Color.FromArgb(0x0B, 0x12, 0x20));
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public void Dispose()
        {
            if (icon != null)
            {
                icon.Visible = false;
                icon.Dispose();
            }
        }
    }
}
