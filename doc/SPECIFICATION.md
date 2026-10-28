# WinGlance - Specification

## Context

When working with multiple windows of the same application (e.g., VS Code), the only way to see thumbnail previews is by hovering over the taskbar icon — a transient popup that disappears immediately. **WinGlance** is a persistent floating panel that displays live thumbnail previews of all windows belonging to monitored applications, enabling **one-click window switching**.

---

## Functional Requirements

### FR-1: Floating Thumbnail Panel

- Display a **floating, always-on-top window** showing live thumbnail previews of all windows belonging to monitored applications
- The panel must remain visible at all times, even when other windows have focus
- Thumbnails must be **live** (real-time rendering of actual window content via DWM, not static screenshots)
- Default layout: **horizontal strip** (like the Windows taskbar hover preview)

### FR-2: One-Click Window Switching

- Clicking on a thumbnail must immediately **bring the corresponding window to the foreground** and give it focus
- If the window is minimized, it must be restored before receiving focus
- The activated window is then outlined by a **red frame blinking three times** (about one second) over its visible bounds (DWM extended frame bounds), on its own monitor: a click-through, non-activating topmost overlay, created off-screen at startup so that the first flash is immediate. Setting: `highlightOnSwitch` (default: on)
- A short stereo **click** is played from the side of the window's monitor: monitors are ordered by the horizontal position of their center and spread from hard left (-1) to hard right (+1), so with three monitors the click is left, center or right. The click (25 ms, 2 kHz sine damped with a 2.5 ms time constant: a dry "tic", discreet in an open office) is generated in memory as a WAV file with constant-power panning. Setting: `clickOnSwitch` (default: on)

### FR-3: Dynamic Window Tracking

- Automatically detect when new windows of monitored applications are **opened** and add their thumbnails
- Automatically remove thumbnails when windows are **closed**
- Polling interval: ~1 second (configurable)
- **UWP/Store app support**: windows running under `ApplicationFrameHost.exe` must be correctly identified by resolving the actual package/app name (via `GetApplicationUserModelId`) rather than showing "ApplicationFrameHost"

### FR-4: Window Title Display

- Show the **window title** (truncated if needed) below each thumbnail
- The text is built from a template, `titleFormat` (default `{project} - {document}`), where `{title}`, `{project}` (agent folder, otherwise the title segment before the application name, empty for a title of fewer than three segments), `{document}` (first title segment) and `{app}` (display name) are replaced; a document equal to the project is not repeated, separators left at either end are removed, and an empty result falls back to the title. The full title stays in the tooltip. Set in Settings > Layout, applied live
- Show the **application icon** on each thumbnail for multi-app identification
- Highlight the **currently active/focused** window's thumbnail with a visual indicator (border color, glow)

### FR-5: Views and Navigation

The panel has **4 views**, switched from a **right-click context menu** on the panel: Preview, Applications, Settings and Prompt Editor (see FR-10). They were first designed as 3 tabs; the "Tab" names below and the mockups keep that original wording.

#### Tab 1 — Preview (main tab)

- Displays all live thumbnails for all monitored applications
- Thumbnails are grouped by application (with app name/icon as section header)
- Click on any thumbnail to switch to that window

#### Tab 2 — Applications

- Lists all **currently running applications** (with window count)
- Checkbox to **add/remove** an application from the monitored set
- Already-monitored apps are checked; monitored apps that are not running are listed too (0 windows), so saving keeps them
- Editable **display name**, used as the group header in Preview
- **Save** button to persist the monitored app list to config
- The list is scanned when the view opens; a Refresh button re-scans it

#### Tab 3 — Settings

- **Layout**: fit to panel (default) / horizontal strip / vertical strip / grid
- **Thumbnail size**: width x height (slider or numeric input)
- **Panel opacity**: slider (30%-100%)
- **Panel position**: X / Y coordinates (or "remember last position" checkbox)
- **Polling interval**: slider (500ms-5000ms)
- **Auto-start with Windows**: checkbox
- **Close button action**: checkbox — minimize to tray (default) or exit
- **Global hotkey**: key combination input with **Record** button (default: Ctrl+Alt+G)
- **LLM Analysis** section:
  - **Enabled**: checkbox (default: off)
  - **Provider**: dropdown — OpenAI / Gemini / Ollama
  - **API endpoint**: text field (pre-filled per provider)
  - **API key**: password field
  - **Model**: text field
  - **Stale threshold**: slider (10s–120s, default: 30s)
  - **Edit prompt**: button that opens the Prompt Editor view
