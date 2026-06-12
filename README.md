# Open Retouch

A local-first, Lightroom-style photo management and editing desktop app for Windows — no cloud required.

## Project Overview

Open Retouch lets photographers, AI-image creators, and e-commerce sellers manage, cull, retouch, and export large volumes of photos entirely on their own PC. Photos, the catalog, edit data, and (future) AI processing never leave the local machine — no subscription, no cloud sync, no telemetry.

- **Stack**: C# / .NET 8 / WinUI 3 (Windows App SDK) / SQLite
- **Principles**: non-destructive editing, always-dark theme, nothing sent off-device
- **Documentation**: see `docs/` (PRD / functional design / architecture / repository structure / development guidelines / glossary)
- **Roadmap**: `docs/milestone plan.md` (Milestones 0–10)

## Features

Milestones 0–5 form the practical MVP; M6/M7 and XMP support are also implemented.

| Area | Features | Status |
|------|----------|--------|
| Foundation (M0) | WinUI 3 shell, MVVM, DI, SQLite catalog, logging, settings | ✅ |
| Local Catalog (M1) | Folder import (JPEG/PNG/TIFF), EXIF, thumbnails, grid view, preview | ✅ |
| Library Workflow (M2) | Star ratings, pick/reject flags, color labels (keyboard-driven), albums, filtering, sorting, filmstrip | ✅ |
| Non-Destructive Editing (M3) | 10 basic adjustments, real-time preview, before/after, undo/redo, state restored across restarts | ✅ |
| Crop & Presets (M4) | Crop, rotate, flip, straighten; 5 detail adjustments; presets (JSON import/export); copy & paste edits; batch apply | ✅ |
| Export & Batch (M5) | JPEG/PNG/TIFF export, 7 export presets, batch export queue, retry failed items only | ✅ |
| RAW Preview (M6) | RAW import (CR2/CR3/NEF/ARW/RAF/ORF/RW2/DNG), embedded preview thumbnails, RAW filter | ✅ |
| RAW Development (M7) | LibRaw development (demosaic + camera WB + color matrix), non-destructive edits on RAW, JPEG/TIFF export | ✅ |
| XMP Sidecars | Reads Lightroom-compatible XMP (develop settings, ratings, labels → developed-state thumbnails); writes XMP when editing RAW (merges with existing sidecars) | ✅ |
| M8–M10 | Local AI, Beta (installer, etc.), official release | Planned |

## Requirements

| Tool | Version |
|------|---------|
| Windows | 10 (1809+) / 11 |
| .NET SDK | 8 or later (development verified on 10.0.300) |
| Visual Studio 2022 | 17.10+ with the WinUI application development workload (not needed for CLI-only builds) |

## How to Build

```powershell
# Build
dotnet build OpenRetouch.slnx

# Run tests
dotnet test OpenRetouch.slnx

# Verify formatting
dotnet format OpenRetouch.slnx --verify-no-changes
```

## How to Run the App

```powershell
# Launch the app (unpackaged)
.\src\OpenRetouch.App\bin\Debug\net8.0-windows10.0.19041.0\win-x64\OpenRetouch.App.exe
```

If the exe is missing or stale, build the App project explicitly first:

```powershell
dotnet build src\OpenRetouch.App\OpenRetouch.App.csproj
```

App data (catalog DB, caches, logs, settings) is created under `%AppData%\OpenRetouch\`.

## Solution Structure

```
src/
├── OpenRetouch.App      # WinUI 3 UI (MVVM, composition root)
├── OpenRetouch.Core     # Business logic (UI-independent)
├── OpenRetouch.Catalog  # SQLite catalog
└── OpenRetouch.Imaging  # Image pipeline (WIC/LibRaw)
tests/
├── OpenRetouch.Core.Tests
├── OpenRetouch.Catalog.Tests
└── OpenRetouch.Imaging.Tests
```

See `docs/repository-structure.md` for details.

## Development Process

This project uses spec-driven development. Each unit of work gets a `.steering/[YYYYMMDD]-[task-name]/` directory and is implemented against its requirements / design / tasklist documents. See `CLAUDE.md` for details.

## Third-Party Licenses

| Library | License | Purpose |
|---------|---------|---------|
| [LibRaw](https://www.libraw.org/) 0.21.1 | LGPL-2.1 / CDDL-1.0 | RAW decoding |
| [Sdcb.LibRaw](https://github.com/sdcb/Sdcb.LibRaw) | MIT | C# bindings for LibRaw |
| CommunityToolkit.Mvvm / Microsoft.* / Dapper / Serilog / MetadataExtractor | MIT / Apache-2.0 | MVVM / DI / DB / logging / EXIF |

This app is planned to be released as open source.

---

> This repository was set up from the template that accompanies the book "Hands-on Introduction to Claude Code" (Gijutsu-Hyohron Co.).
