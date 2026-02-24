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

## Phase 4 — DWM Thumbnails (Preview Tab) ✅

- [x] **4.1** Create `Services/ThumbnailManager.cs`
  - `Register(IntPtr source)` → thumbnail handle, `Update`, `Unregister`, `UnregisterAll`
  - Lifecycle management: automatic cleanup via `Dispose`
- [x] **4.2** Create `Views/PreviewTab.xaml`
  - `ItemsControl` with `GroupStyle` to group by application
  - Group header: app icon + app name + window count
  - Item template: DWM thumbnail + truncated title below
- [x] **4.3** Enhance `ViewModels/PreviewViewModel.cs`
  - `ObservableCollection<TrackedWindow>` with `GroupedWindows` ICollectionView
  - `CollectionViewSource.GetDefaultView` with `PropertyGroupDescription`
- [x] **4.4** Create `Controls/ThumbnailControl.cs` — custom `FrameworkElement`
  - DWM thumbnail rendering via `ThumbnailManager`
  - DPI-aware destination `Rect` via `TransformToAncestor` + `TransformToDevice`
  - Recalculates on `SizeChanged`
- [x] **4.5** Support all 3 layouts via `Converters/LayoutToPanelConverter.cs`
  - `WrapPanel` horizontal, `StackPanel` vertical, `UniformGrid` grid
- [x] **4.6** Visual highlight for the active window via `Converters/BoolToBorderBrushConverter.cs`
  - Active = Windows blue border, Inactive = subtle gray
- [x] **4.7** Handle resizing — `ThumbnailControl.OnSizeChanged` + proportional DWM rect update
- [x] **4.8** Write and run unit tests
  - Test `ThumbnailManager` register/unregister lifecycle
  - Test `PreviewViewModel` properties, grouping, clamping
  - Test converters (LayoutToPanel, BoolToBorderBrush)
  - `dotnet test` passes (114 total)

---

## Phase 5 — Click-to-Switch ✅

- [x] **5.1** Click handler on each thumbnail via `ActivateWindowCommand` (RelayCommand)
  - If `IsIconic(hwnd)` → `ShowWindow(hwnd, SW_RESTORE)`
  - `SetForegroundWindow(hwnd)`
  - `MouseBinding` on thumbnail Border in `PreviewTab.xaml`
- [x] **5.2** Workaround for focus-stealing prevention
  - Alt key simulation via `SendInput` before `SetForegroundWindow`
  - Added `INPUT`, `KEYBDINPUT` structs and `SendInput` P/Invoke
- [x] **5.3** Manual test: click on normal, minimized, maximized window

---

## Phase 6 — Applications Tab (Tab 2) ✅

