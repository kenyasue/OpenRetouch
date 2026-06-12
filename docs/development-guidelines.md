# Development Guidelines

This document defines the coding conventions, Git workflow, test strategy, and review criteria for developing Open Retouch.
The technology stack follows `docs/architecture.md`, and the directory structure follows `docs/repository-structure.md`.

## Coding Conventions (C#)

### Basic Settings

- .NET 8 / `LangVersion=preview`, `Nullable` enabled (`<Nullable>enable</Nullable>`)
  - Note: preview is required for the partial-property form (`[ObservableProperty] public partial int Foo { get; set; }`) that CommunityToolkit.Mvvm 8.4+ requires on WinUI 3. Partial properties cannot have initializers, so initial values are set in the constructor
- Automatically enforced via `.editorconfig` + .NET Analyzers. Warnings are treated as errors (`TreatWarningsAsErrors=true`)
- Formatting follows `dotnet format` (manual formatting is not debated in reviews)

### Naming Conventions

| Target | Rule | Example |
|------|------|-----|
| Class / record / struct | PascalCase (noun) | `ImportService`, `EditSettings` |
| Interface | `I` + PascalCase | `IPhotoRepository` |
| Method | PascalCase (starts with a verb) | `GenerateThumbnailAsync` |
| Async method | Always has the `Async` suffix | `SaveEditAsync` |
| Property | PascalCase | `FilePath` |
| Local variable / parameter | camelCase | `photoId` |
| Private field | `_` + camelCase | `_jobQueue` |
| Constant | PascalCase | `MaxEditVersions` |
| Enum | PascalCase for both type name and values (singular) | `PhotoFlag.Pick` |
| Boolean | Starts with `Is/Has/Can/Should` | `IsMissing`, `CanExport` |

```csharp
// ✅ Good example
public async Task<string> GenerateThumbnailAsync(Photo photo, CancellationToken ct)

// ❌ Bad example: abbreviation, no verb, no Async suffix
public async Task<string> Thumb(Photo p)
```

### Code Formatting

- **Indentation**: 4 spaces
- **Line length**: 120 characters maximum
- **using directives**: At the top of the file, with `System` sorted first. `ImplicitUsings` enabled
- **Namespaces**: Use file-scoped namespaces (`namespace OpenRetouch.Core.Services;`)
- One public type per file (grouping small enums/records is acceptable)

### Language Feature Usage Policy

- Prefer `record` / `init` for immutable data (domain models, configuration objects)
- Annotate nullable reference types correctly. Do not abuse `!` (null-forgiving operator)
- Use `var` only when the type is obvious from the right-hand side
- Use LINQ when it improves readability. Avoid it on hot paths (rendering loops, etc.)
- `async void` is forbidden (event handlers are the only exception, and even then exception handling is required)
- All public APIs that perform heavy processing or I/O must accept a `CancellationToken`

### Comment Conventions

- Public APIs (Core-layer interfaces and services) must have XML documentation comments
- Inline comments explain "why". "What it does" should be expressed by the code itself

```csharp
/// <summary>
/// Applies a preset to photos. Only the parameters included in the preset are overwritten;
/// parameters not included, such as crop, are preserved.
/// </summary>
Task ApplyPresetAsync(IReadOnlyList<string> photoIds, string presetId);

// ✅ Good example: explains the reason
// SkiaSharp allocates memory based on the declared resolution when decoding, so check the cap first
if (info.Width * info.Height > MaxPixelCount) throw new ImageTooLargeException(...);

// ❌ Bad example: restates the code
// Multiply width by height and compare
```

### Error Handling

**Principles**:
- Define domain exceptions for expected errors and use them appropriately (`ImportFailedException`, `ImageDecodeException`, `ExportItemFailedException`, etc.)
- Never swallow exceptions. When catching, always log + notify the user, or rethrow
- Batch operations (Import/Export) record failures per item and continue overall (follow the error classification in the functional design document)
- Handle exceptions before they reach the UI thread, and never include internal information (paths, stack traces) in user-facing messages

