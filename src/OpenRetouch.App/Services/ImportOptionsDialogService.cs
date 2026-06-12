using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Settings;

namespace OpenRetouch.App.Services;

/// <summary>Option selection dialog shown before running an import.</summary>
public interface IImportOptionsDialogService
{
    /// <summary>
    /// Shows the dialog for choosing the import method (copy destination, date folders).
    /// Returns null when cancelled.
    /// </summary>
    Task<ImportOptions?> ShowAsync(string sourceFolder);
}

/// <inheritdoc cref="IImportOptionsDialogService"/>
public sealed class ImportOptionsDialogService : IImportOptionsDialogService
{
    private readonly ISettingsService _settingsService;
    private readonly IFolderPickerService _folderPicker;

    public ImportOptionsDialogService(
        ISettingsService settingsService,
        IFolderPickerService folderPicker)
    {
        _settingsService = settingsService;
        _folderPicker = folderPicker;
    }

    public async Task<ImportOptions?> ShowAsync(string sourceFolder)
    {
        var window = App.Current.Window
            ?? throw new InvalidOperationException("MainWindow is not available yet.");

        var defaultFolder = _settingsService.Current.DefaultImportFolder;
        var hasDefault = !string.IsNullOrWhiteSpace(defaultFolder);
        string? customFolder = null;

        var defaultRadio = new RadioButton
        {
            GroupName = "ImportMode",
            Content = hasDefault
                ? $"Copy to default folder ({defaultFolder})"
                : "Copy to default folder (not set in Settings)",
            IsEnabled = hasDefault,
            IsChecked = hasDefault,
        };
        var customFolderText = new TextBlock
        {
            Text = "(No folder selected)",
            FontSize = 12,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis,
        };
        var browseButton = new Button { Content = "Browse...", IsEnabled = false };
        var customRadio = new RadioButton
        {
            GroupName = "ImportMode",
            Content = "Copy to a specified folder",
        };
        var inPlaceRadio = new RadioButton
        {
            GroupName = "ImportMode",
            Content = "Register the opened folder as-is (no copy)",
            IsChecked = !hasDefault,
        };
        var dateToggle = new ToggleSwitch
        {
            Header = "Organize into date folders (YYYY/MM/DD) when copying",
            IsOn = true,
        };

        var customPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(28, 0, 0, 0),
        };
        customPanel.Children.Add(browseButton);
        customPanel.Children.Add(customFolderText);

        var panel = new StackPanel { Spacing = 8, MinWidth = 420 };
        panel.Children.Add(new TextBlock
        {
            Text = $"Import source: {sourceFolder}",
            TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 8),
        });
        panel.Children.Add(defaultRadio);
        panel.Children.Add(customRadio);
        panel.Children.Add(customPanel);
        panel.Children.Add(inPlaceRadio);
        panel.Children.Add(dateToggle);

        var dialog = new ContentDialog
        {
            Title = "Import Photos",
            Content = panel,
            PrimaryButtonText = "Import",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        void UpdateState()
        {
            var isCopy = defaultRadio.IsChecked == true || customRadio.IsChecked == true;
            dateToggle.IsEnabled = isCopy;
            browseButton.IsEnabled = customRadio.IsChecked == true;
            dialog.IsPrimaryButtonEnabled =
                inPlaceRadio.IsChecked == true
                || defaultRadio.IsChecked == true
                || (customRadio.IsChecked == true && customFolder is not null);
        }

        defaultRadio.Checked += (_, _) => UpdateState();
        customRadio.Checked += (_, _) => UpdateState();
        inPlaceRadio.Checked += (_, _) => UpdateState();
        browseButton.Click += async (_, _) =>
        {
            var picked = await _folderPicker.PickFolderAsync();
            if (picked is not null)
            {
                customFolder = picked;
                customFolderText.Text = picked;
                customFolderText.Opacity = 1.0;
            }

            UpdateState();
        };
        UpdateState();

        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        var mode = defaultRadio.IsChecked == true ? ImportMode.CopyToDefaultFolder
            : customRadio.IsChecked == true ? ImportMode.CopyToCustomFolder
            : ImportMode.RegisterInPlace;

        return new ImportOptions
        {
            SourceFolder = sourceFolder,
            Mode = mode,
            DestinationFolder = mode switch
            {
                ImportMode.CopyToDefaultFolder => defaultFolder,
                ImportMode.CopyToCustomFolder => customFolder,
                _ => null,
            },
            UseDateFolders = dateToggle.IsOn,
            Recursive = true,
        };
    }
}
