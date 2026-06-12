# Local Photo Studio PRD / Technical Specification

> **Note (2026-06-12)**: The product name has been officially decided as **Open Retouch**. This document is preserved as a historical record from the brainstorming phase under its former working title.

## 1. Overview

## 1.1 Product Overview

This product is a **local-first Windows desktop app** with photo management and photo editing capabilities similar to Adobe Lightroom.

It provides no cloud sync; photo management, non-destructive editing, Preset application, batch Export, AI corrections, and more are all completed on the user's local PC.

## 1.2 Working Titles

* Local Photo Studio
* AI Photo Studio
* PhotoFlow Studio
* Cats Photo Studio

## 1.3 Product Concept

**A Lightroom-style local photo management and editing app that runs fast without the cloud**

Primary target users:

* Photographers
* AI image creators
* Social media creators
* Businesses handling e-commerce product photos
* Users who want to organize, correct, and export large volumes of images

---

# 2. Development Policy

## 2.1 Basic Policy

* Do not implement cloud sync
* Develop as a Windows desktop app
* Build the UI with C# / .NET / WinUI 3
* Use SQLite for the Catalog DB
* Use a non-destructive editing model for image editing
* Offload RAW development and high-speed image processing to a Native Module rather than C# alone
* AI features run locally by default
* The MVP does not aim for full Lightroom parity; it focuses on local photo management, basic editing, Presets, and batch processing

---

# 3. Technology Stack

## 3.1 Application

```text
Language: C#
Runtime: .NET 8 / .NET 9
UI Framework: WinUI 3
App Platform: Windows App SDK
Architecture: MVVM
MVVM Library: CommunityToolkit.Mvvm
```

## 3.2 Database

```text
Database: SQLite
ORM candidates:
- Entity Framework Core
- Dapper
```

## 3.3 Image Processing

The following are under consideration for the MVP stage.

```text
C# side:
- ImageSharp
- SkiaSharp

Native side:
- C++ or Rust
- LibRaw
- OpenImageIO
- LittleCMS
- Direct2D / DirectX / Compute Shader
```

## 3.4 RAW Processing

RAW development is handled through a Native Module like the following, rather than C# alone.

```text
C# WinUI App
  ↓
Native RAW Engine
  ↓
LibRaw
  ↓
Preview / 16-bit Image Buffer
```

## 3.5 AI Processing

```text
AI Runtime:
- ONNX Runtime

GPU Acceleration:
- DirectML
- CUDA optional

AI Features:
- Auto Enhance
- Face Detection
- Subject Mask
- Person Mask
- Sky Mask
- Background Mask
- Background Removal
```

---

# 4. Recommended Architecture

```text
Local Photo Studio
│
├─ LocalPhotoStudio.App
│   ├─ WinUI 3
│   ├─ Views
│   ├─ ViewModels
│   ├─ Controls
│   └─ App Shell
│
├─ LocalPhotoStudio.Core
│   ├─ Domain Models
│   ├─ Edit Pipeline
│   ├─ Preset Model
│   ├─ Job System
│   └─ Application Services
│
├─ LocalPhotoStudio.Catalog
│   ├─ SQLite
│   ├─ Photo Repository
│   ├─ Album Repository
│   ├─ Edit Repository
│   ├─ Preset Repository
│   └─ Metadata Repository
│
├─ LocalPhotoStudio.Imaging
│   ├─ Thumbnail Generator
│   ├─ Preview Renderer
│   ├─ Export Pipeline
│   ├─ Color Management
│   └─ Native Bridge
│
├─ LocalPhotoStudio.Native
│   ├─ LibRaw Wrapper
│   ├─ RAW Decode
│   ├─ Image Processing Core
│   ├─ Color Management
│   └─ GPU Processing
│
├─ LocalPhotoStudio.AI
│   ├─ ONNX Runtime
│   ├─ Auto Enhance
│   ├─ Face Detection
│   ├─ Segmentation
│   └─ Background Removal
│
└─ LocalPhotoStudio.Tests
    ├─ Unit Tests
    ├─ Integration Tests
    └─ Image Pipeline Tests
```

---

# 5. Key Features

## 5.1 Photo Import

### Overview

Users import photos into the app from local folders.

### Planned Format Support

MVP:

* JPEG
* PNG
* TIFF

Late MVP or V1:

* RAW
* DNG
* HEIC

### Features

* Folder-level Import
* File-level Import
* Recursive subfolder loading
* Duplicate file detection
* EXIF metadata reading
* Automatic thumbnail generation
* Import progress display
* Log display for failed Import files

