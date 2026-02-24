# Claude Code — Project Instructions

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

## Testing

- Unit tests use **xUnit**
- Test project: `src/WinGlance.Tests/`
- Mirror the main project folder structure (e.g., `ViewModels/`, `NativeApi/`, `Models/`, `Services/`)
- Test naming: `MethodName_Condition_ExpectedResult`
- Run tests with: `dotnet test src/WinGlance.slnx --verbosity minimal`

## Build

- Solution: `src/WinGlance.slnx`
- Build: `dotnet build src/WinGlance.slnx`
- Test: `dotnet test src/WinGlance.slnx`
- Publish: `dotnet publish src/WinGlance/WinGlance.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true`
- Build output is redirected to `D:\ZZ_Temp\` (see `Directory.Build.props`) to avoid Google Drive locking

## References

- Full specification: `doc/SPECIFICATION.md`
- Development roadmap: `TODO.md`
- GitHub: `https://github.com/sebhuet/WinGlance`
