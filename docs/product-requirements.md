# Product Requirements Document

## Product Overview

### Name

**Open Retouch** - A local-first, cloud-free desktop application for photo management and editing

> Official name (decided 2026-06-12). Former working title: Local Photo Studio

### Product Concept

- **Local-first**: No cloud sync. Photos, the catalog, edit data, and AI processing are all handled entirely on the user's local PC
- **Lightroom-style management and non-destructive editing**: Provides photo import, organization, star ratings, and album management together with non-destructive editing that never modifies the original images
- **A workflow built for high-volume processing**: Presets, batch apply, and batch export enable fast processing of hundreds to thousands of images

### Product Vision

Enable every creator who works with photos to manage, adjust, and export their images entirely on their own PC, free from cloud contracts and subscriptions. Deliver the core Lightroom experience (catalog management, non-destructive editing, presets) in a simple, fast package, dramatically reducing working time for users such as AI image creators and e-commerce businesses who need to process large volumes of images efficiently. AI-powered adjustments also run locally by default, achieving both privacy and speed.

### Goals

- Provide a cloud-free, one-time-purchase-capable photo management and editing app specialized for the Windows desktop
- Cover the entire workflow — import, organize, adjust, export — within a single application
- Enable safe, unlimited editing and redo through non-destructive editing that keeps original images intact
- Streamline routine work on large image sets via presets and batch processing
- Further reduce manual editing effort with local AI (auto enhance, mask generation)

## Target Users

### Primary Persona: Miki Sato (27, AI image creator / SNS creator)

- Generates several hundred images per day with image-generation AI, then curates, adjusts, and posts them to social media (X/Instagram)
- Works on a Windows desktop (with a GPU). Avoids cloud storage due to capacity and cost concerns
- **Current pain points**: Curating images in File Explorer has hit its limits. Lightroom's subscription is expensive, and its cloud-oriented design and RAW-centric features don't fit her use case
- **Expected solution**: Import large numbers of PNG/JPEG files quickly, cull with star ratings, batch-adjust with presets, and batch-export at SNS-friendly sizes (4:5, square, etc.)
- **Typical workflow**: Generate images → save to a folder → cull (pick/reject) → adjust color and brightness → export resized for SNS → post

### Secondary Persona 1: Kenichi Tamura (42, e-commerce business owner)

- Shoots and processes 100–300 product photos per week for his own e-commerce site
- **Current pain points**: Unifying brightness and white balance across product photos, and exporting at different sizes for each marketplace, takes too much time
- **Expected solution**: Batch-import each shoot, batch-apply per-category presets, and batch-export using prescribed sizes and naming conventions

### Secondary Persona 2: Naoto Yamaguchi (35, semi-professional photographer)

- Shoots commissioned work (weddings, events) on weekends, capturing thousands of shots in RAW+JPEG
- **Current pain points**: Frustrated with Lightroom's subscription cost and its push toward the cloud. Looking for a lightweight tool that works entirely locally
- **Expected solution**: RAW preview display and catalog management, basic adjustments, and batch export for client delivery
- Note: Full RAW development support is planned for late MVP through V1, so the initial focus is on management based on embedded previews

## Success Metrics (KPIs)

### Primary KPIs

- **MVP release**: Release the MVP (all Must Have features) within 6 months of development start
- **Core workflow completion rate**: 70% or more of new users can complete "import → cull → adjust → export" in their first session (measured via usability testing with 10 participants)
- **Weekly Active Users (WAU)**: 500 users 3 months after release
- **Retention**: 30-day post-install retention rate of 30% or higher

### Secondary KPIs

- **Batch processing adoption**: 50% or more of active users use batch export or batch preset application
- **Preset creation rate**: 30% or more of active users create at least one custom preset
- **Crash-free session rate**: 99.5% or higher
- **AI feature adoption** (after AI features ship): 40% or more of active users use Auto Enhance or mask features

> Measurement method: Usage statistics are collected via opt-in, anonymized local telemetry (in accordance with the privacy policy; photo data, file paths, etc. are never transmitted).

## Functional Requirements

### Core Features (MVP)

#### F-01: Photo Import

**User Story**:
As a creator, I want to import local folders quickly, folder by folder, so that I can manage large numbers of local images in the app

**Acceptance Criteria**:
- [ ] Folder-level and file-level import is supported (including recursive subfolder scanning)
- [ ] JPEG / PNG / TIFF files can be imported
- [ ] Original files are never moved or modified during import (referenced files)
- [ ] Duplicate files (same path) are detected and not registered twice
- [ ] EXIF metadata (capture date/time, camera, lens, ISO, aperture, etc.) is read
- [ ] Thumbnails are generated automatically in the background
- [ ] Import progress (count and percentage) is displayed
- [ ] Files that fail to import are listed in a log
- [ ] Importing 1,000 JPEGs completes as a background process, and the UI remains responsive during processing

**Priority**: P0 (Must Have)

#### F-02: Library Management