---

## 5.2 Library Management

### Overview

Manage photos in a Lightroom-style list view.

### Features

* Grid view
* Single-photo view
* Filmstrip view
* Folder view
* Album/collection management
* Star ratings
* Pick/reject flags
* Color labels
* File name search
* Sort by capture date/time
* Rating filter
* Extension filter
* Camera/lens information display
* EXIF display

---

## 5.3 Non-Destructive Editing

### Overview

The original image file is never modified; only edit parameters are saved to SQLite.

### Basic Policy

```text
Original Image:
- Kept as-is in the user's local folder

Catalog DB:
- Photo paths
- Metadata
- Edit parameters
- Presets
- Rating information
- Album information

Cache:
- Thumbnails
- Previews
- Mask images
```

### Features

* Never overwrite the original image
* Save edit parameters
* Keep edit history
* Reset to the original state
* Before/after comparison
* Copy edit settings
* Paste edit settings
* Batch apply to multiple photos

---

## 5.4 Basic Editing

### Adjustment Items

Basic editing items to implement in the MVP.

```text
- Exposure
- Contrast
- Highlights
- Shadows
- Whites
- Blacks
- Temperature
- Tint
- Saturation
- Vibrance
- Clarity
- Texture
- Dehaze
- Sharpening
- Noise Reduction
```

### Crop / Geometry

```text
- Crop
- Rotate
- Straighten
- Flip Horizontal
- Flip Vertical
- Aspect Ratio Lock
```

### Preview

* Reflect slider changes in real time
* Speed up with low-resolution previews
* Render at high resolution only when needed
* Before / After display

---

## 5.5 Presets

### Overview

Save edit parameters as Presets so they can be applied to other photos.

### Features

* Create Presets
* Apply Presets
* Edit Presets
* Delete Presets
* Manage Preset categories
* Import Presets
* Export Presets
* Batch apply Presets to multiple photos

### Candidate Preset Storage Format

```json
{
  "name": "Warm Portrait",
  "category": "Portrait",
  "settings": {
    "exposure": 0.2,
    "contrast": 10,
    "temperature": 5800,
    "vibrance": 15,
    "saturation": 5
  }
}
```

---

## 5.6 Batch Processing

### Features

* Multiple photo selection
* Batch Preset application
* Batch copy of edit settings
* Batch metadata updates
* Batch Export
* Export queue
* Reprocess error photos only

---

## 5.7 Export

### Supported Formats

```text
- JPEG
- PNG
- TIFF
```

### Export Settings

* Output folder
* File name template
* Resolution setting
* Long edge / short edge setting
* JPEG quality
* Color profile
* Keep/strip metadata
* Add watermark
* Apply sharpening
* Batch Export

### Export Presets

```text
- Original Size JPEG
- Web Optimized JPEG
- Instagram 4:5
- Instagram Square
- YouTube Thumbnail
- EC Product Image
- Print TIFF
```

---

# 6. AI Features

## 6.1 MVP AI Features

The MVP focuses on practical local AI features rather than heavy generative AI.

```text
- Auto Enhance
- Auto White Balance
- Auto Exposure
- Face Detection
- Person Mask
- Subject Mask
- Sky Mask
- Background Mask
- Background Removal
```

## 6.2 Deferred AI Features

The following are for V2 and later.

```text
- Generative Remove
- AI Object Removal Advanced
- Background Generation
- Sky Replacement
- AI Super Resolution
- AI Portrait Retouch Advanced
- Diffusion-based Image Editing
```

## 6.3 AI Execution Model

```text
C# App
  ↓
AI Service Layer
  ↓
ONNX Runtime
  ↓
DirectML / CPU / CUDA optional
```

## 6.4 AI Job Management

AI processing is not executed directly on the UI thread; it goes through a job queue.

```text
AI Job Queue
  ├─ Pending
  ├─ Running
  ├─ Completed
  ├─ Failed
  └─ Cancelled
```

---

# 7. Data Model

## 7.1 Proposed SQLite Tables

```sql
photos
folders
albums
photo_album_map
edits
presets
masks
ratings
export_jobs
metadata
thumbnail_cache
ai_jobs
```

## 7.2 photos

