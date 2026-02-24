# WinGlance — Development Plan

> WPF C# .NET 8+ application — Floating panel with live DWM window previews.
> Full reference: [doc/SPECIFICATION.md](doc/SPECIFICATION.md)

---

## Phase 0 — Scaffolding & Infrastructure ✅

- [x] **0.1** Create the solution `WinGlance.sln` and the project `WinGlance.csproj`
- [x] **0.2** Create the folder structure
- [x] **0.3** Add the application manifest (`app.manifest`)
- [x] **0.4** Configure `App.xaml` / `App.xaml.cs`
- [x] **0.5** Add an application icon (`Assets/icon.ico`)
- [x] **0.6** Verify that `dotnet build` compiles without errors
- [x] **0.7** Verify that `dotnet publish` produces a working executable
- [x] **0.8** Create test project `WinGlance.Tests` (xUnit) and add to solution

---

## Phase 1 — Main Window (Shell) ✅

- [x] **1.1** Create `MainWindow.xaml` — borderless, always-on-top window
- [x] **1.2** Implement drag-move on the title bar
- [x] **1.3** Implement window resizing (resize grip)
- [x] **1.4** Add the `TabControl` with 3 tabs (Preview, Applications, Settings)
- [x] **1.5** Create `MainViewModel.cs` — main orchestrator (MVVM)
- [x] **1.6** Write and run unit tests (7 tests passing)

---

## Phase 2 — Native Layer (P/Invoke) ✅

- [x] **2.1** Create `NativeApi/NativeMethods.cs` — P/Invoke declarations
  - `EnumWindows`, `GetWindowThreadProcessId`, `IsWindowVisible`, `GetWindowText`
  - `GetWindowPlacement`, `IsIconic`, `ShowWindow`, `SetForegroundWindow`
  - `GetForegroundWindow`, `AllowSetForegroundWindow`
  - `GetClassLongPtr`, `SendMessage(WM_GETICON)`, `DestroyIcon`
- [x] **2.2** Add DWM APIs
  - `DwmRegisterThumbnail`, `DwmUpdateThumbnailProperties`, `DwmUnregisterThumbnail`
  - `DwmIsCompositionEnabled`
  - Structures: `DWM_THUMBNAIL_PROPERTIES`, `RECT`, `SIZE`
- [x] **2.3** Add Hotkey APIs
  - `RegisterHotKey`, `UnregisterHotKey`
- [x] **2.4** Add UWP APIs
  - `GetApplicationUserModelId` (or `SHLoadIndirectString`)
  - Resolve real app name from `ApplicationFrameHost.exe`
- [x] **2.5** Add Attention Detection APIs
  - `RegisterShellHookWindow` — receive `HSHELL_FLASH` notifications
  - `IsHungAppWindow` — detect not-responding windows
  - `IsWindowEnabled` — detect modal-blocked windows
  - `GetWindow(GW_ENABLEDPOPUP)` — find the modal popup
  - `PrintWindow` — capture window content as bitmap
- [x] **2.6** Manually test critical P/Invoke calls (DWM enabled, EnumWindows)
- [x] **2.7** Write and run unit tests (14 tests — struct sizes, P/Invoke smoke tests, constants)
  - `dotnet test` passes (21 total)

---

## Phase 3 — Window Enumeration ✅

- [x] **3.1** Create `Models/TrackedWindow.cs`
  - Properties: `IntPtr Hwnd`, `string Title`, `string ProcessName`, `string DisplayName`, `ImageSource Icon`, `bool IsActive`
  - Attention properties: `bool IsFlashing`, `bool IsHung`, `bool IsModalBlocked`
  - LLM properties: `bool IsStale`, `string LlmVerdict` (awaiting_action / idle / null)
- [x] **3.2** Create `Models/MonitoredApp.cs`
  - Properties: `string ProcessName`, `string DisplayName`
- [x] **3.3** Create `Services/WindowEnumerator.cs`
  - Method `List<TrackedWindow> GetWindows(IEnumerable<MonitoredApp> monitoredApps)`
  - Filtering: visible windows, with title, no tool/ghost windows
  - UWP resolution (`ApplicationFrameHost.exe` → real app name via AUMID)
  - `DiscoverRunningApps()` for Applications tab discovery
- [x] **3.4** Extract application icons (`WM_GETICON` cascade + `GetClassLongPtr` fallback)
- [x] **3.5** Implement periodic polling in `PreviewViewModel`
  - Configurable `DispatcherTimer` (default 1000 ms, 500–5000 ms range)
  - Diff-merge between current state and new scan → add/remove/update
  - Run scan on background thread, update UI via `EnableCollectionSynchronization`