**User Story**:
As a creator, I want Lightroom-style list management, rating, and filtering so that I can quickly find and cull the photos I need from a large collection

**Acceptance Criteria**:
- [ ] Photo grid view, single-photo preview view, and filmstrip view are available
- [ ] Photos can be browsed by their source folder in a folder tree
- [ ] Albums (collections) can be created, and photos can be added to / removed from them
- [ ] Star ratings (0–5) can be set
- [ ] Pick/Reject flags can be set
- [ ] Color labels can be set
- [ ] File name search is available
- [ ] Photos can be sorted by capture date/time
- [ ] Photos can be filtered by star rating, flag, and file extension
- [ ] EXIF and file information is displayed in a panel
- [ ] Grid scrolling remains smooth even with a 10,000-photo catalog (virtualized list)

**Priority**: P0 (Must Have)

#### F-03: Non-Destructive Editing Foundation

**User Story**:
As a creator, I want non-destructive editing that stores edits as parameters so that I can adjust and redo freely without any risk of losing the original images

**Acceptance Criteria**:
- [ ] Original image files are never modified by editing
- [ ] Edit parameters are stored as JSON in the catalog DB (SQLite)
- [ ] Edit history is retained and previous states can be restored
- [ ] Reset returns the photo to its unedited state
- [ ] Before/After comparison view is available
- [ ] Edit settings can be copied and pasted
- [ ] Edit settings can be applied to multiple photos in one operation
- [ ] Edit state is correctly restored after the app restarts

**Priority**: P0 (Must Have)

#### F-04: Basic Editing (Adjustment Sliders)

**User Story**:
As a creator, I want adjustment sliders that update in real time so that I can intuitively correct the brightness and color of my photos

**Acceptance Criteria**:
- [ ] The following adjustments are available: Exposure / Contrast / Highlights / Shadows / Whites / Blacks / Temperature / Tint / Saturation / Vibrance
- [ ] Slider changes are reflected in the preview within 200 ms (using a low-resolution preview)
- [ ] High-resolution rendering runs after the user stops adjusting
- [ ] Crop, rotate, straighten, horizontal/vertical flip, and fixed aspect ratio are supported
- [ ] A histogram is displayed

**Priority**: P0 (Must Have)

> Clarity / Texture / Dehaze / Sharpening / Noise Reduction / HSL / Tone Curve are P1 (MVP Should Have).

#### F-05: Presets

**User Story**:
As a creator, I want to save and apply edit parameters as presets so that I don't have to set up my standard adjustments manually every time

**Acceptance Criteria**:
- [ ] The current edit parameters can be saved as a preset
- [ ] A preset can be applied to a photo with one click
- [ ] Presets can be edited, deleted, and organized into categories
- [ ] Presets can be imported/exported as JSON files
- [ ] A preset can be batch-applied to multiple selected photos

**Priority**: P0 (Must Have)

#### F-06: Batch Processing

**User Story**:
As an e-commerce business owner, I want batch apply and batch export across multiple photos so that I can process hundreds of product photos efficiently

**Acceptance Criteria**:
- [ ] Multiple photos can be selected in the grid (Ctrl/Shift selection)
- [ ] Batch preset application and batch pasting of edit settings are supported
- [ ] Batch export runs stably through the job queue
- [ ] A 100-photo batch export completes without crashes or data corruption
- [ ] Only the photos that failed to export can be reprocessed

**Priority**: P0 (Must Have)

#### F-07: Export

**User Story**:
As a creator, I want flexible export settings and export presets so that I can output in the optimal format for each purpose (SNS / e-commerce / client delivery)

**Acceptance Criteria**:
- [ ] Export to JPEG / PNG / TIFF is supported
- [ ] Output folder and file name template can be specified
- [ ] Resolution can be specified (by long edge / short edge)
- [ ] JPEG quality can be specified
- [ ] Metadata retention/removal can be selected (including individual removal of GPS data and creator information)
- [ ] Export settings can be saved as presets (e.g., Web JPEG / Instagram 4:5 / e-commerce product image)
- [ ] Export runs as a background job with progress displayed

**Priority**: P0 (Must Have)

### MVP Should Have (P1)

#### F-08: Advanced Editing

Adds the following adjustments: Clarity / Texture / Dehaze / Sharpening / Noise Reduction / HSL / Tone Curve.

**Priority**: P1 (Important)

#### F-09: RAW Support (Phase 1)

Import, embedded preview display, thumbnail generation, and metadata reading for RAW/DNG files via LibRaw integration. The full RAW development pipeline will be implemented incrementally from V1 onward.

**Acceptance Criteria (summary)**:
- [ ] RAW/DNG files can be imported into the catalog
- [ ] RAW embedded previews are displayed within 500 ms
- [ ] Catalog management at the scale of 500 RAW files is supported

**Priority**: P1 (Important)

#### F-10: Local AI Adjustments (AI MVP)

Local AI features powered by ONNX Runtime + DirectML. Everything runs locally; images are never sent externally.

