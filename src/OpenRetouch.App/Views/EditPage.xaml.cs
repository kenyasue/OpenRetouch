using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using OpenRetouch.App.Services;
using OpenRetouch.App.ViewModels;
using OpenRetouch.Core.Models;
using Windows.Storage.Pickers;
using Windows.System;

namespace OpenRetouch.App.Views;

public sealed partial class EditPage : Page
{
    public EditPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<EditViewModel>();
        InitializeComponent();

        // The "\" (OEM_5) key toggles Before/After (added in code because XAML cannot specify OEM keys)
        var backslash = new KeyboardAccelerator { Key = (VirtualKey)220 };
        backslash.Invoked += (_, args) =>
        {
            ViewModel.IsShowingBefore = !ViewModel.IsShowingBefore;
            args.Handled = true;
        };
        KeyboardAccelerators.Add(backslash);

        // Left/Right arrows move to the previous/next photo (without stealing keys used by sliders etc.)
        var left = new KeyboardAccelerator { Key = VirtualKey.Left };
        left.Invoked += (_, args) => OnNavigateKeyInvoked(args, offsetForward: false);
        KeyboardAccelerators.Add(left);
        var right = new KeyboardAccelerator { Key = VirtualKey.Right };
        right.Invoked += (_, args) => OnNavigateKeyInvoked(args, offsetForward: true);
        KeyboardAccelerators.Add(right);

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditViewModel.PreviewSource) or nameof(EditViewModel.IsCropMode))
            {
                UpdateCropOverlayLayout();
            }
        };
        ViewModel.CropStateChanged += (_, _) => UpdateCropOverlayLayout();
    }

    public EditViewModel ViewModel { get; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.ActivateAsync();
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        await ViewModel.DeactivateAsync();
    }

    // ---- Crop overlay ----

    private void OnPreviewContainerSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateCropOverlayLayout();

    private void OnCropOverlayChanged(object? sender, Controls.CropRectChangedEventArgs e) =>
        ViewModel.SetCropRect(e.X, e.Y, e.Width, e.Height);

    /// <summary>Aligns the overlay with the displayed image area (inside the Uniform letterbox).</summary>
    private void UpdateCropOverlayLayout()
    {
        if (!ViewModel.IsCropMode || ViewModel.PreviewSource is not WriteableBitmap bitmap)
        {
            CropOverlayControl.Visibility = Visibility.Collapsed;
            return;
        }

        var margin = PreviewImage.Margin;
        var availableWidth = PreviewContainer.ActualWidth - margin.Left - margin.Right;
        var availableHeight = PreviewContainer.ActualHeight - margin.Top - margin.Bottom;
        if (availableWidth <= 0 || availableHeight <= 0 || bitmap.PixelWidth <= 0)
        {
            CropOverlayControl.Visibility = Visibility.Collapsed;
            return;
        }

        var scale = Math.Min(availableWidth / bitmap.PixelWidth, availableHeight / bitmap.PixelHeight);
        var displayWidth = bitmap.PixelWidth * scale;
        var displayHeight = bitmap.PixelHeight * scale;
        var offsetX = margin.Left + (availableWidth - displayWidth) / 2;
        var offsetY = margin.Top + (availableHeight - displayHeight) / 2;

        CropOverlayControl.Width = displayWidth;
        CropOverlayControl.Height = displayHeight;
        CropOverlayControl.Margin = new Thickness(offsetX, offsetY, 0, 0);
        CropOverlayControl.Visibility = Visibility.Visible;

        CropOverlayControl.SetImagePixelSize(bitmap.PixelWidth, bitmap.PixelHeight);
        var (x, y, w, h) = ViewModel.GetCropRect();
        CropOverlayControl.SetCrop(x, y, w, h);
        CropOverlayControl.SetAspectRatio(ViewModel.GetAspectRatioValue());
    }

    // ---- Presets ----

    private async void OnApplyPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Preset preset })
        {
            await ViewModel.ApplyPresetAsync(preset);
        }
    }

    private async void OnDeletePresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Preset preset })
        {
            await ViewModel.DeletePresetAsync(preset);
        }
    }

    private async void OnExportPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: Preset preset })
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = preset.Name,
        };
        picker.FileTypeChoices.Add("Preset JSON", [".json"]);
        InitializePicker(picker);

        var file = await picker.PickSaveFileAsync();
        if (file is not null)
        {
            await ViewModel.ExportPresetAsync(preset, file.Path);
        }
    }

    private async void OnImportPresetClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".json");
        InitializePicker(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is not null)
        {
            await ViewModel.ImportPresetAsync(file.Path);
        }
    }

    private static void InitializePicker(object picker)
    {
        var window = App.Current.Window
            ?? throw new InvalidOperationException("MainWindow is not available yet.");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    // ---- Keyboard ----

    private void OnNavigateKeyInvoked(KeyboardAcceleratorInvokedEventArgs args, bool offsetForward)
    {
        // If an input control that uses arrow keys (slider, text box, combo box, etc.) has focus,
        // do not steal the keys; leave them to the default key handling
        if (IsArrowKeyControlFocused())
        {
            return;
        }

        if (offsetForward)
        {
            ViewModel.SelectNextPhotoCommand.Execute(null);
        }
        else
        {
            ViewModel.SelectPreviousPhotoCommand.Execute(null);
        }

        args.Handled = true;
    }

    private bool IsArrowKeyControlFocused() =>
        FocusManager.GetFocusedElement(XamlRoot)
            is Slider or TextBox or NumberBox or ComboBox or AutoSuggestBox or PasswordBox or RichEditBox;

    private void OnUndoInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.UndoCommand.Execute(null);
        args.Handled = true;
    }

    private void OnRedoInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        ViewModel.RedoCommand.Execute(null);
        args.Handled = true;
    }

    private void OnGoLibraryInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        var navigation = App.Current.Services.GetRequiredService<INavigationService>();
        navigation.NavigateTo(ViewMode.Library);
        args.Handled = true;
    }
}