```sql
CREATE TABLE photos (
    id TEXT PRIMARY KEY,
    file_path TEXT NOT NULL,
    file_name TEXT NOT NULL,
    file_extension TEXT NOT NULL,
    file_size INTEGER,
    imported_at TEXT NOT NULL,
    captured_at TEXT,
    width INTEGER,
    height INTEGER,
    camera_make TEXT,
    camera_model TEXT,
    lens_model TEXT,
    iso INTEGER,
    aperture REAL,
    shutter_speed TEXT,
    focal_length REAL,
    rating INTEGER DEFAULT 0,
    flag TEXT,
    color_label TEXT
);
```

## 7.3 edits

```sql
CREATE TABLE edits (
    id TEXT PRIMARY KEY,
    photo_id TEXT NOT NULL,
    version INTEGER NOT NULL,
    edit_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY(photo_id) REFERENCES photos(id)
);
```

## 7.4 presets

```sql
CREATE TABLE presets (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    category TEXT,
    preset_json TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
```

## 7.5 albums

```sql
CREATE TABLE albums (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);
```

## 7.6 photo_album_map

```sql
CREATE TABLE photo_album_map (
    photo_id TEXT NOT NULL,
    album_id TEXT NOT NULL,
    PRIMARY KEY(photo_id, album_id),
    FOREIGN KEY(photo_id) REFERENCES photos(id),
    FOREIGN KEY(album_id) REFERENCES albums(id)
);
```

---

# 8. Non-Destructive Edit JSON

Edit contents are stored in the DB as JSON.

```json
{
  "version": 1,
  "basic": {
    "exposure": 0.35,
    "contrast": 12,
    "highlights": -30,
    "shadows": 20,
    "whites": 5,
    "blacks": -8,
    "temperature": 5400,
    "tint": 4,
    "vibrance": 18,
    "saturation": 5,
    "clarity": 10,
    "texture": 5,
    "dehaze": 0,
    "sharpening": 20,
    "noiseReduction": 10
  },
  "crop": {
    "x": 0.1,
    "y": 0.05,
    "width": 0.8,
    "height": 0.8,
    "rotation": 0.3,
    "aspectRatio": "4:5"
  },
  "masks": [
    {
      "id": "mask_person_001",
      "type": "person",
      "maskPath": "cache/masks/photo123_person.png",
      "adjustments": {
        "exposure": 0.2,
        "texture": -10,
        "saturation": 5
      }
    }
  ]
}
```

---

# 9. Local File Layout

## 9.1 Original Images

Original images remain stored as-is in the user's folders.

```text
D:\Photos\Wedding\IMG_0001.CR3
D:\Photos\Wedding\IMG_0002.CR3
D:\Photos\Travel\IMG_1001.JPG
```

## 9.2 App Data

The app stores the following under AppData.

```text
%AppData%\LocalPhotoStudio\
│
├─ catalog.db
├─ thumbnails\
│   ├─ photo_001.jpg
│   └─ photo_002.jpg
│
├─ previews\
│   ├─ photo_001_preview.jpg
│   └─ photo_002_preview.jpg
│
├─ masks\
│   ├─ photo_001_person.png
│   └─ photo_001_sky.png
│
├─ presets\
│   └─ user_presets.json
│
├─ logs\
│   └─ app.log
│
└─ exports\
```

---

# 10. UI Specification

## 10.1 Main Screen

```text
Top Bar:
- Import
- Export
- Undo
- Redo
- Settings

Left Panel:
- Folders
- Albums
- Filters
- Presets

Center:
- Photo Grid / Photo Preview

Right Panel:
- Histogram
- Metadata
- Edit Controls
- AI Tools

Bottom:
- Filmstrip
- Job Progress
```

## 10.2 Screen Modes

```text
- Library Mode
- Edit Mode
- Export Mode
- AI Tools Mode
- Settings
```

## 10.3 Library Mode

### Left Panel

* Folder list
* Album list
* Rating filter
* File type filter

### Center

* Photo grid
* Thumbnails
* Rating display
* Flag display

### Right Panel

* EXIF
* File information
* Rating
* Color label

## 10.4 Edit Mode

### Left Panel

* Preset list
* Edit history

### Center

* Large photo preview
* Before / After
* Zoom
* Pan
* Crop overlay

### Right Panel

* Basic
* Tone Curve
* Color
* Detail
* Crop
* Mask
* AI

### Bottom

* Filmstrip

---

# 11. MVP Scope

## 11.1 MVP Must Have

