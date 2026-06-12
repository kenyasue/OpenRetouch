using OpenRetouch.Core.Abstractions.Imaging;
using Windows.Graphics.Imaging;

namespace OpenRetouch.Imaging.Rendering;

/// <summary>
/// WICデコードの共通処理。
/// 重要: BitmapTransformのScaledWidth/Heightは「EXIF Orientation適用前」の座標系で指定する仕様のため、
/// 90/270度回転がある画像では縦横を入れ替えて渡す(入れ替えないと画像が崩壊する)。
/// </summary>
internal static class WicDecoding
{
    /// <summary>
    /// Orientation適用済みの画像を、長辺がlongEdge以下になるようスケールしてBGRAで返す。
    /// </summary>
    public static async Task<RenderedImage> DecodeScaledAsync(
        BitmapDecoder decoder, int longEdge, CancellationToken ct)
    {
        var orientedWidth = decoder.OrientedPixelWidth;
        var orientedHeight = decoder.OrientedPixelHeight;
        var scale = Math.Min(1.0, (double)longEdge / Math.Max(orientedWidth, orientedHeight));
        var targetWidth = Math.Max(1u, (uint)Math.Round(orientedWidth * scale));
        var targetHeight = Math.Max(1u, (uint)Math.Round(orientedHeight * scale));

        // Orientationで縦横が入れ替わる画像(90/270度)は、スケール指定を物理座標系に戻す
        var isRotated = decoder.OrientedPixelWidth != decoder.PixelWidth;
        var transform = new BitmapTransform
        {
            ScaledWidth = isRotated ? targetHeight : targetWidth,
            ScaledHeight = isRotated ? targetWidth : targetHeight,
            InterpolationMode = BitmapInterpolationMode.Fant,
        };

        ct.ThrowIfCancellationRequested();

        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            transform,
            ExifOrientationMode.RespectExifOrientation,
            ColorManagementMode.ColorManageToSRgb);

        return new RenderedImage(pixelData.DetachPixelData(), (int)targetWidth, (int)targetHeight);
    }
}
