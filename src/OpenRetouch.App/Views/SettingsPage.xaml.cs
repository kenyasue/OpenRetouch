using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OpenRetouch.App.ViewModels;
using OpenRetouch.Core.Models;

namespace OpenRetouch.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        InitializeComponent();
        Loaded += async (_, _) => await ViewModel.LoadFoldersAsync();
    }

    public SettingsViewModel ViewModel { get; }

    private void OnFolderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ViewModel.SelectedFolder = FolderList.SelectedItem as Folder;
    }

    private async void OnRemoveFolderClicked(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedFolder is not { } folder)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Remove Folder from Catalog",
            Content = $"The photos in \"{folder.Name}\" ({folder.Path}) will be removed from the app's management.\n"
                + "Edits, album membership, and thumbnails will also be deleted.\n"
                + "The image files and XMP sidecars themselves will not be deleted.",
            PrimaryButtonText = "Remove",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
            RequestedTheme = ElementTheme.Dark,
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewModel.RemoveSelectedFolderCommand.ExecuteAsync(null);
        }
    }
}
