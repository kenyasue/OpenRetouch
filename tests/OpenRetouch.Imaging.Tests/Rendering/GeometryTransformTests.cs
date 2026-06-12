using FluentAssertions;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Imaging.Rendering;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Rendering;

public sealed class GeometryTransformTests
{
    [Fact]
    public void Apply_DefaultCrop_ReturnsSourceUnchanged()
    {
        var source = CreateImage(4, 2);

        var result = GeometryTransform.Apply(new CropSettings(), source);

        result.Should().BeSameAs(source);
    }

    [Fact]
    public void Apply_Rotate90_SwapsDimensions()
    {
        var source = CreateImage(4, 2);

        var result = GeometryTransform.Apply(new CropSettings { RotationSteps = 1 }, source);

        result.Width.Should().Be(2);
        result.Height.Should().Be(4);
    }

    [Fact]
    public void Apply_Rotate90_MovesPixelsCorrectly()
    {
        // 2x1画像: 左=10, 右=20
        var source = new RenderedImage(
            [10, 10, 10, 255, 20, 20, 20, 255], 2, 1);

        var result = GeometryTransform.Apply(new CropSettings { RotationSteps = 1 }, source);

        // 時計回り90度: 左ピクセルが上に来る
        result.Width.Should().Be(1);
        result.Height.Should().Be(2);
        result.PixelsBgra[0].Should().Be(10);
        result.PixelsBgra[4].Should().Be(20);
    }

    [Fact]
    public void Apply_Rotate180_ReversesPixels()
    {
        var source = new RenderedImage(
            [10, 10, 10, 255, 20, 20, 20, 255], 2, 1);

        var result = GeometryTransform.Apply(new CropSettings { RotationSteps = 2 }, source);

        result.Width.Should().Be(2);
        result.PixelsBgra[0].Should().Be(20);
        result.PixelsBgra[4].Should().Be(10);
    }

    [Fact]
    public void Apply_Rotate270_MovesPixelsCorrectly()
    {
        // 2x1画像: 左=10, 右=20
        var source = new RenderedImage(
            [10, 10, 10, 255, 20, 20, 20, 255], 2, 1);

        var result = GeometryTransform.Apply(new CropSettings { RotationSteps = 3 }, source);

        // 反時計回り90度相当: 右ピクセルが上に来る
        result.Width.Should().Be(1);
        result.Height.Should().Be(2);
        result.PixelsBgra[0].Should().Be(20);
        result.PixelsBgra[4].Should().Be(10);
    }

    [Fact]
    public void Apply_FlipHorizontal_MirrorsPixels()
    {
        var source = new RenderedImage(
            [10, 10, 10, 255, 20, 20, 20, 255], 2, 1);

        var result = GeometryTransform.Apply(new CropSettings { FlipHorizontal = true }, source);

        result.PixelsBgra[0].Should().Be(20);
        result.PixelsBgra[4].Should().Be(10);
    }

    [Fact]
    public void Apply_FlipVertical_MirrorsRows()
    {
        // 1x2画像: 上=10, 下=20
        var source = new RenderedImage(
            [10, 10, 10, 255, 20, 20, 20, 255], 1, 2);

        var result = GeometryTransform.Apply(new CropSettings { FlipVertical = true }, source);

        result.PixelsBgra[0].Should().Be(20);
        result.PixelsBgra[4].Should().Be(10);
    }

    [Fact]
    public void Apply_NormalizedCrop_ReturnsExpectedDimensions()
    {
        var source = CreateImage(100, 80);
        var crop = new CropSettings { X = 0.25, Y = 0.25, Width = 0.5, Height = 0.5 };

        var result = GeometryTransform.Apply(crop, source);

        result.Width.Should().Be(50);
        result.Height.Should().Be(40);
    }

    [Fact]
    public void Apply_Straighten_KeepsDimensionsAndIsDeterministic()
    {
        var source = CreateImage(40, 30);
        var crop = new CropSettings { Straighten = 5.0 };

        var result1 = GeometryTransform.Apply(crop, source);
        var result2 = GeometryTransform.Apply(crop, source);

        result1.Width.Should().Be(40);
        result1.Height.Should().Be(30);
        result1.PixelsBgra.Should().Equal(result2.PixelsBgra);
    }

    [Fact]
    public void Apply_CombinedRotationAndCrop_Works()
    {
        var source = CreateImage(100, 50);
        var crop = new CropSettings
        {
            RotationSteps = 1,            // 90度回転 → 50x100
            X = 0.1,
            Y = 0.1,
            Width = 0.5,
            Height = 0.5,                 // → 25x50
        };

        var result = GeometryTransform.Apply(crop, source);

        result.Width.Should().Be(25);
        result.Height.Should().Be(50);
    }

    private static RenderedImage CreateImage(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            var index = i / 4;
            pixels[i] = (byte)(index % 256);
            pixels[i + 1] = (byte)((index / 2) % 256);
            pixels[i + 2] = (byte)((index / 3) % 256);
            pixels[i + 3] = 255;
        }

        return new RenderedImage(pixels, width, height);
    }
}
