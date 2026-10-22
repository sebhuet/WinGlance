# Claude Code — Project Instructions

WinGlance is a portable WPF (.NET 10, Windows 10/11) floating panel that shows live DWM thumbnails of the windows of monitored applications, with one-click switching, attention indicators (flashing, hung, modal), optional LLM analysis of idle windows, and coding agent badges (Claude Code, OpenCode). Code lives in `src/WinGlance/` (Models, Services, ViewModels, Views, Controls, Converters, NativeApi), tests in `src/WinGlance.Tests/`, docs in `doc/`.

## Language

Respond in **French** when the user writes in French. Use English for code, comments, and technical identifiers.

## Code Style

### Comments

- Add **XML doc comments** (`/// <summary>`) on all public and internal types, methods, and non-trivial properties
- Add **inline comments** for complex logic, non-obvious decisions, and P/Invoke-related behavior
- Comments must be in **English**
- Do not over-comment obvious code (simple getters, trivial assignments)

### C# Conventions

- Use file-scoped namespaces (`namespace Foo;`)
- Use primary constructors where appropriate
- Use `sealed` on classes that are not meant to be inherited
- Use `internal` by default, `public` only when required
- Nullable reference types are enabled — respect nullability annotations
- Follow the `.editorconfig` rules in the repository

## Architecture

- **MVVM pattern**: logic in ViewModels and Services, minimal code-behind in Views
- **P/Invoke**: all native calls go in `NativeApi/NativeMethods.cs`
- **Models**: immutable identity properties, observable mutable properties via `ViewModelBase.SetProperty`
- **Services**: stateless where possible, `internal sealed` classes
- **DWM thumbnails are drawn above WPF content**: anything placed over the thumbnail rectangle is hidden. Put badges and overlays outside it, and keep item heights fixed: a thumbnail whose position shifts without a size change is not redrawn at its new place
- **Claude Code sessions** come from `claude agents --json` (documented interface). Do not read files under `~/.claude`: they are not a stable interface

## Testing

- Unit tests use **xUnit**
- Test project: `src/WinGlance.Tests/`
- Mirror the main project folder structure (e.g., `ViewModels/`, `NativeApi/`, `Models/`, `Services/`)
- Test naming: `MethodName_Condition_ExpectedResult`
- Run tests with: `dotnet test src/WinGlance.slnx --verbosity minimal`
- WPF elements in tests must be created on an STA thread (see `Views/PreviewTabTests.cs`)

## Build

- Solution: `src/WinGlance.slnx`
- Build: `dotnet build src/WinGlance.slnx`
- Test: `dotnet test src/WinGlance.slnx`
- Publish: `dotnet publish src/WinGlance/WinGlance.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish`
- Build output is redirected to `$(TEMP)\WinGlance\` (see `src/Directory.Build.props`) to avoid Google Drive locking

## Lint

Run before handing work back; pre-commit runs the same checks on every commit (`pre-commit install` once).

- Everything: `pre-commit run --all-files`
- C#: `dotnet format src/WinGlance.slnx --verify-no-changes`
- Markdown: `npx markdownlint-cli2 "**/*.md"` (config in `.markdownlint-cli2.yaml`), formatted by Prettier (`.prettierrc`)

## References

- Full specification: `doc/SPECIFICATION.md`
- Development roadmap: `TODO.md`
- GitHub: `https://github.com/sebhuet/WinGlance`
