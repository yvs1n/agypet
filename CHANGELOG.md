# Changelog

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