- **Save** button to persist all settings

### FR-6: Panel Behavior

- The panel must be **draggable** (user can reposition it anywhere on screen)
- The panel must **remember its position** between sessions (persist to config file)
- The panel should be **resizable**, with thumbnails scaling proportionally: in the **fit to panel** layout, the thumbnail size is the largest at which all windows fit (rows wrapping at the panel width, a header row per application, the vertical scrollbar width kept free), computed by binary search; the cell aspect ratio is the median of the windows' client-area ratios, and the panel size is saved (`panelWidth`, `panelHeight`)
- A **minimize to tray** option to hide the panel when not needed
- The **close button [×]** behavior is **configurable**: either minimize to tray (default) or exit the application
- System tray icon with right-click menu (Show/Hide, Settings, Exit)
- **Single instance**: only one instance of WinGlance can run at a time

### FR-7: Global Hotkey

- A **configurable global keyboard shortcut** toggles panel visibility (show/hide)
- Default hotkey: **Ctrl+Alt+G**
- Implemented via `RegisterHotKey` / `UnregisterHotKey` Win32 APIs
- The hotkey works system-wide, even when WinGlance does not have focus
- Hotkey is configurable in Tab 3 — Settings

### FR-8: Error Handling & Resilience

- If **DWM is disabled** (e.g., remote desktop session or legacy configuration), show a clear error message at startup and exit gracefully
- If a **monitored process crashes** or a window handle becomes invalid, remove the corresponding thumbnail silently without crashing
- If `SetForegroundWindow` fails (Windows **focus-stealing prevention**), use `AllowSetForegroundWindow` or the `Alt` key simulation workaround
- If `config.json` is **corrupt or unreadable**, fall back to default settings and log a warning

### FR-9: Window Attention Detection

- Detect when a monitored window **requires user attention** and display a visual indicator on its thumbnail (pulsating border, attention icon)
- Three detection mechanisms:
  - **Flashing window**: use `RegisterShellHookWindow` to receive `HSHELL_FLASH` notifications when a window starts flashing in the taskbar (e.g., file transfer complete, message received)
  - **Not responding**: use `IsHungAppWindow` to detect windows that have stopped processing messages — display a "(Not Responding)" overlay on the thumbnail
  - **Modal dialog**: use `IsWindowEnabled` to detect when a main window is blocked by a modal dialog (e.g., "Save changes?"), and `GetWindow(GW_ENABLEDPOPUP)` to identify the popup
- The attention indicator must clear automatically when the condition resolves (flash stops, app resumes, dialog is dismissed)

### FR-10: LLM-Assisted Analysis

Extends FR-9 with intelligent window analysis via an external LLM.

#### Inactivity Detection (graphical diff)

- Periodically capture a screenshot of each monitored window (via `PrintWindow` API)
- Compare with the previous capture using **perceptual hashing** (pHash) to detect visual changes
- If a window's content has **not changed for more than 30 seconds**, it is flagged as **stale**

#### LLM Analysis Trigger

- When a window is flagged as stale, a **single LLM call** is made with:
  - The window **screenshot** (if the model supports vision) or a description of the window title/state
  - The window **title**
  - A **system prompt** stored in `config.json` (`llm.prompt`) and edited in the Prompt Editor view
- The LLM determines whether the window:
  - **Awaits user action** (dialog, error, confirmation, input field) → highlight thumbnail with **attention indicator** (orange pulsating border)
  - **Is simply idle** (nothing happening, no action needed) → **dimmed thumbnail**
