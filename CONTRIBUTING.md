# Contributing to WinGlance

Thank you for considering contributing to WinGlance! This document explains how to get started.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/sebhuet/WinGlance.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Make your changes
5. Run the tests: `dotnet test src/WinGlance.slnx`
6. Commit and push
7. Open a pull request

## Development Setup

- **.NET SDK**: 10.0 or later
- **IDE**: Visual Studio 2022+ or VS Code with C# Dev Kit
- **OS**: Windows 10/11 (required — WinGlance uses Windows-specific APIs)

```bash
dotnet build src/WinGlance.slnx
dotnet test src/WinGlance.slnx
```

## Code Style

- Follow the `.editorconfig` rules in the repository
- Use `TreatWarningsAsErrors` — the project will not build with warnings
- Use nullable reference types (`#nullable enable`)
- Prefer MVVM patterns: logic in ViewModels and Services, not in code-behind

## Pull Request Guidelines

- Keep PRs focused — one feature or fix per PR
- Include unit tests for new functionality
- Update documentation if your change affects the public API or user-facing behavior
- Ensure `dotnet build` and `dotnet test` pass before submitting
- Reference relevant issues in your PR description (e.g., "Closes #42")

## Reporting Bugs

Use the [Bug Report](https://github.com/sebhuet/WinGlance/issues/new?template=bug_report.yml) issue template. Include:

- Steps to reproduce
- Expected vs actual behavior
- Windows version and .NET version
- Screenshots if applicable

## Suggesting Features

Use the [Feature Request](https://github.com/sebhuet/WinGlance/issues/new?template=feature_request.yml) issue template.

## Architecture Overview

See [doc/SPECIFICATION.md](doc/SPECIFICATION.md) for the full technical specification and [TODO.md](TODO.md) for the development roadmap.

Key directories:

| Path                        | Purpose                                                      |
| --------------------------- | ------------------------------------------------------------ |
| `src/WinGlance/NativeApi/`  | P/Invoke declarations (Win32, DWM)                           |
| `src/WinGlance/Services/`   | Business logic (window enumeration, thumbnails, config, LLM) |
| `src/WinGlance/ViewModels/` | MVVM ViewModels                                              |
| `src/WinGlance/Views/`      | XAML views for each tab                                      |
| `src/WinGlance/Models/`     | Data models                                                  |
| `src/WinGlance.Tests/`      | Unit tests (xUnit)                                           |
