# WinGlance

A persistent floating panel that displays **live thumbnail previews** of all windows belonging to monitored applications, enabling **one-click window switching**.

When working with multiple windows of the same application (e.g., several VS Code instances), the only way to see previews is by hovering over the taskbar icon — a transient popup that disappears immediately. WinGlance keeps those previews visible at all times.

## Features

- **Live DWM thumbnails** — real-time window previews rendered by the Windows Desktop Window Manager (zero CPU cost)
- **One-click switching** — click any thumbnail to bring that window to the foreground
- **Dynamic tracking** — automatically detects new/closed windows (~1s polling)
- **Multi-app grouping** — thumbnails grouped by application with icons and titles
- **Window attention detection** — highlights windows that are flashing, not responding, or blocked by a modal dialog
- **LLM-assisted analysis** — optional integration with OpenAI, Gemini, or Ollama to intelligently detect windows awaiting user action
- **Coding agent badges** — shows whether Claude Code or OpenCode runs in each window, and whether it is working, idle, or waiting for you
- **Global hotkey** — configurable shortcut (default: Ctrl+Alt+G) to show/hide the panel
- **Dark/light theme** — auto-detects Windows system theme
- **Per-monitor DPI** — works correctly on multi-monitor setups with different scaling
- **System tray** — minimize to tray, single instance enforcement
- **Portable** — single `.exe`, no installer, config stored in `config.json`

## Screenshots

> _Coming soon — the project is under active development._

## Requirements

- Windows 10 or 11
- DWM enabled (default on all modern Windows installations)

## Build from Source

```bash
# Clone the repository
git clone https://github.com/sebhuet/WinGlance.git
cd WinGlance

# Build
dotnet build src/WinGlance.slnx

# Run tests
dotnet test src/WinGlance.slnx

# Publish as a single-file executable
dotnet publish src/WinGlance/WinGlance.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Output: `publish/WinGlance.exe`. Without `-o`, build output goes to `%TEMP%\WinGlance\` (see `src/Directory.Build.props`), to keep it out of synced folders.

## Usage

1. Launch `WinGlance.exe`
2. Right-click the panel and open **Applications**; check the apps you want to monitor and click **Save Configuration**
3. Right-click the panel and open **Preview** to see live thumbnails
4. Click any thumbnail to switch to that window

See the [User Manual](doc/USER_MANUAL.md) for every feature.

### LLM Analysis (optional)

WinGlance can use an LLM with vision capabilities to detect when a window is waiting for user action:

1. Go to **Settings** > **LLM Analysis**
2. Enable and select a provider (OpenAI, Gemini, or Ollama)
3. Enter your API key and model name, then click **Save Configuration** (no restart needed)
4. Customize the system prompt in the **Prompt Editor** view, which also shows a live log of each analysis

The API key is stored encrypted for your Windows account (DPAPI).

## Configuration

Settings are stored in `config.json` next to the executable (falls back to `%LOCALAPPDATA%\WinGlance\config.json` if the directory is read-only). Warnings and errors are logged to `%LOCALAPPDATA%\WinGlance\winglance.log`.

See [doc/SPECIFICATION.md](doc/SPECIFICATION.md) for the full config format.

## Project Structure

```text
WinGlance/
├── src/
│   ├── WinGlance/          # Main WPF application
│   └── WinGlance.Tests/    # xUnit test project
├── doc/
│   ├── SPECIFICATION.md    # Full technical specification
│   └── USER_MANUAL.md      # User manual
├── TODO.md                 # Development roadmap
├── LICENSE                 # GPL-3.0
├── CONTRIBUTING.md         # Contribution guidelines
└── CODE_OF_CONDUCT.md      # Code of conduct
```

## Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting a pull request.

## License

This project is licensed under the **GNU General Public License v3.0** — see [LICENSE](LICENSE) for details.
