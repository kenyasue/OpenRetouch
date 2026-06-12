using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;

namespace OpenRetouch.App.ViewModels;

/// <summary>
/// ViewModel for the Edit screen.
/// Renders slider changes with a 50ms debounce, and saves to the DB plus takes an Undo snapshot with a 500ms debounce.
/// </summary>
public sealed partial class EditViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan RenderDebounce = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(500);
    private const int MaxUndoDepth = 100;

    private readonly IEditService _editService;
    private readonly IPresetService _presetService;
    private readonly ICatalogService _catalogService;
    private readonly IPreviewRenderer _renderer;
    private readonly IAutoToneService _autoToneService;
    private readonly LibraryViewModel _library;
    private readonly ILogger<EditViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;

    /// <summary>ID of the photo being shown (for thumbnail refresh).</summary>
    private string? _activePhotoId;

    /// <summary>Whether the displayed photo has edit changes (decides thumbnail regeneration on leave).</summary>
    private bool _isDirty;

    private EditSettings _settings = new();
    private readonly Stack<EditSettings> _undoStack = [];
    private readonly Stack<EditSettings> _redoStack = [];
    private EditSettings _lastCommitted = new();

    private CancellationTokenSource? _renderCts;
    private CancellationTokenSource? _saveCts;
    private bool _suppressChanges;
    private bool _isActive;

    public EditViewModel(
        IEditService editService,
        IPresetService presetService,
        ICatalogService catalogService,
        IPreviewRenderer renderer,
        IAutoToneService autoToneService,
        LibraryViewModel library,
        ILogger<EditViewModel> logger)
    {
        _editService = editService;
        _presetService = presetService;
        _catalogService = catalogService;
        _renderer = renderer;
        _autoToneService = autoToneService;
        _library = library;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Follow photo switches via the filmstrip while the Edit screen is visible
        _library.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(LibraryViewModel.SelectedPhoto) && _isActive)
            {
                await ActivateAsync();
            }
        };
    }

    public LibraryViewModel Library => _library;

    [ObservableProperty]
    public partial ImageSource? PreviewSource { get; set; }

    [ObservableProperty]
    public partial bool IsShowingBefore { get; set; }

    [ObservableProperty]
    public partial string PhotoName { get; set; } = "";

    [ObservableProperty]
    public partial bool HasPhoto { get; set; }

    public bool HasNoPhoto => !HasPhoto;

    // ---- Slider properties (two-way with EditSettings.Basic) ----

    [ObservableProperty]
    public partial double Exposure { get; set; }

    [ObservableProperty]
    public partial double Contrast { get; set; }

    [ObservableProperty]
    public partial double Highlights { get; set; }

    [ObservableProperty]
    public partial double Shadows { get; set; }

    [ObservableProperty]
    public partial double Whites { get; set; }

    [ObservableProperty]
    public partial double Blacks { get; set; }

    [ObservableProperty]
    public partial double Temperature { get; set; }

    [ObservableProperty]
    public partial double Tint { get; set; }

    [ObservableProperty]
    public partial double Saturation { get; set; }

    [ObservableProperty]
    public partial double Vibrance { get; set; }

    [ObservableProperty]
    public partial double Clarity { get; set; }

    [ObservableProperty]
    public partial double Texture { get; set; }

    [ObservableProperty]
    public partial double Dehaze { get; set; }

    [ObservableProperty]
    public partial double Sharpening { get; set; }

    [ObservableProperty]
    public partial double NoiseReduction { get; set; }

    // ---- Crop ----

    /// <summary>Crop editing mode (shows the frame and renders uncropped).</summary>
    [ObservableProperty]
    public partial bool IsCropMode { get; set; }

    [ObservableProperty]
    public partial double Straighten { get; set; }

    /// <summary>Aspect ratio index (0=Free 1=1:1 2=4:5 3=16:9 4=3:2).</summary>
    [ObservableProperty]
    public partial int CropAspectIndex { get; set; }

    /// <summary>Raised when the crop overlay needs repositioning or a frame update.</summary>
    public event EventHandler? CropStateChanged;

    /// <summary>The current crop rectangle (normalized coordinates).</summary>
    public (double X, double Y, double W, double H) GetCropRect() =>
        (_settings.Crop.X, _settings.Crop.Y, _settings.Crop.Width, _settings.Crop.Height);

    /// <summary>Updates the crop rectangle from overlay manipulation.</summary>
    public void SetCropRect(double x, double y, double w, double h)
    {
        _settings.Crop.X = x;
        _settings.Crop.Y = y;
        _settings.Crop.Width = w;
        _settings.Crop.Height = h;
        _isDirty = true;
        ScheduleSave();
        // While in crop mode only the frame is updated (uncropped view, so no re-render needed)
    }

    /// <summary>Aspect ratio lock value (image pixel ratio after rotation; free = null).</summary>
    public double? GetAspectRatioValue()
    {
        return CropAspectIndex switch
        {
            1 => 1.0,
            2 => 4.0 / 5.0,
            3 => 16.0 / 9.0,
            4 => 3.0 / 2.0,
            _ => null,
        };
    }

    [RelayCommand]
    private void RotateLeft() => RotateBy(3);

    [RelayCommand]
    private void RotateRight() => RotateBy(1);

    private void RotateBy(int steps)
    {
        if (!HasPhoto)
        {
            return;
        }

        _settings.Crop.RotationSteps = (_settings.Crop.RotationSteps + steps) % 4;
        OnCropMutated();
    }

    [RelayCommand]
    private void FlipHorizontal()
    {
        if (!HasPhoto)
        {
            return;
        }

        _settings.Crop.FlipHorizontal = !_settings.Crop.FlipHorizontal;
        OnCropMutated();
    }

    [RelayCommand]
    private void FlipVertical()
    {
        if (!HasPhoto)
        {
            return;
        }

        _settings.Crop.FlipVertical = !_settings.Crop.FlipVertical;
        OnCropMutated();
    }

    [RelayCommand]
    private void ClearCrop()
    {
        if (!HasPhoto)
        {
            return;
        }

        _settings.Crop = new CropSettings();
        _suppressChanges = true;
        Straighten = 0;
        CropAspectIndex = 0;
        _suppressChanges = false;
        OnCropMutated();
    }

    private void OnCropMutated()
    {
        _isDirty = true;
        ScheduleRender(immediate: true);
        ScheduleSave();
        CropStateChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---- Presets ----

    public System.Collections.ObjectModel.ObservableCollection<Preset> Presets { get; } = [];

    [ObservableProperty]
    public partial string NewPresetName { get; set; } = "";

    [ObservableProperty]
    public partial string NewPresetCategory { get; set; } = "";

    public async Task LoadPresetsAsync()
    {
        try
        {
            var presets = await _presetService.GetPresetsAsync();
            Presets.Clear();
            foreach (var preset in presets)
            {
                Presets.Add(preset);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load presets");
        }
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        var name = NewPresetName.Trim();
        if (name.Length == 0 || !HasPhoto)
        {
            return;
        }

        try
        {
            var preset = await _presetService.CreateFromSettingsAsync(
                name, NewPresetCategory.Trim(), _settings);
            Presets.Add(preset);
            NewPresetName = "";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save preset");
        }
    }

    public async Task ApplyPresetAsync(Preset preset)
    {
        if (!HasPhoto)
        {
            return;
        }

        PushUndoSnapshot();
        _settings = PresetMerger.Merge(_settings, preset);
        _isDirty = true;
        LoadSlidersFromSettings();
        ScheduleRender(immediate: true);
        await CommitSaveAsync();
    }

    public async Task DeletePresetAsync(Preset preset)
    {
        try
        {
            await _presetService.DeleteAsync(preset.Id);
            Presets.Remove(preset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete preset");
        }
    }

    public async Task ExportPresetAsync(Preset preset, string filePath)
    {
        try
        {
            await _presetService.ExportAsync(preset.Id, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export preset");
        }
    }

    public async Task ImportPresetAsync(string filePath)
    {
        try
        {
            var preset = await _presetService.ImportAsync(filePath);
            Presets.Add(preset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import preset: {Path}", filePath);
        }
    }

    // ---- Copy & Paste ----

    public bool CanPaste => _editService.CopyBuffer is not null;

    [RelayCommand]
    private void CopySettings()
    {
        if (!HasPhoto)
        {
            return;
        }

        _editService.CopyBuffer = _settings.Clone();
        OnPropertyChanged(nameof(CanPaste));
    }

    [RelayCommand]
    private async Task PasteSettingsAsync()
    {
        if (!HasPhoto || _editService.CopyBuffer is not { } buffer)
        {
            return;
        }

        PushUndoSnapshot();
        _settings = buffer.Clone();
        _isDirty = true;
        LoadSlidersFromSettings();
        ScheduleRender(immediate: true);
        CropStateChanged?.Invoke(this, EventArgs.Empty);
        await CommitSaveAsync();
    }

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    // ---- Lifecycle ----

    /// <summary>Called when EditPage is shown or the photo changes. Loads the edit for the currently selected photo.</summary>
    public async Task ActivateAsync()
    {
        _isActive = true;
        var photo = _library.SelectedPhoto;
        if (photo is null)
        {
            HasPhoto = false;
            PhotoName = "";
            PreviewSource = null;
            return;
        }

        try
        {
            // Commit unsaved changes of the previous photo and regenerate its thumbnail if it was edited
            await FlushPendingSaveAsync();
            RefreshThumbnailIfDirty();

            _activePhotoId = photo.Id;
            HasPhoto = true;
            PhotoName = photo.FileName;
            StatusMessage = "";
            if (Presets.Count == 0)
            {
                await LoadPresetsAsync();
            }

            _settings = await _editService.GetEditAsync(photo.Id);
            _lastCommitted = _settings.Clone();
            _undoStack.Clear();
            _redoStack.Clear();
            NotifyUndoRedoChanged();
            LoadSlidersFromSettings();
            ScheduleRender(immediate: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to activate edit for {PhotoId}", photo.Id);
        }
    }

    /// <summary>Called when EditPage is hidden.</summary>
    public async Task DeactivateAsync()
    {
        _isActive = false;
        await FlushPendingSaveAsync();
        RefreshThumbnailIfDirty();
    }

    /// <summary>Regenerates the thumbnail of an edited photo (so the grid reflects the change).</summary>
    private void RefreshThumbnailIfDirty()
    {
        if (_isDirty && _activePhotoId is { } photoId)
        {
            _isDirty = false;
            _ = _catalogService.RefreshThumbnailsAsync([photoId]).ContinueWith(
                t => _logger.LogError(t.Exception, "Thumbnail refresh failed for {PhotoId}", photoId),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }
    }

    // ---- Commands ----

    /// <summary>Moves to the next photo in display order (Right arrow key).</summary>
    [RelayCommand]
    private void SelectNextPhoto() => _library.SelectAdjacentPhoto(1);

    /// <summary>Moves to the previous photo in display order (Left arrow key).</summary>
    [RelayCommand]
    private void SelectPreviousPhoto() => _library.SelectAdjacentPhoto(-1);

    /// <summary>Status text on the Edit screen (e.g. Auto Tone failure).</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "";

    /// <summary>
    /// Auto Tone: analyzes the image and automatically sets the six tone values (color and crop are kept; undoable).
    /// </summary>
    [RelayCommand]
    private async Task AutoToneAsync(CancellationToken ct)
    {
        var photo = _library.SelectedPhoto;
        if (photo is null)
        {
            return;
        }

        try
        {
            StatusMessage = "";
            var tone = await _autoToneService.ComputeAsync(photo.Photo, ct);
            PushUndoSnapshot();
            AutoToneCalculator.ApplyTone(_settings.Basic, tone);
            _isDirty = true;
            LoadSlidersFromSettings();
            ScheduleRender(immediate: true);
            await CommitSaveAsync();
        }
        catch (OperationCanceledException)
        {
            // Cancelled by switching photos etc. (expected)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto tone failed for {PhotoId}", photo.Id);
            StatusMessage = "Auto Tone failed (the image could not be analyzed)";
        }
    }

    [RelayCommand]
    private async Task ResetAsync()
    {
        if (_library.SelectedPhoto is null)
        {
            return;
        }

        PushUndoSnapshot();
        _settings = new EditSettings();
        _isDirty = true;
        LoadSlidersFromSettings();
        ScheduleRender(immediate: true);
        await CommitSaveAsync();
    }

    [RelayCommand]
    private void Undo()
    {
        if (_undoStack.Count == 0)
        {
            return;
        }

        _redoStack.Push(_settings.Clone());
        _settings = _undoStack.Pop();
        _isDirty = true;
        _lastCommitted = _settings.Clone();
        NotifyUndoRedoChanged();
        LoadSlidersFromSettings();
        ScheduleRender(immediate: true);
        ScheduleSave();
    }

    [RelayCommand]
    private void Redo()
    {
        if (_redoStack.Count == 0)
        {
            return;
        }

        _undoStack.Push(_settings.Clone());
        _settings = _redoStack.Pop();
        _isDirty = true;
        _lastCommitted = _settings.Clone();
        NotifyUndoRedoChanged();
        LoadSlidersFromSettings();
        ScheduleRender(immediate: true);
        ScheduleSave();
    }

    // ---- Settings <-> slider sync ----

    private void LoadSlidersFromSettings()
    {
        _suppressChanges = true;
        var b = _settings.Basic;
        Exposure = b.Exposure;
        Contrast = b.Contrast;
        Highlights = b.Highlights;
        Shadows = b.Shadows;
        Whites = b.Whites;
        Blacks = b.Blacks;
        Temperature = b.Temperature;
        Tint = b.Tint;
        Saturation = b.Saturation;
        Vibrance = b.Vibrance;
        Clarity = b.Clarity;
        Texture = b.Texture;
        Dehaze = b.Dehaze;
        Sharpening = b.Sharpening;
        NoiseReduction = b.NoiseReduction;
        Straighten = _settings.Crop.Straighten;
        CropAspectIndex = _settings.Crop.AspectRatio switch
        {
            "1:1" => 1,
            "4:5" => 2,
            "16:9" => 3,
            "3:2" => 4,
            _ => 0,
        };
        _suppressChanges = false;
    }

    private void OnAdjustmentChanged()
    {
        if (_suppressChanges || !HasPhoto)
        {
            return;
        }

        var b = _settings.Basic;
        b.Exposure = Math.Round(Exposure, 2);
        b.Contrast = (int)Contrast;
        b.Highlights = (int)Highlights;
        b.Shadows = (int)Shadows;
        b.Whites = (int)Whites;
        b.Blacks = (int)Blacks;
        b.Temperature = (int)Temperature;
        b.Tint = (int)Tint;
        b.Saturation = (int)Saturation;
        b.Vibrance = (int)Vibrance;
        b.Clarity = (int)Clarity;
        b.Texture = (int)Texture;
        b.Dehaze = (int)Dehaze;
        b.Sharpening = (int)Sharpening;
        b.NoiseReduction = (int)NoiseReduction;

        _isDirty = true;
        ScheduleRender();
        ScheduleSave();
    }

    partial void OnExposureChanged(double value) => OnAdjustmentChanged();
    partial void OnContrastChanged(double value) => OnAdjustmentChanged();
    partial void OnHighlightsChanged(double value) => OnAdjustmentChanged();
    partial void OnShadowsChanged(double value) => OnAdjustmentChanged();
    partial void OnWhitesChanged(double value) => OnAdjustmentChanged();
    partial void OnBlacksChanged(double value) => OnAdjustmentChanged();
    partial void OnTemperatureChanged(double value) => OnAdjustmentChanged();
    partial void OnTintChanged(double value) => OnAdjustmentChanged();
    partial void OnSaturationChanged(double value) => OnAdjustmentChanged();
    partial void OnVibranceChanged(double value) => OnAdjustmentChanged();
    partial void OnClarityChanged(double value) => OnAdjustmentChanged();
    partial void OnTextureChanged(double value) => OnAdjustmentChanged();
    partial void OnDehazeChanged(double value) => OnAdjustmentChanged();
    partial void OnSharpeningChanged(double value) => OnAdjustmentChanged();
    partial void OnNoiseReductionChanged(double value) => OnAdjustmentChanged();

    partial void OnStraightenChanged(double value)
    {
        if (_suppressChanges || !HasPhoto)
        {
            return;
        }

        _settings.Crop.Straighten = Math.Round(value, 1);
        _isDirty = true;
        ScheduleRender();
        ScheduleSave();
    }

    partial void OnCropAspectIndexChanged(int value)
    {
        if (_suppressChanges || !HasPhoto)
        {
            return;
        }

        _settings.Crop.AspectRatio = value switch
        {
            1 => "1:1",
            2 => "4:5",
            3 => "16:9",
            4 => "3:2",
            _ => "free",
        };
        ScheduleSave();
        CropStateChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsCropModeChanged(bool value)
    {
        // Crop mode toggle: uncropped view vs. crop-applied view
        ScheduleRender(immediate: true);
        CropStateChanged?.Invoke(this, EventArgs.Empty);
        if (!value)
        {
            ScheduleSave();
        }
    }

    partial void OnIsShowingBeforeChanged(bool value) => ScheduleRender(immediate: true);

    partial void OnHasPhotoChanged(bool value) => OnPropertyChanged(nameof(HasNoPhoto));

    // ---- Rendering (50ms debounce, latest request only) ----

    private void ScheduleRender(bool immediate = false)
    {
        var photo = _library.SelectedPhoto;
        if (photo is null)
        {
            return;
        }

        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _renderCts = new CancellationTokenSource();
        var ct = _renderCts.Token;
        var settingsSnapshot = IsShowingBefore ? new EditSettings() : _settings.Clone();

        _ = RenderAsync(photo, settingsSnapshot, immediate, ct);
    }

    private async Task RenderAsync(
        PhotoItemViewModel photo, EditSettings settings, bool immediate, CancellationToken ct)
    {
        try
        {
            if (!immediate)
            {
                await Task.Delay(RenderDebounce, ct);
            }

            // RenderAsync offloads heavy work to a worker internally, so await it directly.
            // While in crop mode, show the uncropped image (editing happens via the frame overlay)
            var rendered = await _renderer.RenderAsync(photo.Photo, settings, applyCrop: !IsCropMode, ct: ct);
            ct.ThrowIfCancellationRequested();

            // At this point we are back on the UI thread (DispatcherQueueSynchronizationContext)
            var bitmap = new WriteableBitmap(rendered.Width, rendered.Height);
            rendered.PixelsBgra.CopyTo(bitmap.PixelBuffer);
            bitmap.Invalidate();
            PreviewSource = bitmap;
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer request (expected)
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preview render failed for {PhotoId}", photo.Id);
        }
    }

    // ---- Save (500ms debounce) + Undo snapshot ----

    private void ScheduleSave()
    {
        _saveCts?.Cancel();
        _saveCts?.Dispose();
        _saveCts = new CancellationTokenSource();
        var ct = _saveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounce, ct);
                _dispatcherQueue.TryEnqueue(() => _ = CommitSaveAsync());
            }
            catch (OperationCanceledException)
            {
            }
        }, ct);
    }

    private async Task CommitSaveAsync()
    {
        var photo = _library.SelectedPhoto;
        if (photo is null)
        {
            return;
        }

        try
        {
            PushUndoSnapshotIfChanged();
            await _editService.SaveEditAsync(photo.Id, _settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save edit for {PhotoId}", photo.Id);
        }
    }

    private async Task FlushPendingSaveAsync()
    {
        if (_saveCts is not null)
        {
            _saveCts.Cancel();
            _saveCts.Dispose();
            _saveCts = null;
            await CommitSaveAsync();
        }
    }

    private void PushUndoSnapshotIfChanged()
    {
        if (!SettingsEqual(_lastCommitted, _settings))
        {
            PushUndoSnapshot();
        }
    }

    private void PushUndoSnapshot()
    {
        _undoStack.Push(_lastCommitted.Clone());
        if (_undoStack.Count > MaxUndoDepth)
        {
            // Trim the bottom of the stack (rebuild since it is a Stack)
            var items = _undoStack.ToArray()[..MaxUndoDepth];
            _undoStack.Clear();
            for (var i = items.Length - 1; i >= 0; i--)
            {
                _undoStack.Push(items[i]);
            }
        }

        _redoStack.Clear();
        _lastCommitted = _settings.Clone();
        NotifyUndoRedoChanged();
    }

    private static bool SettingsEqual(EditSettings a, EditSettings b) =>
        a.Basic.ValuesEqual(b.Basic) && a.Crop.ValuesEqual(b.Crop);

    private void NotifyUndoRedoChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void Dispose()
    {
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        _saveCts?.Cancel();
        _saveCts?.Dispose();
    }
}
