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

### FR-3: Dynamic Window Tracking

- Automatically detect when new windows of monitored applications are **opened** and add their thumbnails
- Automatically remove thumbnails when windows are **closed**
- Polling interval: ~1 second (configurable)
- **UWP/Store app support**: windows running under `ApplicationFrameHost.exe` must be correctly identified by resolving the actual package/app name (via `GetApplicationUserModelId`) rather than showing "ApplicationFrameHost"

### FR-4: Window Title Display

- Show the **window title** (truncated if needed) below each thumbnail
- Show the **application icon** on each thumbnail for multi-app identification
- Highlight the **currently active/focused** window's thumbnail with a visual indicator (border color, glow)

### FR-5: Tab-Based Interface

The panel has **3 tabs**:

#### Tab 1 — Preview (main tab)

- Displays all live thumbnails for all monitored applications
- Thumbnails are grouped by application (with app name/icon as section header)
- Click on any thumbnail to switch to that window

#### Tab 2 — Applications

- Lists all **currently running applications** (with window count)
- Checkbox to **add/remove** an application from the monitored set
- Already-monitored apps are checked
- **Save** button to persist the monitored app list to config
- Refresh button to re-scan running applications

#### Tab 3 — Settings

- **Layout**: horizontal strip / vertical strip / grid
- **Thumbnail size**: width x height (slider or numeric input)
- **Panel opacity**: slider (70%-100%)
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
  - **Edit prompt**: button to open `prompt.txt`
- **Save** button to persist all settings

### FR-6: Panel Behavior

- The panel must be **draggable** (user can reposition it anywhere on screen)
- The panel must **remember its position** between sessions (persist to config file)
- The panel should be **resizable**, with thumbnails scaling proportionally
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
  - A **system prompt** loaded from a dedicated file (`prompt.txt` next to the executable)
- The LLM determines whether the window:
  - **Awaits user action** (dialog, error, confirmation, input field) → highlight thumbnail with **attention indicator** (orange pulsating border)
  - **Is simply idle** (nothing happening, no action needed) → highlight thumbnail with **idle indicator** (dimmed/grayed border)
- Only **one LLM call per stale window** — not repeated until the window content changes again

#### Supported LLM Providers

- **OpenAI** — API-compatible (GPT-4o, GPT-4o-mini with vision)
- **Google Gemini** — API-compatible (Gemini Pro/Flash with vision)
- **Ollama** (local) — for privacy-conscious users, local models with optional vision support
- Provider, endpoint, API key, and model name are configurable in Tab 3 — Settings
- The prompt is stored in a separate editable file: `prompt.txt` (not in `config.json`)

#### Configuration (Tab 3 — Settings)

- **LLM enabled**: checkbox (default: disabled)
- **Provider**: dropdown — OpenAI / Gemini / Ollama
- **API endpoint**: text field (pre-filled with default for each provider; editable for Ollama custom URL)
- **API key**: password field (not needed for Ollama)
- **Model**: text field (e.g., `gpt-4o-mini`, `gemini-2.0-flash`, `llava`)
- **Stale threshold**: slider (10s–120s, default: 30s)
- **Edit prompt**: button that opens `prompt.txt` in the default text editor

---

## Non-Functional Requirements

### NFR-1: Technology Stack

- **Language**: C# (.NET 8+)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Target Framework**: `net8.0-windows`
- **Build**: Single-file self-contained executable (no runtime dependency)
- **Target OS**: Windows 10/11 with DWM enabled (required for live thumbnails)
- **NuGet packages**:
  - `System.Text.Json` (included in .NET 8) — config serialization
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

### Core Windows APIs (P/Invoke)

