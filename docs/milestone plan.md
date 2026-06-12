# Open Retouch Milestone Plan

## Milestone 0: Product Foundation

### Purpose

Build the foundation for development and establish a structure that makes it easy to add features later.

### Goals

* The WinUI 3 app launches
* A basic layout exists
* An MVVM structure exists
* The app can connect to SQLite
* The app can write logs
* The app can save a settings file

### Main Implementation Items

* Create the WinUI 3 / .NET project
* Create the App Shell
* Create empty layouts for the Library / Edit / Export / Settings screens
* Introduce CommunityToolkit.Mvvm
* Set up Dependency Injection
* Initialize SQLite
* Create the AppData folder structure
* Introduce Logging
* Basic settings screen

### Completion Criteria

* The app can be launched
* Screen navigation works
* The SQLite DB is created
* Log files are written
* Settings values can be saved and loaded

---

# Milestone 1: Local Catalog MVP

## Purpose

Enable importing photos from local folders and managing them as a Catalog.

## Goal

Build a minimal version of a Lightroom-style "Library screen."

## Main Implementation Items

### Import

* Folder selection
* JPEG / PNG / TIFF scanning
* Recursive subfolder loading
* File information retrieval
* Duplicate checking
* Photo registration into SQLite

### Metadata

* File name
* File path
* Extension
* File size
* Image dimensions
* Creation date/time
* Modification date/time
* Basic EXIF

### Thumbnail

* Thumbnail generation
* Thumbnail cache storage
* Background generation
* Generation progress display

### Library UI

* Photo grid display
* Thumbnail display
* Photo selection
* Single-photo preview
* Basic metadata display

## Completion Criteria

* Images can be imported from a folder
* Photo information is saved to SQLite
* A thumbnail list is displayed
* Selecting an image shows its preview
* The Catalog is restored after restarting the app

## Deliverables

* The first hands-on local photo management app
* A simple Lightroom-style Library supporting JPEG/PNG/TIFF

---

# Milestone 2: Library Workflow

## Purpose

Implement the basic workflow for organizing large numbers of photos.

## Goal

Enable culling, classifying, and searching photos.

## Main Implementation Items

### Rating / Flag

* Star rating 0–5
* Pick flag
* Reject flag
* Color labels

### Album

* Create albums
* Delete albums
* Add photos to albums
* Remove photos from albums
* Per-album view

### Filtering / Sorting

* Star rating filter
* Flag filter
* Color label filter
* File format filter
* Sort by capture date/time
* Sort by import date/time
* Sort by file name

### UI Improvements

* Left panel: folders/albums/filters
* Center: grid
* Right panel: metadata
* Bottom: initial version of the filmstrip

## Completion Criteria

* Photos can be given star ratings
* Pick/reject status can be managed
* Albums can be created to organize photos
* Photos can be filtered by rating or flag
* Scrolling is practical with around 1,000 photos

## Deliverables

* Library features usable for photo culling and organization

---

# Milestone 3: Non-Destructive Editing MVP

## Purpose

Enable basic photo corrections without modifying the original image.

## Goal

Deliver a Lightroom-style basic editing experience.

## Main Implementation Items

### Edit Model

* Create the Edit JSON model
* Save edit parameters to SQLite
* Manage edit state per photo
* Reset feature

### Basic Adjustments

* Exposure
* Contrast
* Highlights
* Shadows
* Whites
* Blacks
* Temperature
* Tint
* Saturation
* Vibrance

### Preview Rendering

* Reflect edits in the preview
* Generate low-resolution previews
* Before / After display
* Real-time saving of edit values
* Initial version of Undo / Redo

### Edit UI

* Edit Mode screen
* Sliders in the right panel
* Numeric input
* Reset button
* Before / After button

## Completion Criteria

* Images can be edited non-destructively
* Slider changes are reflected in the preview
* Edit state persists after restarting the app
* Reset restores the original state
* The original image file is never modified

## Deliverables

* A version that stands on its own as a minimal photo editing app

---

# Milestone 4: Crop, Presets and Editing Workflow

## Purpose

Increase the practicality of photo editing and enable applying edits to multiple photos.

## Goal

Make the app usable for everyday photo editing workflows.

## Main Implementation Items

### Crop / Geometry

* Crop
* Rotate
* Straighten
* Flip Horizontal
* Flip Vertical
* Aspect Ratio Lock
* 1:1
* 4:5
* 16:9
* 3:2

### Additional Editing

* Clarity
* Texture
* Dehaze
* Sharpening
* Noise Reduction

