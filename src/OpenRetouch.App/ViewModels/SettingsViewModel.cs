using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OpenRetouch.App.Services;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using OpenRetouch.Core.Settings;

namespace OpenRetouch.App.ViewModels;

/// <summary>ViewModel for the Settings screen. Displays, changes, and saves settings, and manages folders.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ICatalogService _catalogService;
    private readonly IFolderPickerService _folderPicker;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly ILogger<SettingsViewModel> _logger;

    [ObservableProperty]
    public partial int CacheLimitGb { get; set; }

    [ObservableProperty]
    public partial string DefaultImportFolder { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFolderCommand))]
    public partial Folder? SelectedFolder { get; set; }

    /// <summary>Enables/disables the remove button (tied to SelectedFolder).</summary>
    [ObservableProperty]
    public partial bool IsFolderSelected { get; set; }

    partial void OnSelectedFolderChanged(Folder? value) => IsFolderSelected = value is not null;

    /// <summary>Folders registered in the catalog.</summary>
    public ObservableCollection<Folder> Folders { get; } = [];

    public SettingsViewModel(
        ISettingsService settingsService,
        ICatalogService catalogService,
        IFolderPickerService folderPicker,
        LibraryViewModel libraryViewModel,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService;
        _catalogService = catalogService;
        _folderPicker = folderPicker;
        _libraryViewModel = libraryViewModel;
        _logger = logger;
        CacheLimitGb = settingsService.Current.CacheLimitGb;
        DefaultImportFolder = settingsService.Current.DefaultImportFolder;
        StatusMessage = "";
    }

    /// <summary>Loads the registered folder list (called when the page is shown).</summary>
    public async Task LoadFoldersAsync()
    {
        try
        {
            var folders = await _catalogService.GetFoldersAsync();
            Folders.Clear();
            foreach (var folder in folders)
            {
                Folders.Add(folder);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load folders");
            StatusMessage = "Failed to load the folder list.";
        }
    }

    [RelayCommand]
    private async Task BrowseDefaultImportFolderAsync()
    {
        var picked = await _folderPicker.PickFolderAsync();
        if (picked is not null)
        {
            DefaultImportFolder = picked;
        }
    }

    [RelayCommand]
    private void ClearDefaultImportFolder() => DefaultImportFolder = "";

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (CacheLimitGb < 1)
        {
            StatusMessage = "The cache limit must be at least 1 GB.";
            return;
        }

        var defaultFolder = DefaultImportFolder.Trim();
        if (defaultFolder.Length > 0 && !Directory.Exists(defaultFolder))
        {
            StatusMessage = "The default import destination folder does not exist.";
            return;
        }

        try
        {
            // Save via a new instance so Current is not left modified if the save fails
            var settings = new AppSettings
            {
                Version = _settingsService.Current.Version,
                CacheLimitGb = CacheLimitGb,
                LastViewMode = _settingsService.Current.LastViewMode,
                DefaultImportFolder = defaultFolder,
            };
            await _settingsService.SaveAsync(settings);
            StatusMessage = "Settings saved.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Failed to save settings");
            StatusMessage = "Failed to save settings.";
        }
    }

    /// <summary>
    /// Removes the selected folder from the catalog (files are not deleted).
    /// The confirmation dialog must already have been shown by the View.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFolder))]
    private async Task RemoveSelectedFolderAsync()
    {
        if (SelectedFolder is not { } folder)
        {
            return;
        }

        try
        {
            await _catalogService.RemoveFolderFromCatalogAsync(folder.Id);
            Folders.Remove(folder);
            SelectedFolder = null;
            StatusMessage = $"Removed \"{folder.Name}\" from the catalog (the files remain).";

            // Reflect the change in the Library screen's photo and folder lists
            await _libraryViewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove folder from catalog: {FolderId}", folder.Id);
            StatusMessage = "Failed to remove the folder.";
        }
    }

    private bool CanRemoveSelectedFolder() => SelectedFolder is not null;

    [RelayCommand]
    private async Task ClearThumbnailCacheAsync()
    {
        try
        {
            StatusMessage = "Clearing the thumbnail cache...";
            await _catalogService.ClearThumbnailCacheAsync();
            StatusMessage = "Thumbnail cache cleared. Regeneration will start.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear thumbnail cache");
            StatusMessage = "Failed to clear the thumbnail cache.";
        }
    }
}
