using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using OpenRetouch.App.ViewModels;
using OpenRetouch.Core.Models;
using Windows.System;

namespace OpenRetouch.App.Views;

public sealed partial class LibraryPage : Page
{
    private bool _initialized;

    public LibraryPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<LibraryViewModel>();
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (!_initialized)
            {
                _initialized = true;
                await ViewModel.LoadAsync();
            }
        };
    }

    public LibraryViewModel ViewModel { get; }

    // ---- Timeline (year/month bar) ----

    private ScrollViewer? _gridScrollViewer;

    /// <summary>Gets the grid's ScrollViewer and highlights the current month as the user scrolls.</summary>
    private void OnPhotoGridLoaded(object sender, RoutedEventArgs e)
    {
        if (_gridScrollViewer is not null)
        {
            return;
        }

        _gridScrollViewer = FindDescendantScrollViewer(PhotoGrid);
        if (_gridScrollViewer is null)
        {
            return; // If unavailable, continue without follow highlighting (click-to-jump still works)
        }

        _gridScrollViewer.ViewChanged += (_, _) =>
        {
            // FirstVisibleIndex returns a flat item index even for a grouped GridView
            // (headers are not counted). Verified on a real device, but since this depends on
            // WinUI internals, UpdateCurrentTimelineEntry safely clamps out-of-range values to the last group
            if (PhotoGrid.ItemsPanelRoot is ItemsWrapGrid panel && panel.FirstVisibleIndex >= 0)
            {
                ViewModel.UpdateCurrentTimelineEntry(panel.FirstVisibleIndex);
            }
        };
    }

    private void OnTimelineMonthClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TimelineEntryViewModel entry }
            && entry.GroupIndex < ViewModel.PhotoGroups.Count
            && ViewModel.PhotoGroups[entry.GroupIndex].FirstOrDefault() is { } firstPhoto)
        {
            PhotoGrid.ScrollIntoView(firstPhoto, ScrollIntoViewAlignment.Leading);
        }
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        for (var i = 0; i < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer viewer)
            {
                return viewer;
            }

            if (FindDescendantScrollViewer(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    // ---- Grid ----

    private void OnGridSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Sync the GridView's SelectedItems (multi-selection) to the ViewModel
        ViewModel.SelectedPhotos.Clear();
        foreach (var item in PhotoGrid.SelectedItems.OfType<PhotoItemViewModel>())
        {
            ViewModel.SelectedPhotos.Add(item);
        }

        ViewModel.SelectedPhoto = PhotoGrid.SelectedItems.OfType<PhotoItemViewModel>().LastOrDefault()
            ?? ViewModel.SelectedPhoto;
    }

    private void OnGridDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Double-click enters Edit Mode (equivalent to Lightroom's D)
        if (ViewModel.SelectedPhoto is not null)
        {
            App.Current.Services.GetRequiredService<Services.INavigationService>()
                .NavigateTo(Services.ViewMode.Edit);
        }
    }

    private void OnEditKeyInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused() || ViewModel.SelectedPhoto is null)
        {
            return;
        }

        args.Handled = true;
        App.Current.Services.GetRequiredService<Services.INavigationService>()
            .NavigateTo(Services.ViewMode.Edit);
    }

    private void OnEnterKeyInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled = true;
        ViewModel.EnterSinglePhotoViewCommand.Execute(null);
    }

    private void OnShowAllPhotos(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedFolder = null;
        ViewModel.SelectedAlbum = null;
    }

    // ---- Context menu ----

    private void OnGridContextMenuOpening(object sender, object e)
    {
        GridContextMenu.Items.Clear();

        var addToAlbum = new MenuFlyoutSubItem { Text = "Add to Album" };
        foreach (var album in ViewModel.Albums)
        {
            var item = new MenuFlyoutItem { Text = album.Name };
            var target = album;
            item.Click += async (_, _) => await ViewModel.AddSelectionToAlbumAsync(target);
            addToAlbum.Items.Add(item);
        }

        if (addToAlbum.Items.Count == 0)
        {
            addToAlbum.Items.Add(new MenuFlyoutItem { Text = "(No albums)", IsEnabled = false });
        }

        GridContextMenu.Items.Add(addToAlbum);

        if (ViewModel.IsAlbumSelected)
        {
            var remove = new MenuFlyoutItem { Text = "Remove from Album" };
            remove.Click += async (_, _) => await ViewModel.RemoveSelectionFromAlbumCommand.ExecuteAsync(null);
            GridContextMenu.Items.Add(remove);
        }

        GridContextMenu.Items.Add(new MenuFlyoutSeparator());

        var autoTone = new MenuFlyoutItem { Text = "Auto Tone" };
        autoTone.Click += (_, _) => ViewModel.AutoToneSelectionCommand.Execute(null);
        GridContextMenu.Items.Add(autoTone);

        var copy = new MenuFlyoutItem { Text = "Copy Edit Settings" };
        copy.Click += (_, _) => ViewModel.CopySettingsFromSelectionCommand.Execute(null);
        GridContextMenu.Items.Add(copy);

        var paste = new MenuFlyoutItem
        {
            Text = "Paste Edit Settings",
            IsEnabled = ViewModel.CanPasteSettings,
        };
        paste.Click += async (_, _) => await ViewModel.PasteSettingsToSelectionAsync();
        GridContextMenu.Items.Add(paste);

        var presetMenu = new MenuFlyoutSubItem { Text = "Apply Preset" };
        presetMenu.Items.Add(new MenuFlyoutItem { Text = "(Loading...)", IsEnabled = false });
        GridContextMenu.Items.Add(presetMenu);
        _ = PopulatePresetItemsAsync(presetMenu.Items);
    }

    /// <summary>The "Apply Preset" dropdown in the top toolbar (same content as the context menu).</summary>
    private void OnPresetToolbarFlyoutOpening(object sender, object e)
    {
        PresetToolbarFlyout.Items.Clear();
        PresetToolbarFlyout.Items.Add(new MenuFlyoutItem { Text = "(Loading...)", IsEnabled = false });
        _ = PopulatePresetItemsAsync(PresetToolbarFlyout.Items);
    }

    private async Task PopulatePresetItemsAsync(IList<MenuFlyoutItemBase> items)
    {
        try
        {
            var presets = await ViewModel.GetPresetsAsync();
            items.Clear();
            if (presets.Count == 0)
            {
                items.Add(new MenuFlyoutItem { Text = "(No presets)", IsEnabled = false });
                return;
            }

            foreach (var preset in presets)
            {
                var item = new MenuFlyoutItem { Text = preset.Name };
                var target = preset;
                item.Click += async (_, _) => await ViewModel.ApplyPresetToSelectionAsync(target);
                items.Add(item);
            }
        }
        catch
        {
            // Ignore menu build failures (retried the next time the menu opens)
        }
    }

    // ---- Album deletion ----

    private async void OnDeleteAlbum(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Album album })
        {
            await ViewModel.DeleteAlbumCommand.ExecuteAsync(album);
        }
    }

    // ---- Culling: right panel ----

    private async void OnRatingControlChanged(RatingControl sender, object args)
    {
        // Treat the RatingControl's cleared state (-1) as 0
        var rating = sender.Value < 0 ? 0 : (int)sender.Value;
        if (ViewModel.SelectedPhoto is not null && rating != ViewModel.SelectedPhoto.Rating)
        {
            await ViewModel.SetRatingAsync(rating);
        }
    }

    private async void OnLabelRed(object sender, RoutedEventArgs e) =>
        await ViewModel.SetColorLabelAsync(ColorLabel.Red);

    private async void OnLabelYellow(object sender, RoutedEventArgs e) =>
        await ViewModel.SetColorLabelAsync(ColorLabel.Yellow);

    private async void OnLabelGreen(object sender, RoutedEventArgs e) =>
        await ViewModel.SetColorLabelAsync(ColorLabel.Green);

    private async void OnLabelBlue(object sender, RoutedEventArgs e) =>
        await ViewModel.SetColorLabelAsync(ColorLabel.Blue);

    private async void OnLabelPurple(object sender, RoutedEventArgs e) =>
        await ViewModel.SetColorLabelAsync(ColorLabel.Purple);

    private async void OnLabelNone(object sender, RoutedEventArgs e) =>
        await ViewModel.SetColorLabelAsync(ColorLabel.None);

    // ---- Keyboard shortcuts ----

    private bool IsTextInputFocused() =>
        FocusManager.GetFocusedElement(XamlRoot)
            is TextBox or PasswordBox or AutoSuggestBox or NumberBox or RichEditBox;

    private async void OnRatingKeyInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled = true;
        var rating = sender.Key - VirtualKey.Number0;
        await ViewModel.SetRatingAsync(rating);
    }

    private async void OnLabelKeyInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled = true;
        var label = sender.Key switch
        {
            VirtualKey.Number6 => ColorLabel.Red,
            VirtualKey.Number7 => ColorLabel.Yellow,
            VirtualKey.Number8 => ColorLabel.Green,
            VirtualKey.Number9 => ColorLabel.Blue,
            _ => ColorLabel.None,
        };
        await ViewModel.SetColorLabelAsync(label);
    }

    private async void OnFlagKeyInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (IsTextInputFocused())
        {
            return;
        }

        args.Handled = true;
        var flag = sender.Key switch
        {
            VirtualKey.P => PhotoFlag.Pick,
            VirtualKey.X => PhotoFlag.Reject,
            _ => PhotoFlag.None,
        };
        await ViewModel.SetFlagAsync(flag);
    }

    private void OnEscapeInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel.IsSinglePhotoView)
        {
            ViewModel.ExitSinglePhotoViewCommand.Execute(null);
            args.Handled = true;
        }
    }
}
