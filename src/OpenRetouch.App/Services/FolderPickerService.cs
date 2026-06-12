using Windows.Storage.Pickers;

namespace OpenRetouch.App.Services;

/// <summary>Folder selection dialog.</summary>
public interface IFolderPickerService
{
    /// <summary>Shows the folder picker and returns the selected path (null when cancelled).</summary>
    Task<string?> PickFolderAsync();
}

/// <inheritdoc cref="IFolderPickerService"/>
public sealed class FolderPickerService : IFolderPickerService
{
    public async Task<string?> PickFolderAsync()
    {
        var window = App.Current.Window
            ?? throw new InvalidOperationException("MainWindow is not available yet.");

        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        // HWND association is required for unpackaged apps
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
