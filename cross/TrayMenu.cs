using System;
using Avalonia.Controls;
using Avalonia.Threading;

namespace AgyPet
{
    // Native OS Menu Bar / System Tray integration via Avalonia TrayIcon + NativeMenu
    // (macOS Menu Bar NSStatusItem + Linux StatusNotifierItem / AppIndicator).
    class TrayMenu : IDisposable
    {
        readonly TrayIcon icon;
        readonly PetWindow window;
        readonly PetSettings settings;
        readonly Updater updater;

        NativeMenuItem skinClassicItem, skinDarkItem;
        NativeMenuItem sizeSmallItem, sizeMediumItem, sizeLargeItem;
        NativeMenuItem pinAboveTaskbarItem, lockPosItem, hoverCardItem, startupItem, autoUpdateItem;
        NativeMenuItem hideInactiveItem, hideMinimizedItem, liveModeItem, updateItem;
        NativeMenu cornerMenu, cliMenu, previewMenu;

        public TrayMenu(PetWindow window, PetSettings settings, Updater updater)
        {
            this.window = window;
            this.settings = settings;
            this.updater = updater;

            var menu = new NativeMenu();
            menu.NeedsUpdate += (s, e) => SyncChecks();

            var skinMenu = new NativeMenu();
            skinClassicItem = Check("Classic (Official Antigravity Colors)", () => { window.ApplySkin(MascotCore.ClassicSkin); SyncChecks(); });
            skinDarkItem = Check("Dark Remake (Monochrome)", () => { window.ApplySkin(MascotCore.DarkSkin); SyncChecks(); });
            skinMenu.Add(skinClassicItem);
            skinMenu.Add(skinDarkItem);
            menu.Add(new NativeMenuItem("Skin") { Menu = skinMenu });

            cornerMenu = new NativeMenu();
            bool sepAdded = false;
            foreach (var opt in PetSettings.AwayLocations)
            {
                if (!sepAdded && opt.Id.StartsWith("taskbar-", StringComparison.Ordinal))
                {
                    cornerMenu.Add(new NativeMenuItemSeparator());
                    sepAdded = true;
                }
                string id = opt.Id;
                var item = new NativeMenuItem(opt.Label) { ToggleType = NativeMenuItemToggleType.CheckBox };
                item.Click += (s, e) => { window.SetMinimizedCorner(id); SyncChecks(); };
                cornerMenu.Add(item);
            }
            menu.Add(new NativeMenuItem("Location When Away / Minimized") { Menu = cornerMenu });

            pinAboveTaskbarItem = Check("Pin Above Taskbar (Stay in Place)", () => { window.SetPinAboveTaskbar(!settings.PinAboveTaskbar); SyncChecks(); });
            lockPosItem = Check("Lock Position (Disable Dragging)", () => { window.SetLockPosition(!settings.LockPosition); SyncChecks(); });
            menu.Add(pinAboveTaskbarItem);
            menu.Add(lockPosItem);

            cliMenu = new NativeMenu();
            foreach (var opt in PetSettings.CliLocations)
            {
                string id = opt.Id;
                var item = new NativeMenuItem(opt.Label) { ToggleType = NativeMenuItemToggleType.CheckBox };
                item.Click += (s, e) => { window.SetCliPlacement(id); SyncChecks(); };
                cliMenu.Add(item);
            }
            menu.Add(new NativeMenuItem("CLI Terminal Placement") { Menu = cliMenu });

            var autoHideMenu = new NativeMenu();
            hideInactiveItem = Check("Hide When Inactive (App & CLI Closed)", () => { window.SetHideWhenInactive(!settings.HideWhenInactive); SyncChecks(); });
            hideMinimizedItem = Check("Hide When Minimized", () => { window.SetHideWhenMinimized(!settings.HideWhenMinimized); SyncChecks(); });
            autoHideMenu.Add(hideInactiveItem);
            autoHideMenu.Add(hideMinimizedItem);
            menu.Add(new NativeMenuItem("Auto-Hide") { Menu = autoHideMenu });

            var sizeMenu = new NativeMenu();
            sizeSmallItem = Check("Small", () => { window.ApplySize("small"); SyncChecks(); });
            sizeMediumItem = Check("Medium", () => { window.ApplySize("medium"); SyncChecks(); });
            sizeLargeItem = Check("Large", () => { window.ApplySize("large"); SyncChecks(); });
            sizeMenu.Add(sizeSmallItem);
            sizeMenu.Add(sizeMediumItem);
            sizeMenu.Add(sizeLargeItem);
            menu.Add(new NativeMenuItem("Size") { Menu = sizeMenu });

            previewMenu = new NativeMenu();
            liveModeItem = Check("Live Mode (Auto-react to Antigravity)", () => { window.SetPreviewMood(null); SyncChecks(); });
            previewMenu.Add(liveModeItem);
            previewMenu.Add(new NativeMenuItemSeparator());
            foreach (MascotMood m in Enum.GetValues(typeof(MascotMood)))
            {
                var captured = m;
                var item = new NativeMenuItem(captured.ToString()) { ToggleType = NativeMenuItemToggleType.CheckBox };
                item.Click += (s, e) => { window.SetPreviewMood(captured); SyncChecks(); };
                previewMenu.Add(item);
            }
            menu.Add(new NativeMenuItem("Preview Mood") { Menu = previewMenu });

            menu.Add(new NativeMenuItemSeparator());
            hoverCardItem = Check("Show Hover Progress Card", () =>
            {
                settings.ShowHoverCard = !settings.ShowHoverCard;
                settings.Save();
                if (!settings.ShowHoverCard) window.HideHoverCard();
                SyncChecks();
            });
            startupItem = Check("Launch on Login", () =>
            {
                settings.StartWithWindows = !settings.StartWithWindows;
                settings.Save();
                PetSettings.ApplyStartup(settings.StartWithWindows, Environment.ProcessPath ?? "");
                SyncChecks();
            });
            var resetItem = new NativeMenuItem("Reset / Snap Back Above Window");
            resetItem.Click += (s, e) => window.ResetPosition();
            autoUpdateItem = Check("Auto-Update from GitHub", () => { settings.AutoUpdate = !settings.AutoUpdate; settings.Save(); SyncChecks(); });

            menu.Add(hoverCardItem);
            menu.Add(startupItem);
            menu.Add(resetItem);
            menu.Add(autoUpdateItem);

            updateItem = new NativeMenuItem("Check for Updates");
            updateItem.Click += (s, e) => updater.CheckNow();
            menu.Add(updateItem);

            var fbItem = new NativeMenuItem("Send Feedback (GitHub Issue)...");
            fbItem.Click += (s, e) => FeedbackWindow.ShowDialogOnce(settings.Skin, settings.Size);
            menu.Add(fbItem);

            var creditsMenu = new NativeMenu();
            var cr1 = new NativeMenuItem("Made by Yassin Ragab (yassinr.me)");
            cr1.Click += (s, e) => PetWindow.OpenUrl("https://yassinr.me");
            var cr2 = new NativeMenuItem("GitHub: yvs1n/agypet");
            cr2.Click += (s, e) => PetWindow.OpenUrl("https://github.com/yvs1n/agypet");
            var cr3 = new NativeMenuItem("For Antigravity, built with Antigravity");
            cr3.Click += (s, e) => PetWindow.OpenUrl("https://antigravity.google");
            creditsMenu.Add(cr1);
            creditsMenu.Add(cr2);
            creditsMenu.Add(cr3);
            menu.Add(new NativeMenuItem("Credits") { Menu = creditsMenu });

            menu.Add(new NativeMenuItemSeparator());
            var quitItem = new NativeMenuItem("Quit AgyPet");
            quitItem.Click += (s, e) => Program.ShutdownApplication();
            menu.Add(quitItem);

            icon = new TrayIcon
            {
                Icon = PetTheme.CreateMascotWindowIcon(),
                ToolTipText = "AgyPet — Antigravity Companion",
                Menu = menu,
                IsVisible = true,
            };

            updater.Changed += () => Dispatcher.UIThread.Post(SyncUpdateLabel);
            SyncChecks();
            SyncUpdateLabel();
        }

