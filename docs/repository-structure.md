# Repository Structure Document

This document defines the repository structure of Open Retouch.
It physically reflects the layered architecture defined in `docs/architecture.md` (App → Core → Catalog / Imaging) as a .NET solution layout.

## Project Structure

```
repo/
├── OpenRetouch.slnx              # Solution file
├── Directory.Build.props              # Shared build settings (language version, Nullable, etc.)
├── Directory.Packages.props           # NuGet central package management
├── .editorconfig                      # Coding conventions (analyzer settings)
│
├── src/                               # Product code
│   ├── OpenRetouch.App/          # UI layer (WinUI 3)
│   ├── OpenRetouch.Core/         # Service layer (business logic)
│   ├── OpenRetouch.Catalog/      # Data layer (SQLite)
│   ├── OpenRetouch.Imaging/      # Image processing layer
│   ├── OpenRetouch.Native/       # C++/Rust native modules (P1 and later)
│   └── OpenRetouch.AI/           # Local AI (P1 and later)
│
├── tests/                             # Test code
│   ├── OpenRetouch.Core.Tests/         # Core unit tests
│   ├── OpenRetouch.Catalog.Tests/      # Catalog integration tests (real SQLite)
│   ├── OpenRetouch.Imaging.Tests/      # Image pipeline tests
│   └── TestAssets/                          # Test images and golden images
│
├── docs/                              # Persistent documentation
│   └── ideas/                         # Drafts and ideas
├── .steering/                         # Per-task work documents (history)
├── .claude/                           # Claude Code configuration (skills/commands/agents)
├── scripts/                           # Build and development helper scripts
├── CLAUDE.md                          # Project memory
└── README.md                          # Project overview
```

> **Note**: The MVP implements `App` / `Core` / `Catalog` / `Imaging` and their corresponding test projects. `Native` / `AI` will be added in P1 (the structure is reserved only).

## Directory Details

### src/OpenRetouch.App/ (UI Layer)

**Role**: UI rendering with WinUI 3, user input handling, and the View/ViewModel parts of MVVM

**Structure**:
```
OpenRetouch.App/
├── App.xaml / App.xaml.cs             # App entry point (RequestedTheme="Dark", DI setup)
├── MainWindow.xaml / .xaml.cs         # Main window (Shell)
├── Views/                             # Screens and pages
│   ├── LibraryPage.xaml
│   ├── EditPage.xaml
│   ├── SettingsPage.xaml
│   └── Dialogs/                       # Dialogs such as ExportDialog
├── ViewModels/                        # ViewModels (CommunityToolkit.Mvvm)
│   ├── ShellViewModel.cs
│   ├── LibraryViewModel.cs
│   ├── EditViewModel.cs
│   ├── PresetPanelViewModel.cs
│   ├── ExportDialogViewModel.cs
│   ├── JobProgressViewModel.cs
│   └── FilmstripViewModel.cs
├── Controls/                          # Reusable custom controls
│   ├── PhotoGridView.xaml             # Virtualized grid
│   ├── FilmstripView.xaml
│   ├── HistogramControl.xaml
│   ├── EditSliderPanel.xaml
│   └── RatingControl.xaml
├── Converters/                        # XAML binding converters
├── Themes/                            # Dark theme resources
│   └── DarkTheme.xaml                 # Color token definitions (per the functional design document)
├── Services/                          # UI-only services
│   ├── NavigationService.cs
│   ├── DialogService.cs
│   └── DispatcherService.cs           # UI thread marshaling
└── Assets/                            # Icon and image resources
```

**Naming conventions**:
- View: `[ScreenName]Page.xaml` / Dialog: `[Name]Dialog.xaml`
- ViewModel: `[CorrespondingViewName]ViewModel.cs`
- Custom control: `[Name]View.xaml` or `[Name]Control.xaml`