- [x] **6.1** Create `Views/ApplicationsTab.xaml`
  - ListView with checkbox (IsMonitored), display name, process name, window count columns
  - `[Refresh]` button in header, `[Save Configuration]` button in footer
  - Dark theme (#1A1A1A background, #CCCCCC text)
- [x] **6.2** Create `ViewModels/ApplicationsViewModel.cs`
  - `AppEntry` model: ProcessName, DisplayName, WindowCount, IsMonitored (with PropertyChanged)
  - `Refresh()` — calls `WindowEnumerator.DiscoverRunningApps()`, merges with monitored state
  - `Save()` — persists to config, notifies PreviewViewModel via callback
- [x] **6.3** Implement `Refresh` — rescan currently running applications
- [x] **6.4** Implement `Save` — persist the monitored app list to config via `ConfigService`
- [x] **6.5** Synchronize: when apps are saved, `MainViewModel.OnMonitoredAppsChanged` updates `PreviewViewModel.MonitoredApps`
- [x] **6.6** Write and run unit tests (15 tests)
  - `ApplicationsViewModelTests`: defaults, refresh, monitored state preservation, save + callback
  - `AppEntryTests`: defaults, PropertyChanged, no-raise-on-same-value
  - `dotnet test` passes (145 total)

---

## Phase 7 — Configuration & Persistence ✅

- [x] **7.1** Create `Models/AppConfig.cs` — JSON config model
  - `AppConfig`: monitoredApps, layout, thumbnailWidth/Height, panelOpacity, pollingIntervalMs, rememberPosition, panelX/Y, autoStart, closeToTray, hotkey
  - `MonitoredAppConfig`: processName, displayName (separate DTO from MonitoredApp)
  - `LlmConfig`: enabled, provider, endpoint, apiKey, model, staleThresholdSeconds
  - All properties have sensible defaults
- [x] **7.2** Create `Services/ConfigService.cs`
  - `Load()`: read `config.json`, fallback to defaults on missing/corrupt file
  - `Save()`: atomic write (`.tmp` → rename) with `System.Text.Json` (camelCase, indented)
  - `GetConfigPath()`: exe directory primary, `%LOCALAPPDATA%\WinGlance\` fallback
  - Internal constructor for testability (custom config directory)
- [x] **7.3** Load config at startup — `App.xaml.cs` creates `ConfigService` + loads config, passes to `MainWindow`
- [x] **7.4** Save panel position on close — `MainWindow.OnClosing` persists position if `RememberPosition` enabled
- [x] **7.5** Config applied at startup: panel position, opacity, preview settings (layout, thumbnail size, polling interval, monitored apps)
- [x] **7.6** Write and run unit tests (14 tests)
  - `AppConfigTests`: defaults, LlmConfig defaults, MonitoredAppConfig defaults, JSON round-trip
  - `ConfigServiceTests`: nonexistent file, round-trip, corrupt JSON, empty file, path validation, overwrite, partial JSON merge
  - `MainViewModelTests`: constructor applies config to PreviewViewModel, exposes config/service
  - `dotnet test` passes (145 total)

---

## Phase 8 — Settings Tab (Tab 3) ✅

- [x] **8.1** Create `Views/SettingsTab.xaml`
  - Layout: RadioButtons via `LayoutRadioConverter` (Horizontal / Vertical / Grid)
  - Thumbnail size: sliders with value display (width 100–500, height 75–400)
  - Panel opacity: slider 30%–100% with percentage display
  - Polling interval: slider 500ms–5000ms with ms display
  - Checkboxes: remember position, close to tray, auto-start
  - Global hotkey: TextBox (text-editable, recording deferred to Phase 10)
  - LLM Analysis: enabled checkbox, provider dropdown (openai/google/ollama), endpoint, API key, model, stale threshold slider, Edit Prompt button
  - `[Save Configuration]` button
  - LLM section visibility toggled via `BoolToVisibilityConverter`
- [x] **8.2** Create `ViewModels/SettingsViewModel.cs`
  - All config properties with live-apply to PreviewViewModel (layout, thumbnail size, polling interval)
  - Opacity change via callback to MainWindow
  - Commands: `SaveCommand`, `EditPromptCommand`
  - `Save()` writes all settings (including LLM) to config file
  - `EditPrompt()` creates default `prompt.txt` if missing, opens in default editor
- [x] **8.3** Hotkey text input (full keyboard recording deferred to Phase 10 HotkeyService)
- [x] **8.4** Apply changes live: layout → PreviewViewModel, opacity → Window.Opacity callback, polling → PreviewViewModel, thumbnail size → PreviewViewModel
- [x] **8.5** Implement `EditPrompt` — opens `prompt.txt` via `Process.Start` with `UseShellExecute`
- [x] **8.6** Create converters: `BoolToVisibilityConverter`, `LayoutRadioConverter`
- [x] **8.7** Write and run unit tests (25 tests)
  - `SettingsViewModelTests`: constructor init, live-apply to preview, opacity callback, PropertyChanged (8 properties via Theory), save persistence, commands
  - `BoolToVisibilityConverterTests`: true/false/null Convert, ConvertBack
  - `LayoutRadioConverterTests`: matching/non-matching/case-insensitive Convert, ConvertBack true/false
  - `dotnet test` passes (174 total)

---

## Phase 9 — Single Instance & System Tray ✅

- [x] **9.1** Implement named Mutex (`WinGlance_SingleInstance_Mutex`) in `App.xaml.cs`
  - Second instance shows info message and exits
  - Mutex released and disposed in `OnExit`
- [x] **9.2** Integrate system tray icon via `Hardcodet.NotifyIcon.Wpf` (`TaskbarIcon`)
  - Uses `SystemIcons.Application` as tray icon
  - Right-click context menu: Show/Hide, Settings (switches to tab 2), separator, Exit
- [x] **9.3** Implement `[×]` button close-to-tray behavior
  - If `CloseToTray` config → cancel close, hide window instead
  - If not → normal exit (save config, dispose, close tray icon)
  - `_isExiting` flag distinguishes tray-hide from real exit
- [x] **9.4** Double-click on tray icon → `ShowPanel()` (show, restore, activate)
- [x] **9.5** `TogglePanel()` method exposed for hotkey integration (Phase 10)

---

## Phase 10 — Global Hotkey ✅

- [x] **10.1** Create `Services/HotkeyService.cs`
  - `TryParseHotkey()` parses string like "Ctrl+Alt+G" into Win32 modifier flags + virtual key
  - `Register(string)` / `Unregister()` wrap `RegisterHotKey` / `UnregisterHotKey`
  - `HotkeyPressed` event, WndProc hook via `HwndSource.AddHook` for `WM_HOTKEY`
  - Supports Ctrl, Alt, Shift, Win modifiers + letter, digit, and F-key keys
- [x] **10.2** Wire to panel visibility toggle — `HotkeyPressed` → `MainWindow.TogglePanel()`
- [x] **10.3** Hotkey registered from config on load (settings update to be re-registered in future)
- [x] **10.4** Cleanup: `HotkeyService.Dispose()` calls `UnregisterHotKey` + removes WndProc hook
- [x] **10.5** Write and run unit tests (13 tests)
  - `HotkeyServiceTests`: valid parsing (Ctrl+Alt+G, Ctrl+Shift+F1, Alt+G, Ctrl+1), invalid inputs, null, case insensitivity, spaces, Control alias, Win modifier
  - `dotnet test` passes (187 total)

---

## Phase 11 — Theme (Light / Dark) ✅

- [x] **11.1** Create `ResourceDictionary` files for light and dark themes
  - `Themes/DarkTheme.xaml` and `Themes/LightTheme.xaml`
  - Colors: background, text, borders, accents, tabs, buttons, attention states
- [x] **11.2** Create `Services/ThemeService.cs`
  - Read `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`
  - Apply the corresponding `ResourceDictionary` via `MergedDictionaries`
- [x] **11.3** Detect theme changes in real time
  - `SystemEvents.UserPreferenceChanged` → `ApplyTheme` on UI dispatcher
  - Dynamically switches `ResourceDictionary` (removes old, adds new)
- [x] **11.4** Wired into `App.xaml.cs` — `ThemeService.Initialize()` on startup, `Dispose()` on exit
- [x] **11.5** Write and run unit tests (1 test — `ThemeServiceTests`)
  - `dotnet test` passes (200 total)

---

## Phase 12 — DPI & Multi-Monitor & Multi-Desktop ✅

- [x] **12.1** `PerMonitorV2` manifest already applied in `app.manifest` (Phase 0)
- [x] **12.2** `ThumbnailControl` uses DPI-aware `TransformToDevice` for DWM rect calculation
- [x] **12.3** Panel position save/restore handles multi-monitor via absolute coordinates
- [x] **12.4** No additional code needed — WPF + manifest + existing DPI-aware controls handle it

---

## Phase 13 — Error Handling & Resilience ✅

- [x] **13.1** DWM check at startup
  - `DwmIsCompositionEnabled` in `App.xaml.cs` → error message + shutdown if disabled
- [x] **13.2** Handle invalid window handles
  - `DwmUnregisterThumbnail` + silent removal via diff-merge in polling cycle (Phase 3)
- [x] **13.3** Handle config errors
  - Corrupt JSON → default values (ConfigService catches `JsonException`)
  - Read-only directory → fallback to `%LOCALAPPDATA%\WinGlance\`
- [x] **13.4** Handle `SetForegroundWindow` failure (implemented in Phase 5.2)
  - `Alt` key simulation via `SendInput` before `SetForegroundWindow`
- [x] **13.5** Global `DispatcherUnhandledException` handler in `App.xaml.cs`
  - Shows user-friendly MessageBox, marks handled to prevent crash

---

## Phase 14 — Auto-Start with Windows ✅

- [x] **14.1** Create `Services/AutoStartService.cs`
  - `SetAutoStart(bool)` — adds/removes `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` key
  - `IsAutoStartEnabled()` — checks registry for current state
  - Value = `Environment.ProcessPath` (full exe path)
- [x] **14.2** Synchronized with Settings checkbox
  - `SettingsViewModel.Save()` calls `AutoStartService.SetAutoStart()` based on config
- [x] **14.3** Write and run unit tests (1 test — `AutoStartServiceTests`)
  - `dotnet test` passes (200 total)

---

## Phase 15 — Window Attention Detection (FR-9) ✅

- [x] **15.1** Create `Services/AttentionDetector.cs`
  - `RegisterShellHookWindow` to receive `HSHELL_FLASH` notifications
  - WndProc hook via `HwndSource.AddHook`, tracks flashing windows in `HashSet<IntPtr>`
  - Thread-safe with `lock`
- [x] **15.2** Integrate `IsHungAppWindow` check into polling cycle
  - `PreviewViewModel.MergeResults` sets `TrackedWindow.IsHung` on each pass
- [x] **15.3** Integrate `IsWindowEnabled` check into polling cycle
  - Sets `TrackedWindow.IsModalBlocked` flag per window
- [x] **15.4** Add visual indicators in Preview tab
  - `WindowStateToBorderBrushConverter` (IMultiValueConverter) — priority: Hung (red) > Flashing (orange) > ModalBlocked (yellow) > Active (blue) > Inactive (gray)
  - MultiBinding in `PreviewTab.xaml` for IsActive, IsFlashing, IsHung, IsModalBlocked
  - "Not Responding" overlay (red banner) visible when `IsHung` is true
- [x] **15.5** Auto-clear flashing when window gains focus (in polling cycle)
  - `AttentionDetector.ClearFlashing(hwnd)` called when window becomes active
- [x] **15.6** Write and run unit tests (12 tests)
  - `AttentionDetectorTests`: defaults, IsFlashing/ClearFlashing, GetFlashingWindows
  - `WindowStateToBorderBrushConverterTests`: all states, priority logic, empty values
  - `dotnet test` passes (200 total)

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

| Phase | Description                      | Depends on |
| ----- | -------------------------------- | ---------- |
| 0     | Scaffolding & Infrastructure ✅  | —          |
| 1     | Main Window (Shell) ✅           | 0          |
| 2     | Native Layer (P/Invoke) ✅       | 0          |
| 3     | Window Enumeration ✅            | 2          |
| 4     | DWM Thumbnails (Preview Tab) ✅  | 1, 2, 3    |
| 5     | Click-to-Switch ✅               | 4          |
| 6     | Applications Tab (Tab 2) ✅      | 1, 3, 7    |
| 7     | Configuration & Persistence ✅   | 0          |
| 8     | Settings Tab (Tab 3) ✅          | 1, 7       |
| 9     | Single Instance & System Tray ✅ | 1, 7       |
| 10    | Global Hotkey ✅                 | 2, 9       |
| 11    | Theme (Light / Dark) ✅          | 1          |
| 12    | DPI & Multi-Monitor ✅           | 4          |
| 13    | Error Handling & Resilience ✅   | 3, 4, 7    |
| 14    | Auto-Start with Windows ✅       | 7, 8       |
| 15    | Window Attention Detection ✅    | 2, 3, 4    |
| 16    | LLM-Assisted Analysis (FR-10)    | 2, 3, 15   |
| 17    | Polish & Final Touches           | All        |