| API                                           | Purpose                                                       |
| --------------------------------------------- | ------------------------------------------------------------- |
| `EnumWindows`                                 | Enumerate all top-level windows                               |
| `GetWindowThreadProcessId`                    | Map window handles to process IDs                             |
| `IsWindowVisible`                             | Filter invisible/hidden windows                               |
| `GetWindowText`                               | Retrieve window titles                                        |
| `DwmRegisterThumbnail`                        | Register a live DWM thumbnail relationship                    |
| `DwmUpdateThumbnailProperties`                | Set thumbnail region, size, opacity                           |
| `DwmUnregisterThumbnail`                      | Clean up when a source window closes                          |
| `SetForegroundWindow`                         | Bring clicked window to front                                 |
| `ShowWindow` / `IsIconic`                     | Restore minimized windows                                     |
| `GetWindowPlacement`                          | Get window state (minimized, maximized, normal)               |
| `GetClassLongPtr` / `SendMessage(WM_GETICON)` | Retrieve application icon                                     |
| `RegisterHotKey` / `UnregisterHotKey`         | Register/unregister global keyboard shortcut                  |
| `GetApplicationUserModelId`                   | Resolve UWP app identity from `ApplicationFrameHost.exe`      |
| `DwmIsCompositionEnabled`                     | Check if DWM is enabled at startup                            |
| `AllowSetForegroundWindow`                    | Workaround for focus-stealing prevention                      |
| `RegisterShellHookWindow`                     | Receive shell notifications (window flash events)             |
| `IsHungAppWindow`                             | Detect windows that stopped responding                        |
| `IsWindowEnabled`                             | Detect windows blocked by a modal dialog                      |
| `GetWindow(GW_ENABLEDPOPUP)`                  | Find the modal popup blocking a disabled window               |
| `PrintWindow`                                 | Capture window content as bitmap (for graphical diff and LLM) |

### Application Structure

```text
WinGlance/
├── App.xaml                        # Application entry point
├── App.xaml.cs                     # Startup logic, single-instance mutex, tray icon
├── MainWindow.xaml                 # Floating panel with TabControl (3 tabs)
├── MainWindow.xaml.cs              # Panel logic: drag, resize, tab switching
├── Models/
│   ├── AppConfig.cs                # Root config model (JSON serializable)
│   ├── MonitoredApp.cs             # Process name + display name
│   └── TrackedWindow.cs            # HWND + title + process info
├── Services/
│   ├── WindowEnumerator.cs         # EnumWindows + process filtering
│   ├── ThumbnailManager.cs         # DWM thumbnail registration & lifecycle
│   ├── AttentionDetector.cs        # Shell hook + IsHungAppWindow + modal detection
│   ├── ScreenshotComparer.cs       # PrintWindow capture + perceptual hash diff
│   ├── LlmAnalyzer.cs             # LLM API calls (OpenAI, Gemini, Ollama)
│   └── ConfigService.cs            # Load/save config.json
├── NativeApi/
│   └── NativeMethods.cs            # All P/Invoke declarations (DWM, User32)
├── Views/
│   ├── PreviewTab.xaml             # Tab 1: thumbnail grid per app
│   ├── ApplicationsTab.xaml        # Tab 2: app list with checkboxes
│   └── SettingsTab.xaml            # Tab 3: layout/size/opacity controls
├── ViewModels/
│   ├── MainViewModel.cs            # Orchestrates tabs, holds config
│   ├── PreviewViewModel.cs         # Observable collection of tracked windows
│   ├── ApplicationsViewModel.cs    # Running apps list, add/remove monitoring
│   └── SettingsViewModel.cs        # Bindable settings properties
├── Converters/
│   └── BoolToVisibilityConverter.cs
├── Assets/
│   └── icon.ico                    # App + system tray icon
└── prompt.txt                      # LLM system prompt (editable by user)
```

### Key Implementation Details

1. **DWM Thumbnail Rendering**: The DWM API draws live window content directly — no screen capture or bitmap copying. This is the same mechanism Windows uses for taskbar previews. The host WPF window provides a region, and DWM composites the source window content into it in real-time.

2. **Window Enumeration Loop**: A `DispatcherTimer` (1s interval) calls `EnumWindows`, filters by monitored process names, and diffs against the current tracked set to add/remove thumbnails.

3. **Click-to-Switch**: Each thumbnail has a click handler that calls `SetForegroundWindow(hwnd)`. If `IsIconic(hwnd)` returns true, `ShowWindow(hwnd, SW_RESTORE)` is called first.

4. **Panel Always-On-Top**: The WPF window has `Topmost = true` and `WindowStyle = None` (borderless). A custom title bar area enables drag-move.

5. **Multi-App Grouping**: In the Preview tab, thumbnails are grouped by application using an `ItemsControl` with `GroupStyle`. Each group header shows the app icon and name.