**Dependencies**:
- Allowed: `OpenRetouch.Core`
- Forbidden: direct references to `Catalog` / `Imaging` (always go through Core's interfaces)

### src/OpenRetouch.Core/ (Service Layer)

**Role**: Business logic, domain models, application services, and the job system. **UI-framework independent** (references to `Microsoft.UI.*` are forbidden)

**Structure**:
```
OpenRetouch.Core/
├── Models/                            # Domain models
│   ├── Photo.cs / ExifInfo.cs
│   ├── Album.cs / Preset.cs
│   ├── PhotoQuery.cs                  # Search criteria
│   └── Enums.cs                       # PhotoFlag / ColorLabel, etc.
├── Editing/                           # Editing domain
│   ├── EditSettings.cs                # Edit parameters (persisted as JSON)
│   ├── BasicAdjustments.cs / CropSettings.cs
│   ├── EditSettingsSerializer.cs      # Serialization + version migration
│   └── PresetMerger.cs                # Preset merge rules
├── Services/                          # Application services
│   ├── Interfaces/                    # IImportService, ICatalogService,
│   │                                  # IEditService, IPresetService, IExportService
│   ├── ImportService.cs
│   ├── CatalogService.cs
│   ├── EditService.cs
│   ├── PresetService.cs
│   └── ExportService.cs
├── Jobs/                              # Job system
│   ├── IJob.cs / IJobQueue.cs / JobStatus.cs
│   ├── JobQueue.cs                    # Priority and concurrency control
│   ├── ImportJob.cs / ThumbnailJob.cs / ExportJob.cs
│   └── JobProgress.cs
├── Export/                            # Export domain
│   ├── ExportSettings.cs / MetadataPolicy.cs
│   └── FileNameTemplate.cs            # Template expansion and sanitization
└── Abstractions/                      # Interfaces for the lower layers
    ├── Repositories/                  # IPhotoRepository, etc. (implemented by Catalog)
    └── Imaging/                       # IThumbnailGenerator, IPreviewRenderer,
                                       # IExportPipeline, IImageDecoder (implemented by Imaging)
```

**Dependencies**:
- Allowed: none (innermost layer; it defines the repository/imaging interfaces itself and has implementations injected)
- Forbidden: App / Catalog / Imaging / WinUI

> **Dependency inversion**: Core defines interfaces in `Abstractions/`, and Catalog / Imaging implement them. DI registration is performed at App startup.

### src/OpenRetouch.Catalog/ (Data Layer)

**Role**: Persistence to SQLite. Implements Core's `Abstractions/Repositories`

**Structure**:
```
OpenRetouch.Catalog/
├── Database/
│   ├── ConnectionFactory.cs           # Connection creation (WAL configuration)
│   ├── MigrationRunner.cs             # user_version-based migrations
│   └── Migrations/
│       └── M001_InitialSchema.sql     # Sequentially numbered SQL files
├── Repositories/
│   ├── PhotoRepository.cs
│   ├── EditRepository.cs
│   ├── AlbumRepository.cs
│   ├── PresetRepository.cs
│   ├── ExportJobRepository.cs
│   └── ThumbnailCacheRepository.cs
├── Backup/
│   └── CatalogBackupService.cs        # Backup and restore at startup
└── Records/                           # DB row record types (internal DTOs)
```

**Naming conventions**:
- Repository: `[EntityName]Repository.cs`
- Migration: `M[3-digit sequence]_[description].sql`

**Dependencies**:
- Allowed: `OpenRetouch.Core` (to implement its interfaces)
- Forbidden: App / Imaging

### src/OpenRetouch.Imaging/ (Image Processing Layer)

**Role**: Decoding/rendering/encoding and cache file management. Implements Core's `Abstractions/Imaging`

**Structure**:
```
OpenRetouch.Imaging/
├── Decoding/
│   ├── IImageDecoder implementation (SkiaImageDecoder.cs)
│   └── ExifReader.cs                  # MetadataExtractor wrapper
├── Rendering/
│   ├── PreviewRenderer.cs             # Two-stage Draft/Full rendering
│   ├── RenderPipeline.cs              # Shared implementation of processing order (WYSIWYG)
│   └── Adjustments/                   # Adjustment operations (one adjustment = one class)
│       ├── WhiteBalanceAdjustment.cs
│       ├── ToneAdjustment.cs          # Exposure/Contrast/HL/SH/Whites/Blacks
│       ├── ColorAdjustment.cs         # Vibrance/Saturation
│       └── GeometryTransform.cs       # Crop/Rotate/Flip
├── Thumbnails/
│   ├── ThumbnailGenerator.cs
│   └── CacheStorage.cs                # Cache file path management, invalidation, LRU
├── Export/
│   ├── ExportPipeline.cs
│   ├── Encoders/                      # JpegEncoder / PngEncoder / TiffEncoder
│   └── MetadataWriter.cs              # Keep/strip EXIF, strip GPS
└── NativeBridge/                      # P/Invoke boundary (implemented in P1)
```

**Dependencies**:
- Allowed: `OpenRetouch.Core` (to implement its interfaces), `OpenRetouch.Native` (P1)
- Forbidden: App / Catalog

### src/OpenRetouch.Native/ and src/OpenRetouch.AI/ (P1 and Later)

**Roles**:
- `Native`: C++/Rust modules such as a LibRaw wrapper (RAW decoding, high-speed image processing)
- `AI`: Local inference with ONNX Runtime + DirectML (Auto Enhance, mask generation)

Empty in the MVP (or not yet added to the solution). They will be added at the start of P1 and connected to the existing `IImageDecoder` / job infrastructure.

### tests/ (Test Directory)

**Structure**:
```
tests/
├── OpenRetouch.Core.Tests/       # Unit tests (fast, no dependencies)
│   ├── Editing/                       # EditSettingsSerializerTests.cs, etc.
│   ├── Services/                      # Tests for each service (using mocks)
│   ├── Jobs/                          # JobQueueTests.cs
│   └── Export/                        # FileNameTemplateTests.cs
├── OpenRetouch.Catalog.Tests/    # Integration tests (using real SQLite files)
│   ├── Repositories/
│   └── MigrationTests.cs
├── OpenRetouch.Imaging.Tests/    # Image pipeline tests
│   ├── Rendering/                     # Golden image comparison tests
│   ├── Export/
│   └── GoldenComparer.cs              # Image comparison helper with tolerance
└── TestAssets/
    ├── Images/                        # Input images for testing (small sizes)
    └── Golden/                        # Expected output images
```

**Naming conventions**:
- Test project: `[TargetProjectName].Tests`
- Test class: `[ClassUnderTest]Tests.cs`
- Test method: `[MethodUnderTest]_[Condition]_[ExpectedResult]` format (e.g., `Merge_PresetWithoutCrop_KeepsExistingCrop`)
- The directory structure inside test projects mirrors the directory structure of the target project

### docs/ (Documentation Directory)

**Documents located here**:
- `product-requirements.md`: Product Requirements Document
- `functional-design.md`: Functional Design Document
- `architecture.md`: Technical Specification
- `repository-structure.md`: Repository Structure Document (this document)
- `development-guidelines.md`: Development Guidelines
- `glossary.md`: Glossary
- `ideas/`: Output of brainstorming sessions (free-form)

### scripts/ (Scripts Directory)

**Files located here**:
- Build and development helper scripts (PowerShell `.ps1` by default)
- Examples: `build.ps1` (solution build), `test.ps1` (run all tests), `gen-test-images.ps1` (generate test images)

### .steering/ (Steering Files)

**Role**: Defines "what to do this time" for a specific development task (retained as history)

**Structure**:
```
.steering/
└── [YYYYMMDD]-[task-name]/
    ├── requirements.md      # Requirements for this task
    ├── design.md            # Design of the changes
    └── tasklist.md          # Task list
```

**Naming convention**: `20260610-add-import-service` format (kebab-case)

### .claude/ (Claude Code Configuration)

**Structure**:
```
.claude/
├── commands/                # Slash commands
├── skills/                  # Skills per task mode
└── agents/                  # Subagent definitions
```

## File Placement Rules

### Source Files

| File type | Location | Naming convention | Example |
|------------|--------|---------|-----|
| View (XAML) | App/Views/ | PascalCase + Page/Dialog | `LibraryPage.xaml` |
| ViewModel | App/ViewModels/ | PascalCase + ViewModel | `LibraryViewModel.cs` |
| Custom control | App/Controls/ | PascalCase + View/Control | `PhotoGridView.xaml` |
| Domain model | Core/Models/ | PascalCase (noun) | `Photo.cs` |
| Service | Core/Services/ | PascalCase + Service | `ImportService.cs` |
| Service interface | Core/Services/Interfaces/ | `I` + service name | `IImportService.cs` |
| Job | Core/Jobs/ | PascalCase + Job | `ExportJob.cs` |
| Repository | Catalog/Repositories/ | PascalCase + Repository | `PhotoRepository.cs` |
| Migration | Catalog/Database/Migrations/ | `M[sequence]_[description].sql` | `M001_InitialSchema.sql` |
| Image processing | Responsibility-based folders under Imaging/ | PascalCase | `PreviewRenderer.cs` |

### Test Files

| Test type | Location | Naming convention | Example |
|-----------|--------|---------|-----|
| Unit test | tests/Core.Tests/ | `[Target]Tests.cs` | `JobQueueTests.cs` |
| Integration test | tests/Catalog.Tests/ | `[Target]Tests.cs` | `PhotoRepositoryTests.cs` |
| Image pipeline | tests/Imaging.Tests/ | `[Target]Tests.cs` | `RenderPipelineTests.cs` |

### Configuration Files

| File type | Location | Notes |
|------------|--------|------|
| Shared build settings | `Directory.Build.props` (root) | TargetFramework, Nullable, analyzers |
| Package versions | `Directory.Packages.props` (root) | Centralized NuGet version management for all projects |
| Coding conventions | `.editorconfig` (root) | C# style + analyzer severities |
| CI definition | `.github/workflows/` | `ci.yml` (build + test) |

## Naming Conventions

### Project Names

- Pattern: `OpenRetouch.[LayerName]` (PascalCase)
- Root namespace = project name. Keep folder structure and namespaces aligned

### Directory Names (Within Projects)

- **Role directories**: plural PascalCase — `Views/`, `Services/`, `Repositories/`, `Jobs/`
- **Domain area directories**: singular allowed — `Editing/`, `Export/`, `Rendering/`
- Vague names (`Utils/`, `Misc/`, `Common/`) are forbidden. Use names that describe the role

### C# File Names

- One public type per file as a rule; file name = type name (PascalCase)
- Interfaces use the `I` prefix
- Grouping small enums/records is acceptable (e.g., `Enums.cs`)

## Dependency Rules

### Dependencies Between Layers (Physically Enforced via Project References)

```
App ──► Core ◄── Catalog
              ◄── Imaging ──► Native (P1)
        Core ◄── AI (P1)
```

- `App` references only `Core` (+ referencing `Catalog` / `Imaging` for DI registration is allowed **only in the startup configuration (Composition Root)**)
- `Catalog` / `Imaging` implement `Core`'s interfaces (dependency inversion)
- `Core` references no other project at all
- The configuration makes circular references impossible at the project-reference level

### Dependencies Within Projects

- References from ViewModel to View are forbidden (one-way MVVM binding)
- The adjustment classes in `Rendering/Adjustments/` do not depend on each other (the pipeline controls ordering)

## Scaling Strategy

### Adding Features

1. **Small features** (extending an existing service): add files to existing directories
2. **Medium features** (new domain areas): create a new domain directory inside Core (e.g., `Core/Masking/`)
3. **Large features** (RAW, AI): add as new projects and connect to Core's interfaces (`Native` / `AI` fall here)

### Managing File Size

- Up to 300 lines per file recommended; splitting strongly recommended above 500 lines
- If a ViewModel grows too large, split it into child ViewModels (e.g., `EditViewModel` → `EditViewModel` + `CropViewModel`)
- If XAML grows too large, extract it into a custom control under `Controls/`

## Exclusion Settings

### .gitignore

```
# .NET / Visual Studio
bin/
obj/
.vs/
*.user
TestResults/

# Build artifacts
artifacts/
*.msix

# Logs and temporary files
*.log
.DS_Store

# Template-derived (when using a Node environment)
node_modules/
```

> **Note**: `.steering/` is **committed** as work history (per the operating policy in CLAUDE.md).
> Keep the images in `tests/TestAssets/` small to avoid repository bloat (consider Git LFS if needed).