```csharp
// ✅ Record item failures and continue
foreach (var item in job.Items)
{
    try
    {
        await _pipeline.ExportAsync(item.Photo, item.Edit, settings, ct);
        item.MarkCompleted();
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.Error(ex, "Export failed for {PhotoId}", item.Photo.Id);
        item.MarkFailed(ex.Message);
    }
}
```

### Layer Rules (Restated — Mandatory)

- `Core` must not reference `Microsoft.UI.*` (marshaling to the UI thread is the App layer's responsibility)
- Image decoding and DB I/O on the UI thread are forbidden
- SQL must always be parameterized (Dapper). Building SQL via string concatenation is forbidden
- Code that writes to original image paths is forbidden (non-destructive guarantee)
- Do not write business logic in View code-behind (rendering-only processing allowed)

### XAML Conventions

- Resources (colors, styles) must reference the color tokens in `Themes/DarkTheme.xaml`. Hard-coding color literals in XAML is forbidden
- Prefer `x:Bind` (compile-time binding) over `Binding`
- Name controls only when necessary (do not abuse `x:Name`)

## Git Workflow Rules

### Branching Strategy

We adopt GitHub Flow (simple workflow).

**Branch types**:
- `main`: Always builds and passes tests. Direct commits are forbidden
- `feature/[feature-name]`: New feature development (e.g. `feature/import-service`)
- `fix/[fix-description]`: Bug fixes
- `refactor/[target]`: Refactoring
- `docs/[target]`: Documentation-only changes

```
main
  ├─ feature/import-service
  ├─ feature/photo-grid
  └─ fix/exif-parse-error
```

### Commit Message Convention

Conventional Commits format.

```
<type>(<scope>): <subject>

<body>
```

**Type**: `feat` / `fix` / `docs` / `style` / `refactor` / `test` / `chore`

**Scope** (optional): `app` / `core` / `catalog` / `imaging` / `native` / `ai` / `docs`

**Example**:
```
feat(core): Add batch preset application service

- Support applying to multiple photos via ApplyPresetAsync
- Implement preset merge rule (only included parameters are overwritten)
- Save the application result as a new edit version
```

### Pull Request Process

**Pre-creation checks**:
- [ ] `dotnet build` succeeds with no warnings
- [ ] `dotnet test` passes in full
- [ ] `dotnet format --verify-no-changes` passes
- [ ] Related documents (`docs/`) and steering files are updated

**PR template**:
```markdown
## Summary
[Concise description of the change]

## Reason for Change
[Why this change is needed / related .steering task]

## Changes
- [Change 1]
- [Change 2]

## Testing
- [ ] Unit tests added
- [ ] Integration tests added (if applicable)
- [ ] Manual testing performed (attach screenshots for UI changes)

## Related Issue
Closes #[issue number]
```

**Review process**:
1. Self-review (check the entire diff yourself)
2. Confirm CI (build + test) passes
3. Assign reviewers
4. Address feedback
5. After approval, squash merge (keeps history at PR granularity)

## Test Strategy

### Test Pyramid

| Type | Project | Targets | Coverage target |
|------|-------------|------|---------------|
| Unit tests | Core.Tests | Services, domain logic, jobs | 80%+ for the Core layer |
| Integration tests | Catalog.Tests | Repositories + real SQLite, migrations | Full coverage of major CRUD |
| Image pipeline | Imaging.Tests | Rendering and Export (golden comparison) | All adjustment parameters |
| E2E / manual | Checklist | Core workflows and performance | Required before release |

### Test Implementation Conventions

- Frameworks: xUnit + FluentAssertions + NSubstitute
- Test method names: `[Target]_[Condition]_[ExpectedResult]`

```csharp
public class PresetMergerTests
{
    [Fact]
    public void Merge_PresetWithoutCrop_KeepsExistingCrop()
    {
        var current = new EditSettings { Crop = new CropSettings { AspectRatio = "4:5" } };
        var preset = new EditSettings { Basic = new BasicAdjustments { Exposure = 0.5 } };

        var result = PresetMerger.Merge(current, preset);

        result.Basic.Exposure.Should().Be(0.5);
        result.Crop!.AspectRatio.Should().Be("4:5");  // Crop is preserved
    }
}
```

### When to Mock vs. Use the Real Thing

| Dependency | Unit tests | Integration tests |
|------|--------------|-----------|
| Repository | Mock with NSubstitute | Real SQLite (temporary file) |
| File system | Abstract and mock | Temporary folder + test images |
| Image processing | Interface mocks | Real SkiaSharp + golden comparison |
| Job queue | Synchronous execution mode | Real queue |

### Golden Image Tests

- Expected images are placed in `tests/TestAssets/Golden/` (kept small)
- Comparison uses tolerances (mean/max thresholds on pixel differences). `GoldenComparer` is shared
- When updating SkiaSharp, run the full golden test suite for regression verification (per the dependency policy in architecture.md)

### TDD Recommendation

New Core-layer logic (merge rules, template expansion, job state transitions, etc.) is implemented test-first:
1. Write a failing test
2. Pass it with a minimal implementation
3. Refactor

UI (Views/XAML, excluding ViewModels) is out of scope for TDD and is covered by the manual test checklist.

## Code Review Criteria

### Review Points

**Functionality**:
- [ ] Does it satisfy the requirements of the PRD / functional design document?
- [ ] Are edge cases considered (zero photos selected, corrupted files, huge images, invalid paths)?
- [ ] Is cancellation (`CancellationToken`) propagated?

**Architecture**:
- [ ] Are there any layer dependency rule violations (e.g. Core → UI references)?
- [ ] Is any heavy processing done on the UI thread?
- [ ] Does it break the non-destructive guarantee (writes to original images)?

**Readability / Maintainability**:
- [ ] Is naming clear and compliant with the conventions?
- [ ] Has any file grown beyond the ~300-line-per-file guideline?
- [ ] Is there duplicated code (did you check for existing implementations with Grep)?

**Performance**:
- [ ] Are queries and loops designed for large data sets (10,000 photos) (paging/batching)?
- [ ] Are large resources such as bitmaps reliably disposed (`using` / `Dispose`)?
- [ ] Are there unnecessary re-renders or re-queries?

**Security**:
- [ ] Is SQL parameterized?
- [ ] Are paths and file names sanitized?
- [ ] Has any external transmission code slipped in (fully forbidden in the MVP)?

### How to Write Review Comments

State the priority explicitly: `[Required]` / `[Recommended]` / `[Suggestion]` / `[Question]`

```markdown
✅ Good example
[Required] This query has no LIMIT, so it loads everything in a 10,000-photo Catalog.
Please go through PhotoQuery's paging (see: PhotoRepository.QueryAsync).

❌ Bad example
This code is not good.
```

## CI / Quality Automation

GitHub Actions (`.github/workflows/ci.yml`, windows-latest) runs per PR:

1. `dotnet restore --locked-mode`
2. `dotnet build -warnaserror`
3. `dotnet format --verify-no-changes`
4. `dotnet test` (unit + integration + image pipeline)

Merging into main requires all CI checks to pass (branch protection).

## Development Environment Setup

### Required Tools

| Tool | Version | Notes |
|--------|-----------|------|
| Visual Studio 2022 | 17.10 or later | Enable the "WinUI application development" workload |
| .NET SDK | 8.x | Bundled with VS or `winget install Microsoft.DotNet.SDK.8` |
| Windows App SDK | 1.6 or later | Via NuGet (included in project references) |
| Git | Latest | - |

### Setup Steps

```powershell
# 1. Clone the repository
git clone <URL>
cd repo

# 2. Build
dotnet build OpenRetouch.slnx

# 3. Run tests
dotnet test OpenRetouch.slnx

# 4. Launch the app (debug)
# In Visual Studio, set OpenRetouch.App as the startup project and press F5
```

### Integration with Spec-Driven Development

- Before implementing, always check in this order: `CLAUDE.md` → relevant `docs/` documents → existing similar implementations (Grep)
- For each unit of work, create `.steering/[YYYYMMDD]-[task-name]/` and prepare `requirements.md` / `design.md` / `tasklist.md` before implementing
- During implementation, keep updating the progress in `tasklist.md`
- If a specification change occurs, update the relevant `docs/` document before implementing
