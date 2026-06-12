using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Models;
using OpenRetouch.Imaging.Raw;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace OpenRetouch.Imaging.Thumbnails;

/// <summary>
/// 長辺320pxサムネイルJPEG生成。
/// JPEG / PNG / TIFF はWIC、RAWはLibRaw(内蔵プレビュー優先)でデコードする。
/// RAWサムネイルはスループット優先でカメラ生成の内蔵プレビューを使うため、
/// 現像ベースのEdit画面プレビューと色再現が異なる場合がある(意図した設計)。
/// </summary>
public sealed class WicThumbnailGenerator : IThumbnailGenerator
{
    /// <summary>サムネイルの長辺サイズ(px)。</summary>
    public const int LongEdge = 320;

    /// <summary>デコードを拒否する画素数上限(メモリ枯渇防止)。</summary>
    private const long MaxPixelCount = 200_000_000;

    private readonly IAppEnvironment _environment;
    private readonly LibRawDecoder _rawDecoder;
    private readonly ILogger<WicThumbnailGenerator> _logger;

    public WicThumbnailGenerator(
        IAppEnvironment environment,
        LibRawDecoder rawDecoder,
        ILogger<WicThumbnailGenerator> logger)
    {
        _environment = environment;
        _rawDecoder = rawDecoder;
        _logger = logger;
    }

    public async Task<string> GenerateAsync(Photo photo, EditSettings edit, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 再生成時にUI側の画像キャッシュ(URI単位)を確実に無効化するため、ファイル名は毎回一意にする
        var outputPath = Path.Combine(
            _environment.ThumbnailsPath,
            photo.Id + "." + Guid.NewGuid().ToString("N")[..8] + ".jpg");

        // クロップがある場合は切り出し後の解像度を確保するためベースを大きめにデコードする
        var baseLongEdge = edit.Crop.IsDefault ? LongEdge : LongEdge * 2;

        RenderedImage image;
        if (RawFileTypes.IsRaw(photo.FileExtension))
        {
            image = await _rawDecoder.DecodePreviewAsync(photo.FilePath, baseLongEdge, ct);
        }
        else
        {
            image = await DecodeWithWicAsync(photo.FilePath, baseLongEdge, ct);
        }

        ct.ThrowIfCancellationRequested();

        // 現行編集を適用(プレビュー/書き出しと同一の変換実装)
        if (!edit.IsDefault)
        {
            image = await Task.Run(
                () =>
                {
                    var transformed = Rendering.GeometryTransform.Apply(edit.Crop, image);
                    Rendering.AdjustmentPipeline.Apply(
                        edit.Basic, transformed.PixelsBgra, transformed.Width, transformed.Height);
                    return transformed;
                },
                ct);
        }

        await SaveJpegAsync(image, outputPath, ct);

        _logger.LogDebug("Thumbnail generated: {Output} ({W}x{H})", outputPath, image.Width, image.Height);
        return outputPath;
    }

    private static async Task<RenderedImage> DecodeWithWicAsync(string filePath, int longEdge, CancellationToken ct)
    {
        var file = await StorageFile.GetFileFromPathAsync(filePath);
        using var inputStream = await file.OpenAsync(FileAccessMode.Read);

        var decoder = await BitmapDecoder.CreateAsync(inputStream);

        long pixelCount = (long)decoder.PixelWidth * decoder.PixelHeight;
        if (pixelCount > MaxPixelCount)
        {
            throw new InvalidOperationException(
                $"Image too large to decode ({decoder.PixelWidth}x{decoder.PixelHeight}): {filePath}");
        }

        // Orientation適用後のサイズを基準に長辺longEdgeへ縮小(回転画像のスケール座標系補正込み)
        return await Rendering.WicDecoding.DecodeScaledAsync(decoder, longEdge, ct);
    }

    private async Task SaveJpegAsync(RenderedImage image, string outputPath, CancellationToken ct)
    {
        Directory.CreateDirectory(_environment.ThumbnailsPath);
        // 並行ジョブ/多重起動による衝突を避けるため一時ファイル名は一意にする
        var tempPath = outputPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var outputStream = new InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, outputStream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Ignore,
                    (uint)image.Width,
                    (uint)image.Height,
                    96,
                    96,
                    image.PixelsBgra);
                await encoder.FlushAsync();

                await using var fileStream = File.Create(tempPath);
                await outputStream.AsStreamForRead().CopyToAsync(fileStream, ct);
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
    }
}