6. **Config Persistence**: On save or close, the panel writes position, size, monitored apps, and settings to `config.json`.

7. **Single Instance**: A named `Mutex` prevents multiple instances. If a second instance is launched, it sends a message to the first to bring it to foreground.

8. **UWP App Resolution**: UWP/Store app windows are hosted by `ApplicationFrameHost.exe`. The window enumerator detects this and resolves the real app name via `GetApplicationUserModelId` so thumbnails are grouped under the correct application.

9. **Global Hotkey**: On startup, `RegisterHotKey` registers the configured shortcut. A WndProc handler listens for `WM_HOTKEY` to toggle panel visibility. `UnregisterHotKey` is called on exit.

10. **Theme Detection**: The app reads `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` and applies a matching WPF resource dictionary. A `SystemEvents.UserPreferenceChanged` handler detects live theme changes.

11. **Attention Detection**: `AttentionDetector` uses three mechanisms: (a) `RegisterShellHookWindow` to receive `HSHELL_FLASH` when a window starts flashing, (b) `IsHungAppWindow` polled alongside window enumeration to detect hung apps, (c) `IsWindowEnabled` checked each cycle to detect modal-blocked windows. Detected states are exposed as properties on `TrackedWindow` and bound to visual indicators (pulsating border, overlay) in the Preview tab.

12. **Graphical Diff (Stale Detection)**: `ScreenshotComparer` captures each window via `PrintWindow` at the polling interval, computes a perceptual hash (pHash), and compares with the previous capture. If the hash is identical for longer than the configured stale threshold (default 30s), the window is marked as stale and triggers LLM analysis.

13. **LLM Analysis**: `LlmAnalyzer` provides a unified interface for OpenAI, Gemini, and Ollama APIs. When a stale window is detected: (a) the latest screenshot is encoded as base64, (b) the system prompt is loaded from `prompt.txt`, (c) a single API call is made with the screenshot + window title, (d) the response is parsed to classify the window as "awaiting action" or "idle", (e) the result is set on `TrackedWindow.LlmState` and bound to the appropriate visual indicator. No further calls are made until the window content changes again.

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
│                                          [Edit prompt.txt]       │
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
    { "processName": "notepad++", "displayName": "Notepad++" }
  ],
  "layout": "horizontal",
  "thumbnailWidth": 200,
  "thumbnailHeight": 150,
  "panelOpacity": 0.85,
  "pollingIntervalMs": 1000,
  "rememberPosition": true,
  "panelX": 100,
  "panelY": 100,
  "autoStart": false,
  "closeToTray": true,
  "hotkey": "Ctrl+Alt+G",
  "llm": {
    "enabled": false,
    "provider": "openai",
    "endpoint": "https://api.openai.com/v1",
    "apiKey": "",
    "model": "gpt-4o-mini",
    "staleThresholdSeconds": 30
  }
}
```

---

## Build & Run

```bash
dotnet new wpf -n WinGlance
cd WinGlance
# develop...
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Output: single `WinGlance.exe` (~30-60 MB self-contained)

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
20. **Attention — not responding**: Force-hang a test app — verify its thumbnail shows a "(Not Responding)" overlay
21. **Attention — modal dialog**: Open a "Save As" dialog in a monitored app — verify the thumbnail indicates the window is blocked by a modal
22. **Stale detection**: Leave a monitored window untouched for >30s — verify the thumbnail is flagged as stale (dimmed border)
23. **LLM — awaiting action**: Configure an LLM provider, open a dialog in a monitored window, wait 30s — verify LLM identifies it as awaiting action (orange indicator)
24. **LLM — idle**: Configure an LLM provider, leave a static window (e.g., a text file) untouched — verify LLM classifies it as idle (dimmed indicator)
25. **LLM — single call**: Verify that only one LLM call is made per stale window — no repeated calls until the window content changes
26. **LLM — Ollama local**: Configure Ollama as provider with a local model — verify analysis works without an API key
27. **Prompt file**: Edit `prompt.txt`, trigger a new analysis — verify the updated prompt is used

---

## Project Location

`Z:\My Drive\00_DEV\WinGlance\`