        static NativeMenuItem Check(string label, Action onClick)
        {
            var item = new NativeMenuItem(label) { ToggleType = NativeMenuItemToggleType.CheckBox };
            item.Click += (s, e) => onClick();
            return item;
        }

        public void SyncChecks()
        {
            skinClassicItem.IsChecked = settings.Skin == MascotCore.ClassicSkin;
            skinDarkItem.IsChecked = settings.Skin == MascotCore.DarkSkin;
            sizeSmallItem.IsChecked = settings.Size == "small";
            sizeMediumItem.IsChecked = settings.Size == "medium";
            sizeLargeItem.IsChecked = settings.Size == "large";
            pinAboveTaskbarItem.IsChecked = settings.PinAboveTaskbar;
            lockPosItem.IsChecked = settings.LockPosition;
            hideInactiveItem.IsChecked = settings.HideWhenInactive;
            hideMinimizedItem.IsChecked = settings.HideWhenMinimized;
            hoverCardItem.IsChecked = settings.ShowHoverCard;
            startupItem.IsChecked = settings.StartWithWindows;
            autoUpdateItem.IsChecked = settings.AutoUpdate;
            liveModeItem.IsChecked = !window.IsPreviewMode;

            int locIdx = 0;
            foreach (var raw in cornerMenu.Items)
            {
                var mi = raw as NativeMenuItem;
                if (mi == null || locIdx >= PetSettings.AwayLocations.Length) continue;
                mi.IsChecked = !settings.HasCustomFreePos && settings.MinimizedCorner == PetSettings.AwayLocations[locIdx].Id;
                locIdx++;
            }

            int cliIdx = 0;
            foreach (var raw in cliMenu.Items)
            {
                var mi = raw as NativeMenuItem;
                if (mi == null || cliIdx >= PetSettings.CliLocations.Length) continue;
                mi.IsChecked = settings.CliPlacement == PetSettings.CliLocations[cliIdx].Id;
                cliIdx++;
            }
        }

        void SyncUpdateLabel()
        {
            if (updateItem == null || updater == null) return;
            updateItem.Header = updater.UpdateReady
                ? "Install Update (" + updater.ReadyTag + ")"
                : "Check for Updates (" + updater.Status + ")";
        }

        public void Dispose()
        {
            try { icon.IsVisible = false; icon.Dispose(); } catch { }
        }
    }
}