- Only **one LLM call per stale window** — not repeated until the window content changes again
- A failed call (network, HTTP error, timeout, unrecognized answer) gives **no verdict**: the error is logged and shown in the debug log, and the call is retried after one minute
- Minimized and not-responding windows are not captured

#### Supported LLM Providers

- **OpenAI** — API-compatible (GPT-4o, GPT-4o-mini with vision)
- **Google Gemini** — API-compatible (Gemini Pro/Flash with vision)
- **Ollama** (local) — for privacy-conscious users, local models with optional vision support
- Provider, endpoint, API key, and model name are configurable in Tab 3 — Settings
- The prompt is stored in `config.json` and editable via the in-app **Prompt Editor** screen

#### Configuration (Tab 3 — Settings)

- **LLM enabled**: checkbox (default: disabled)
- **Provider**: dropdown — OpenAI / Gemini / Ollama
- **API endpoint**: text field (pre-filled with default for each provider; editable for Ollama custom URL)
- **API key**: password field (not needed for Ollama); stored encrypted with DPAPI for the current Windows user (`apiKeyProtected`)
- **Model**: text field (e.g., `gpt-4o-mini`, `gemini-2.0-flash`, `llava`)
- **Stale threshold**: slider (10s–120s, default: 30s)
- **Edit Prompt & Debug Log**: button that navigates to the Prompt Editor screen

#### Prompt Editor Screen

- Dedicated screen accessible from Settings ("Edit Prompt & Debug Log" button) or right-click context menu
- **Editable prompt**: monospace multi-line text field with the system prompt sent to the LLM
- **Save Prompt**: persists the prompt to `config.json` and hot-reloads it into the active LLM service
- **Reset to Default**: restores the built-in default prompt
- **Debug mode**: checkbox to enable/disable real-time debug logging
- **Debug log**: scrollable list displaying each LLM analysis result — window title, verdict (`awaiting_action` / `idle`), and the LLM's reasoning (second line of response)
- Log is capped at 200 entries (oldest removed first) and can be cleared manually

### FR-11: Coding Agent Detection

Shows, for each monitored window, whether a coding agent (**Claude Code** or **OpenCode**) is running in it, and whether it is working or waiting for the user.

#### Discovery