- [x] **3.6** Detect the active window (`GetForegroundWindow`) for highlight
- [x] **3.7** Write and run unit tests (21 new tests — models, enumerator, AUMID parsing)
  - `dotnet test` passes (42 total)

---

## Phase 4 — DWM Thumbnails (Preview Tab)

- [ ] **4.1** Create `Services/ThumbnailManager.cs`
  - `Register(IntPtr source, IntPtr destination, Rect region)` → thumbnail handle
  - `Update(IntPtr thumbnail, Rect region, double opacity)`
  - `Unregister(IntPtr thumbnail)`
  - Lifecycle management: automatic cleanup when source window disappears
- [ ] **4.2** Create `Views/PreviewTab.xaml`
  - `ItemsControl` with `GroupStyle` to group by application
  - Group header: app icon + app name
  - Item template: DWM zone + truncated title below
- [ ] **4.3** Create `ViewModels/PreviewViewModel.cs`
  - `ObservableCollection<TrackedWindow>` grouped by app
  - `CollectionViewSource` property with `GroupDescriptions`
- [ ] **4.4** Integrate DWM rendering into `ItemsControl` items
  - Each item exposes an `HwndHost` or custom control to receive the DWM thumbnail
  - Recalculate destination `Rect` when size changes
- [ ] **4.5** Support all 3 layouts (horizontal / vertical / grid)
  - `WrapPanel` horizontal, `StackPanel` vertical, `UniformGrid`
- [ ] **4.6** Visual highlight for the active window (colored border / glow)
- [ ] **4.7** Handle resizing — proportional rescaling of thumbnails
- [ ] **4.8** Write and run unit tests
  - Test `ThumbnailManager` register/unregister lifecycle logic
  - Test `PreviewViewModel` grouping and collection updates
  - `dotnet test` passes

---

## Phase 5 — Click-to-Switch

- [ ] **5.1** Click handler on each thumbnail
  - If `IsIconic(hwnd)` → `ShowWindow(hwnd, SW_RESTORE)`
  - `SetForegroundWindow(hwnd)`
- [ ] **5.2** Workaround for focus-stealing prevention
  - `AllowSetForegroundWindow` or `Alt` key simulation (see also Phase 13.4)
- [ ] **5.3** Test: click on normal, minimized, maximized window, and on another monitor

---

## Phase 6 — Applications Tab (Tab 2)

- [ ] **6.1** Create `Views/ApplicationsTab.xaml`
  - List with checkbox, app name, process name, window count
  - `[Refresh]` button, `[Save Configuration]` button
- [ ] **6.2** Create `ViewModels/ApplicationsViewModel.cs`
  - Scan all processes with visible windows
  - Group by process name
  - `IsMonitored` property bound to checkboxes
- [ ] **6.3** Implement `Refresh` — rescan currently running applications
- [ ] **6.4** Implement `Save` — persist the monitored app list to config
- [ ] **6.5** Synchronize: when apps are saved, the Preview tab updates accordingly
- [ ] **6.6** Write and run unit tests
  - Test `ApplicationsViewModel` scan, grouping, and monitored toggle
  - `dotnet test` passes

---

## Phase 7 — Configuration & Persistence

- [ ] **7.1** Create `Models/AppConfig.cs` — JSON config model
  - `monitoredApps`, `layout`, `thumbnailWidth`, `thumbnailHeight`
  - `panelOpacity`, `pollingIntervalMs`, `rememberPosition`, `panelX`, `panelY`
  - `autoStart`, `closeToTray`, `hotkey`
  - LLM settings: `llmEnabled`, `llmProvider`, `llmEndpoint`, `llmApiKey`, `llmModel`, `staleThresholdSeconds`
  - Sensible default values
