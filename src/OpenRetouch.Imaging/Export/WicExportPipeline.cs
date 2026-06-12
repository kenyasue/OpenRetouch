using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Export;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Raw;
using OpenRetouch.Imaging.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OpenRetouch.Imaging.Export;

/// <summary>
/// 書き出しパイプライン。
/// フル解像度デコード(JPEG/PNG/TIFF=WIC、RAW=LibRaw現像)→ GeometryTransform → AdjustmentPipeline
/// (プレビューと同一実装=WYSIWYG)→ リサイズ → エンコード → メタデータ → アトミック保存。
/// </summary>
public sealed class WicExportPipeline : IExportPipeline
{
    private const long MaxPixelCount = 200_000_000;

    private readonly LibRawDecoder _rawDecoder;
    private readonly ILogger<WicExportPipeline> _logger;

    public WicExportPipeline(LibRawDecoder rawDecoder, ILogger<WicExportPipeline> logger)
    {
        _rawDecoder = rawDecoder;
        _logger = logger;
    }

    public async Task ExportAsync(
        Photo photo,
        EditSettings edit,
        ExportSettings settings,
        string outputPath,
        CancellationToken ct = default)
    {
        // 非破壊保証: 元画像パスへの書き込みを拒否する
        if (string.Equals(
                Path.GetFullPath(outputPath),
                Path.GetFullPath(photo.FilePath),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot export because the output path is the same as the source image (non-destructive guarantee).");
        }

        // 1. フル解像度デコード(JPEG等はWIC+DPI引き継ぎ、RAWはLibRawフル現像)
        RenderedImage source;
        double dpiX;
        double dpiY;
        if (RawFileTypes.IsRaw(photo.FileExtension))
        {
            source = await _rawDecoder.DecodeDevelopedAsync(photo.FilePath, halfSize: false, ct);
            (dpiX, dpiY) = (96, 96);
        }
        else
        {
            (source, dpiX, dpiY) = await DecodeFullAsync(photo.FilePath, ct);
        }

        ct.ThrowIfCancellationRequested();

        // 2-3. 編集適用(ワーカースレッド)
        var image = await Task.Run(
            () =>
            {
                var transformed = GeometryTransform.Apply(edit.Crop, source);
                AdjustmentPipeline.Apply(edit.Basic, transformed.PixelsBgra, transformed.Width, transformed.Height);
                return transformed;
            },
            ct);

        // 4. リサイズ寸法
        var (outWidth, outHeight) = CalculateOutputSize(image.Width, image.Height, settings);

        // 5. エンコード→アトミック保存
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        // 並行ジョブによる衝突を避けるため一時ファイル名は一意にする
        var tempPath = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new InMemoryRandomAccessStream())
            {
                var encoder = await CreateEncoderAsync(stream, settings);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    (uint)image.Width,
                    (uint)image.Height,
                    dpiX,
                    dpiY,
                    image.PixelsBgra);

                if (outWidth != image.Width || outHeight != image.Height)
                {
                    encoder.BitmapTransform.ScaledWidth = (uint)outWidth;
                    encoder.BitmapTransform.ScaledHeight = (uint)outHeight;
                    encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
                }

                await WriteMetadataAsync(encoder, photo, settings);
                await encoder.FlushAsync();

                ct.ThrowIfCancellationRequested();
                await using var fileStream = File.Create(tempPath);
                await stream.AsStreamForRead().CopyToAsync(fileStream, ct);
            }

            File.Move(tempPath, outputPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }

        _logger.LogInformation("Exported {Photo} -> {Output} ({W}x{H})",
            photo.FileName, outputPath, outWidth, outHeight);
    }

    private static async Task<(RenderedImage Image, double DpiX, double DpiY)> DecodeFullAsync(
        string filePath, CancellationToken ct)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        using var stream = await file.OpenAsync(FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);

        if ((long)decoder.PixelWidth * decoder.PixelHeight > MaxPixelCount)
        {
            throw new InvalidOperationException($"Image too large to export: {filePath}");
        }

        ct.ThrowIfCancellationRequested();

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            new BitmapTransform(),
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);

        var image = new RenderedImage(
            pixelData.DetachPixelData(),
            (int)decoder.OrientedPixelWidth,
            (int)decoder.OrientedPixelHeight);
        return (image, decoder.DpiX, decoder.DpiY);
    }

    internal static (int Width, int Height) CalculateOutputSize(int width, int height, ExportSettings settings)
    {
        if (settings.ResizeMode == ResizeMode.None || settings.ResizeValue is not { } target || target <= 0)
        {
            return (width, height);
        }

        var reference = settings.ResizeMode == ResizeMode.LongEdge
            ? Math.Max(width, height)
            : Math.Min(width, height);

        if (reference <= target)
        {
            return (width, height);   // 拡大はしない
        }

        var scale = (double)target / reference;
        return (
            Math.Max(1, (int)Math.Round(width * scale)),
            Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static async Task<BitmapEncoder> CreateEncoderAsync(
        IRandomAccessStream stream, ExportSettings settings)
    {
        switch (settings.Format)
        {
            case ExportFormat.Png:
                return await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            case ExportFormat.Tiff:
                return await BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, stream);
            default:
                var quality = Math.Clamp(settings.JpegQuality, 1, 100) / 100f;
                var options = new BitmapPropertySet
                {
                    ["ImageQuality"] = new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single),
                };
                return await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, options);
        }
    }

    /// <summary>
    /// メタデータ書き込み(JPEG/TIFFのみ)。KeepExif時は基本EXIFのホワイトリストを写真情報から書き込む。
    /// GPSはRemoveGps=falseでも書き込まない(カタログにGPSを保持していないため。実質常に削除)。
    /// </summary>
    private async Task WriteMetadataAsync(BitmapEncoder encoder, Photo photo, ExportSettings settings)
    {
        if (!settings.Metadata.KeepExif || settings.Format == ExportFormat.Png)
        {
            return;
        }

        try
        {
            var properties = new BitmapPropertySet();
            if (photo.CapturedAt is { } captured)
            {
                // WinRTのDateTimeはDateTimeOffsetとしてマーシャリングされる
                properties["System.Photo.DateTaken"] = new BitmapTypedValue(
                    captured, Windows.Foundation.PropertyType.DateTime);
            }

            if (photo.Exif.CameraMake is { } make)
            {
                properties["System.Photo.CameraManufacturer"] = new BitmapTypedValue(
                    make, Windows.Foundation.PropertyType.String);
            }

            if (photo.Exif.CameraModel is { } model)
            {
                properties["System.Photo.CameraModel"] = new BitmapTypedValue(
                    model, Windows.Foundation.PropertyType.String);
            }

            if (properties.Count > 0)
            {
                await encoder.BitmapProperties.SetPropertiesAsync(properties);
            }
        }
        catch (Exception ex)
        {
            // メタデータ書き込み失敗は書き出し自体を失敗させない
            _logger.LogWarning(ex, "Failed to write metadata for {Photo}", photo.FileName);
        }
    }
}
