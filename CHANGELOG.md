# Changelog

## 0.4.10 (2026-11-27)
- **Unified Single Musical Note Across Dancing & Active Audio**: Updated dancing (`DrawDanceNotes`) across Windows ([src/Mascot.cs](src/Mascot.cs)), macOS/Linux ([cross/MascotView.cs](cross/MascotView.cs)), and the website ([site/app.js](site/app.js)) to share the same single eighth-note glyph (`♪` via `DrawEighthNote` / `drawEighthNote`) used when speakers/headphones audio is active.

## 0.4.9 (2026-11-27)
- **Bottom-Anchored Petting Squish Across All OSes**: Fixed clicking/petting the mascot on Windows (`src/Mascot.cs`) and macOS/Linux (`cross/MascotView.cs`) so the inner sprite canvas has explicit pixel dimensions (`11 * px` × `9 * px`) and scales strictly around the bottom-center feet pivot `(5.5 * px, 9.0 * px)` instead of collapsing to top-left `(0, 0)` and lifting his legs. Removed vertical `pressDip` / `hurt` offsets across Windows, macOS, Linux, and [site/app.js](site/app.js) (`ScaleX = 1.06, ScaleY = 0.90` on press) so his legs stay 100% planted while petting squishes him smoothly.

## 0.4.8 (2026-11-27)
- **Orb-Free Scrolling-Code Working Animation**: Restored the classic multi-line scrolling syntax-highlighted `DrawLaptop` animation with blinking cursor and tapping fingers across Windows (`src/Mascot.cs`), macOS/Linux (`cross/MascotView.cs`), and the website (`site/app.js`), without the two floating orbs above the laptop.
- **Full 18-Animation Desktop Parity & Top-of-Page Logo Navigation**: Synced `Sleeping`, `Speaking`, `Searching`, `Reading`, `Terminal`, `Working`, `Multitasking`, and `Done` on [site/app.js](site/app.js) pixel-for-pixel with the desktop mascot, and updated clicking the navbar logo to scroll cleanly to the very top of the page (`y = 0`).

## 0.4.7 (2026-11-27)
- **Website-Matched Working Animation**: Updated `DrawLaptop` across Windows (`src/Mascot.cs`) and macOS/Linux (`cross/MascotView.cs`) to match the clean laptop & typing arm on the landing page (`site/app.js`), removing the two floating orbs above the laptop.
- **Release Workflow Race Fix (`.github/workflows/release.yml`)**: Trigger releases strictly on `v*.*.*` tags (and `workflow_dispatch`) with concurrency serialization instead of firing duplicate parallel builds for both `main` and the version tag (`HTTP 422: Release.tag_name already exists`).

## 0.4.6 (2026-11-27)
- **Themed Headphones & Full Windows/macOS/Linux Audio Sync**: Added optional **Headphones (Music Notes)** toggle across Windows, macOS (`CoreAudio`), and Linux (`/proc/asound` + PulseAudio/PipeWire `pactl`), sharing `MascotCore.UpdateMusicPlaybackState` hysteresis and static cached brushes:
  - **Classic**: Metallic slate band, midnight cushions, sky-blue accent strips, and cyan/gold musical notes.
  - **Dark Remake**: Monochrome graphite/obsidian/silver headphones and monochrome notes matching the Dark Remake palette.