- [ ] **7.2** Create `Services/ConfigService.cs`
  - `Load()`: read `config.json` next to the exe, fallback to `%LOCALAPPDATA%\WinGlance\`
  - `Save()`: atomic write (write to `.tmp` then rename)
  - Error handling: corrupt JSON → default values + log warning
- [ ] **7.3** Load config at startup (`App.xaml.cs`)
- [ ] **7.4** Save panel position on close (if `rememberPosition` is enabled)
- [ ] **7.5** Test: delete `config.json`, launch the app → default values created
- [ ] **7.6** Write and run unit tests
  - Test `AppConfig` default values and JSON round-trip (serialize → deserialize)
  - Test `ConfigService` load/save, corrupt JSON fallback, atomic write
  - `dotnet test` passes

---

## Phase 8 — Settings Tab (Tab 3)

- [ ] **8.1** Create `Views/SettingsTab.xaml`
  - Layout: RadioButtons (Horizontal / Vertical / Grid)
  - Thumbnail size: sliders or numeric inputs (width, height)
  - Panel opacity: slider 70%-100%
  - Panel position: X / Y inputs (or "remember last position" checkbox)
  - Polling interval: slider 500ms-5000ms
  - Global hotkey: TextBox + `[Record]` button
  - Checkboxes: remember position, close to tray, auto-start
  - LLM Analysis section: enabled checkbox, provider dropdown, endpoint, API key, model, stale threshold slider, Edit Prompt button
  - `[Save Configuration]` button
- [ ] **8.2** Create `ViewModels/SettingsViewModel.cs`
  - Properties bound to config
  - Commands: `Save`, `RecordHotkey`, `EditPrompt`
- [ ] **8.3** Implement hotkey recording (keyboard capture in the TextBox)
- [ ] **8.4** Apply changes live when relevant
  - Layout change → rearrange thumbnails immediately
  - Opacity change → apply immediately
  - Polling interval change → restart the timer
- [ ] **8.5** Implement `EditPrompt` — open `prompt.txt` in default text editor
- [ ] **8.6** Create `Converters/BoolToVisibilityConverter.cs` and other necessary converters
- [ ] **8.7** Write and run unit tests
  - Test `SettingsViewModel` property bindings and save command
  - Test converters (BoolToVisibility, etc.)
  - `dotnet test` passes

---

## Phase 9 — Single Instance & System Tray

- [ ] **9.1** Implement named Mutex in `App.xaml.cs`
  - If an instance already exists → send a message to bring it to foreground, then exit
- [ ] **9.2** Integrate System Tray icon (`Hardcodet.NotifyIcon.Wpf` or P/Invoke)
  - Icon in the tray when the panel is hidden
  - Right-click menu: Show/Hide, Settings, Exit
- [ ] **9.3** Implement `[×]` button behavior
  - If `closeToTray` → hide to tray
  - Otherwise → exit the application
- [ ] **9.4** Double-click on tray icon → show the panel
- [ ] **9.5** Test: close, reopen from tray, launch a 2nd instance

---

## Phase 10 — Global Hotkey

- [ ] **10.1** Create `Services/HotkeyService.cs`
  - `Register(ModifierKeys modifiers, Key key)` → `RegisterHotKey`
  - `Unregister()` → `UnregisterHotKey`
  - `HotkeyPressed` event
  - WndProc hook to intercept `WM_HOTKEY`
- [ ] **10.2** Wire to panel visibility toggle
- [ ] **10.3** Update hotkey when the user changes it in Settings
  - `Unregister` old → `Register` new
- [ ] **10.4** Cleanup: call `UnregisterHotKey` on application shutdown
- [ ] **10.5** Test: Ctrl+Alt+G toggles the panel, even when WinGlance doesn't have focus

---

## Phase 11 — Theme (Light / Dark)

- [ ] **11.1** Create `ResourceDictionary` files for light and dark themes
  - Colors: background, text, borders, accents, tabs, buttons
- [ ] **11.2** Create `Services/ThemeService.cs`
  - Read `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`
  - Apply the corresponding `ResourceDictionary`
- [ ] **11.3** Detect theme changes in real time
  - `SystemEvents.UserPreferenceChanged` or registry monitoring
  - Dynamically switch resources
- [ ] **11.4** Test: switch Windows to dark mode → UI adapts live

---

## Phase 12 — DPI & Multi-Monitor & Multi-Desktop

- [ ] **12.1** Verify that the `PerMonitorV2` manifest is properly applied
- [ ] **12.2** Test panel positioning on different monitors with different DPI settings
- [ ] **12.3** Handle DPI changes when the panel is moved between monitors
  - `DpiChanged` event on the window
  - Recalculate thumbnail sizes
- [ ] **12.4** Ensure position save/restore works correctly in multi-monitor setups

---

## Phase 13 — Error Handling & Resilience

- [ ] **13.1** DWM check at startup
  - `DwmIsCompositionEnabled` → if disabled, show clear error message + exit
- [ ] **13.2** Handle invalid window handles
  - If a process crashes or a window disappears → `DwmUnregisterThumbnail` + silent removal
  - No crash, no error message
- [ ] **13.3** Handle config errors
  - Corrupt JSON → default values
  - Read-only directory → fallback to `%LOCALAPPDATA%`
- [ ] **13.4** Handle `SetForegroundWindow` failure (implemented in Phase 5.2, verified here)
  - Workaround: `Alt` key simulation or `AttachThreadInput`
- [ ] **13.5** Global `DispatcherUnhandledException` handler → log + user-friendly message

---

## Phase 14 — Auto-Start with Windows

- [ ] **14.1** Implement registry key add/remove
  - `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run`
  - Value = path to the exe
- [ ] **14.2** Synchronize with the checkbox in Settings
- [ ] **14.3** Test: enable, restart Windows → WinGlance launches

---

## Phase 15 — Window Attention Detection (FR-9)

- [ ] **15.1** Create `Services/AttentionDetector.cs`
  - `RegisterShellHookWindow` to receive `HSHELL_FLASH` notifications
  - WndProc handler for shell hook messages
  - Track which windows are currently flashing
- [ ] **15.2** Integrate `IsHungAppWindow` check into polling cycle
  - Set `TrackedWindow.IsHung` flag on each enumeration pass
- [ ] **15.3** Integrate `IsWindowEnabled` check into polling cycle
  - If disabled, use `GetWindow(GW_ENABLEDPOPUP)` to find the blocking modal
  - Set `TrackedWindow.IsModalBlocked` flag
- [ ] **15.4** Add visual indicators in Preview tab
  - Flashing: pulsating orange border (WPF animation)
  - Not responding: "(Not Responding)" overlay on thumbnail
  - Modal blocked: attention icon / distinct border style
- [ ] **15.5** Auto-clear indicators when condition resolves
- [ ] **15.6** Write and run unit tests
  - Test `AttentionDetector` state tracking and auto-clear logic
  - `dotnet test` passes
- [ ] **15.7** Manual test: trigger flash, hang an app, open a modal dialog — verify indicators

---

## Phase 16 — LLM-Assisted Analysis (FR-10)

- [ ] **16.1** Create `Services/ScreenshotComparer.cs`
  - Capture window screenshot via `PrintWindow` API
  - Implement perceptual hashing (pHash) for image comparison
  - Detect stale windows (no visual change for configurable threshold, default 30s)
- [ ] **16.2** Create `Services/LlmAnalyzer.cs`
  - Abstract provider interface for LLM calls
  - OpenAI implementation (GPT-4o / GPT-4o-mini with vision)
  - Google Gemini implementation (Gemini Pro/Flash with vision)
  - Ollama implementation (local, optional vision)
- [ ] **16.3** Load system prompt from `prompt.txt` next to the executable
  - Create default `prompt.txt` if not present
- [ ] **16.4** Trigger LLM call when window is flagged stale
  - Send screenshot + window title + system prompt
  - Parse response: "awaiting_action" or "idle"
  - Set `TrackedWindow.LlmVerdict` accordingly
  - One call per stale window — no repeat until content changes
- [ ] **16.5** Add visual indicators in Preview tab
  - Awaiting action: orange pulsating border
  - Idle: dimmed/grayed border
- [ ] **16.6** Write and run unit tests
  - Test `ScreenshotComparer` pHash comparison logic (with synthetic bitmaps)
  - Test `LlmAnalyzer` request formatting and response parsing per provider
  - Test stale detection threshold logic
  - `dotnet test` passes
- [ ] **16.7** Manual test: with each provider, stale a window → verify LLM call and indicator

---

## Phase 17 — Polish & Final Touches

- [ ] **17.1** Animations and transitions
  - Smooth panel appear/disappear (fade in/out)
  - Tab transitions
- [ ] **17.2** Tooltips on thumbnails (full window title)
- [ ] **17.3** Verify performance
  - CPU < 2% at idle
  - RAM < 50 MB
  - Profile if necessary
- [ ] **17.4** Run the 21-point verification plan (SPECIFICATION.md § Verification Plan)
- [ ] **17.5** Clean up code, remove remaining placeholders and TODOs
- [ ] **17.6** Replace placeholder icon with the final icon
- [ ] **17.7** Final single-file build and standalone exe test

---

## Phase Summary

| Phase | Description                         | Depends on |
| ----- | ----------------------------------- | ---------- |
| 0     | Scaffolding & Infrastructure ✅     | —          |
| 1     | Main Window (Shell) ✅              | 0          |
| 2     | Native Layer (P/Invoke) ✅          | 0          |
| 3     | Window Enumeration ✅               | 2          |
| 4     | DWM Thumbnails (Preview Tab)        | 1, 2, 3    |
| 5     | Click-to-Switch                     | 4          |
| 6     | Applications Tab (Tab 2)            | 1, 3, 7    |
| 7     | Configuration & Persistence         | 0          |
| 8     | Settings Tab (Tab 3)                | 1, 7       |
| 9     | Single Instance & System Tray       | 1, 7       |
| 10    | Global Hotkey                       | 2, 9       |
| 11    | Theme (Light / Dark)                | 1          |
| 12    | DPI & Multi-Monitor & Multi-Desktop | 4          |
| 13    | Error Handling & Resilience         | 3, 4, 7    |
| 14    | Auto-Start with Windows             | 7, 8       |
| 15    | Window Attention Detection (FR-9)   | 2, 3, 4    |
| 16    | LLM-Assisted Analysis (FR-10)       | 2, 3, 15   |
| 17    | Polish & Final Touches              | All        |
