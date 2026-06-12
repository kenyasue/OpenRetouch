using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;

namespace OpenRetouch.App.ViewModels;

/// <summary>
/// ViewModel for the Library screen.
/// Manages the photo list, filters, sorting, folders/albums, culling (rating/flag/label), and single-photo view.
/// </summary>
public sealed partial class LibraryViewModel : ObservableObject
{
    private readonly ICatalogService _catalogService;
    private readonly IImportService _importService;
    private readonly IEditService _editService;
    private readonly IPresetService _presetService;
    private readonly IAutoToneService _autoToneService;
    private readonly ILogger<LibraryViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly Dictionary<string, PhotoItemViewModel> _itemsById = [];
    private bool _suppressReload;

    public LibraryViewModel(
        ICatalogService catalogService,
        IImportService importService,
        IEditService editService,
        IPresetService presetService,
        IAutoToneService autoToneService,
        ILogger<LibraryViewModel> logger)
    {
        _catalogService = catalogService;
        _importService = importService;
        _editService = editService;
        _presetService = presetService;
        _autoToneService = autoToneService;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Marshal notifications from worker threads onto the UI thread
        _catalogService.ThumbnailReady += (_, e) =>
            _dispatcherQueue.TryEnqueue(() => OnThumbnailReady(e));
        _importService.ImportCompleted += (_, e) =>
            _dispatcherQueue.TryEnqueue(() => OnImportCompleted(e));

        Photos.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsEmptyStateVisible));
    }

    // ---- List / selection ----

    public ObservableCollection<PhotoItemViewModel> Photos { get; } = [];

    /// <summary>
    /// Year/month groups (for the grid view). Shares the same order and instances as Photos.
    /// </summary>
    public ObservableCollection<PhotoGroup> PhotoGroups { get; } = [];

    /// <summary>Timeline bar entries (1:1 with PhotoGroups).</summary>
    public ObservableCollection<TimelineEntryViewModel> TimelineEntries { get; } = [];

    /// <summary>Whether to show the timeline bar (only when sorting by capture date).</summary>
    [ObservableProperty]
    public partial bool IsTimelineVisible { get; set; }

    /// <summary>Index of the currently highlighted group (cache to avoid scanning all items).</summary>
    private int _currentGroupIndex = -1;

    /// <summary>Photos in multi-selection (synced with the GridView's SelectedItems).</summary>
    public ObservableCollection<PhotoItemViewModel> SelectedPhotos { get; } = [];

    [ObservableProperty]
    public partial PhotoItemViewModel? SelectedPhoto { get; set; }

    [ObservableProperty]
    public partial bool IsSinglePhotoView { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    public bool HasSelection => SelectedPhoto is not null;

    public bool IsGridVisible => !IsSinglePhotoView;

    public bool IsEmptyStateVisible => Photos.Count == 0;

    // ---- Folders / Albums ----

    public ObservableCollection<Folder> Folders { get; } = [];

    public ObservableCollection<Album> Albums { get; } = [];

    [ObservableProperty]
    public partial Folder? SelectedFolder { get; set; }

    [ObservableProperty]
    public partial Album? SelectedAlbum { get; set; }

    /// <summary>Whether an album is being shown (controls visibility of "Remove from Album" in the context menu).</summary>
    public bool IsAlbumSelected => SelectedAlbum is not null;

    [ObservableProperty]
    public partial string NewAlbumName { get; set; } = "";

    // ---- Filters / Sorting ----

    /// <summary>Minimum star rating (0 = all).</summary>
    [ObservableProperty]
    public partial int MinRatingFilter { get; set; }

    /// <summary>Flag filter index (0=All 1=Pick 2=Reject 3=Unflagged).</summary>
    [ObservableProperty]
    public partial int FlagFilterIndex { get; set; }

    /// <summary>Color label filter index (0=All 1-5=Red..Purple).</summary>
    [ObservableProperty]
    public partial int ColorLabelFilterIndex { get; set; }

    /// <summary>Extension filter index (0=All 1=JPEG 2=PNG 3=TIFF).</summary>
    [ObservableProperty]
    public partial int ExtensionFilterIndex { get; set; }

    /// <summary>Sort field index (0=Capture date 1=Import date 2=File name).</summary>
    [ObservableProperty]
    public partial int SortFieldIndex { get; set; }

    [ObservableProperty]
    public partial bool SortDescending { get; set; } = true;

    internal PhotoQuery BuildQuery() => new()
    {
        FolderId = SelectedFolder?.Id,
        AlbumId = SelectedAlbum?.Id,
        MinRating = MinRatingFilter,
        Flag = FlagFilterIndex switch
        {
            1 => PhotoFlag.Pick,
            2 => PhotoFlag.Reject,
            3 => PhotoFlag.None,
            _ => null,
        },
        ColorLabel = ColorLabelFilterIndex switch
        {
            1 => ColorLabel.Red,
            2 => ColorLabel.Yellow,
            3 => ColorLabel.Green,
            4 => ColorLabel.Blue,
            5 => ColorLabel.Purple,
            _ => null,
        },
        Extensions = ExtensionFilterIndex switch
        {
            1 => [".jpg", ".jpeg"],
            2 => [".png"],
            3 => [".tif", ".tiff"],
            4 => RawFileTypes.Extensions.ToList(),
            _ => null,
        },
        SortBy = SortFieldIndex switch
        {
            1 => PhotoSortField.ImportedAt,
            2 => PhotoSortField.FileName,
            _ => PhotoSortField.CapturedAt,
        },
        SortDescending = SortDescending,
        // For RAW+JPG pairs shot together, show only the RAW
        ExcludeJpegWithRawPair = true,
    };

    // ---- Loading ----

    /// <summary>Loads folders, albums, and the photo list (on initialization and when an import completes).</summary>
    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            _suppressReload = true;
            var folders = await _catalogService.GetFoldersAsync();
            var albums = await _catalogService.GetAlbumsAsync();
            var selectedFolderId = SelectedFolder?.Id;
            var selectedAlbumId = SelectedAlbum?.Id;

            Folders.Clear();
            foreach (var folder in folders)
            {
                Folders.Add(folder);
            }

            Albums.Clear();
            foreach (var album in albums)
            {
                Albums.Add(album);
            }

            SelectedFolder = Folders.FirstOrDefault(f => f.Id == selectedFolderId);
            SelectedAlbum = Albums.FirstOrDefault(a => a.Id == selectedAlbumId);
            _suppressReload = false;

            await ReloadPhotosAsync();
        }
        catch (Exception ex)
        {
            _suppressReload = false;
            _logger.LogError(ex, "Failed to load catalog");
            StatusText = "Failed to load the catalog";
        }
    }

    private async Task ReloadPhotosAsync()
    {
        try
        {
            var photos = await _catalogService.QueryPhotosAsync(BuildQuery());
            var thumbnails = await _catalogService.GetThumbnailPathsAsync();

            var selectedId = SelectedPhoto?.Id;
            Photos.Clear();
            _itemsById.Clear();

            foreach (var photo in photos)
            {
                var thumbPath = thumbnails.TryGetValue(photo.Id, out var p) && File.Exists(p) ? p : null;
                var item = new PhotoItemViewModel(photo, thumbPath);
                Photos.Add(item);
                _itemsById[photo.Id] = item;
            }

            RebuildGroupsAndTimeline();

            if (selectedId is not null && _itemsById.TryGetValue(selectedId, out var reselect))
            {
                SelectedPhoto = reselect;
            }

            StatusText = $"{Photos.Count} photos";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query photos");
            StatusText = "Failed to load the photo list";
        }
    }

    /// <summary>Rebuilds the year/month groups and timeline entries from Photos (in display order).</summary>
    private void RebuildGroupsAndTimeline()
    {
        PhotoGroups.Clear();
        TimelineEntries.Clear();
        _currentGroupIndex = -1;

        if (Photos.Count == 0)
        {
            IsTimelineVisible = false;
            return;
        }

        // When not sorting by capture date, month groups fragment (worst case: one photo per group),
        // so use a single headerless group and hide the timeline as well
        if (SortFieldIndex != 0)
        {
            PhotoGroups.Add(new PhotoGroup(-1, 0, Photos));
            IsTimelineVisible = false;
            return;
        }

        var segments = MonthGrouping.Segment(
            Photos.Select(p => p.Photo.CapturedAt).ToList());

        var previousYearLabel = "";
        foreach (var (segment, index) in segments.Select((s, i) => (s, i)))
        {
            PhotoGroups.Add(new PhotoGroup(
                segment.Year, segment.Month,
                Photos.Skip(segment.StartIndex).Take(segment.Count)));

            var yearLabel = segment.Year > 0 ? segment.Year.ToString() : "—";
            TimelineEntries.Add(new TimelineEntryViewModel(
                yearLabel == previousYearLabel ? "" : yearLabel,
                segment.Month > 0 ? segment.Month.ToString() : "?",
                index));
            previousYearLabel = yearLabel;
        }

        IsTimelineVisible = TimelineEntries.Count > 0;
        UpdateCurrentTimelineEntry(0);
    }

    /// <summary>
    /// Highlights the current month based on the grid's first visible item (flat index).
    /// Called at high frequency while scrolling, so it does nothing if the group has not changed.
    /// </summary>
    public void UpdateCurrentTimelineEntry(int firstVisibleItemIndex)
    {
        if (TimelineEntries.Count == 0)
        {
            return;
        }

        // Groups are contiguous in display order, so locate the containing group by cumulative count
        var groupIndex = PhotoGroups.Count - 1;
        var cumulative = 0;
        for (var i = 0; i < PhotoGroups.Count; i++)
        {
            cumulative += PhotoGroups[i].Count;
            if (firstVisibleItemIndex < cumulative)
            {
                groupIndex = i;
                break;
            }
        }

        if (groupIndex == _currentGroupIndex)
        {
            return;
        }

        if (_currentGroupIndex >= 0 && _currentGroupIndex < TimelineEntries.Count)
        {
            TimelineEntries[_currentGroupIndex].IsCurrent = false;
        }

        TimelineEntries[groupIndex].IsCurrent = true;
        _currentGroupIndex = groupIndex;
    }

    // ---- Culling operations (optimistic updates) ----

    /// <summary>Sets the star rating on the selected photos (keyboard 0-5).</summary>
    [RelayCommand]
    public async Task SetRatingAsync(int rating)
    {
        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var item in targets)
        {
            item.Rating = rating;
        }

        await PersistAsync(() => _catalogService.SetRatingAsync(targets.Select(t => t.Id).ToList(), rating));
    }

    [RelayCommand]
    public Task SetPickFlagAsync() => SetFlagAsync(PhotoFlag.Pick);

    [RelayCommand]
    public Task SetRejectFlagAsync() => SetFlagAsync(PhotoFlag.Reject);

    [RelayCommand]
    public Task ClearFlagAsync() => SetFlagAsync(PhotoFlag.None);

    public async Task SetFlagAsync(PhotoFlag flag)
    {
        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var item in targets)
        {
            item.Flag = flag;
        }

        await PersistAsync(() => _catalogService.SetFlagAsync(targets.Select(t => t.Id).ToList(), flag));
    }

    public async Task SetColorLabelAsync(ColorLabel label)
    {
        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            return;
        }

        foreach (var item in targets)
        {
            item.ColorLabel = label;
        }

        await PersistAsync(() => _catalogService.SetColorLabelAsync(targets.Select(t => t.Id).ToList(), label));
    }

    /// <summary>Culling targets: the multi-selection if any, otherwise the single selection.</summary>
    private List<PhotoItemViewModel> GetCullingTargets() =>
        SelectedPhotos.Count > 0
            ? SelectedPhotos.ToList()
            : SelectedPhoto is { } single ? [single] : [];

    private async Task PersistAsync(Func<Task> save)
    {
        try
        {
            await save();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist culling change");
            StatusText = "Failed to save";
            // Roll back optimistically updated UI values to the actual DB values
            await ReloadPhotosAsync();
        }
    }

    // ---- Album operations ----

    [RelayCommand]
    public async Task CreateAlbumAsync()
    {
        var name = NewAlbumName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        try
        {
            var album = await _catalogService.CreateAlbumAsync(name);
            Albums.Add(album);
            NewAlbumName = "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create album");
            StatusText = "Failed to create the album";
        }
    }

    [RelayCommand]
    public async Task DeleteAlbumAsync(Album album)
    {
        try
        {
            await _catalogService.DeleteAlbumAsync(album.Id);
            if (SelectedAlbum?.Id == album.Id)
            {
                SelectedAlbum = null;
            }

            Albums.Remove(album);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete album");
            StatusText = "Failed to delete the album";
        }
    }

    /// <summary>Adds the selected photos to the specified album (context menu).</summary>
    public async Task AddSelectionToAlbumAsync(Album album)
    {
        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            return;
        }

        try
        {
            await _catalogService.AddToAlbumAsync(album.Id, targets.Select(t => t.Id).ToList());
            StatusText = $"Added {targets.Count} photos to \"{album.Name}\"";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add photos to album");
            StatusText = "Failed to add to the album";
        }
    }

    /// <summary>Removes the selected photos from the album being shown.</summary>
    [RelayCommand]
    public async Task RemoveSelectionFromAlbumAsync()
    {
        if (SelectedAlbum is not { } album)
        {
            return;
        }

        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            return;
        }

        try
        {
            await _catalogService.RemoveFromAlbumAsync(album.Id, targets.Select(t => t.Id).ToList());
            await ReloadPhotosAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove photos from album");
            StatusText = "Failed to remove from the album";
        }
    }

    // ---- Edit workflow (batch paste / batch preset apply) ----

    /// <summary>Whether the edit copy buffer has a value (enables the context menu item).</summary>
    public bool CanPasteSettings => _editService.CopyBuffer is not null;

    /// <summary>
    /// Stores the current edit settings of the selected photo (the active one) into the copy buffer.
    /// The buffer is shared with copy &amp; paste on the Edit screen.
    /// </summary>
    [RelayCommand]
    public async Task CopySettingsFromSelectionAsync()
    {
        if (SelectedPhoto is not { } photo)
        {
            StatusText = "No photo is selected";
            return;
        }

        try
        {
            // GetEditAsync returns a fresh instance each time, so no Clone is needed
            _editService.CopyBuffer = await _editService.GetEditAsync(photo.Id);
            OnPropertyChanged(nameof(CanPasteSettings));
            StatusText = $"Copied edit settings from \"{photo.FileName}\"";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy edit settings from {PhotoId}", photo.Id);
            StatusText = "Failed to copy edit settings";
        }
    }

    /// <summary>Batch-applies the copied edit settings to the selected photos.</summary>
    [RelayCommand]
    public async Task PasteSettingsToSelectionAsync()
    {
        if (_editService.CopyBuffer is not { } buffer)
        {
            StatusText = "No copied edit settings (use \"Copy Settings\" on the Edit screen)";
            return;
        }

        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            StatusText = "No photo is selected";
            return;
        }

        try
        {
            var targetIds = targets.Select(t => t.Id).ToList();
            var applied = await _editService.ApplyToPhotosAsync(buffer, targetIds);
            await _catalogService.RefreshThumbnailsAsync(targetIds);
            StatusText = $"Pasted edit settings to {applied} photos";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to paste settings to selection");
            StatusText = "Failed to paste edit settings";
        }
    }

    /// <summary>Gets the preset list (for building the context menu).</summary>
    public Task<IReadOnlyList<Preset>> GetPresetsAsync() => _presetService.GetPresetsAsync();

    /// <summary>Batch-applies a preset to the selected photos.</summary>
    public async Task ApplyPresetToSelectionAsync(Preset preset)
    {
        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            return;
        }

        try
        {
            var targetIds = targets.Select(t => t.Id).ToList();
            var applied = await _presetService.ApplyToPhotosAsync(preset.Id, targetIds);
            await _catalogService.RefreshThumbnailsAsync(targetIds);
            StatusText = $"Applied \"{preset.Name}\" to {applied} photos";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply preset to selection");
            StatusText = "Failed to batch-apply the preset";
        }
    }

    /// <summary>
    /// Batch-applies Auto Tone (automatic tone correction) to the selected photos.
    /// Takes a CancellationToken, so it can be interrupted via AutoToneSelectionCancelCommand.
    /// </summary>
    [RelayCommand(IncludeCancelCommand = true)]
    public async Task AutoToneSelectionAsync(CancellationToken ct)
    {
        var targets = GetCullingTargets();
        if (targets.Count == 0)
        {
            StatusText = "No photo is selected";
            return;
        }

        var applied = 0;
        var cancelled = false;
        foreach (var (item, index) in targets.Select((t, i) => (t, i)))
        {
            if (ct.IsCancellationRequested)
            {
                cancelled = true;
                break;
            }

            StatusText = $"Applying Auto Tone... ({index + 1}/{targets.Count})";
            try
            {
                var tone = await _autoToneService.ComputeAsync(item.Photo, ct);
                var settings = await _editService.GetEditAsync(item.Id, ct);
                AutoToneCalculator.ApplyTone(settings.Basic, tone);
                await _editService.SaveEditAsync(item.Id, settings, ct);
                applied++;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
                break;
            }
            catch (Exception ex)
            {
                // A single failure (e.g. undecodable image) must not stop the whole batch
                _logger.LogError(ex, "Auto tone failed for {PhotoId}", item.Id);
            }
        }

        try
        {
            // Regenerate thumbnails for already-applied photos even when cancelled
            await _catalogService.RefreshThumbnailsAsync(targets.Select(t => t.Id).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Thumbnail refresh failed after auto tone");
        }

        StatusText = cancelled
            ? $"Auto Tone cancelled ({applied} photos applied)"
            : applied == targets.Count
                ? $"Applied Auto Tone to {applied} photos"
                : $"Applied Auto Tone to {applied} photos ({targets.Count - applied} failed)";
    }

    // ---- Single-photo view / adjacent navigation ----

    /// <summary>
    /// Moves the selection to the previous/next photo in display order (e.g. arrow keys on the Edit screen).
    /// Does nothing at either end.
    /// </summary>
    public void SelectAdjacentPhoto(int offset)
    {
        if (Photos.Count == 0 || SelectedPhoto is null)
        {
            return;
        }

        var index = Photos.IndexOf(SelectedPhoto);
        if (index < 0)
        {
            return;
        }

        var next = index + offset;
        if (next >= 0 && next < Photos.Count)
        {
            SelectedPhoto = Photos[next];
        }
    }

    [RelayCommand]
    private void EnterSinglePhotoView()
    {
        if (SelectedPhoto is not null)
        {
            IsSinglePhotoView = true;
        }
    }

    [RelayCommand]
    private void ExitSinglePhotoView() => IsSinglePhotoView = false;

    // ---- Event / change handlers ----

    private void OnThumbnailReady(ThumbnailReadyEventArgs e)
    {
        if (_itemsById.TryGetValue(e.PhotoId, out var item))
        {
            item.ThumbnailPath = e.ThumbnailPath;
        }
    }

    private void OnImportCompleted(ImportCompletedEventArgs e)
    {
        StatusText = e.FailedFiles.Count > 0
            ? $"Import complete: {e.Imported} added / {e.Skipped} skipped / {e.FailedFiles.Count} failed"
            : $"Import complete: {e.Imported} added / {e.Skipped} skipped";
        _ = LoadAsync();
    }

    private void TriggerReload()
    {
        if (!_suppressReload)
        {
            _ = ReloadPhotosAsync();
        }
    }

    partial void OnSelectedPhotoChanged(PhotoItemViewModel? value) =>
        OnPropertyChanged(nameof(HasSelection));

    partial void OnIsSinglePhotoViewChanged(bool value) =>
        OnPropertyChanged(nameof(IsGridVisible));

    partial void OnSelectedFolderChanged(Folder? value)
    {
        if (value is not null && SelectedAlbum is not null)
        {
            // Folder and album selection are mutually exclusive (save and restore the outer suppression state)
            var prev = _suppressReload;
            _suppressReload = true;
            SelectedAlbum = null;
            _suppressReload = prev;
        }

        TriggerReload();
    }

    partial void OnSelectedAlbumChanged(Album? value)
    {
        OnPropertyChanged(nameof(IsAlbumSelected));
        if (value is not null && SelectedFolder is not null)
        {
            var prev = _suppressReload;
            _suppressReload = true;
            SelectedFolder = null;
            _suppressReload = prev;
        }

        TriggerReload();
    }

    partial void OnMinRatingFilterChanged(int value) => TriggerReload();

    partial void OnFlagFilterIndexChanged(int value) => TriggerReload();

    partial void OnColorLabelFilterIndexChanged(int value) => TriggerReload();

    partial void OnExtensionFilterIndexChanged(int value) => TriggerReload();

    partial void OnSortFieldIndexChanged(int value) => TriggerReload();

    partial void OnSortDescendingChanged(bool value) => TriggerReload();
}
