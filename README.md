<div align="center">

<h1>AgyPet</h1>

<p><strong>A live, zero-bloat pixel companion for Google Antigravity App, Antigravity IDE, and Antigravity CLI (<code>agy</code>).</strong><br/>Single-file run across <strong>Windows</strong>, <strong>macOS Universal</strong>, and <strong>Linux</strong> — zero Electron patches, zero telemetry.</p>

<p>
  <a href="https://github.com/yvs1n/agypet/releases/latest"><img src="https://img.shields.io/github/v/release/yvs1n/agypet?label=Download&color=1E78FF" alt="Latest Release" /></a>
  <a href="https://github.com/yvs1n/agypet/actions/workflows/ci.yml"><img src="https://github.com/yvs1n/agypet/actions/workflows/ci.yml/badge.svg" alt="CI" /></a>
  <a href="https://github.com/yvs1n/agypet/actions/workflows/release.yml"><img src="https://github.com/yvs1n/agypet/actions/workflows/release.yml/badge.svg" alt="Release" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-4EC446.svg" alt="License: MIT" /></a>
</p>

<p>
  <a href="https://github.com/yvs1n/agypet/releases/latest"><strong>Download Latest Release</strong></a>
  &nbsp;·&nbsp;
  <a href="site/index.html"><strong>Interactive Website &amp; Preview</strong></a>
  &nbsp;·&nbsp;
  <a href="#all-18-live-reactions"><strong>18 Reactions</strong></a>
  &nbsp;·&nbsp;
  <a href="#build-from-source"><strong>Build from Source</strong></a>
</p>

<br/>

<img src="docs/agypet-preview.gif" alt="AgyPet Live Preview perched inside Google Antigravity" width="820" />

</div>

---

## Quick Download (One-File Run)

