using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Models;
using ExifDirectoryBase = MetadataExtractor.Formats.Exif.ExifDirectoryBase;

namespace OpenRetouch.Imaging.Metadata;

/// <summary>
/// MetadataExtractorによるEXIF・画像サイズ読み込み。
/// 読み取れない項目はデフォルト値のままにし、例外を投げない。
/// </summary>
public sealed class PhotoMetadataReader : IPhotoMetadataReader
{
    private readonly ILogger<PhotoMetadataReader> _logger;

    public PhotoMetadataReader(ILogger<PhotoMetadataReader> logger)
    {
        _logger = logger;
    }

    public PhotoMetadata Read(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            var subIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();

            var (width, height) = ReadDimensions(directories, subIfd);

            return new PhotoMetadata
            {
                CapturedAt = ReadCapturedAt(subIfd, ifd0),
                Width = width,
                Height = height,
                Orientation = ifd0?.TryGetInt32(ExifDirectoryBase.TagOrientation, out var o) == true ? o : 1,
                Exif = new ExifInfo
                {
                    CameraMake = ifd0?.GetString(ExifDirectoryBase.TagMake)?.Trim(),
                    CameraModel = ifd0?.GetString(ExifDirectoryBase.TagModel)?.Trim(),
                    LensModel = subIfd?.GetString(ExifDirectoryBase.TagLensModel)?.Trim(),
                    Iso = subIfd?.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out var iso) == true ? iso : null,
                    Aperture = subIfd?.TryGetDouble(ExifDirectoryBase.TagFNumber, out var f) == true ? f : null,
                    ShutterSpeed = subIfd?.GetDescription(ExifDirectoryBase.TagExposureTime),
                    FocalLength = subIfd?.TryGetDouble(ExifDirectoryBase.TagFocalLength, out var fl) == true ? fl : null,
                },
            };
        }
        catch (Exception ex) when (ex is ImageProcessingException or IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to read metadata: {Path}", filePath);
            return PhotoMetadata.Empty;
        }
    }

    private static DateTimeOffset? ReadCapturedAt(ExifSubIfdDirectory? subIfd, ExifIfd0Directory? ifd0)
    {
        // 撮影日時: DateTimeOriginal優先、なければDateTime
        if (subIfd?.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var original) == true)
        {
            return new DateTimeOffset(original, TimeSpan.Zero);
        }

        if (ifd0?.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime) == true)
        {
            return new DateTimeOffset(dateTime, TimeSpan.Zero);
        }

        return null;
    }

    private static (int Width, int Height) ReadDimensions(
        IReadOnlyList<MetadataExtractor.Directory> directories,
        ExifSubIfdDirectory? subIfd)
    {
        // フォーマット固有ディレクトリ(JpegDirectory/PngDirectory等)の汎用タグ名から取得
        foreach (var directory in directories)
        {
            int? width = null;
            int? height = null;
            foreach (var tag in directory.Tags)
            {
                if (tag.Name is "Image Width" && directory.TryGetInt32(tag.Type, out var w))
                {
                    width = w;
                }
                else if (tag.Name is "Image Height" && directory.TryGetInt32(tag.Type, out var h))
                {
                    height = h;
                }
            }

            if (width is > 0 && height is > 0)
            {
                return (width.Value, height.Value);
            }
        }

        // EXIFのExifImageWidth/Heightにフォールバック
        if (subIfd is not null
            && subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out var ew)
            && subIfd.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out var eh)
            && ew > 0 && eh > 0)
        {
            return (ew, eh);
        }

        return (0, 0);
    }
}