- Auto Enhance / Auto White Balance / Auto Exposure
- Face Detection
- Person Mask / Subject Mask / Sky Mask / Background Mask
- Background Removal
- AI processing is managed by the job queue (Pending / Running / Completed / Failed / Cancelled) and does not block the UI

**Priority**: P1 (Important)

### Future Features (Post-MVP / V2 and later)

- Full RAW development pipeline (16-bit processing, GPU pipeline)
- Generative Remove / advanced AI object removal
- Background generation and Sky Replacement
- AI Super Resolution
- Advanced AI portrait retouching
- Diffusion-based image editing
- Preset marketplace
- Advanced watermark customization

**Priority**: P2 (Nice to Have)

## UI Requirements

### Screen Structure

- **View modes**: Library / Edit / Export / Settings (plus AI Tools in the future)
- **Main screen layout**:
  - Top Bar: Import / Export / Undo / Redo / Settings
  - Left Panel: folder tree / albums / filters / presets
  - Center: photo grid or photo preview
  - Right Panel: histogram / metadata / edit controls / AI tools
  - Bottom: filmstrip / job progress

### Dark Theme Policy (Mandatory)

The application UI is **always dark-themed**.

- No light theme is provided. The app does not follow the OS light/dark mode setting
- The app always displays in the dark theme at launch and after restarts
- User-facing theme switching is not provided in the MVP
- Color scheme policy: the main background is dark neutral gray, panels are a slightly lighter dark gray, the image preview area is nearly black, text is high-contrast light gray/white, and the accent color is fixed in the MVP
- Rationale: a dark UI is better suited to editing work because it allows accurate evaluation of a photo's color, brightness, and contrast

**Acceptance Criteria**:
- [ ] The app always displays in the dark theme even when Windows is in light mode
- [ ] All screens — Library / Edit / Export / Settings — are consistently dark-themed
- [ ] The photo preview area background is dark and does not interfere with reviewing photos

## Non-Functional Requirements

### Performance

- App startup time: within 5 seconds (on a typical Windows PC)
- Basic adjustment slider preview update: within 200 ms
- RAW embedded preview display: within 500 ms (P1 feature)
- Importing 1,000 JPEGs: completes in the background within a practical time, with the UI remaining responsive during processing
- Grid scrolling with a 10,000-photo catalog: smooth via a virtualized list
- 100-photo batch export: runs stably through the job queue (zero crashes or corruption)
- All heavy operations (import / thumbnail generation / EXIF extraction / RAW preview generation / AI mask generation / batch export) run as background jobs

### Reliability

- Non-destructive guarantee for original image files: the app never modifies or corrupts original images (highest priority)
- Catalog DB integrity: the catalog does not become corrupted even after abnormal termination (SQLite transaction management)
- Even when an export fails, the original data and edit data are preserved, and only the failed items can be reprocessed
- Edit parameters are saved in real time, so the most recent edits are not lost even if the app crashes

### Security and Privacy

- All photos and edit data are processed within the local PC and are never transmitted externally without the user's permission
- AI processing also runs locally as a rule (no external transmission of images)
- Telemetry is opt-in, and photo data, file paths, and personal information are never collected
- At export time, users can choose EXIF retention/removal, GPS data removal, and creator information removal

### Usability

- New users can learn the basic "import → cull → adjust → export" flow within 30 minutes
- The UI layout allows Lightroom users to understand the main operations without explanation
- Keyboard shortcuts are provided for primary operations (star ratings, flags, Undo/Redo, etc.)

### Supported Environments

- OS: Windows 10 / 11 (64-bit)
- Distribution: Windows desktop application (a one-time-purchase sales model is envisioned)
- GPU: not required (AI features are accelerated on DirectML-capable GPUs, with CPU fallback)

### Scalability

- Catalog size: manages 10,000+ photos with practical performance
- Cache management: thumbnails / previews / masks / export temporary files are managed under AppData, with growth kept under control

## Technical Assumptions (Reference)

> Details are defined in architecture.md. The constraints at the PRD level are as follows.

- UI: C# / .NET 8 or later / WinUI 3 (Windows App SDK), MVVM architecture
- Catalog DB: SQLite
- Image processing: C# (ImageSharp / SkiaSharp) plus C++/Rust native modules for performance-critical paths (LibRaw, LittleCMS, etc.)
- AI: ONNX Runtime + DirectML (local execution)
- Data storage: original images remain in the user's folders (referenced files); the catalog and caches live under `%AppData%\OpenRetouch\`

## Out of Scope

Items explicitly out of scope (not implemented in the MVP):

- Cloud sync and cloud storage integration
- Mobile and web versions
- Team sharing and collaboration features
- Importing/migrating Lightroom catalogs
- Advanced generative AI features (generative remove, background generation, sky replacement, super resolution, diffusion-based editing)
- Preset marketplace
- Layer editing or Photoshop-class compositing
- Light theme / theme switching / following the system theme / custom theme editor
- macOS / Linux support
