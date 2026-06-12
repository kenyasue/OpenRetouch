using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace OpenRetouch.Imaging.Tests;

/// <summary>テスト用画像をプログラム生成するヘルパー。</summary>
public static class TestImageFactory
{
    /// <summary>指定サイズの単色画像をJPEGまたはPNGで生成する。</summary>
    public static async Task CreateAsync(string path, int width, int height)
    {
        var encoderId = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => BitmapEncoder.JpegEncoderId,
            ".png" => BitmapEncoder.PngEncoderId,
            ".tif" or ".tiff" => BitmapEncoder.TiffEncoderId,
            _ => throw new NotSupportedException(path),
        };

        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 0x30;       // B
            pixels[i + 1] = 0x60;   // G
            pixels[i + 2] = 0x90;   // R
            pixels[i + 3] = 0xFF;   // A
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)width,
            (uint)height,
            96,
            96,
            pixels);
        await encoder.FlushAsync();

        await using var fileStream = File.Create(path);
        await stream.AsStreamForRead().CopyToAsync(fileStream);
    }

    /// <summary>
    /// EXIF Orientation付きのJPEGを生成する(回転デコードの回帰テスト用)。
    /// 物理レイアウトは「上半分=赤、下半分=青」。
    /// </summary>
    public static async Task CreateOrientedJpegAsync(string path, int width, int height, ushort orientation)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = (y * width + x) * 4;
                if (y < height / 2)
                {
                    pixels[offset + 2] = 0xFF;   // R(上半分=赤)
                }
                else
                {
                    pixels[offset] = 0xFF;       // B(下半分=青)
                }

                pixels[offset + 3] = 0xFF;
            }
        }

        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore,
            (uint)width, (uint)height, 96, 96, pixels);
        var properties = new BitmapPropertySet
        {
            ["System.Photo.Orientation"] = new BitmapTypedValue(
                orientation, Windows.Foundation.PropertyType.UInt16),
        };
        await encoder.BitmapProperties.SetPropertiesAsync(properties);
        await encoder.FlushAsync();

        await using var fileStream = File.Create(path);
        await stream.AsStreamForRead().CopyToAsync(fileStream);
    }

    /// <summary>画像ファイルのピクセルサイズを読み取る。</summary>
    public static async Task<(uint Width, uint Height)> GetSizeAsync(string path)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        return (decoder.PixelWidth, decoder.PixelHeight);
    }
}
