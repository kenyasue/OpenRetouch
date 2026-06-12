# Architecture Design Document

This document defines the system structure, technology stack, and technical constraints of Open Retouch.
Prerequisite documents: `docs/product-requirements.md` (PRD) and `docs/functional-design.md` (functional design document).

## Technology Stack

### Languages and Runtime

| Technology | Version | Notes |
|------|-----------|------|
| C# | 12 or later | The C# version corresponding to .NET 8 |
| .NET | 8 (LTS) | LTS support until November 2026. Depending on release timing, consider updating to .NET 10 LTS |
| Windows App SDK | 1.6 or later | Runtime foundation for WinUI 3 |
| Target OS | Windows 10 1809 or later / Windows 11 | Follows the minimum requirements of the Windows App SDK |

### Frameworks and Libraries

| Technology | Version | Purpose | Rationale |
|------|-----------|------|----------|
| WinUI 3 | Bundled with Windows App SDK | UI framework | Modern Windows-native UI. Easy to force a dark theme (`RequestedTheme="Dark"`). Fast rendering possible via integration with Composition/Win2D |
| CommunityToolkit.Mvvm | 8.x | MVVM foundation | Source generators (`[ObservableProperty]`, etc.) reduce boilerplate. Official Microsoft library with a strong track record with WinUI 3 |
| Microsoft.Extensions.DependencyInjection | 8.x | DI container | .NET standard. Loose coupling of services/repositories and improved testability |
| Microsoft.Extensions.Hosting | 8.x | Application foundation | Integrated hosting for DI/configuration/logging |
| Microsoft.Data.Sqlite | 8.x | SQLite driver | Official Microsoft SQLite ADO.NET provider. Supports WAL mode |
| Dapper | 2.x | Data access | Lightweight, fast micro-ORM. Catalog CRUD will be optimized with hand-written SQL, so it offers more control than EF Core |
| Windows.Graphics.Imaging (WIC) | Bundled with OS | Image decoding and thumbnail generation | **SkiaSharp cannot decode TIFF**, so WIC was adopted (decided in M1) because it can handle all supported formats (JPEG/PNG/TIFF) with a single API. Applies EXIF Orientation, supports high-quality Fant downscaling, and being an OS standard component avoids licensing issues |
| SkiaSharp | 3.x | Edit rendering (adoption to be considered in M3) | To be re-evaluated when implementing the tone adjustment pipeline. The expected configuration is to decode TIFF with WIC and then process with Skia |
| MetadataExtractor | 2.x | EXIF reading | The de facto standard for reading the main metadata of JPEG/PNG/TIFF |
| Serilog + Serilog.Sinks.File | 4.x / 6.x | Structured logging | Rolling file logs; failure analysis via structured events |
| System.Text.Json | Bundled with .NET | JSON serialization | JSON persistence for EditSettings, etc. No external dependencies, fast |

### Components to Be Added in P1 and Later

| Technology | Purpose | Notes |
|------|------|------|
| Sdcb.LibRaw 0.21.x | RAW decoding | **Already adopted in M6**. MIT-licensed C# bindings (bundles LibRaw 0.21.1 native, LGPL-2.1). Because it is a managed binding, the originally planned in-house C++ wrapper (`OpenRetouch.Native`) was deemed unnecessary. Since this app is planned to be released as OSS, LGPL is not a problem |
| LittleCMS (Native) | Color management | ICC profile conversion. When introducing the 16-bit pipeline |
| ONNX Runtime + DirectML | Local AI inference | Auto Enhance / mask generation. CPU fallback available |

### Development Tools

| Technology | Version | Purpose | Rationale |
|------|-----------|------|----------|
| Visual Studio 2022 | 17.10 or later | IDE | Official support for WinUI 3 / Windows App SDK development |
| .NET SDK | 8.x | Build | CI builds via `dotnet build` |
| xUnit | 2.x | Unit testing | The standard test framework for .NET |
| FluentAssertions | 6.x | Assertions | Improves test readability |
| NSubstitute | 5.x | Mocking | Concise interface mocking |
| EditorConfig + .NET Analyzers | - | Static analysis | Automatic enforcement of coding conventions (`dotnet format`) |

