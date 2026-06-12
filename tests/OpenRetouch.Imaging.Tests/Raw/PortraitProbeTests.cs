using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Imaging.Raw;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Raw;

/// <summary>
/// 縦位置RAWのデバッグ用プローブ(実カメラファイルがある環境でのみ動作する診断テスト)。
/// 環境変数 LPS_PROBE_RAW にRAWファイルパスを設定すると、デコード結果を%TEMP%へPNG出力する。
/// </summary>
public sealed class PortraitProbeTests
{
    [Fact]
    public void Probe_ListThumbnails()
    {
        var rawPath = Environment.GetEnvironmentVariable("LPS_PROBE_RAW");
        if (string.IsNullOrEmpty(rawPath) || !File.Exists(rawPath))
        {
            return;
        }

        var outDir = Path.Combine(Path.GetTempPath(), "lps-probe");
        Directory.CreateDirectory(outDir);
        var lines = new List<string>();

        using var context = Sdcb.LibRaw.RawContext.OpenFile(rawPath);
        for (var i = 0; i < 4; i++)
        {
            try
            {
                context.UnpackThumbnail(i);
                using var thumb = context.MakeDcrawMemoryThumbnail();
                lines.Add($"thumb[{i}]: type={thumb.ImageType} {thumb.Width}x{thumb.Height} " +
                          $"channels={thumb.Channels} bits={thumb.Bits} dataSize={thumb.DataSize}");
                if (thumb.ImageType == Sdcb.LibRaw.ProcessedImageType.Jpeg)
                {
                    File.WriteAllBytes(Path.Combine(outDir, $"thumb{i}.jpg"), thumb.AsSpan<byte>().ToArray());
                }
            }
            catch (Exception ex)
            {
                lines.Add($"thumb[{i}]: ERROR {ex.Message}");
            }
        }

        File.WriteAllLines(Path.Combine(outDir, "thumbs.txt"), lines);
    }

    [Fact]
    public async Task Probe_DumpDecodedImages()
    {
        var rawPath = Environment.GetEnvironmentVariable("LPS_PROBE_RAW");
        if (string.IsNullOrEmpty(rawPath) || !File.Exists(rawPath))
        {
            return; // プローブ対象なし(通常のCIではスキップ相当)
        }

        var decoder = new LibRawDecoder(NullLogger<LibRawDecoder>.Instance);
        var outDir = Path.Combine(Path.GetTempPath(), "lps-probe");
        Directory.CreateDirectory(outDir);

        var preview = await decoder.DecodePreviewAsync(rawPath, 640);
        await SavePngAsync(preview, Path.Combine(outDir, "preview.png"));
        File.WriteAllText(Path.Combine(outDir, "preview.txt"),
            $"preview: {preview.Width}x{preview.Height} bytes={preview.PixelsBgra.Length}");

        var developed = await decoder.DecodeDevelopedAsync(rawPath, halfSize: true);
        File.WriteAllText(Path.Combine(outDir, "developed.txt"),
            $"developed(half): {developed.Width}x{developed.Height} bytes={developed.PixelsBgra.Length}");
        // 縮小して保存(ハーフサイズは大きいため)
        var small = await decoder.DecodeDevelopedPreviewAsync(rawPath, 640);
        await SavePngAsync(small, Path.Combine(outDir, "developed-small.png"));
        File.WriteAllText(Path.Combine(outDir, "developed-small.txt"),
            $"developed-small: {small.Width}x{small.Height} bytes={small.PixelsBgra.Length}");
    }

    private static async Task SavePngAsync(RenderedImage image, string path)
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            (uint)image.Width, (uint)image.Height, 96, 96, image.PixelsBgra);
        await encoder.FlushAsync();
        await using var file = File.Create(path);
        await stream.AsStreamForRead().CopyToAsync(file);
    }
}