- **Smart Z-Order Perch & Startup Path Sync (PR #10)**: Keeps Agy perched on Antigravity on Windows while clicking the tray or working side-by-side whenever the target spot remains visible (`IsTargetVisible`), and syncs moved/updated executable startup paths (`SyncStartupIfEnabled`).
- **Updated Landing Page & Docs**: Added interactive **Headphones** preview toggle to [site/index.html](site/index.html) and updated [README.md](README.md) with all cross-platform features and build commands.

## 0.4.5 (2026-11-27)
- **macOS Menu Bar + Right-Click Parity**: Merged true Universal `lipo` packaging (`tools/package-macos.sh`), monochrome menu bar template icon (`RenderMacMenuBarPng` + `MacOSProperties.SetIsTemplateIcon`), `agy`/`agy-cli`/`antigravity-cli` process detection, and `"Dock"` terminology while keeping both the top menu bar icon and mascot right-click (including `Ctrl+Click`) synchronized live (`StateChanged`).
- **Default Above-Dock Without Hard-Override**: Set `"bottom-center"` as the first-run/fallback macOS default in `PetSettings` (`DefaultMinimizedCornerFor`) instead of overriding explicit user choices in `Location When Away / Minimized` when Antigravity is closed.

## 0.4.4 (2026-11-27)
- **macOS & Linux Right-Click Submenu Crash & Overlay Fix**: Stopped `OpenSubmenu` from re-parenting a previously attached `StackPanel` when hovering back and forth across submenus or clicking a submenu row (building fresh submenu controls per open and detaching closed menu contents cleanly), coordinated menu deactivation timers across `menuWindow` and `submenuWindow`, ignored AgyPet's own windows in `PollMacOs` / `PollLinux`, and preserved macOS `NSWindow` overlay ordering (`setHidesOnDeactivate: false` + `orderFrontRegardless`) when menus deactivate or close.
- **Taskbar Preview Sizing Fix**: Kept `Preview Mood` at the same taskbar-fitted pixel size when Antigravity is minimized and docked in the taskbar instead of swelling back up to the larger in-app slider size.

## 0.4.3 (2026-11-27)
- **Anchored Eyes & Live Pupil Tracking**: Kept eye sockets firmly anchored in place while pupils smoothly track your cursor in `Idle` / `Listening` and animate dynamically across `Thinking`, `Reading`, `Working`, `Terminal`, `Searching`, `Browsing`, `Multitasking`, `Delegating`, `Speaking`, `Typing`, `Alert`, and `Question` in both `Classic` and `Dark Remake` skins.
- **Clean Eyebrow-Free Focus & Proportional Speaking Mouth**: Removed floating eyebrows from focused working/terminal states and resized `Speaking` mouth frames (`0.72×0.24` closed / `0.86×0.42` open) to sit cleanly between the eyes above the inner arch cutout.
- **Smaller Self-Contained Linux & macOS Binaries (~53% Smaller)**: Added compile-time JSON source generation (`PetSettingsJsonContext`) and partial IL trimming (`PublishTrimmed=true`, `TrimMode=partial`) in release builds, shrinking Linux binaries from `~42 MB` to `~19.7 MB` and cutting Universal macOS `.dmg` size roughly in half with zero runtime overhead.

## 0.4.2 (2026-11-27)
- **Tight Mascot Hit-Testing & Click-Through**: Moved click, drag, hover, and context-menu hit-testing strictly onto the `11*px × 9*px` mascot body (`stage`) and left surrounding container padding (`petHitArea`) and floating props (`air`) completely click-through (`0x00` alpha / `IsHitTestVisible = false`), so clicks anywhere around the mascot pass cleanly to underlying apps and taskbar buttons.
- **Typing & Reading Eye Fixes**: Fixed `Typing` to render symmetric blinking eyes (`DrawEyes` / `DrawFocusEyes`) instead of shifted `shortEyes`, and bounded `shortEyes` (`Reading`) white glints and dark pupils inside `[ly .. ly + 0.5]` so they no longer poke outside the eye bounds.
- **Precise Continuous Size Slider (`2.4`–`7.2 px`, `0.1 px` step)**: Replaced fixed `Small` / `Medium` / `Large` presets with persisted continuous `PixelSize` (`DefaultPixelSize = 4.2`, `ResolveEffectivePixelSize` taskbar scaling) and live sliders across Windows right-click & tray menus (`Slider` / `TrackBar`), macOS/Linux right-click `Slider` + precision tray steps, and the interactive website playground (`#size-slider`), plus `Reset Default (100% · 4.2 px)`.

## 0.4.1 (2026-11-27)
- **Instant Transcript Reaction Sync**: Fixed `ParseTranscriptLines` skipping past `GENERIC` tool-result records so `edit_file` (`Working`), `read_file` (`Reading`), and `run_command` (`Terminal`) stay accurate instead of falling back to `Thinking`, and tightened `MinBusyDwellMs` (`280ms`) and monitor wakeup debounce (`20ms`) for crisp animation switching.
- **Per-Chat Switching & Multitasking Fixes**: Scoped UIA chat header detection to the active composer pane (`ResolveChatPaneRoot`), prioritized exact/prefix title matching before partial substring hits, woke `ActivityMonitor` immediately on focus change, and enabled `Multitasking` when `2+` sessions run while pinned above the taskbar or viewing overview/Settings screens.
- **Sleep, Typing Wake & Click Polish**: Typing or voice activity now wakes the mascot cleanly to `Idle` (returning to `Sleeping` after 6 minutes of inactivity); waking clicks trigger a brief annoyed `> <` poke, single clicks play a subtle tactile squish (`Press()`), and 3+ rapid clicks play the leg-hop dance (when `Idle`) or wince (when busy) before resetting cleanly to single-press behavior.
- **Dark Skin Multi-Session Parity**: Synced Dark Remake trailing orbit pixels (`MascotView.cs`), monochrome website hover progress fills, and dedicated Windows + `.NET 8` parity tests.
- **Website CLI Simulation**: Replaced static mock bubbles in **agy CLI Terminal** with a standalone interactive terminal window (`PS C:\...\AgyPet> agy`), command execution (`/help`, `/model`, `/approval`, `clear`, `Tests`), and 5-edge CLI mascot perching.

## 0.4.0 (2026-11-27)
- **macOS & Linux Support (One-File Install & Play)**:
  - Added cross-platform desktop companion (`cross/`) using `.NET 8` + Avalonia sharing 100% of `MascotCore`, `ActivityMonitor`, `PetSettings`, `Updater`, and `Json` across all OSes.
  - **macOS Universal (`AgyPet-macOS.dmg`)**: Single disk image bundling both `osx-arm64` and `osx-x64` under a Universal Mach-O launcher inside `AgyPet.app` (`LSUIElement = true`, menu bar tray, CoreGraphics frontmost window & cursor tracking, `~/Library/LaunchAgents` startup).
  - **Linux (`AgyPet-linux-x64` & `AgyPet-linux-arm64`)**: Single self-contained executable ELF binaries with embedded SQLite (`e_sqlite3`), `libX11` window/cursor tracking with Wayland process fallback, System Tray / StatusNotifierItem, and `~/.config/autostart` login support.
  - **Website & Multi-OS Releases**: Added OS-aware single-file download buttons in [site/index.html](site/index.html) and parallel Windows, Linux (`x64`/`arm64`), and macOS (`arm64`+`x64`) release builds with SHA-256 checksums.

## 0.3.8 (2026-11-27)
- **Website Design System Across Desktop UI**: Styled the right-click context menu (including nested submenus) and updated the hover progress card and feedback dialog to match the website's OKLCH hue-250 dark surfaces, alpha borders, concentric `14px`/`9px` radii, and `QuarticEase` hover scale + lift entrance/exit motion.
- **Taskbar Auto-Sizing with Breathing Room & Empty-Space Placement**: Automatically sizes the mascot to fit the Windows taskbar height with `>= 6` DIP buffer (`large` on `>= 52.8`-DIP bars, `medium` on `48`-DIP Windows 11 bars, `small` on thinner bars) when outside the app and restores your saved size inside Antigravity. Scans `Shell_TrayWnd` so `taskbar-left`, `taskbar-center`, and `taskbar-right` sit in open taskbar space clear of Widgets, app icons, system tray icons, and the clock.
- **Dead-Center Hover Card**: Replaced WPF `Popup` with a non-activating transparent `Window` (`WS_EX_NOACTIVATE`) immune to Windows `MenuDropAlignment` flipping and `SM_CXMINTRACK` width stretching so hover cards sit dead-center above the mascot.
- **Working Multi-Press Wince**: Rapid multi-clicks (`3+` within `2.2s`) now trigger the brief `> <` wince while working as well as when `Idle`, while `1–2` clicks stay calm when busy and hop playfully only when `Idle`.

## 0.3.7 (2026-11-27)
- **Leg-Hop Dance & Original Rapid-Click Wince (Idle Only)**: Replaced the swaying click dance with an alternating leg-hop animation on single-click, restored the original brief wince (`> <` + subtle squish) on 3+ rapid clicks instead of the annoyed glare, and restricted both click animations strictly to when AgyPet is `Idle`.
- **Body-Matched Animated Limbs**: Updated every animated hand, raised arm, and lifted leg across moods (`Idle` hop, `Thinking`, `Question`, `Typing`, `Working`, `Terminal`, `Delegating`, `Multitasking`, `Alert`, `Done`, `Error`) so they sample the exact `Idle` body gradient color at each limb position in both `Classic` and `Dark Remake` skins.

## 0.3.6 (2026-11-27)
- **Single-Click Dance & Rapid-Click Annoyed Reaction**: Single-clicking AgyPet triggers a calmer, gentle dance groove (without stacking frame speedups), while clicking rapidly 4+ times makes him annoyed (`\ /` glare + pulsing mark).
- **Cursor-Following Idle Eyes**: When `Idle`, AgyPet's pupils subtly track your mouse cursor smoothly across the screen while still blinking naturally.
- **Tighter Floating Props & Gentler Dance**: Reduced dance bob/sway amplitude and pulled floating icons (clouds, orbs, sparkles, speech dots, globe, server rack, cards, and lightning) closer in toward the mascot.
- **Keyboard Safety & Focus Guard**: Added `WM_MOUSEACTIVATE` (`MA_NOACTIVATE`) hook on `PetWindow` and switched typing detection strictly to non-destructive `0x8000` key-down state (ignoring `Win` key chords as well as `Ctrl`/`Alt`) so clicking never drops key-up events or triggers repeating keys.

## 0.3.5 (2026-11-27)
- **Dynamic Multi-Session Orbiting Orbs**: `Multitasking` now juggles one orbiting orb per active session (`2`–`8` orbs spaced evenly overhead, matching live `ActiveSessions.Count` while defaulting to `3` in Preview Mode).
- **Updated Landing Page**: Added interactive `agy CLI Terminal` simulator tab plus explicit coverage of Antigravity IDE, App, `agy` CLI perching, inner prompt alignment, Pin Above Taskbar, Lock Position, Auto-Hide, Wake-on-Nudge, and dynamic session orbs.

## 0.3.4 (2026-11-27)
- **Inner Prompt Box Perch & Project Header Exclusion (PR `#8` by `@Dunya2200`)**:
  - Excluded Antigravity's top `"No Project ⌄"` / project selector header bar from composer bounds (`WindowTracker`) while keeping attachments inside the box, so AgyPet sits solidly on the inner prompt input box border.
  - Added **Pin Above Taskbar (Stay in Place)** and **Lock Position (Disable Dragging)** options in both the Right-Click and System Tray menus (both default to `false`), clearing prior free-drag overrides when pinning above the taskbar.

## 0.3.3 (2026-11-26)
- **Auto-Hide When Inactive or Minimized (`#3` / PR `#7` by `@Bobdo-b`, requested by `@Manufacturing-Downtime`)**:
  - Added optional **Auto-Hide** settings (`Hide When Inactive (App & CLI Closed)` and `Hide When Minimized`) under both the Right-Click and System Tray menus (both default to `false` to preserve existing behavior).
  - Automatically hides the mascot and hover card when enabled conditions apply, stays visible during `Preview Mode` or open menus, and snaps cleanly back to position when reappearing.

## 0.3.2 (2026-11-26)
- **Chatbox Corner Perch Alignment (`#6`)**: Increased the horizontal right-edge inset on `PlaceAboveChatBox` from `14px` to `28px` (scaling safely on narrow boxes) so AgyPet's right foot sits squarely on the flat top border of Antigravity's chatbox instead of hanging off the rounded top-right corner—especially noticeable on `Small` size.

## 0.3.1 (2026-11-26)
- **Wake-on-Nudge from Sleep**: Clicking or nudging AgyPet while `Sleeping` wakes it up immediately to `Idle` (`"At rest · Ready"`) with a subtle startle blink and resets the 6-minute idle sleep timer.
- **Improved Per-Chat Reaction Matching**: Added UIA active-pane header and selected-conversation probing (`ActiveChatCandidates`) on top of window titles, ellipsis-prefix matching (`…`), App-vs-CLI focus isolation, `New conversation` detection, per-session `Done` tracking, and 0ms chat-switch updates.
- **Outside-Click Menu Dismissal & Accessibility**: Fixed the right-click context menu on the `WS_EX_NOACTIVATE` overlay so clicking anywhere outside the menu/submenus or pressing `Esc` closes it cleanly; added `Esc`/`Ctrl+Enter` shortcuts in Feedback and screen-reader `AutomationProperties.Name` metadata.
- **Website UX & Accessibility Polish**: Synced all copy and metadata to 18 live reactions, added skip-link, keyboard tablist navigation, `aria-live` state announcements, persistent skin preference, and sleep wake-on-nudge parity in the interactive simulator.

## 0.3.0 (2026-11-26)
- **CLI Terminal Placements (`agy` CLI Support)**:
  - Added **CLI Terminal Placement** options (`Bottom-Right (CLI Prompt)`, `Bottom-Middle (Prompt Center)`, `Bottom-Left (Above Prompt)`, `Top-Right (Window Edge)`, `Top-Left (Window Edge)`, and `Off`) under Right-Click / System Tray.
  - Automatically detects terminal windows actively hosting `agy.exe` (`WindowsTerminal.exe`, `pwsh.exe`, `powershell.exe`, `cmd.exe`, `wezterm-gui.exe`, `alacritty.exe`, `mintty.exe`, etc.), perches right beside the CLI prompt or window edge, and prioritizes live `Antigravity CLI` session updates + user keystroke `Typing`.
- **18 Original Live Reactions**:
  - Removed the `Happy` celebration state and turn-sentiment crying/steaming overlays along with the `0.2.0`–`0.2.3` prompt-sentiment & quota regex code adapted from Atom, leaving 100% original code and animations in `0.3.0`.

## 0.2.3 (2026-11-26)
- **Distinctive Visual Overhaul for AgyPet Reactions**:
  - **Thinking:** Hand-on-chin pondering pose with a pulsing neural thought cloud overhead (Antigravity arch silhouette instead of capsule/orbits).
  - **Delegating:** Streams dispatch packets from AgyPet's outstretched hand into twin stacked subagent worker cards.
  - **Happy:** Rosy pixel blush cheeks + twinkling 4-point Antigravity Aurora stars.
  - **Error:** Dizzy glitch squint eyes (`> <`) with glitch shake and a flashing red lightning bolt.
  - **Angry / Sad Turn Sentiments:** Steam puffs and falling pixel teardrop overlays.

## 0.2.2 (2026-11-26)
- **Rock-Steady Mascot Posture**: Removed continuous vertical sine-wave oscillation across resting and active states so AgyPet stays planted cleanly on the chatbox border, corner edge, and taskbar (while keeping celebratory hops for `Done` and double-click `Dance`).
- **Mobile Carousel Swiping & Refreshed Media**: Fixed horizontal touch swiping (`touch-action: pan-x pan-y`) on the landing page reactions strip.

## 0.2.1 (2026-11-26)
- **Along-the-Taskbar & Bottom-Middle Dock Locations** (suggested by `@Bobdo-b` in #1): Added **Taskbar · Left**, **Taskbar · Middle**, **Taskbar · Right**, and **Bottom-Middle (Above Taskbar)**.

## 0.2.0 (2026-11-26)
- *Historical Attribution Note:* Turn-sentiment moods, per-model quota sleep, and delegation concepts in `0.2.0`–`0.2.3` (commits `f339d2c`–`5db1b17`) were inspired by [Atom](https://github.com/Arnav8452/atom), and prompt sentiment detection in `0.2.0`–`0.2.3` was adapted from Atom's code ([MIT License](https://github.com/Arnav8452/atom/blob/main/LICENSE), Copyright (c) 2026 Antigravity Atom Contributors). Both were removed in `0.3.0`.
- **19 live reactions** (added `Delegating` for subagents/swarms/workflows, `Happy` rising pixel hearts on user praise, and `Error` with `X X` eyes and red `!` badge).
- **Sustained Turn Sentiment Moods (`Angry` & `Sad`)**: Detects hostility/ALL-CAPS shouting (`Angry` crimson tint + `\ /` brows) and insults (`Sad` muted-blue tint + `/ \` brows) across active working steps until the turn ends, while cleanly ignoring code blocks, URLs, file paths, and SQL queries.
- **Per-Model Quota Sleep & Instant Wakeup**: Enters `Sleeping` (`Quota limit · Sleeping`) when hitting 429 / quota limits and wakes immediately once you switch models or start a new turn.
- **Bottom-Right & Bottom-Left Above-Taskbar Corners**: Added `Bottom-Right (Above Taskbar)` and `Bottom-Left (Above Taskbar)` options under **Corner When Away / Minimized** (available in both the right-click menu and system tray) with trimmed bottom hit-padding so taskbar and upper chatbox clicks aren't blocked.
- **Instant `FileSystemWatcher` Wakeup & Anti-Flicker Dwell**: Watches Antigravity transcript/DB changes natively for sub-100ms reaction updates, with a `1.2s` minimum busy dwell so rapid tool sequences don't flicker.
- **Double-Click Dance**: Double-click the mascot when calm (`Idle` / `Done` / `Happy`) for a joyful dance groove while keeping single-click above the chatbox as the subtle press wince.
- **Browser False-Positive Fix**: Removed loose window-title fallback matching so web browsers viewing Antigravity pages (e.g., Reddit/docs) never trigger fake chatbox perching or typing.

## 0.1.0 (2026-11-25)
- Initial public release of **AgyPet** — standalone floating pixel companion for Google Antigravity App, Antigravity IDE, and Antigravity CLI (`agy`).
- **16 live reactions**: `Typing`, `Thinking`, `Working`, `Terminal`, `Browsing`, `Reading`, `Searching`, `Listening`, `Speaking`, `Multitasking`, `Background`, `Alert`, `Question`, `Done`, `Idle`, and `Sleeping`.
- **Two official skins**: `Classic Official` (radial official Antigravity colors) and `Dark Remake` (monochrome obsidian).
- **Per-chat, typing, dictation & Voice Mode awareness**: Automatically tracks whichever conversation is currently open, detects typing, dictation, and distinguishes Listening vs Speaking in Voice Mode.
- **Adaptive chatbox perch & smooth 60fps glide**: Follows attachments and multi-line prompt growth across `Small`, `Medium`, and `Large` sizes; parks calmly in the corner when minimized, Alt+Tabbed away, or inside Settings.
- **Scrollable multi-session hover card**: Displays exact step percentages (`% done / % left`) across all active sessions when outside Antigravity.
- **Built-in GitHub Auto-Updater & Feedback**: Automatically checks `yvs1n/agypet` releases and submits feedback straight to GitHub Issues.