> **Note**: The Node.js/TypeScript environment in the repository template is for documentation workflows and tooling only; the product itself is developed with the C#/.NET stack above.

## Architecture Patterns

### Layered Architecture + MVVM

```
┌──────────────────────────────────────────────┐
│ OpenRetouch.App (UI layer)               │
│   Views / Controls / ViewModels (MVVM)       │ ← User input and display
├──────────────────────────────────────────────┤
│ OpenRetouch.Core (service layer)         │
│   Application Services / Domain / Job System │ ← Business logic
├──────────────────┬───────────────────────────┤
│ Catalog          │ Imaging                   │
│ (data layer)     │ (image processing layer)  │ ← Persistence / image I/O
│   Repositories   │   Thumbnail / Renderer /  │
│   SQLite         │   Export / Native Bridge  │
├──────────────────┴───────────────────────────┤
│ Native (C++/Rust) / AI (ONNX)  *P1 and later │ ← High-speed processing / inference
└──────────────────────────────────────────────┘
```

#### UI Layer (App)

- **Responsibilities**: Screen rendering, accepting user input, state management via ViewModels
- **Allowed operations**: Calling Core service interfaces, subscribing to progress streams
- **Forbidden operations**: Direct access to Catalog/Imaging, writing SQL or image processing code
- **Convention**: No logic in Views (XAML). Code-behind is limited to rendering-only processing

#### Service Layer (Core)

- **Responsibilities**: Use case implementation (Import/Catalog/Edit/Preset/Export), job management, domain model
- **Allowed operations**: Calling Catalog repositories and Imaging components
- **Forbidden operations**: Dependence on the UI framework (WinUI types). Do not reference `Microsoft.UI.*`
- **Convention**: Notify the UI via `IObservable` / events / `IProgress<T>`

#### Data Layer (Catalog)

- **Responsibilities**: Persistence to SQLite, query execution, schema migrations
- **Allowed operations**: DB access, transaction management
- **Forbidden operations**: Implementing business logic, image processing

#### Image Processing Layer (Imaging)

- **Responsibilities**: Decoding/rendering/encoding, cache file management, Native Bridge
- **Convention**: The processing order of the rendering pipeline follows the functional design document, with a shared implementation for preview and Export (WYSIWYG guarantee)

### Project Structure and Dependency Direction

```
App ──► Core ──► Catalog
              ──► Imaging ──► Native (P1)
              ──► AI (P1)
```

- Dependencies always flow one way, from left to right. Reverse references are physically enforced as build errors via project references
- `Core` is UI-independent (a class library equivalent to netstandard) to make unit testing easy

### Threading Model

| Operation | Execution thread | UI notification method |
|------|------------|-----------|
| UI operations / binding | UI thread | - |
| Draft preview rendering | ThreadPool (dedicated serial queue) | `DispatcherQueue.TryEnqueue` |
| Thumbnail generation | ThreadPool (parallelism = logical cores / 2) | Progress stream |
| Import scan / DB registration | ThreadPool (serial) | Progress stream |
| Export | ThreadPool (parallelism 2) | Progress stream |
| DB access | Caller's thread (async) | - |

- Rendering requests follow a "latest request wins" policy; stale requests are canceled (by replacing the `CancellationTokenSource`)
- SQLite connections serialize writes (single write queue) while allowing concurrent reads (WAL)

## Data Persistence Strategy

### Storage Approach

