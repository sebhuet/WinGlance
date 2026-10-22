# Contributing to WinGlance

Thank you for considering contributing to WinGlance! This document explains how to get started.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/<your-account>/WinGlance.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Make your changes, with their tests
5. Run the tests and the linters (see below)
6. Commit and push
7. Open a pull request

Read [doc/ARCHITECTURE.md](doc/ARCHITECTURE.md) first: it explains how the code is organized, which thread runs what, and the constraints that every change must respect (DWM thumbnails drawn above WPF content, fixed item heights).

## Development Setup

- **OS**: Windows 10/11 (required — WinGlance uses Windows-specific APIs)
- **.NET SDK**: 10.0 or later
- **IDE**: Visual Studio 2022+ or VS Code with C# Dev Kit
- **Linters**: [Node.js](https://nodejs.org) (Prettier and markdownlint run through `npx`) and [pre-commit](https://pre-commit.com) (`pip install pre-commit`)

```bash
dotnet build src/WinGlance.slnx
dotnet test src/WinGlance.slnx
pre-commit install   # once: runs the linters on every commit
```

Build output goes to `%TEMP%\WinGlance\` (see `src/Directory.Build.props`), to keep it out of synced folders.

## Code Style

- Follow the `.editorconfig` rules in the repository
- Warnings are errors (`TreatWarningsAsErrors`): the project does not build with warnings
- Nullable reference types are enabled: respect the annotations
- MVVM: logic in ViewModels and Services, minimal code-behind in Views
- All P/Invoke declarations go in `NativeApi/NativeMethods.cs`
- `internal sealed` by default, `public` only when required; file-scoped namespaces
- XML doc comments (`/// <summary>`) on public and internal types, methods and non-trivial properties; inline comments for non-obvious decisions. Comments are in English

## Tests

- xUnit, in `src/WinGlance.Tests/`, mirroring the source folders; names follow `MethodName_Condition_ExpectedResult`
- Code that touches the system takes its dependencies through an `internal` constructor or parameter: use those seams (listed in [ARCHITECTURE.md](doc/ARCHITECTURE.md#seams)) rather than real windows, network or registry
- A test never touches the user's state: no registry write, no file outside a temporary folder, no log file, no real window
- WPF elements must be created on an STA thread: use `ViewTestHelpers.RunOnSta`

Measure the coverage with:

```bash
dotnet test src/WinGlance.slnx --settings src/coverage.runsettings --collect:"XPlat Code Coverage" --results-directory TestResults
```

The report is `TestResults/<run id>/coverage.cobertura.xml`; ARCHITECTURE.md shows how to turn it into HTML.

## Lint

Run before submitting; pre-commit runs the same checks on every commit:

- Everything: `pre-commit run --all-files`
- C#: `dotnet format src/WinGlance.slnx --verify-no-changes`
- Markdown: `npx markdownlint-cli2 "**/*.md"`, formatted by Prettier

## Documentation

- A change visible to the user updates the [user manual](doc/USER_MANUAL.md) (and the README when it changes a feature listed there)
- A change of behavior or configuration updates the [specification](doc/SPECIFICATION.md); a change of structure updates [ARCHITECTURE.md](doc/ARCHITECTURE.md)
- Screenshots in `doc/images/` show staged windows with fictitious content, never real ones: their content is published with the repository

## Pull Request Guidelines

- Keep PRs focused — one feature or fix per PR
- Include unit tests for new functionality
- Ensure the build, the tests and the linters pass before submitting
- Reference relevant issues in your PR description (e.g., "Closes #42")

## Reporting Bugs

Use the [Bug Report](https://github.com/sebhuet/WinGlance/issues/new?template=bug_report.yml) issue template. Include:

- Steps to reproduce
- Expected vs actual behavior
- Windows version and .NET version
- Screenshots if applicable, and the relevant lines of `%LOCALAPPDATA%\WinGlance\winglance.log`

## Suggesting Features

Use the [Feature Request](https://github.com/sebhuet/WinGlance/issues/new?template=feature_request.yml) issue template.
