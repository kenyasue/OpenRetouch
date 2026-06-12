using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using OpenRetouch.Core.Models;

namespace OpenRetouch.App.ViewModels;

/// <summary>Photo item for grid cells and the metadata panel.</summary>
public sealed partial class PhotoItemViewModel : ObservableObject
{
    private static readonly SolidColorBrush TransparentBrush = new(Colors.Transparent);
    private static readonly Dictionary<ColorLabel, SolidColorBrush> LabelBrushes = new()
    {
        [ColorLabel.Red] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE5, 0x48, 0x4D)),
        [ColorLabel.Yellow] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xE5, 0xC0, 0x4B)),
        [ColorLabel.Green] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x53, 0xB2, 0x5C)),
        [ColorLabel.Blue] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0x4C, 0x9C, 0xE5)),
        [ColorLabel.Purple] = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0xA1, 0x6B, 0xD8)),
    };

    public PhotoItemViewModel(Photo photo, string? thumbnailPath)
    {
        Photo = photo;
        ThumbnailPath = thumbnailPath;
        Rating = photo.Rating;
        Flag = photo.Flag;
        ColorLabel = photo.ColorLabel;
    }

    public Photo Photo { get; }

    public string Id => Photo.Id;

    public string FileName => Photo.FileName;

    public string FilePath => Photo.FilePath;

    /// <summary>Path in the thumbnail cache. Null if not yet generated (a placeholder is shown).</summary>
    [ObservableProperty]
    public partial string? ThumbnailPath { get; set; }

    public bool HasThumbnail => ThumbnailPath is not null;

    // ---- Culling state (subject to optimistic updates) ----

    [ObservableProperty]
    public partial int Rating { get; set; }

    [ObservableProperty]
    public partial PhotoFlag Flag { get; set; }

    [ObservableProperty]
    public partial ColorLabel ColorLabel { get; set; }

    /// <summary>Star display for grid cells (filled-star style; empty string for 0).</summary>
    public string RatingStars => Rating == 0 ? "" : new string('★', Rating);

    public bool IsPicked => Flag == PhotoFlag.Pick;

    public bool IsRejected => Flag == PhotoFlag.Reject;

    /// <summary>Brush for the color label border (transparent for None).</summary>
    public SolidColorBrush ColorLabelBrush =>
        LabelBrushes.TryGetValue(ColorLabel, out var brush) ? brush : TransparentBrush;

    // ---- For the metadata panel ----

    public string FileSizeText => Photo.FileSize switch
    {
        >= 1024 * 1024 => $"{Photo.FileSize / (1024.0 * 1024.0):F1} MB",
        >= 1024 => $"{Photo.FileSize / 1024.0:F1} KB",
        _ => $"{Photo.FileSize} B",
    };

    public string DimensionsText =>
        Photo.Width > 0 && Photo.Height > 0 ? $"{Photo.Width} x {Photo.Height}" : "-";

    public string CapturedAtText =>
        Photo.CapturedAt?.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss") ?? "-";

    public string CameraText
    {
        get
        {
            var make = Photo.Exif.CameraMake;
            var model = Photo.Exif.CameraModel;
            if (model is null)
            {
                return "-";
            }

            return make is not null && !model.StartsWith(make, StringComparison.OrdinalIgnoreCase)
                ? $"{make} {model}"
                : model;
        }
    }

    public string LensText => Photo.Exif.LensModel ?? "-";

    public string IsoText => Photo.Exif.Iso?.ToString() ?? "-";

    public string ApertureText => Photo.Exif.Aperture is { } f ? $"f/{f:0.#}" : "-";

    public string ShutterSpeedText => Photo.Exif.ShutterSpeed ?? "-";

    public string FocalLengthText => Photo.Exif.FocalLength is { } fl ? $"{fl:0.#} mm" : "-";

    partial void OnThumbnailPathChanged(string? value) => OnPropertyChanged(nameof(HasThumbnail));

    partial void OnRatingChanged(int value) => OnPropertyChanged(nameof(RatingStars));

    partial void OnFlagChanged(PhotoFlag value)
    {
        OnPropertyChanged(nameof(IsPicked));
        OnPropertyChanged(nameof(IsRejected));
    }

    partial void OnColorLabelChanged(ColorLabel value) => OnPropertyChanged(nameof(ColorLabelBrush));
}
