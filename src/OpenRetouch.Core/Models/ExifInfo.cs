namespace OpenRetouch.Core.Models;

/// <summary>写真の基本EXIF情報。値が取得できない項目はnull。</summary>
public sealed class ExifInfo
{
    public string? CameraMake { get; set; }

    public string? CameraModel { get; set; }

    public string? LensModel { get; set; }

    public int? Iso { get; set; }

    public double? Aperture { get; set; }

    public string? ShutterSpeed { get; set; }

    public double? FocalLength { get; set; }
}