```text
1. WinUI 3 desktop app
2. Local folder Import
3. JPEG/PNG/TIFF loading
4. Thumbnail generation
5. Photo grid display
6. Single-photo preview
7. Filmstrip
8. SQLite Catalog
9. Star ratings
10. Flags
11. Albums
12. Basic EXIF display
13. Non-destructive editing
14. Exposure adjustment
15. Contrast adjustment
16. Highlights/Shadows
17. Temperature/Tint
18. Saturation/Vibrance
19. Crop/Rotate
20. Preset save/apply
21. Edit copy & paste
22. Multiple photo selection
23. Batch Preset application
24. JPEG/PNG/TIFF Export
25. Batch Export
```

## 11.2 MVP Should Have

```text
1. Embedded RAW preview display
2. LibRaw integration
3. HSL
4. Tone Curve
5. Sharpening
6. Noise Reduction
7. Auto Enhance
8. Face Detection
9. Person Mask
10. Background Mask
```

## 11.3 Not in the MVP

```text
1. Cloud sync
2. Mobile version
3. Web version
4. Team sharing
5. Lightroom catalog migration
6. Advanced generative AI removal
7. AI background generation
8. Preset marketplace
9. Complex layer editing
10. Photoshop-level compositing
```

---

# 12. Development Phases

## Phase 1: App Foundation

### Purpose

Build the basic structure of the WinUI 3 app.

### Implementation Items

* Create the WinUI 3 project
* MVVM structure
* Screen layouts
* DI setup
* Logging
* Settings
* SQLite connection

---

## Phase 2: Catalog and Import

### Purpose

Enable importing photos and managing them in the Catalog.

### Implementation Items

* Folder selection
* File scanning
* SQLite registration
* EXIF reading
* Thumbnail generation
* Grid display
* Sorting/filtering

---

## Phase 3: Library UX

### Purpose

Build a Lightroom-style photo management UI.

### Implementation Items

* Folder tree
* Albums
* Star ratings
* Flags
* Color labels
* Single-photo preview
* Filmstrip
* Metadata panel

---

## Phase 4: Non-Destructive Editing

### Purpose

Save edit parameters and achieve editing that never destroys the original image.

### Implementation Items

* Edit JSON model
* Basic Adjustments
* Preview Renderer
* Reset
* Before / After
* Edit History
* Copy / Paste Settings

---

## Phase 5: Presets and Batch

### Purpose

Implement Presets and batch processing.

### Implementation Items

* Preset saving
* Preset application
* Preset management
* Multiple photo selection
* Batch edit application
* Batch Export

---

## Phase 6: RAW Support

### Purpose

Support RAW images.

### Implementation Items

* LibRaw Wrapper
* Embedded RAW preview retrieval
* RAW thumbnail generation
* RAW metadata reading
* Initial version of the RAW development pipeline

---

## Phase 7: AI MVP

### Purpose

Add local AI correction features.

### Implementation Items

* ONNX Runtime integration
* DirectML support
* Auto Enhance
* Face Detection
* Person Mask
* Background Mask
* AI Job Queue

---

# 13. Performance Requirements

## 13.1 MVP Targets

```text
- App startup: within 5 seconds
- Importing 1,000 JPEGs: within a practical timeframe
- Managing a Catalog of 500 RAW files: supported
- Thumbnail generation: background processing
- Grid scrolling: smooth operation
- Basic slider response: target within 200ms
- RAW preview response: target within 500ms
- Batch Export of 100 photos: stable operation via queue processing
```

## 13.2 Cache Strategy

```text
- Thumbnail Cache
- Preview Cache
- Edit Preview Cache
- Mask Cache
- Export Temp Cache
```

## 13.3 Background Processing

The following are handled as background jobs.

```text
- Import
- Thumbnail generation
- EXIF extraction
- RAW preview generation
- AI mask generation
- Batch Export
```

---

# 14. Security and Privacy

## 14.1 Basic Policy

* Photos are processed on the local PC
* No cloud sync
* No external transmission without user permission
* AI processing is also performed locally as a rule

## 14.2 Privacy at Export Time

* Selectable keep/strip EXIF
* GPS information removal option
* Author information removal option

---

# 15. Key Technical Risks

## 15.1 RAW Development Quality

Because comparisons with Lightroom are inevitable, RAW development quality is a major risk.

### Mitigations

* Start with embedded RAW preview display initially
* Use LibRaw
* Implement advanced RAW development incrementally

---

## 15.2 Performance

Grid display of large photo libraries and real-time editing are heavy workloads.

### Mitigations

* Thumbnail cache
* Low-resolution previews
* Virtualized lists
* Background jobs
* GPU acceleration

---

## 15.3 Color Management

Color fidelity is critical for a photo editing app.

