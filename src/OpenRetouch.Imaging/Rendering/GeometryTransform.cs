using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;

namespace OpenRetouch.Imaging.Rendering;

/// <summary>
/// クロップ/ジオメトリ変換(プレビューと書き出しで共通=WYSIWYG保証)。
/// 適用順: 90度回転 → 反転 → 角度補正(内接ズーム付きバイリニア回転) → 正規化クロップ切り出し。
/// </summary>
public static class GeometryTransform
{
    public static RenderedImage Apply(CropSettings crop, RenderedImage source)
    {
        if (crop.IsDefault)
        {
            return source;
        }

        var image = source;

        if (crop.RotationSteps % 4 != 0)
        {
            image = Rotate90(image, ((crop.RotationSteps % 4) + 4) % 4);
        }

        if (crop.FlipHorizontal || crop.FlipVertical)
        {
            image = Flip(image, crop.FlipHorizontal, crop.FlipVertical);
        }

        if (crop.Straighten != 0)
        {
            image = Straighten(image, crop.Straighten);
        }

        if (crop.X != 0 || crop.Y != 0 || crop.Width != 1.0 || crop.Height != 1.0)
        {
            image = CropNormalized(image, crop.X, crop.Y, crop.Width, crop.Height);
        }

        return image;
    }

    private static RenderedImage Rotate90(RenderedImage source, int steps)
    {
        var (sw, sh) = (source.Width, source.Height);
        var (dw, dh) = steps % 2 == 1 ? (sh, sw) : (sw, sh);
        var dst = new byte[dw * dh * 4];
        var src = source.PixelsBgra;

        for (var y = 0; y < sh; y++)
        {
            for (var x = 0; x < sw; x++)
            {
                var (dx, dy) = steps switch
                {
                    1 => (sh - 1 - y, x),           // 時計回り90度
                    2 => (sw - 1 - x, sh - 1 - y),  // 180度
                    _ => (y, sw - 1 - x),           // 270度
                };
                CopyPixel(src, (y * sw + x) * 4, dst, (dy * dw + dx) * 4);
            }
        }

        return new RenderedImage(dst, dw, dh);
    }

    private static RenderedImage Flip(RenderedImage source, bool horizontal, bool vertical)
    {
        var (w, h) = (source.Width, source.Height);
        var dst = new byte[w * h * 4];
        var src = source.PixelsBgra;

        for (var y = 0; y < h; y++)
        {
            var sy = vertical ? h - 1 - y : y;
            for (var x = 0; x < w; x++)
            {
                var sx = horizontal ? w - 1 - x : x;
                CopyPixel(src, (sy * w + sx) * 4, dst, (y * w + x) * 4);
            }
        }

        return new RenderedImage(dst, w, h);
    }

    /// <summary>角度補正。元の寸法を維持し、余白が出ないよう内接ズームしてバイリニア補間で回転する。</summary>
    private static RenderedImage Straighten(RenderedImage source, double degrees)
    {
        var (w, h) = (source.Width, source.Height);
        var dst = new byte[w * h * 4];
        var src = source.PixelsBgra;

        var radians = degrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);

        // 回転後も全面が元画像で埋まる最小ズーム率(内接)
        var absCos = Math.Abs(cos);
        var absSin = Math.Abs(sin);
        var scale = Math.Max(
            (absCos * w + absSin * h) / w,
            (absSin * w + absCos * h) / h);

        var cx = (w - 1) / 2.0;
        var cy = (h - 1) / 2.0;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                // 出力座標→入力座標(逆変換: 逆回転+ズーム)
                var rx = (x - cx) * scale;
                var ry = (y - cy) * scale;
                var sx = cos * rx + sin * ry + cx;
                var sy = -sin * rx + cos * ry + cy;
                BilinearSample(src, w, h, sx, sy, dst, (y * w + x) * 4);
            }
        }

        return new RenderedImage(dst, w, h);
    }

    private static RenderedImage CropNormalized(
        RenderedImage source, double nx, double ny, double nw, double nh)
    {
        var (w, h) = (source.Width, source.Height);
        var x0 = Math.Clamp((int)Math.Round(nx * w), 0, w - 1);
        var y0 = Math.Clamp((int)Math.Round(ny * h), 0, h - 1);
        var cw = Math.Clamp((int)Math.Round(nw * w), 1, w - x0);
        var ch = Math.Clamp((int)Math.Round(nh * h), 1, h - y0);

        var dst = new byte[cw * ch * 4];
        var src = source.PixelsBgra;

        for (var y = 0; y < ch; y++)
        {
            Array.Copy(src, ((y0 + y) * w + x0) * 4, dst, y * cw * 4, cw * 4);
        }

        return new RenderedImage(dst, cw, ch);
    }

    private static void BilinearSample(
        byte[] src, int width, int height, double sx, double sy, byte[] dst, int dstOffset)
    {
        var x0 = (int)Math.Floor(sx);
        var y0 = (int)Math.Floor(sy);
        var fx = sx - x0;
        var fy = sy - y0;

        var x1 = Math.Clamp(x0 + 1, 0, width - 1);
        var y1 = Math.Clamp(y0 + 1, 0, height - 1);
        x0 = Math.Clamp(x0, 0, width - 1);
        y0 = Math.Clamp(y0, 0, height - 1);

        var o00 = (y0 * width + x0) * 4;
        var o10 = (y0 * width + x1) * 4;
        var o01 = (y1 * width + x0) * 4;
        var o11 = (y1 * width + x1) * 4;

        for (var c = 0; c < 4; c++)
        {
            var top = src[o00 + c] + (src[o10 + c] - src[o00 + c]) * fx;
            var bottom = src[o01 + c] + (src[o11 + c] - src[o01 + c]) * fx;
            dst[dstOffset + c] = (byte)Math.Clamp(top + (bottom - top) * fy + 0.5, 0, 255);
        }
    }

    private static void CopyPixel(byte[] src, int srcOffset, byte[] dst, int dstOffset)
    {
        dst[dstOffset] = src[srcOffset];
        dst[dstOffset + 1] = src[srcOffset + 1];
        dst[dstOffset + 2] = src[srcOffset + 2];
        dst[dstOffset + 3] = src[srcOffset + 3];
    }
}