### Presets

* Create Presets
* Save Presets
* Apply Presets
* Delete Presets
* Preset categories
* Preset JSON export
* Preset JSON import

### Copy / Paste Settings

* Copy edit settings
* Paste edit settings
* Batch apply to multiple photos

## Completion Criteria

* Cropping and rotation work
* The current edit state can be turned into a Preset
* Presets can be applied to other photos
* The same edits can be applied to multiple photos
* Edit settings can be copied/pasted

## Deliverables

* A version that enables efficient Preset-based photo editing

---

# Milestone 5: Export and Batch Processing

## Purpose

Enable batch Export of edited photos in practical formats.

## Goal

Enable Export targeting social media, web, e-commerce, and print.

## Main Implementation Items

### Export

* JPEG export
* PNG export
* TIFF export
* Output folder selection
* JPEG quality setting
* Resolution setting
* Long edge / short edge setting
* Color profile setting
* Keep/strip metadata
* Strip GPS information

### Export Presets

* Original Size JPEG
* Web Optimized JPEG
* Instagram Square
* Instagram 4:5
* YouTube Thumbnail
* EC Product Image
* Print TIFF

### Batch Export

* Multiple photo selection
* Batch export
* Export queue
* Progress display
* Cancellation
* Error display
* Retry only failed files

## Completion Criteria

* Edited images can be exported
* Multiple photos can be exported in a batch
* Export with social media / e-commerce Presets works
* Progress is visible during export
* Failure causes are identifiable

## Deliverables

* The first practical MVP that can be put in front of external users

---

# Milestone 6: RAW Preview Support

## Purpose

Enable importing RAW images into the Catalog and previewing/organizing them.

## Goal

Implement the first stage of RAW support.

## Main Implementation Items

### RAW Import

* RAW file detection
* Support for extensions such as CR2 / CR3 / NEF / ARW / RAF / ORF / RW2 / DNG
* RAW metadata retrieval
* SQLite registration

### LibRaw Integration

* Create the Native Module
* LibRaw Wrapper
* Retrieve embedded RAW thumbnails
* Retrieve embedded RAW previews
* Retrieve basic RAW EXIF

### UI

* Display RAW images in the grid
* Display RAW image previews
* Allow filtering by RAW/JPEG

## Completion Criteria

* RAW files can be imported
* RAW thumbnails are displayed
* RAW previews are displayed
* Basic RAW metadata is displayed
* The original RAW files are never modified

## Deliverables

* A version usable for managing and culling RAW photos

---

# Milestone 7: RAW Development Initial Version

## Purpose

Enable basic development processing of RAW images.

## Goal

Go beyond merely displaying RAW files: apply basic corrections and export them.

## Main Implementation Items

### RAW Decode

* RAW decoding via LibRaw
* 16-bit buffer retrieval
* Initial demosaicing support
* White balance application
* Initial camera profile support

### RAW Editing

* Exposure
* Contrast
* White Balance
* Highlights
* Shadows
* Saturation
* Crop
* Sharpening
* Noise Reduction

### RAW Export

* Export RAW edit results to JPEG
* Export RAW edit results to TIFF
* Color space selection at export time

## Completion Criteria

* RAW images can undergo basic development
* Edit parameters can be applied to RAW images
* RAW can be exported to JPEG/TIFF
* RAW is handled with the same non-destructive editing model as JPEG editing

## Deliverables

* A RAW development version that takes the first step toward a Lightroom alternative

---

# Milestone 8: Local AI MVP

## Purpose

Add automatic correction and mask features powered by local AI.

## Goal

Implement AI-assist features that differentiate the product from Lightroom.

## Main Implementation Items

### AI Runtime

* Introduce ONNX Runtime
* DirectML support
* CPU fallback
* AI model management
* AI job queue

### Auto Enhance

* Auto Exposure
* Auto Contrast
* Auto White Balance
* Auto Color
* Portrait Auto Enhance

### Detection / Mask

* Face Detection
* Person Mask
* Subject Mask
* Background Mask
* Sky Mask

### Mask Editing

* Mask display
* Per-mask corrections
* Mask saving
* Mask regeneration

## Completion Criteria

* Auto Enhance sets correction values automatically
* Face detection works
* People/background/sky can be masked
* Corrections can be applied to mask regions only
* The UI does not freeze during AI processing

## Deliverables

* A local Lightroom-style app with AI corrections

---

# Milestone 9: Beta Release

## Purpose

Bring the product to a quality level suitable for distribution to real users.

