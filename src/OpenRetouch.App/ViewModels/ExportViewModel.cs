using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using OpenRetouch.App.Services;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Services;

namespace OpenRetouch.App.ViewModels;

/// <summary>Display item for an export result.</summary>
public sealed record ExportResultItem(string FileName, string Status, string? Detail);

/// <summary>ViewModel for the Export screen. Handles the settings form, job submission, and result display.</summary>
public sealed partial class ExportViewModel : ObservableObject
{
    private readonly IExportService _exportService;
    private readonly IFolderPickerService _folderPicker;
    private readonly LibraryViewModel _library;
    private readonly ILogger<ExportViewModel> _logger;
    private readonly DispatcherQueue _dispatcherQueue;
    private string? _lastJobId;

    public ExportViewModel(
        IExportService exportService,
        IFolderPickerService folderPicker,
        LibraryViewModel library,
        ILogger<ExportViewModel> logger)
    {
        _exportService = exportService;
        _folderPicker = folderPicker;
        _library = library;
        _logger = logger;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _exportService.ExportCompleted += (_, summary) =>
            _dispatcherQueue.TryEnqueue(() => _ = OnExportCompletedAsync(summary));
    }

    /// <summary>Built-in export preset names (for the ComboBox; the first entry is "Custom").</summary>
    public IReadOnlyList<string> PresetNames { get; } =
        new[] { "Custom" }.Concat(ExportPresets.BuiltIn.Select(p => p.Name)).ToList();

    [ObservableProperty]
    public partial int SelectedPresetIndex { get; set; }

    [ObservableProperty]
    public partial string OutputFolder { get; set; } = "";

    /// <summary>Format (0=JPEG 1=PNG 2=TIFF).</summary>
    [ObservableProperty]
    public partial int FormatIndex { get; set; }

    [ObservableProperty]
    public partial double JpegQuality { get; set; } = 90;

    /// <summary>Resize (0=None 1=Long edge 2=Short edge).</summary>
    [ObservableProperty]
    public partial int ResizeModeIndex { get; set; }

    [ObservableProperty]
    public partial double ResizeValue { get; set; } = 2048;

    [ObservableProperty]
    public partial string FileNameTemplateText { get; set; } = "{filename}";

    /// <summary>How to handle name conflicts (0=Rename 1=Overwrite 2=Skip).</summary>
    [ObservableProperty]
    public partial int ConflictPolicyIndex { get; set; }

    [ObservableProperty]
    public partial bool KeepExif { get; set; } = true;

    [ObservableProperty]
    public partial bool RemoveGps { get; set; } = true;

    [ObservableProperty]
    public partial string TargetSummary { get; set; } = "";

    [ObservableProperty]
    public partial string ResultSummary { get; set; } = "";

    [ObservableProperty]
    public partial bool IsExporting { get; set; }

    public bool IsNotExporting => !IsExporting;

    partial void OnIsExportingChanged(bool value) => OnPropertyChanged(nameof(IsNotExporting));

    [ObservableProperty]
    public partial bool HasFailedItems { get; set; }

    public ObservableCollection<ExportResultItem> ResultItems { get; } = [];

    public bool IsJpegQualityVisible => FormatIndex == 0;

    public bool IsResizeValueVisible => ResizeModeIndex != 0;

    /// <summary>Photo IDs to export (the selection if any, otherwise all displayed photos).</summary>
    private List<string> GetTargetPhotoIds()
    {
        var selected = _library.SelectedPhotos.Count > 0
            ? _library.SelectedPhotos.Select(p => p.Id).ToList()
            : _library.SelectedPhoto is { } single ? [single.Id] : [];
        return selected.Count > 0 ? selected : _library.Photos.Select(p => p.Id).ToList();
    }

    /// <summary>Updates the target summary when the screen is shown.</summary>
    public void RefreshTargetSummary()
    {
        var count = GetTargetPhotoIds().Count;
        var isSelection = _library.SelectedPhotos.Count > 0 || _library.SelectedPhoto is not null;
        TargetSummary = count == 0
            ? "No photos to export (import/select photos in the Library)"
            : isSelection ? $"Exporting {count} selected photos" : $"Exporting all {count} displayed photos";
    }

    [RelayCommand]
    private async Task PickOutputFolderAsync()
    {
        var folder = await _folderPicker.PickFolderAsync();
        if (folder is not null)
        {
            OutputFolder = folder;
        }
    }