### Mitigations

* ICC profile support
* Evaluate LittleCMS
* Introduce a 16-bit processing pipeline in the future

---

## 15.4 AI Model Size

For local AI, model size and inference speed are challenges.

### Mitigations

* Use ONNX Runtime
* DirectML support
* Limit the MVP to lightweight AI models
* Defer generative AI

---

# 16. Recommended MVP Positioning

## 16.1 MVP Pitch

**No cloud required. A Lightroom-style desktop app that organizes, corrects, and batch-exports photos fast, entirely locally.**

## 16.2 Initial Target Users

Initially, it is more realistic to target the following users rather than professional photographers.

```text
- AI image creators
- Social media creators
- Users batch-processing large volumes of e-commerce product photos
- Users who do not need Lightroom's full complexity but want photo management and correction in one place
```

## 16.3 Differentiation from Lightroom

```text
- No cloud required
- Local-first
- One-time purchase pricing is possible
- Strong for AI-generated image workflows
- Strong for e-commerce/social media exports
- Simple, lightweight UI
- Specialized for the Windows desktop
```

---

# 17. Implementation Priorities

## Priority 1

```text
- WinUI 3 Shell
- SQLite Catalog
- Folder Import
- Thumbnail Grid
- Single Photo Preview
- Rating / Flag
- Basic Editing
- Non-Destructive Edit JSON
```

## Priority 2

```text
- Presets
- Copy / Paste Settings
- Batch Apply
- Export
- Export Presets
- Filmstrip
```

## Priority 3

```text
- RAW Preview
- LibRaw Integration
- HSL
- Tone Curve
- Noise Reduction
- Sharpening
```

## Priority 4

```text
- Auto Enhance
- Face Detection
- Person Mask
- Background Mask
- ONNX Runtime
- DirectML
```

## Priority 5

```text
- Full RAW Development
- GPU Image Pipeline
- Advanced AI
- Object Removal
- Background Generation
```

---

# 18. Current Conclusion

Building a Lightroom-style local photo management and editing app with C# / .NET / WinUI 3 is entirely feasible.

However, the optimal composition is as follows.

```text
UI / App / Catalog / Workflow:
C# / .NET / WinUI 3

Database:
SQLite

Basic Image Handling:
C# + ImageSharp or SkiaSharp

RAW / High Performance Image Processing:
C++ or Rust Native Module

RAW Decode:
LibRaw

Color Management:
LittleCMS

AI:
ONNX Runtime + DirectML

Storage:
Local AppData Cache
```

Rather than aiming for full Lightroom parity from the start, first complete the following.

```text
Local photo management
+
Non-destructive basic editing
+
Presets
+
Batch Export
+
RAW preview
+
Lightweight AI corrections
```

Building in this order is the most realistic approach.

# UI Theme Requirement Update

## Dark Theme Policy

The UI of this app **always uses the dark theme**.

## Policy

* No light theme is provided
* The app does not follow the system theme setting
* The app always launches with the dark theme
* User-configurable theme switching is not provided in the MVP
* Prioritize visibility during photo editing by using a dark-based background

## Rationale

In a photo editing app, users need to verify image color, brightness, and contrast accurately, so a dark theme is better suited to editing work than a bright UI.

## UI Design Requirement

```text
Theme:
- Always Dark Theme

Background:
- Main background: dark neutral gray
- Panel background: slightly lighter dark gray
- Image preview area: near-black
- Border/separator: low-contrast gray
- Text: high-contrast light gray / white
- Disabled text: muted gray
- Accent color: configurable later, fixed in the MVP

Theme Switching:
- Not supported in MVP
- Ignore OS light/dark mode
- No light theme assets required
```

## WinUI Implementation Note

On the WinUI side, force the Dark Theme across the entire app.

```csharp
<Application
    x:Class="LocalPhotoStudio.App"
    RequestedTheme="Dark">
</Application>
```

Alternatively, specify it explicitly on the Window / Root Element.

```csharp
rootElement.RequestedTheme = ElementTheme.Dark;
```

## Updated Non-Goals

The MVP does not implement the following.

```text
- Light Theme
- System Theme Sync
- Theme Switching
- Custom Theme Editor
```

## Acceptance Criteria

* Even when Windows is in light mode, the app always displays in the dark theme
* The app remains in the dark theme after restarting
* All Library / Edit / Export / Settings screens are uniformly dark themed
* The photo preview area background is dark and does not interfere with photo review
* UI adjustments for a light theme are out of scope for the MVP

```
```