Grab the single executable for your OS from [`yvs1n/agypet` Latest Release](https://github.com/yvs1n/agypet/releases/latest) — no external runtimes or plugins required:

| Platform | Download | Notes |
| :--- | :--- | :--- |
| **Windows 10 / 11** | **[`AgyPet.exe`](https://github.com/yvs1n/agypet/releases/latest/download/AgyPet.exe)** | ~160 KB portable WPF binary (built-in `.NET 4.x`; click *More info → Run anyway* if SmartScreen prompts) |
| **macOS (Apple Silicon & Intel)** | **[`AgyPet-macOS.dmg`](https://github.com/yvs1n/agypet/releases/latest/download/AgyPet-macOS.dmg)** | True `lipo` Universal `.app` with monochrome Menu Bar template icon & right-click support |
| **Linux (`x64` / `arm64`)** | **[`AgyPet-linux-x64`](https://github.com/yvs1n/agypet/releases/latest/download/AgyPet-linux-x64)** · **[`AgyPet-linux-arm64`](https://github.com/yvs1n/agypet/releases/latest/download/AgyPet-linux-arm64)** | Self-contained trimmed binary (~20 MB), zero FUSE or extra libraries required |

```bash
# Linux quick run
chmod +x AgyPet-linux-x64 && ./AgyPet-linux-x64
```

> AgyPet checks GitHub Releases right from its tray / menu bar (**Check for Updates**) and automatically updates your login startup entry if you move or replace the binary.

---

## Two Official Pixel Skins

Switch live at any time from the right-click menu, Menu Bar, or System Tray — zero restart required:

| Classic Official | Dark Remake |
| :---: | :---: |
| ![Classic Official Skin Showcase](docs/showcase-classic.gif) | ![Dark Remake Skin Showcase](docs/showcase-dark.gif) |
| *Official Antigravity radial gradient with sky-blue/gold headphones & accents* | *Obsidian-to-graphite monochrome arch with luminous rim & silver headphones* |

---

## Highlights

- **Zero-Patch Chatbox & CLI Perching**: Floats cleanly above the top-right edge of Antigravity's prompt box (including multi-line inputs & attachments) or attaches to **`agy` CLI** terminals across Windows Terminal, iTerm2, Ghostty, PowerShell, CMD, Alacritty, WezTerm, Warp, Hyper, Tabby, and Mintty.
- **Fullscreen Auto-Hide (Windows, macOS & Linux)**: Automatically hides whenever another app (YouTube, games, media players, presentations) goes fullscreen on the same screen, while staying visible & properly bounded when Antigravity or `agy` CLI itself is fullscreen.
- **Tactile Petting, Dance & Anchored Gaze**: Single-clicking gently squishes Agy with planted feet and a floating pixel heart/sparkle; 3× rapid clicks dance in `Idle` (or wince `> <` while busy). Pupils smoothly track your cursor in `Idle` and `Listening`.
- **Headphones & Live Music Notes**: Optional **Headphones (Music Notes)** toggle renders theme-matched pixel headphones and eighth-note (`♪`) / beamed notes whenever system audio plays (WASAPI on Windows, `CoreAudio` on macOS, ALSA/`pactl` on Linux), suppressed during Voice Mode.
- **Multi-Session Juggling & Scrollable Hover Card**: When away or minimized with multiple chats/CLIs running, AgyPet switches to `Multitasking` (orbiting session orbs) and hovering reveals a scrollable progress card (`% done / % left`).
- **Continuous Scaling & Smart Gliding**: Resize continuously from `2.4 px` to `7.2 px` (`60%–171%`), park in screen corners or empty taskbar/Dock gaps, or pin above the taskbar while keeping right-click parity everywhere.

---

## How It Works

```text
┌───────────────────────────┐      Read-only watcher       ┌───────────────────────────────┐
│  Google Antigravity App   │  (Local SQLite + transcripts) │         AgyPet Overlay        │
│  Antigravity IDE / agy    │ ─────────────────────────────▶│  • 18 real-time reactions     │
└───────────────────────────┘                               │  • App / CLI / Taskbar perch  │
                                                            │  • Fullscreen & Audio smart   │
  Zero Electron / CDP injection · Zero telemetry            └───────────────────────────────┘
```

1. **Read-Only Local Listeners**: File watchers detect Antigravity transcript updates in under `100 ms` with minimum busy-dwell smoothing so rapid tool chains never flicker.
2. **Focus & Z-Order Aware**: Matches the focused chat tab or `agy` terminal window, stays perched during side-by-side work (`IsTargetVisible`), and yields cleanly whenever fullscreen apps take over.

---

## All 18 Live Reactions

| Reaction | Trigger in Antigravity | Reaction | Trigger in Antigravity |
| :--- | :--- | :--- | :--- |
| **Typing** | Streaming reply or you typing in prompt / CLI | **Speaking** | Antigravity speaking in Voice Mode |
| **Thinking** | Planner reasoning before tool calls | **Multitasking** | Away / minimized with 2+ active sessions |
| **Working** | `edit_file`, `write_file`, code edits | **Background** | Chat finished with live dev server running |
| **Terminal** | `run_command` / shell execution | **Alert** | Permission or confirmation needed |
| **Browsing** | Live browser agent & DevTools | **Question** | `ask_question` awaiting your choice |
| **Delegating** | `invoke_subagent`, `swarm`, `run_workflow` | **Done** | Turn finished celebration |
| **Reading** | `read_file`, `grep`, `glob`, `list` | **Error** | Tool or turn failure |
| **Searching** | `search_web`, `search_marketplace`, `fetch` | **Idle** | Ready at rest |
| **Listening** | Dictation mic active / user speaking in Voice | **Sleeping** | Quiet after extended idle (click to wake) |

---

## Controls

- **Left-click mascot**: Gentle bottom-anchored petting squish with floating heart & sparkle (wakes up if `Sleeping`). Click **3× rapidly** to make Agy dance (`Idle`) or wince (`> <` when busy).
- **Left-drag**: Freely reposition anywhere on screen (snaps back above the chatbox next time you focus Antigravity).
- **Hover (when outside Antigravity)**: Opens the scrollable multi-session progress card.
- **Right-click / Menu Bar / System Tray**:
  - Switch **Skin** (`Classic` / `Dark Remake`), toggle **Headphones (Music Notes)**, or **Preview Mood** (`18` states).
  - Configure **Location When Away / Minimized** (`Corners` or `Taskbar / Dock · Left · Middle · Right`), **Pin Above Taskbar / Dock**, **Lock Position**, and **CLI Terminal Placement**.
  - Set **Auto-Hide** (`Hide When Inactive`, `Hide When Minimized`, `Hide When Other App Is Fullscreen`), adjust **Size** (`2.4–7.2 px`), toggle **Launch on Login**, **Check for Updates**, or **Send Feedback**.

---

## Build from Source

**Windows** builds with zero SDK installs using the built-in `.NET Framework 4.x` C# compiler:

```powershell
.\build.ps1            # Build bin\AgyPet.exe
.\build.ps1 -Test      # Run all Windows unit tests
.\build.ps1 -Showcase  # Render docs\showcase-*.png & site\og-screen.png
```

**macOS & Linux** use the shared `.NET 8` + Avalonia project under `cross/`:

```bash
dotnet run --project cross/tests/AgyPet.Cross.Tests.csproj -c Release  # Cross-platform unit tests
dotnet build cross/AgyPet.Cross.csproj -c Release                       # Release build
```

---

## Feedback & Credits

- **Bug reports or reaction ideas**: **[Open a GitHub Issue](https://github.com/yvs1n/agypet/issues/new/choose)**, use **Send Feedback...** in the tray menu, or submit from the **[landing page](site/index.html#feedback)**.
- Made by **[Yassin Ragab](https://yassinr.me)** for **[Google Antigravity](https://antigravity.google)**, built with Antigravity · **[MIT License](LICENSE)** *(see [CHANGELOG.md](CHANGELOG.md) for historical `0.2.0`–`0.2.3` attribution)*.
