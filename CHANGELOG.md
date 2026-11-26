# Changelog

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