| Data type | Storage | Format | Rationale |
|-----------|----------|-------------|------|
| Catalog (photos, edits, albums, presets, etc.) | `%AppData%\OpenRetouch\catalog.db` | SQLite (WAL) | Single file, transactions, fast queries |
| Original images | User's folders (reference only) | As-is | Non-destructive guarantee. The app only reads them |
| Thumbnails / previews | `thumbnails\` / `previews\` | JPEG | Regenerable cache. Managed as files rather than stored in the DB (prevents DB bloat) |
| App settings | `settings.json` | JSON | Simple key-value. Human-inspectable |
| Logs | `logs\app-{date}.log` | Text (structured) | For failure analysis. 14-day rotation |

### SQLite Operational Settings

```sql
PRAGMA journal_mode = WAL;      -- Read/write concurrency and crash resilience
PRAGMA synchronous = NORMAL;    -- Balanced performance and safety in combination with WAL
PRAGMA foreign_keys = ON;
PRAGMA user_version = <n>;      -- Schema version (migration at startup)
```

- Migrations check `user_version` at startup and apply any missing migration SQL in sequence (each step runs in a transaction)

### Backup Strategy

- **Target**: `catalog.db` only (caches are excluded because they can be regenerated)
- **Frequency**: Once at app startup, a snapshot is created with the SQLite Online Backup API
- **Destination**: `%AppData%\OpenRetouch\backups\catalog-{yyyyMMdd}.db`
- **Retention**: Keep the 5 most recent generations; delete the oldest first
- **Restore procedure**: If `PRAGMA integrity_check` fails at startup, a recovery dialog offers to restore from the latest backup. Manual restore is done by copying a backup file over `catalog.db`

## Performance Requirements

### Response Times (per PRD)

| Operation | Target time | Measurement environment / method |
|------|---------|---------------|
| App startup (launch to Library display) | Within 5 seconds | Core i5-class / 8GB / SSD, 10,000-photo Catalog. Measured with Stopwatch |
| Draft preview update for basic sliders | Within 200 ms | 24MP JPEG, from input event to screen update |
| Full preview (after interaction stops) | Within 1 second | Same as above |
| RAW embedded preview display (P1) | Within 500 ms | Typical RAW file (30-60 MB) |
| Grid scrolling | 60 fps target / no obvious stutter | Manual check with a 10,000-photo Catalog + ETW profiling |
| Import of 1,000 JPEGs | Completes in the background, UI remains usable | DB registration only (excluding thumbnails) within roughly 5 minutes |
| Batch Export of 100 photos | Completes with zero crashes or corruption | 24MP JPEG, resized to 2048 px on the long edge |

### Resource Usage

| Resource | Target ceiling | Rationale / control method |
|---------|---------|---------------|
| Memory (normal operation) | 1.5 GB or less | Bitmap LRU cache cap (512 MB) + aggressive release of off-screen images |
| Memory (Export peak) | 3 GB or less | Limit full-resolution processing parallelism to 2 |
| CPU (idle) | ~0% | No polling after background jobs complete (event-driven) |
| Disk (cache) | Configured cap (default 20 GB) | When exceeded, delete oldest previews via LRU. Manual clearing available from the settings screen |

## Security Architecture

### Data Protection

- **Fully local**: The MVP contains no code that performs network communication (no HttpClient). Future telemetry would be opt-in, anonymized, and isolated in a separate module
- **Non-destructive guarantee for original images**:
  - The Imaging layer exposes only read-only APIs against original image paths
  - If the Export output destination is the same path as the original image, it is a validation error
- **Access permissions**: AppData resides under the user profile, so OS-standard user isolation applies. No additional encryption in the MVP (the photo data itself is managed by the user)

### Input Validation

- **File paths**: At Import time, verify existence and supported extensions. After expanding file name templates, sanitize invalid characters (`\/:*?"<>|`)
- **Image decoding**: Corrupted files and spoofed extensions are skipped by catching decode exceptions (never crash). Decoding includes size cap checks (prevents memory exhaustion from abnormal declared resolutions)
- **SQL**: All queries are parameterized (Dapper). Building SQL via string concatenation is forbidden
- **Preset import**: JSON schema validation + range clamping (out-of-range values are rounded to the boundary)

### Handling of Error Information

- User-facing messages must not include internal paths or stack traces (details go to the logs)
- Logs are stored locally only. There is no external transmission feature

## Scalability Design

### Handling Data Growth

