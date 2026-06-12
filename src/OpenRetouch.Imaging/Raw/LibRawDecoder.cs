using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using Sdcb.LibRaw;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OpenRetouch.Imaging.Raw;

/// <summary>
/// LibRaw(Sdcb.LibRawバインディング)によるRAWデコード。
/// - DecodePreviewAsync: 内蔵プレビュー(JPEG)を抽出してスケール。なければハーフサイズ現像にフォールバック
/// - DecodeDevelopedAsync: dcraw現像(デモザイク+カメラWB+sRGB)
/// 元のRAWファイルは読み取り専用で扱い、一切変更しない。
/// </summary>
public sealed class LibRawDecoder
{
    private readonly ILogger<LibRawDecoder> _logger;

    public LibRawDecoder(ILogger<LibRawDecoder> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// RAW内蔵プレビューをデコードし、長辺がlongEdge以下になるようスケールして返す。
    /// 内蔵プレビューがない場合はハーフサイズ現像にフォールバックする。
    /// </summary>
    public async Task<RenderedImage> DecodePreviewAsync(string path, int longEdge, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        byte[]? embeddedJpeg = null;
        try
        {
            embeddedJpeg = await Task.Run(() => ExtractLargestJpegPreview(path), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInformation(ex, "No usable embedded preview in {Path}. Falling back to half-size develop.", path);
        }

        if (embeddedJpeg is not null)
        {
            return await DecodeAndScaleJpegAsync(embeddedJpeg, longEdge, ct);
        }

        // フォールバック: ハーフサイズ現像
        var developed = await DecodeDevelopedAsync(path, halfSize: true, ct);
        return ScaleNearest(developed, longEdge);
    }

    /// <summary>
    /// 編集ベース用: ハーフサイズ現像し、longEdge以下に縮小して返す。
    /// 内蔵プレビューと違い現像結果(デモザイク+カメラWB+色行列)の色が得られる(WYSIWYG)。
    /// </summary>
    public async Task<RenderedImage> DecodeDevelopedPreviewAsync(
        string path, int longEdge, CancellationToken ct = default)
    {
        var developed = await DecodeDevelopedAsync(path, halfSize: true, ct);
        return ScaleNearest(developed, longEdge);
    }

    /// <summary>dcraw現像(デモザイク+カメラWB+sRGB 8bit)を行いBGRAで返す。</summary>
    public Task<RenderedImage> DecodeDevelopedAsync(string path, bool halfSize, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            using var context = RawContext.OpenFile(path);
            context.Unpack();
            context.DcrawProcess(p =>
            {
                p.UseCameraWb = true;       // カメラのホワイトバランスを反映
                p.UseCameraMatrix = true;   // カメラプロファイル(色行列)を反映
                p.OutputColor = LibRawColorSpace.SRGB;
                p.OutputBps = 8;            // 内部16bit現像 → 8bit出力(MVP)
                p.HalfSize = halfSize;
            });

            ct.ThrowIfCancellationRequested();

            using var image = context.MakeDcrawMemoryImage();
            if (image.ImageType != ProcessedImageType.Bitmap || image.Channels != 3 || image.Bits != 8)
            {
                throw new InvalidOperationException(
                    $"Unexpected LibRaw output: type={image.ImageType}, channels={image.Channels}, bits={image.Bits}");
            }

            return RgbToBgra(image.AsSpan<byte>(), image.Width, image.Height);
        }, ct);
    }

    /// <summary>
    /// 内蔵サムネイルの中から最大のJPEGプレビューを抽出する。
    /// RAWには複数のサムネイル(例: CR3は160px / 1620px / フルサイズ)が含まれるため、
    /// データサイズ最大のものを選ぶ。JPEGが1つもなければ例外(呼び出し側が現像へフォールバック)。
    /// ※ Bitmap型サムネイルは縦位置で寸法情報が信頼できないため使用しない。
    /// </summary>
    private static byte[] ExtractLargestJpegPreview(string path)
    {
        const int MaxThumbnailCount = 8;

        using var context = RawContext.OpenFile(path);

        byte[]? best = null;
        for (var index = 0; index < MaxThumbnailCount; index++)
        {
            try
            {
                context.UnpackThumbnail(index);
                using var thumbnail = context.MakeDcrawMemoryThumbnail();
                if (thumbnail.ImageType == ProcessedImageType.Jpeg
                    && (best is null || thumbnail.DataSize > best.Length))
                {
                    best = thumbnail.AsSpan<byte>().ToArray();
                }
            }
            catch (LibRawException)
            {
                break; // 存在しないインデックスに到達
            }
        }

        return best ?? throw new InvalidOperationException("No JPEG preview found in RAW file.");
    }

    private static async Task<RenderedImage> DecodeAndScaleJpegAsync(
        byte[] jpegBytes, int longEdge, CancellationToken ct)
    {
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(jpegBytes.AsBuffer());
        stream.Seek(0);

        var decoder = await BitmapDecoder.CreateAsync(stream);
        // 回転画像のスケール座標系補正込みの共通デコード
        return await Rendering.WicDecoding.DecodeScaledAsync(decoder, longEdge, ct);
    }

    /// <summary>RGB888 → BGRA8888 変換。</summary>
    private static RenderedImage RgbToBgra(ReadOnlySpan<byte> rgb, int width, int height)
    {
        var bgra = new byte[width * height * 4];
        var pixelCount = width * height;
        for (var i = 0; i < pixelCount; i++)
        {
            bgra[i * 4] = rgb[i * 3 + 2];       // B
            bgra[i * 4 + 1] = rgb[i * 3 + 1];   // G
            bgra[i * 4 + 2] = rgb[i * 3];       // R
            bgra[i * 4 + 3] = 255;
        }

        return new RenderedImage(bgra, width, height);
    }

    /// <summary>最近傍法による縮小(プレビュー用途。longEdge以下なら原寸のまま)。</summary>
    private static RenderedImage ScaleNearest(RenderedImage source, int longEdge)
    {
        var currentLongEdge = Math.Max(source.Width, source.Height);
        if (currentLongEdge <= longEdge)
        {
            return source;
        }

        var scale = (double)longEdge / currentLongEdge;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var dst = new byte[width * height * 4];

        for (var y = 0; y < height; y++)
        {
            var sy = Math.Min(source.Height - 1, (int)(y / scale));
            for (var x = 0; x < width; x++)
            {
                var sx = Math.Min(source.Width - 1, (int)(x / scale));
                Array.Copy(source.PixelsBgra, (sy * source.Width + sx) * 4, dst, (y * width + x) * 4, 4);
            }
        }

        return new RenderedImage(dst, width, height);
    }
}
