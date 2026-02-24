# WinGlance — User Manual

WinGlance is a portable, always-on-top floating panel for Windows 10 and Windows 11 that displays **live thumbnail previews** of your monitored application windows. It enables instant **one-click window switching**, making it ideal for power users who work with multiple instances of the same application (e.g., several VS Code editors, Chrome windows, or File Explorer instances).

---

## Table of Contents

1. [Getting Started](#1-getting-started)
2. [Interface Overview](#2-interface-overview)
3. [Preview View](#3-preview-view)
4. [Applications View](#4-applications-view)
5. [Settings View](#5-settings-view)
6. [LLM-Assisted Analysis](#6-llm-assisted-analysis)
7. [Global Hotkey](#7-global-hotkey)
8. [Keyboard Shortcuts Reference](#8-keyboard-shortcuts-reference)
9. [Configuration Files](#9-configuration-files)
10. [Default Settings Reference](#10-default-settings-reference)
11. [Troubleshooting / FAQ](#11-troubleshooting--faq)

---

## 1. Getting Started

### 1.1 System Requirements

- **Windows 10 or Windows 11** with Desktop Window Manager (DWM) enabled.
  DWM is enabled by default on all modern Windows systems. It may be disabled in some Remote Desktop sessions.
- No additional runtime or framework installation required (the executable is self-contained).
- No administrator privileges required.

### 1.2 Installation

WinGlance is a **portable single-file application**. There is no installer.

1. Download `WinGlance.exe`.
2. Place it anywhere on your computer — your Desktop, a dedicated folder, or even a USB drive.
3. That's it. Double-click to run.

**To uninstall**: delete `WinGlance.exe` and, optionally, the `config.json` and `prompt.txt` files located next to it (or in `%LOCALAPPDATA%\WinGlance\` if the executable was in a read-only location).

### 1.3 First Launch

When you start WinGlance for the first time:

- A small floating panel appears near the top-left corner of your screen.
- The panel is **empty** because no applications are monitored yet.
- A **system tray icon** appears in your taskbar notification area.

### 1.4 Quick Start

1. **Launch** WinGlance by double-clicking `WinGlance.exe`.
2. **Right-click** anywhere on the panel to open the navigation menu.
3. Select **Applications**.
4. **Check** the applications you want to monitor in the list.
5. Click **Save Configuration**.
6. **Right-click** the panel again and select **Preview**.
7. Live thumbnails of all windows from your selected applications now appear. **Click any thumbnail** to instantly switch to that window.

---

## 2. Interface Overview

### 2.1 The Floating Panel

WinGlance appears as a borderless, semi-transparent window that stays **always on top** of other windows. Key characteristics:

- **Drag** the title bar to move the panel anywhere on screen.
- A **resize grip** at the bottom-right corner lets you resize the panel.
- The panel's **transparency** is adjustable (default: 85% opaque).
- The panel automatically follows your **Windows dark/light theme** and adapts in real time if you change your system theme.

### 2.2 Navigation

WinGlance has three views: **Preview**, **Applications**, and **Settings**.

To switch between views, **right-click anywhere on the panel**. A context menu appears with the following options:

| Menu Item        | Action                                |
| ---------------- | ------------------------------------- |
| **Preview**      | Show live window thumbnails (default) |
| **Applications** | Choose which apps to monitor          |
| **Settings**     | Configure WinGlance                   |
| **Exit**         | Quit the application                  |

### 2.3 Title Bar Buttons

- **Minimize** (—): minimizes the panel to the Windows taskbar.
- **Close** (×): behavior depends on your settings:
  - If **Close to system tray** is enabled (default): hides the panel to the system tray.
  - If disabled: exits the application entirely.

### 2.4 System Tray

WinGlance places an icon in the Windows system tray (notification area) at launch.

| Action                    | Result                                  |
| ------------------------- | --------------------------------------- |
| **Double-click** the icon | Show or restore the panel               |
| **Right-click** the icon  | Context menu: Show/Hide, Settings, Exit |

The tray icon remains visible even when the panel is hidden, so you can always bring it back.

---

## 3. Preview View

### 3.1 Overview

The Preview view is the main view of WinGlance. It displays **live thumbnail previews** of all windows from your monitored applications. Thumbnails are rendered in real time by the Windows Desktop Window Manager — they are not static screenshots and update continuously with zero CPU cost.

### 3.2 Thumbnail Grouping

Thumbnails are **grouped by application**. Each group has a header showing:

- The application icon
- The application name
- The number of open windows in parentheses (e.g., "VS Code (3)")

### 3.3 Layout Modes

Three layout modes are available (configurable in Settings):

| Mode           | Description                                         |
| -------------- | --------------------------------------------------- |
| **Horizontal** | Thumbnails arranged in a horizontal strip (default) |
| **Vertical**   | Thumbnails stacked top-to-bottom                    |
| **Grid**       | Thumbnails arranged in a multi-column grid          |

### 3.4 Thumbnail Contents

Each thumbnail displays:

- A **live preview** of the window content.
- The **window title** below the thumbnail (truncated with "..." if too long).
- Hover over a thumbnail to see the **full window title** in a tooltip.

### 3.5 Switching Windows

**Left-click** on any thumbnail to instantly switch to that window:

- If the window is minimized, it is automatically restored first.
- WinGlance uses a workaround to reliably bring the window to the foreground, even when Windows would normally prevent focus stealing.

### 3.6 Window State Indicators

Thumbnails display **color-coded borders** to communicate the state of each window. States are listed below in priority order (highest first):

| Border Color | State          | Meaning                                                                                                   |
| ------------ | -------------- | --------------------------------------------------------------------------------------------------------- |
| **Red**      | Not Responding | The application has stopped responding. A red "Not Responding" banner also appears on the thumbnail.      |
| **Orange**   | Flashing       | The window is requesting attention (e.g., a file transfer completed, a message was received).             |
| **Yellow**   | Modal Dialog   | The window is blocked by a dialog box (e.g., "Save As", a confirmation prompt). Address the dialog first. |
| **Blue**     | Active         | This is the currently focused window.                                                                     |
| **Gray**     | Inactive       | Normal state — the window is running but not currently focused.                                           |

All indicators update automatically and clear when the condition resolves.

### 3.7 LLM Analysis Indicators

When LLM analysis is enabled (see [Section 6](#6-llm-assisted-analysis)), additional visual cues appear on thumbnails:

| Indicator                              | Meaning                                                                                                           |
| -------------------------------------- | ----------------------------------------------------------------------------------------------------------------- |
| Orange **"Awaiting Action"** badge     | The AI detected that the window likely requires your attention (a dialog, error, prompt, or completed operation). |
| **Dimmed thumbnail** (reduced opacity) | The AI classified the window as idle (no action needed).                                                          |

### 3.8 Dynamic Window Tracking

- When a new window opens for a monitored application, its thumbnail appears automatically (within approximately one second).
- When a window is closed, its thumbnail is removed automatically.
- UWP / Microsoft Store applications (e.g., Calculator, Photos) are correctly identified by their real app name.

---

## 4. Applications View

### 4.1 Overview

The Applications view lists all currently running applications that have visible windows. Use this view to choose which applications WinGlance should monitor.

### 4.2 Application List

Each entry in the list shows:

| Column           | Description                                  |
| ---------------- | -------------------------------------------- |
| **Checkbox**     | Check to monitor, uncheck to stop monitoring |
| **Display Name** | User-friendly application name               |
| **Process**      | Executable process name (read-only)          |
| **Windows**      | Number of visible windows for this app       |

Applications already being monitored have their checkbox checked.

### 4.3 Adding and Removing Applications

1. **Check** the checkbox next to an application to add it to your monitored set.
2. **Uncheck** to remove it.
3. Click **Save Configuration** to apply and persist your changes.
4. Switch back to the Preview view to see the updated thumbnails.

### 4.4 Refreshing the List

Click the **Refresh** button to re-scan currently running applications. This is useful if you launched a new application after opening the Applications view.

---

## 5. Settings View

Open the Settings view by right-clicking the panel and selecting **Settings**, or by right-clicking the tray icon and choosing **Settings**.

### 5.1 Layout

Choose between **Horizontal** (default), **Vertical**, or **Grid** using radio buttons. Changes apply immediately to the Preview view.

### 5.2 Thumbnail Size

| Setting    | Range        | Default | Increment |
| ---------- | ------------ | ------- | --------- |
| **Width**  | 100 – 500 px | 200 px  | 10 px     |
| **Height** | 75 – 400 px  | 150 px  | 10 px     |

Drag the sliders to resize thumbnails. The current value is displayed next to each slider. Changes apply immediately.

### 5.3 Panel Opacity

Controls the transparency of the floating panel.

- **Range**: 30% (very transparent) to 100% (fully opaque)
- **Default**: 85%
- Changes apply immediately.

### 5.4 Polling Interval

How frequently WinGlance scans for window changes (new windows opened, windows closed, title changes).

- **Range**: 500 ms to 5000 ms
- **Default**: 1000 ms (one second)
- Lower values give faster detection but use slightly more CPU.
- Changes apply immediately.

### 5.5 General Options

| Option                       | Default | Description                                                                   |
| ---------------------------- | ------- | ----------------------------------------------------------------------------- |
| **Remember window position** | On      | Saves the panel's position on screen and restores it on next launch.          |
| **Close to system tray**     | On      | Clicking the close button (×) hides the panel to the tray instead of exiting. |
| **Start with Windows**       | Off     | WinGlance launches automatically when you log in to Windows.                  |

### 5.6 Global Hotkey

A text field showing the current hotkey combination (default: `Ctrl+Alt+G`).

- Type the desired key combination using the format: `Modifier+Modifier+Key`
- **Supported modifiers**: `Ctrl`, `Alt`, `Shift`, `Win`
- **Supported keys**: letters `A`–`Z`, digits `0`–`9`, function keys `F1`–`F24`
- Examples: `Ctrl+Alt+G`, `Ctrl+Shift+F1`, `Win+Alt+W`

The hotkey is applied after clicking **Save Configuration**.

### 5.7 LLM Analysis Settings

See [Section 6](#6-llm-assisted-analysis) for a detailed explanation.

### 5.8 Saving Settings

Click **Save Configuration** to persist all settings to `config.json`.

> **Note**: Layout, thumbnail size, opacity, and polling interval apply immediately as you adjust them. However, these changes will be lost on restart if you do not save.

---

## 6. LLM-Assisted Analysis

### 6.1 What It Does

LLM analysis is an **optional advanced feature** that uses an AI model with vision capabilities to detect when a monitored window may require your attention.

Here is how it works:

1. WinGlance periodically captures screenshots of each monitored window and compares them using perceptual hashing.
2. If a window's content has **not changed** for longer than the **stale threshold** (default: 30 seconds), WinGlance sends a single screenshot to the configured AI model.
3. The AI analyzes the screenshot and classifies the window as:
   - **awaiting_action** — the window needs your attention (a dialog, error, prompt, or completed operation).
   - **idle** — nothing to do, the window is showing normal or static content.
4. The result is displayed as a visual indicator on the thumbnail (see [Section 3.7](#37-llm-analysis-indicators)).
5. A new analysis is **only triggered** when the window content changes again and becomes stale again. It does not repeatedly call the AI for the same unchanged window.

### 6.2 Enabling LLM Analysis

1. Open the **Settings** view.
2. Check **Enable LLM analysis**.
3. Configure your preferred provider (see below).
4. Click **Save Configuration**.

LLM analysis is **disabled by default**.

### 6.3 Provider: OpenAI

| Setting      | Value                                                                                                         |
| ------------ | ------------------------------------------------------------------------------------------------------------- |
| **Provider** | openai                                                                                                        |
| **Endpoint** | `https://api.openai.com/v1` (pre-filled)                                                                      |
| **API Key**  | Your OpenAI API key (starts with `sk-...`). Obtain one at [platform.openai.com](https://platform.openai.com). |
| **Model**    | `gpt-4o-mini` (default) or `gpt-4o` for better accuracy. The model must support vision (image input).         |

Each LLM call consumes API credits on your OpenAI account.

### 6.4 Provider: Google Gemini

| Setting      | Value                                                                                            |
| ------------ | ------------------------------------------------------------------------------------------------ |
| **Provider** | google                                                                                           |
| **Endpoint** | `https://generativelanguage.googleapis.com/v1beta`                                               |
| **API Key**  | Your Google AI Studio API key. Obtain one at [aistudio.google.com](https://aistudio.google.com). |
| **Model**    | `gemini-2.0-flash` (recommended) or another vision-capable Gemini model.                         |

Each LLM call consumes API credits on your Google account.

### 6.5 Provider: Ollama (Local)

| Setting      | Value                                                               |
| ------------ | ------------------------------------------------------------------- |
| **Provider** | ollama                                                              |
| **Endpoint** | `http://localhost:11434` (default Ollama URL)                       |
| **API Key**  | Leave empty (Ollama runs locally).                                  |
| **Model**    | A vision-capable model such as `llava`, `llava:13b`, or `bakllava`. |

**Advantages**: fully local, no data leaves your machine, no API costs.

**Prerequisite**: Ollama must be installed and running on your machine. See [ollama.com](https://ollama.com).

### 6.6 Stale Threshold

Controls how long a window must remain visually unchanged before triggering an LLM analysis.

- **Range**: 10 seconds to 120 seconds
- **Default**: 30 seconds
- Lower values trigger analysis sooner but produce more LLM calls (and thus more API cost for cloud providers).

### 6.7 Editing the Prompt

Click **Edit Prompt File** in the LLM settings section to open `prompt.txt` in your default text editor. This file contains the instructions sent to the AI model, telling it how to classify windows.

You can customize the classification rules — for example, to add application-specific patterns or change what counts as "awaiting action".

Changes take effect the next time an analysis is triggered (no restart needed).

---

## 7. Global Hotkey

### 7.1 Usage

Press **Ctrl+Alt+G** (default) from anywhere on your desktop to toggle the WinGlance panel:

- If the panel is **visible**, it hides with a short fade-out animation.
- If the panel is **hidden**, it appears with a fade-in animation.

The hotkey works **system-wide**, regardless of which application currently has focus.

### 7.2 Changing the Hotkey

1. Open the Settings view.
2. Edit the **Global Hotkey** text field (e.g., type `Ctrl+Shift+W`).
3. Click **Save Configuration**.

The new hotkey takes effect immediately after saving (the old one is unregistered and the new one is registered).

If the chosen combination conflicts with another application's hotkey, the registration may silently fail. Try a different combination if the hotkey does not work.

---

## 8. Keyboard Shortcuts Reference

| Shortcut                 | Context               | Action                              |
| ------------------------ | --------------------- | ----------------------------------- |
| **Ctrl+Alt+G** (default) | System-wide           | Toggle panel visibility (show/hide) |
| **Left-click** thumbnail | Preview view          | Switch to the clicked window        |
| **Right-click** panel    | Anywhere on the panel | Open navigation context menu        |
| **Drag** title bar       | Panel title bar       | Move the panel to a new position    |

---

## 9. Configuration Files

### 9.1 config.json

Stores all WinGlance settings: monitored apps, layout, thumbnail size, opacity, polling interval, hotkey, general options, and LLM configuration.

- **Location**: next to `WinGlance.exe` by default.
- **Fallback**: `%LOCALAPPDATA%\WinGlance\config.json` if the executable's folder is read-only (e.g., on a network drive).
- The file is **created automatically** the first time you save your configuration.
- If the file is missing or corrupted, WinGlance starts with default settings.

### 9.2 prompt.txt

Contains the system prompt sent to the AI model during LLM analysis.

- **Location**: next to `WinGlance.exe`.
- Can be freely edited. Changes are picked up without restarting WinGlance.
- If deleted, WinGlance uses a built-in fallback prompt.

---

## 10. Default Settings Reference

| Setting           | Default Value             |
| ----------------- | ------------------------- |
| Layout            | Horizontal                |
| Thumbnail width   | 200 px                    |
| Thumbnail height  | 150 px                    |
| Panel opacity     | 85%                       |
| Polling interval  | 1000 ms                   |
| Remember position | On                        |
| Close to tray     | On                        |
| Auto-start        | Off                       |
| Global hotkey     | Ctrl+Alt+G                |
| LLM analysis      | Off                       |
| LLM provider      | openai                    |
| LLM endpoint      | https://api.openai.com/v1 |
| LLM model         | gpt-4o-mini               |
| Stale threshold   | 30 seconds                |

---

## 11. Troubleshooting / FAQ

**Q: WinGlance shows a "DWM desktop composition is not enabled" error and will not start.**

> DWM is required for live thumbnails. This error typically appears in Remote Desktop sessions where DWM is disabled. Ensure your RDP session has desktop composition enabled, or run WinGlance on the local machine directly.

**Q: I see "WinGlance is already running" when I try to launch it.**

> Only one instance of WinGlance can run at a time. Check your system tray for the existing instance and double-click its icon to show the panel.

**Q: The panel disappeared and I cannot find it.**

> Try one of the following:
>
> - Double-click the WinGlance icon in the system tray.
> - Press the global hotkey (Ctrl+Alt+G by default).
> - If the panel moved off-screen, delete `config.json` to reset the panel position to defaults.

**Q: Thumbnails appear blank or black.**

> This can happen if the source window is on a different virtual desktop or if the DWM session was reset. Try switching back to the application's desktop, or restart WinGlance.

**Q: Clicking a thumbnail does not bring the window to the foreground.**

> Windows has built-in focus-stealing prevention. WinGlance uses a workaround, but it may not work in all cases (e.g., full-screen games or elevated/administrator applications). If the target window is minimized, it should still be restored correctly.

**Q: The global hotkey does not work.**

> Another application may have registered the same key combination. Try changing the hotkey in Settings to a different combination. Also verify the format: modifiers separated by `+`, ending with the key (e.g., `Ctrl+Alt+G`).

**Q: LLM analysis is enabled but no indicators appear.**

> Check the following:
>
> - The provider, endpoint, API key, and model are correctly configured in Settings.
> - The stale threshold has elapsed (wait at least 30 seconds without interacting with the monitored window).
> - Your API key is valid and the chosen model supports vision (image) input.
> - For Ollama, ensure the Ollama service is running locally (`http://localhost:11434`).

**Q: Can I run WinGlance from a USB drive?**

> Yes. WinGlance is fully portable. Place the executable on any drive and run it. Configuration is saved next to the executable. If the drive is read-only, configuration falls back to `%LOCALAPPDATA%\WinGlance\`.

**Q: How do I stop WinGlance from starting with Windows?**

> Open Settings, uncheck **Start with Windows**, and click **Save Configuration**.

**Q: How do I completely exit WinGlance (not just hide it)?**

> Right-click the panel or the system tray icon and select **Exit**. Alternatively, if "Close to system tray" is disabled in Settings, clicking the close button (×) will exit the application.