- **Expected data volume**: 10,000+ photos, with an average of 10 edit versions per photo
- **Measures**:
  - Queries always use paging (`LIMIT/OFFSET` or keyset) + indexes (capture date, rating, folder)
  - The grid uses a virtualized list that materializes only what is displayed
  - Thumbnails/previews are managed on the file system to keep DB size down
  - Edit history has a per-photo cap (50 versions); excess versions are thinned out starting from the oldest
- **Archiving strategy**: Not implemented in the MVP. Substituted with per-folder "Remove from Catalog" (files are kept)

### Feature Extensibility

- **RAW support (P1)**: An `IImageDecoder` abstraction is provided; the MVP uses the SkiaSharp decoder, with a LibRaw decoder additionally registered in P1
- **AI support (P1)**: Only the AI service interfaces are defined in `Core`; the implementation (`OpenRetouch.AI`) is plugged in later. AI processing runs on the existing job infrastructure
- **Edit parameter extension**: Schema evolution via `EditSettings.Version`. Forward compatibility via ignoring unknown fields + default completion
- **Export presets**: Because they are defined in JSON, adding built-in presets only requires adding resource files

## Test Strategy

### Unit Tests

- **Framework**: xUnit + FluentAssertions + NSubstitute
- **Targets**: Core-layer services and domain logic, EditSettings serialization, template expansion, job state transitions
- **Coverage target**: 80% or more for the Core layer (for the App layer, only the main commands of ViewModels)

### Integration Tests

- **Method**: Run with a temporary folder + real SQLite (file-based, not in-memory) + test image sets
- **Targets**: Import → Catalog registration → restore after restart, edit save → restore, Export (golden image comparison)

### E2E Tests

- **Tooling**: A manual test checklist for the MVP (UI automation for WinUI 3 has a poor cost-benefit ratio). Adoption of Windows App SDK UI tests for smoke tests only will be considered in the future
- **Scenarios**: Core workflow of Import → culling → adjustment → Export, UI interaction during a 1,000-photo Import, dark theme display verification (on an OS light-mode environment)

### CI

- GitHub Actions (windows-latest) runs `dotnet build` + `dotnet test` (unit + integration)
- Golden tests for the image pipeline use tolerance-based comparison (absorbs platform differences)

## Technical Constraints

### Environment Requirements

- **OS**: Windows 10 1809 or later / Windows 11 (64-bit)
- **Minimum memory**: 8 GB recommended (4 GB minimum)
- **Required disk space**: App itself ~500 MB + cache (default cap 20 GB, configurable)
- **GPU**: Not required. AI features (P1) are accelerated on DirectML-capable GPUs, with CPU fallback otherwise
- **Required external dependencies**: Windows App SDK Runtime (included in the self-contained distribution)

### Performance Constraints

- Limit the number of full-resolution images held simultaneously (parallelism control) to prevent OOM
- Image decoding and DB I/O on the UI thread are forbidden (enforced via analyzers/reviews)

### Security Constraints

- Implementing external network communication is forbidden (MVP)
- APIs that write to original image paths are forbidden
- SQL must always be parameterized

## Distribution and Updates

- **Distribution format**: MSIX (self-contained / Windows App SDK bundled). Sideloading or Microsoft Store distribution is assumed
- **Updates**: Manual updates in the MVP (installing a new version). Automatic updates to be considered post-MVP
- **Signing**: Signed with a code signing certificate (to be obtained before distribution)

## Dependency Management

| Library | Purpose | Version management policy |
|-----------|------|-------------------|
| Windows App SDK | UI foundation | Pinned to minor version (update only after verifying behavior) |
| CommunityToolkit.Mvvm | MVVM | `^8.x` (minor updates allowed) |
| Dapper / Microsoft.Data.Sqlite | DB | Minor updates allowed; major versions after verification |
| SkiaSharp | Image processing | **Fully pinned** (risk of changed rendering output; verify with golden tests when updating) |
| MetadataExtractor | EXIF | Minor updates allowed |
| Serilog | Logging | Minor updates allowed |

- NuGet packages are centrally managed in `Directory.Packages.props` (Central Package Management)
- Lock files (`packages.lock.json`) are enabled, and CI restores with `--locked-mode`
