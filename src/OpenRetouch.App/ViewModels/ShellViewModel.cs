using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using OpenRetouch.App.Services;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Services;

namespace OpenRetouch.App.ViewModels;

/// <summary>Provides the Shell (MainWindow) state, navigation, import, and job progress.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly IImportService _importService;
    private readonly IFolderPickerService _folderPicker;
    private readonly IImportOptionsDialogService _importOptionsDialog;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    public partial string CurrentModeName { get; set; }

    [ObservableProperty]
    public partial bool IsJobRunning { get; set; }

    [ObservableProperty]
    public partial string JobStatusText { get; set; }

    [ObservableProperty]
    public partial double JobProgressValue { get; set; }

    [ObservableProperty]
    public partial bool IsJobIndeterminate { get; set; }

    public ShellViewModel(
        INavigationService navigation,
        IImportService importService,
        IFolderPickerService folderPicker,
        IImportOptionsDialogService importOptionsDialog,
        IJobQueue jobQueue)
    {
        CurrentModeName = nameof(ViewMode.Library);
        JobStatusText = "";
        _navigation = navigation;
        _importService = importService;
        _folderPicker = folderPicker;
        _importOptionsDialog = importOptionsDialog;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        _navigation.Navigated += (_, mode) => CurrentModeName = mode.ToString();
        jobQueue.ProgressChanged += (_, progress) =>
            _dispatcherQueue.TryEnqueue(() => OnJobProgress(progress));
    }

    [RelayCommand]
    private void NavigateLibrary() => _navigation.NavigateTo(ViewMode.Library);

    [RelayCommand]
    private void NavigateEdit() => _navigation.NavigateTo(ViewMode.Edit);

    [RelayCommand]
    private void NavigateExport() => _navigation.NavigateTo(ViewMode.Export);

    [RelayCommand]
    private void NavigateSettings() => _navigation.NavigateTo(ViewMode.Settings);

    [RelayCommand]
    private async Task ImportFolderAsync()
    {
        var folderPath = await _folderPicker.PickFolderAsync();
        if (folderPath is null)
        {
            return;
        }

        // Choose the import method (copy to default / copy to specified folder / register in place) and date folders
        var options = await _importOptionsDialog.ShowAsync(folderPath);
        if (options is null)
        {
            return;
        }

        _navigation.NavigateTo(ViewMode.Library);
        _importService.Import(options);
    }

    private void OnJobProgress(JobProgress progress)
    {
        switch (progress.Status)
        {
            case JobStatus.Pending:
            case JobStatus.Running:
                IsJobRunning = true;
                if (progress.Total > 0)
                {
                    IsJobIndeterminate = false;
                    JobProgressValue = 100.0 * progress.Done / progress.Total;
                    JobStatusText = $"{progress.DisplayName} ({progress.Done}/{progress.Total})";
                }
                else
                {
                    IsJobIndeterminate = true;
                    JobStatusText = progress.DisplayName;
                }

                break;

            case JobStatus.Completed:
            case JobStatus.Failed:
            case JobStatus.Cancelled:
                IsJobRunning = false;
                JobProgressValue = 0;
                JobStatusText = progress.Status switch
                {
                    JobStatus.Failed => $"{progress.DisplayName}: failed (check the logs)",
                    JobStatus.Cancelled => $"{progress.DisplayName}: cancelled",
                    _ => "",
                };
                break;
        }
    }
}
