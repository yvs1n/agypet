# Changelog

## 0.2.1 (2026-11-26)
- **Along-the-Taskbar & Bottom-Middle Dock Locations** (suggested by `@Bobdo-b` in #1):
  - Added **Taskbar · Left**, **Taskbar · Middle**, and **Taskbar · Right** — docks AgyPet vertically centered directly inside the Windows taskbar strip (works for bottom, top, and auto-hide taskbars, with automatic `HWND_TOPMOST` layering).
  - Added **Bottom-Middle (Above Taskbar)** alongside **Bottom-Left** and **Bottom-Right**.
  - Updated hover card placement to pop cleanly **above** the mascot whenever docked at the bottom or along the taskbar so status details never clip behind the taskbar.

## 0.2.0 (2026-11-26)
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
