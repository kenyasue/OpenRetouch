using System.Globalization;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Catalog.Records;

/// <summary>photosテーブルの行レコード(Dapperマッピング用の内部DTO)。</summary>
internal sealed class PhotoRow
{
    public string Id { get; init; } = "";
    public string Folder_Id { get; init; } = "";
    public string File_Path { get; init; } = "";
    public string File_Name { get; init; } = "";
    public string File_Extension { get; init; } = "";
    public long? File_Size { get; init; }
    public string Imported_At { get; init; } = "";
    public string? Captured_At { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public int? Orientation { get; init; }
    public string? Camera_Make { get; init; }
    public string? Camera_Model { get; init; }
    public string? Lens_Model { get; init; }
    public int? Iso { get; init; }
    public double? Aperture { get; init; }
    public string? Shutter_Speed { get; init; }
    public double? Focal_Length { get; init; }
    public int Rating { get; init; }
    public string? Flag { get; init; }
    public string? Color_Label { get; init; }
    public int Is_Missing { get; init; }

    public Photo ToModel() => new()
    {
        Id = Id,
        FolderId = Folder_Id,
        FilePath = File_Path,
        FileName = File_Name,
        FileExtension = File_Extension,
        FileSize = File_Size ?? 0,
        ImportedAt = ParseTimestamp(Imported_At) ?? DateTimeOffset.MinValue,
        CapturedAt = ParseTimestamp(Captured_At),
        Width = Width ?? 0,
        Height = Height ?? 0,
        Orientation = Orientation ?? 1,
        Exif = new ExifInfo
        {
            CameraMake = Camera_Make,
            CameraModel = Camera_Model,
            LensModel = Lens_Model,
            Iso = Iso,
            Aperture = Aperture,
            ShutterSpeed = Shutter_Speed,
            FocalLength = Focal_Length,
        },
        Rating = Rating,
        Flag = ParseFlag(Flag),
        ColorLabel = ParseColorLabel(Color_Label),
        IsMissing = Is_Missing != 0,
    };

    public static object ToParameters(Photo photo) => new
    {
        photo.Id,
        FolderId = photo.FolderId,
        FilePath = photo.FilePath,
        FileName = photo.FileName,
        FileExtension = photo.FileExtension,
        FileSize = photo.FileSize,
        ImportedAt = FormatTimestamp(photo.ImportedAt),
        CapturedAt = photo.CapturedAt is { } captured ? FormatTimestamp(captured) : null,
        photo.Width,
        photo.Height,
        photo.Orientation,
        CameraMake = photo.Exif.CameraMake,
        CameraModel = photo.Exif.CameraModel,
        LensModel = photo.Exif.LensModel,
        Iso = photo.Exif.Iso,
        Aperture = photo.Exif.Aperture,
        ShutterSpeed = photo.Exif.ShutterSpeed,
        FocalLength = photo.Exif.FocalLength,
        photo.Rating,
        Flag = FormatFlag(photo.Flag),
        ColorLabel = FormatColorLabel(photo.ColorLabel),
        IsMissing = photo.IsMissing ? 1 : 0,
    };

    internal static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString("o", CultureInfo.InvariantCulture);

    internal static DateTimeOffset? ParseTimestamp(string? value) =>
        string.IsNullOrEmpty(value)
            ? null
            : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    internal static string? FormatFlag(PhotoFlag flag) => flag switch
    {
        PhotoFlag.Pick => "pick",
        PhotoFlag.Reject => "reject",
        _ => null,
    };

    internal static PhotoFlag ParseFlag(string? value) => value switch
    {
        "pick" => PhotoFlag.Pick,
        "reject" => PhotoFlag.Reject,
        _ => PhotoFlag.None,
    };

    internal static string? FormatColorLabel(ColorLabel label) =>
        label == ColorLabel.None ? null : label.ToString().ToLowerInvariant();

    internal static ColorLabel ParseColorLabel(string? value) =>
        value is null ? ColorLabel.None
        : Enum.TryParse<ColorLabel>(value, ignoreCase: true, out var label) ? label : ColorLabel.None;
}