- **Claude Code**: sessions are listed with `claude agents --json`, the interface documented for external tools ([agent view](https://code.claude.com/docs/en/agent-view)). Each live session gives its `pid`, `cwd`, `status` (`busy` / `waiting` / `idle`) and, when waiting, `waitingFor` (`permission prompt`, `input needed`, …). The files under `~/.claude` are not read: the documentation states they are not a stable interface.
- **Background commands**: `claude agents --json` reports `idle` as soon as a turn ends, even while commands started in the background keep running. For an idle session, WinGlance counts the direct children of the Claude process that are Claude Code tool shells (Bash tool: `bash -c "source …/.claude/shell-snapshots/…"`; PowerShell tool: `cmd … $__claudeCodeScript …`), reading their command lines from the process (PEB); MCP servers are not counted. A non-zero count shows as working ("N background tasks"). These markers are internal to Claude Code: if they change, the badge falls back to Claude's status.
- **OpenCode**: no state interface exists, so instances are found from the process list (`opencode.exe`, helpers spawned by an instance excluded). Their working directory is read from the process (PEB). Status is reported as **running**, without busy/idle detail.
- The `claude` executable is looked up on `PATH`, then in `%USERPROFILE%\.local\bin` (native installer). An npm install (`claude.cmd`) is supported.
- Discovery runs in the background every `refreshIntervalMs` (default 5 s), not on every window poll: each `claude agents` call costs about 0.2 s of CPU. It is skipped while no `claude.exe` process runs.

#### Window Association

- Walk up the agent's process tree (Toolhelp32 snapshot) to the nearest process owning a monitored window.
- If that process owns **several** windows (VS Code serves all its windows from one process; Windows Terminal likewise), match the agent's working directory against the window titles: full path in the title first, then the folder name as a title segment (`file - Folder - Visual Studio Code`), then parent folders with a lower score (agent started in a subfolder). A tie leaves the session unassigned rather than guessing.
- Covers the Claude Code VS Code extension, the CLI in a terminal, and agents in the VS Code integrated terminal. Background sessions run by the Claude supervisor have no window and are not shown.

#### Display

- A badge next to the thumbnail title (outside the DWM thumbnail area, which covers WPF content): `Claude · working` (blue), `Claude · permission prompt` (red: the agent waits for an answer), `Claude · idle` (orange: the agent waits for the next prompt), `OpenCode · running` (teal); `+N` when several sessions share a window; tooltip with each session's name and folder.
- **Red border** when an agent waits for the user's answer (permission prompt, question) while the window is not focused; it comes back whenever the window loses focus while the agent still waits. The red border ranks right after "not responding", before the orange states.
- **Orange border** when an agent finished a turn (busy → idle) while the window was not focused; it clears once the window is focused.
- Settings: **Show Claude Code / OpenCode status on thumbnails** checkbox (default: on), applied live.

#### Notifications and next agent

- When a window's agent goes from busy to idle (finished) or starts waiting (needs input) while the window is not in front, a Windows notification is shown through the tray icon (balloon tip, displayed as a toast). Clicking it activates the window. An agent first seen already waiting (startup) is not notified. Setting: `agentDetection.notify` (default: on).
- A second global hotkey, `nextAgentHotkey` (default `Ctrl+Alt+N`, empty for none), activates the next window whose agent needs the user: waiting first, then finished but not seen, then idle; within a group in panel order, starting after the window in front, so repeated presses cycle.

#### Urgency order, filter, status time, reminders

- **Status time**: each window remembers when its status last changed: the agent status for a window with an agent (seeing a finished turn does not reset it), otherwise its attention state (not responding, modal, flashing, LLM awaiting action, none). The time since then is shown next to the badges, e.g. `12 min`, `1 h 05`, for windows that have a status. Setting: `showStatusTime` (default: on).
- **Sort by urgency** (`sortByUrgency`, default: off): one list without application groups, ordered by rank, then by time in the status, longest first: (1) agent waiting for an answer, (2) not responding, modal, flashing or LLM awaiting action, (3) agent finished and not seen, (4) idle agent, (5) working agent, (6) no agent and no state.
- **Only the windows that need me** (`showOnlyWindowsThatNeedMe`, default: off): hides ranks 5 and 6; an empty panel says "Nothing needs you right now."
- **Calm reordering**: the order and the filter follow status changes at most every 3 seconds and never while the mouse is over the thumbnails; a pending change applies when the mouse leaves. New windows take their place at once.
- **Reminders** (`agentDetection.remindAfterMinutes`, 0-60, default 10, 0 = off): an agent waiting for an answer longer than the delay while its window is not in front gets a blinking red ring around its thumbnail, and one reminder notification per waiting period ("Claude still needs you", with the waiting time).
- **Tray counter**: the tray icon shows the number of agents waiting for an answer plus finished turns not seen (red badge if any agent waits, orange otherwise), and lists them in its tooltip.
- The order and filter options are in Settings > Layout and in the Options panel; the reminder delay in Settings > Coding Agents.

---

## Non-Functional Requirements

### NFR-1: Technology Stack

- **Language**: C# (.NET 10)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Target Framework**: `net10.0-windows`
- **Build**: Single-file self-contained executable (no runtime dependency)
- **Target OS**: Windows 10/11 with DWM enabled (required for live thumbnails)
- **NuGet packages**:
  - `System.Text.Json` (included in .NET) — config serialization
  - `Hardcodet.NotifyIcon.Wpf` — system tray icon (or manual `Shell_NotifyIcon` P/Invoke)
- **App manifest**: `dpiAwareness` set to `PerMonitorV2`

### NFR-2: Performance

- CPU usage must remain below **2%** when idle (thumbnails are rendered by DWM, not by the app)
- Memory usage should stay under **50 MB**
- Window enumeration polling must not cause UI lag (run on background thread, update UI via Dispatcher)

### NFR-3: Low Footprint

- No installer required — portable single `.exe` file
- Minimal dependencies (only Windows native APIs via P/Invoke)
- Config stored in `config.json` next to the executable
- **Fallback**: if the executable directory is read-only or on a network path, fall back to `%LOCALAPPDATA%\WinGlance\config.json`

### NFR-4: Display Compatibility

- The application must be **per-monitor DPI aware** (`PerMonitorV2`)
- Panel position and thumbnail sizing must account for different DPI scales across monitors
- DPI awareness is declared in the application manifest

### NFR-5: Theming

- WinGlance **auto-detects the Windows system theme** (dark or light) on startup
- Reads registry key `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`
- UI colors (backgrounds, borders, text) adapt accordingly
- Theme changes while WinGlance is running are detected and applied live

---

## Technical Architecture

How the code is organized, which thread runs what, and how it is tested: see [ARCHITECTURE.md](ARCHITECTURE.md).

### Core Windows APIs (P/Invoke)

All declarations are in `NativeApi/NativeMethods.cs`.

| API                                                               | Purpose                                                                    |
| ----------------------------------------------------------------- | -------------------------------------------------------------------------- |
| `EnumWindows` / `GetWindowThreadProcessId`                        | Enumerate top-level windows and map them to processes                      |
| `IsWindowVisible` / `GetWindowLong` / `GetWindow(GW_OWNER)`       | Apply the taskbar's rules (visible, not a tool window, not owned)          |
| `DwmGetWindowAttribute(DWMWA_CLOAKED)`                            | Skip windows on other virtual desktops and ghost UWP frames                |
| `GetWindowText` / `GetWindowTextLength`                           | Retrieve window titles                                                     |
| `GetApplicationUserModelId` / `EnumChildWindows`                  | Resolve the real app behind `ApplicationFrameHost.exe` (UWP)               |
| `SendMessageTimeout(WM_GETICON)` / `GetClassLongPtr`              | Retrieve application icons without blocking on a hung window               |
| `DwmRegisterThumbnail` / `DwmUpdateThumbnailProperties`           | Register a live DWM thumbnail and set its rectangle and opacity            |
| `DwmQueryThumbnailSourceSize` / `DwmUnregisterThumbnail`          | Keep the source proportions; clean up when a window closes                 |
| `DwmIsCompositionEnabled`                                         | Check that DWM is enabled at startup                                       |
| `IsIconic` / `ShowWindow` / `SendInput` / `SetForegroundWindow`   | Restore and activate a window (a simulated Alt press lifts the focus lock) |
| `SetWindowPos` / `SetWindowLong`                                  | Place the click-through red frame over the window switched to              |
| `MonitorFromWindow` / `GetMonitorInfo` / `EnumDisplayMonitors`    | Find the side of the screen a window is on, for the click                  |
| `RegisterHotKey` / `UnregisterHotKey`                             | Global keyboard shortcuts                                                  |
| `RegisterShellHookWindow` / `RegisterWindowMessage`               | Receive `HSHELL_FLASH` (flashing windows)                                  |
| `PostMessage(HWND_BROADCAST)`                                     | Ask the running instance to show its panel (single instance)               |
| `IsHungAppWindow`                                                 | Detect windows that stopped responding                                     |
| `IsWindowEnabled`                                                 | Detect windows blocked by a modal dialog                                   |
| `PrintWindow` / `GetWindowRect`                                   | Capture window content (graphical diff and LLM analysis)                   |
| `CreateToolhelp32Snapshot` / `Process32First` / `Process32Next`   | Process tree, to tie coding agent sessions to their windows                |
| `OpenProcess` / `NtQueryInformationProcess` / `ReadProcessMemory` | Working folder of OpenCode processes                                       |
| `CryptProtectData` / `CryptUnprotectData`                         | Encrypt the LLM API key for the current Windows user (DPAPI)               |

---

## UI Mockup

### Tab 1 — Preview (horizontal strip, default)

```text
┌─ WinGlance ──────────────────────────────────────────── [_][×] ─┐
│ [Preview]  [Applications]  [Settings]                            │
│                                                                  │
│  ── VS Code ──────────────────────────────────────────────────   │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐            │
│  │ ▓▓▓▓▓▓▓ │  │ ▓▓▓▓▓▓▓ │  │ ▓▓▓▓▓▓▓ │  │ ▓▓▓▓▓▓▓ │            │
│  │ ▓ live ▓ │  │ ▓ live ▓ │  │ ▓ live ▓ │  │ ▓ live ▓ │            │
│  │ ▓▓▓▓▓▓▓ │  │ ▓▓▓▓▓▓▓ │  │ ▓▓▓▓▓▓▓ │  │ ▓▓▓▓▓▓▓ │            │
│  └─────────┘  └─────────┘  └─────────┘  └─────────┘            │
│   readme.md    Faire une..  Ce projet..  Je souhai..             │
│   ═════════                                                      │
│   (active)                                                       │
│                                                                  │
│  ── Notepad++ ────────────────────────────────────────────────   │
│  ┌─────────┐  ┌─────────┐                                       │
│  │ ▓ live ▓ │  │ ▓ live ▓ │                                       │
│  └─────────┘  └─────────┘                                       │
│   config.json  notes.txt                                         │
└──────────────────────────────────────────────────────────────────┘
```

### Tab 2 — Applications

```text
┌─ WinGlance ──────────────────────────────────────────── [_][×] ─┐
│ [Preview]  [Applications]  [Settings]                            │
│                                                                  │
│  Currently running applications:                     [Refresh]   │
│                                                                  │
│  [✓] VS Code (Code.exe)                   — 5 windows            │
│  [✓] Notepad++ (notepad++.exe)            — 2 windows            │
│  [ ] Google Chrome (chrome.exe)           — 12 windows           │
│  [ ] File Explorer (explorer.exe)         — 3 windows            │
│  [ ] Windows Terminal (WindowsTerminal.exe) — 1 window           │
│                                                                  │
│                                              [Save Configuration]│
└──────────────────────────────────────────────────────────────────┘
```

### Tab 3 — Settings

```text
┌─ WinGlance ──────────────────────────────────────────── [_][×] ─┐
│ [Preview]  [Applications]  [Settings]                            │
│                                                                  │
│  Layout:        (●) Horizontal  ( ) Vertical  ( ) Grid          │
│                                                                  │
│  Thumbnail size:  Width [200]px   Height [150]px                 │
│                                                                  │
│  Panel opacity:   [═══════●══] 85%                               │
│                                                                  │
│  Refresh interval: [═══●═════] 1000ms                            │
│                                                                  │
│  Global hotkey:   [Ctrl+Alt+G]  [Record]                         │
│                                                                  │
│  [✓] Remember panel position                                     │
│  [✓] Close button minimizes to tray                              │
│  [ ] Auto-start with Windows                                     │
│                                                                  │
│  ── LLM Analysis ────────────────────────────────────────────    │
│  [ ] Enable LLM analysis                                         │
│  Provider:       [OpenAI     ▾]                                  │
│  API endpoint:   [https://api.openai.com/v1      ]               │
│  API key:        [••••••••••••••••                ]               │
│  Model:          [gpt-4o-mini                     ]               │
│  Stale threshold: [═══●═════] 30s                                │
│                              [Edit Prompt & Debug Log]           │
│                                                                  │
│                                              [Save Configuration]│
└──────────────────────────────────────────────────────────────────┘
```

---

## Config File Format (`config.json`)

```json
{
  "monitoredApps": [
    { "processName": "Code", "displayName": "VS Code" },
    { "processName": "WindowsTerminal", "displayName": "Terminal" }
  ],
  "layout": "fit",
  "sortByUrgency": false,
  "showOnlyWindowsThatNeedMe": false,
  "showStatusTime": true,
  "titleFormat": "{project} - {document}",
  "thumbnailWidth": 200,
  "thumbnailHeight": 150,
  "panelOpacity": 0.85,
  "pollingIntervalMs": 1000,
  "rememberPosition": true,
  "panelX": 100,
  "panelY": 100,
  "panelWidth": 900,
  "panelHeight": 560,
  "autoStart": false,
  "closeToTray": true,
  "highlightOnSwitch": true,
  "clickOnSwitch": true,
  "hotkey": "Ctrl+Alt+G",
  "nextAgentHotkey": "Ctrl+Alt+N",
  "llm": {
    "enabled": false,
    "provider": "openai",
    "endpoint": "https://api.openai.com/v1",
    "apiKeyProtected": "",
    "model": "gpt-4o-mini",
    "staleThresholdSeconds": 30,
    "prompt": "You are a desktop window analyst for WinGlance. …",
    "debugEnabled": false
  },
  "agentDetection": {
    "enabled": true,
    "notify": true,
    "refreshIntervalMs": 5000,
    "remindAfterMinutes": 10
  }
}
```

Values out of range are brought back into range on load (see `ConfigService.Normalize`); missing keys take their default.

---

## Build & Run

```bash
dotnet build src/WinGlance.slnx
dotnet test src/WinGlance.slnx
dotnet publish src/WinGlance/WinGlance.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Output: single self-contained `publish/WinGlance.exe` (about 166 MB, uncompressed)

---

## Verification Plan

1. **Build**: `dotnet build` succeeds with no errors
2. **Launch**: Run WinGlance.exe — floating panel appears, always-on-top
3. **Tab 2 — Applications**: Running apps are listed, check VS Code, save
4. **Tab 1 — Preview**: Live thumbnails appear for all VS Code windows
5. **Click-to-switch**: Click a thumbnail — the corresponding window gets focus
6. **Dynamic tracking**: Open a new VS Code window — thumbnail appears within ~1s; close it — thumbnail disappears
7. **Minimized windows**: Minimize a window, click its thumbnail — it restores and gets focus
8. **Multi-app**: Monitor a second app (e.g., Notepad++), verify thumbnails appear grouped
9. **Tab 3 — Settings**: Change layout to vertical, change thumbnail size — verify preview updates
10. **Persistence**: Drag the panel, change settings, close and reopen — position and config are preserved
11. **Tray**: Minimize to tray, right-click tray icon — Show/Hide/Exit work
12. **Single instance**: Launch a second instance — it should activate the first instead of opening a duplicate
13. **Global hotkey**: Press Ctrl+Alt+G — panel hides; press again — panel shows. Change hotkey in settings and verify new binding works
14. **Close button**: With "close to tray" enabled, click [×] — panel hides to tray. Disable the option, click [×] — app exits
15. **UWP apps**: Open a UWP app (e.g., Calculator), add it in Tab 2 — verify it appears with its real name, not "ApplicationFrameHost"
16. **DWM disabled**: In a remote desktop session (DWM disabled), launch WinGlance — verify graceful error message
17. **Theme**: Switch Windows to dark mode — verify WinGlance UI adapts. Switch back to light mode — verify it adapts again
18. **DPI scaling**: On a multi-monitor setup with different DPI, move the panel between monitors — verify correct sizing and positioning
19. **Attention — flash**: Trigger a window flash (e.g., a background app requesting focus) — verify the thumbnail shows a pulsating attention indicator
20. **Attention — not responding**: Force-hang a test app — verify its thumbnail shows a red border and a "Not Responding" badge
21. **Attention — modal dialog**: Open a "Save As" dialog in a monitored app — verify the thumbnail indicates the window is blocked by a modal
22. **Stale detection**: Leave a monitored window untouched for >30s — verify the thumbnail is flagged as stale (dimmed border)
23. **LLM — awaiting action**: Configure an LLM provider, open a dialog in a monitored window, wait 30s — verify LLM identifies it as awaiting action (orange indicator)
24. **LLM — idle**: Configure an LLM provider, leave a static window (e.g., a text file) untouched — verify LLM classifies it as idle (dimmed indicator)
25. **LLM — single call**: Verify that only one LLM call is made per stale window — no repeated calls until the window content changes
26. **LLM — Ollama local**: Configure Ollama as provider with a local model — verify analysis works without an API key
27. **Prompt editor**: Edit the prompt in the Prompt Editor, click Save Prompt, then Run — verify the updated prompt is used

---

## Project Location

`Z:\My Drive\00_DEV\WinGlance\`