    [RelayCommand]
    private async Task StartExportAsync()
    {
        var photoIds = GetTargetPhotoIds();
        if (photoIds.Count == 0 || string.IsNullOrWhiteSpace(OutputFolder))
        {
            ResultSummary = "Check the output folder and export targets.";
            return;
        }

        try
        {
            Directory.CreateDirectory(OutputFolder);
            var settings = BuildSettings();
            IsExporting = true;
            HasFailedItems = false;
            ResultItems.Clear();
            ResultSummary = "Exporting...";
            _lastJobId = await _exportService.EnqueueExportAsync(photoIds, settings);
        }
        catch (Exception ex)
        {
            IsExporting = false;
            _logger.LogError(ex, "Failed to start export");
            ResultSummary = $"Failed to start export: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CancelExport()
    {
        if (_lastJobId is { } jobId)
        {
            _exportService.Cancel(jobId);
        }
    }

    [RelayCommand]
    private async Task RetryFailedAsync()
    {
        if (_lastJobId is not { } jobId)
        {
            return;
        }

        try
        {
            IsExporting = true;
            HasFailedItems = false;
            ResultSummary = "Retrying failed items...";
            var newJobId = await _exportService.RetryFailedItemsAsync(jobId);
            if (newJobId is null)
            {
                IsExporting = false;
                ResultSummary = "No failed items to retry.";
            }
            else
            {
                _lastJobId = newJobId;
            }
        }
        catch (Exception ex)
        {
            IsExporting = false;
            _logger.LogError(ex, "Failed to retry export");
            ResultSummary = $"Retry failed: {ex.Message}";
        }
    }

    internal ExportSettings BuildSettings() => new()
    {
        OutputFolder = OutputFolder,
        FileNameTemplate = string.IsNullOrWhiteSpace(FileNameTemplateText) ? "{filename}" : FileNameTemplateText,
        Format = FormatIndex switch
        {
            1 => ExportFormat.Png,
            2 => ExportFormat.Tiff,
            _ => ExportFormat.Jpeg,
        },
        JpegQuality = (int)JpegQuality,
        ResizeMode = ResizeModeIndex switch
        {
            1 => ResizeMode.LongEdge,
            2 => ResizeMode.ShortEdge,
            _ => ResizeMode.None,
        },
        ResizeValue = ResizeModeIndex == 0 ? null : (int)ResizeValue,
        Metadata = new MetadataPolicy { KeepExif = KeepExif, RemoveGps = RemoveGps },
        Conflict = ConflictPolicyIndex switch
        {
            1 => ConflictPolicy.Overwrite,
            2 => ConflictPolicy.Skip,
            _ => ConflictPolicy.Rename,
        },
    };

    private async Task OnExportCompletedAsync(ExportJobSummary summary)
    {
        if (summary.JobId != _lastJobId)
        {
            return;
        }

        IsExporting = false;
        HasFailedItems = summary.Failed > 0;
        ResultSummary = summary.Failed > 0
            ? $"Done: {summary.Succeeded} succeeded / {summary.Failed} failed / {summary.Skipped} skipped"
            : $"Done: exported {summary.Succeeded} photos ({summary.Skipped} skipped)";

        try
        {
            var items = await _exportService.GetJobItemsAsync(summary.JobId);
            ResultItems.Clear();
            foreach (var item in items.Where(i => i.Status != "completed"))
            {
                var photo = _library.Photos.FirstOrDefault(p => p.Id == item.PhotoId);
                ResultItems.Add(new ExportResultItem(
                    photo?.FileName ?? item.PhotoId,
                    item.Status,
                    item.ErrorMessage));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load export results");
        }
    }

    partial void OnSelectedPresetIndexChanged(int value)
    {
        if (value <= 0)
        {
            return;
        }

        var (_, preset) = ExportPresets.BuiltIn[value - 1];
        FormatIndex = preset.Format switch
        {
            ExportFormat.Png => 1,
            ExportFormat.Tiff => 2,
            _ => 0,
        };
        JpegQuality = preset.JpegQuality;
        ResizeModeIndex = preset.ResizeMode switch
        {
            ResizeMode.LongEdge => 1,
            ResizeMode.ShortEdge => 2,
            _ => 0,
        };
        if (preset.ResizeValue is { } v)
        {
            ResizeValue = v;
        }

        OnPropertyChanged(nameof(IsJpegQualityVisible));
        OnPropertyChanged(nameof(IsResizeValueVisible));
    }

    partial void OnFormatIndexChanged(int value) => OnPropertyChanged(nameof(IsJpegQualityVisible));

    partial void OnResizeModeIndexChanged(int value) => OnPropertyChanged(nameof(IsResizeValueVisible));
}