## Goal

Make the MVP Beta ready for external testing.

## Main Implementation Items

### Stability

* Crash mitigation
* Error handling
* Handling of missing files
* DB migrations
* Log collection
* Settings reset

### Performance

* Grid virtualization
* Thumbnail cache optimization
* Preview cache optimization
* Background job optimization
* Memory usage reduction

### UX Polish

* Keyboard shortcuts
* Right-click menus
* Drag & drop
* Improved progress display
* Empty-state screens
* First-run tutorial

### Installer

* MSIX / EXE installer
* Evaluate auto-update
* Version management
* License display

## Completion Criteria

* General users can install the app
* Core features run without crashing
* Practical to use with 1,000+ photos
* Basic defect logs can be collected
* Distributable to Beta testers

## Deliverables

* Public / Closed Beta Release

---

# Milestone 10: Commercial Release

## Purpose

Reach a state where the product can be sold commercially or officially released.

## Goal

Make the product sellable and continuously improvable.

## Main Implementation Items

### Product

* License management
* Free/paid feature gating
* One-time purchase license
* Trial period
* Update notifications

### Quality

* Bug fixes
* Performance improvements
* Expanded RAW camera support
* AI model improvements
* UI improvements

### Documentation

* User guide
* Shortcut reference
* FAQ
* Troubleshooting
* Release notes

### Marketing

* Website
* Demo videos
* Comparison page
* Screenshots
* Sample Presets
* Use case showcases

## Completion Criteria

* Distributable as a paid version
* Users can purchase / activate licenses
* Documentation is complete
* The product is supportable

## Deliverables

* Version 1.0 Commercial Release

---

# Recommended Release Composition

## Internal Alpha

Audience: developers and internal testers

Included features:

* Catalog
* Import
* Thumbnail
* Basic Library
* Basic Editing

Corresponding Milestones:

* Milestones 0–3

---

## Private Alpha

Audience: a small number of test users

Included features:

* Library Workflow
* Presets
* Batch Apply
* Export

Corresponding Milestones:

* Milestones 0–5

---

## Private Beta

Audience: photographers, AI creators, and e-commerce users

Included features:

* RAW Preview
* Initial RAW development
* Auto Enhance
* Initial version of AI Mask

Corresponding Milestones:

* Milestones 0–8

---

## Public Beta

Audience: general testers

Included features:

* Stabilization
* Installer
* UX Polish
* Performance improvements

Corresponding Milestones:

* Milestones 0–9

---

## Version 1.0

Audience: paying users

Included features:

* Local photo management
* Non-destructive editing
* Presets
* Batch processing
* RAW support
* Local AI corrections
* Export Presets
* License management

Corresponding Milestones:

* Milestones 0–10

---

# Priority Summary

## Highest Priority

1. Local Catalog
2. Thumbnail Grid
3. Non-Destructive Editing
4. Presets
5. Export

## Next Priority

6. RAW Preview
7. RAW Development
8. Batch Processing
9. Auto Enhance
10. AI Mask

## Deferred

11. Advanced RAW Quality
12. Generative Remove
13. Background Generation
14. Marketplace
15. Team Features

---

# Recommended Development Order

```text
Foundation
↓
Local Catalog
↓
Library Workflow
↓
Non-Destructive Editing
↓
Presets
↓
Export
↓
RAW Preview
↓
RAW Development
↓
Local AI
↓
Beta
↓
Commercial Release
```

---

# Initial MVP Definition

## Features to Include in the MVP

```text
- WinUI 3 Desktop App
- SQLite Catalog
- Folder Import
- JPEG/PNG/TIFF Support
- Thumbnail Grid
- Single Photo Preview
- Rating / Flag / Album
- Basic Metadata
- Non-Destructive Editing
- Basic Adjustments
- Crop / Rotate
- Presets
- Copy / Paste Settings
- Batch Apply
- JPEG/PNG/TIFF Export
- Export Presets
```

## Features to Exclude from the MVP

```text
- Cloud Sync
- Mobile App
- Web App
- Team Collaboration
- Marketplace
- Advanced Generative AI
- Photoshop-style Layer Editing
```

---

# Decision

The first practical MVP is fully achievable with **Milestones 0–5** alone.

RAW support and AI features are attractive, but including them from the start would sharply increase development difficulty.
Therefore, first complete **local photo management centered on JPEG/PNG/TIFF + non-destructive editing + Presets + batch Export**.

After that, expanding in the order **RAW Preview → RAW Development → Local AI** is the safest path.
