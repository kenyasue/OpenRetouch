using FluentAssertions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Imaging.Rendering;
using Xunit;

namespace OpenRetouch.Imaging.Tests.Rendering;

public sealed class AdjustmentPipelineTests
{
    [Fact]
    public void Apply_DefaultSettings_IsIdentity()
    {
        var pixels = CreatePixels((120, 80, 200), (0, 255, 30));
        var original = (byte[])pixels.Clone();

        AdjustmentPipeline.Apply(new BasicAdjustments(), pixels, pixels.Length / 4, 1);

        pixels.Should().Equal(original);
    }

    [Fact]
    public void Apply_PositiveExposure_Brightens()
    {
        var pixels = CreatePixels((100, 100, 100));

        AdjustmentPipeline.Apply(new BasicAdjustments { Exposure = 1.0 }, pixels, pixels.Length / 4, 1);

        pixels[0].Should().BeGreaterThan(100);
        pixels[1].Should().BeGreaterThan(100);
        pixels[2].Should().BeGreaterThan(100);
    }

    [Fact]
    public void Apply_NegativeExposure_Darkens()
    {
        var pixels = CreatePixels((100, 100, 100));

        AdjustmentPipeline.Apply(new BasicAdjustments { Exposure = -1.0 }, pixels, pixels.Length / 4, 1);

        pixels[0].Should().BeLessThan(100);
    }

    [Fact]
    public void Apply_FullDesaturation_ProducesGrayscale()
    {
        var pixels = CreatePixels((40, 90, 220));

        AdjustmentPipeline.Apply(new BasicAdjustments { Saturation = -100 }, pixels, pixels.Length / 4, 1);

        pixels[0].Should().Be(pixels[1]);
        pixels[1].Should().Be(pixels[2]);
    }

    [Fact]
    public void Apply_PositiveContrast_IncreasesSpread()
    {
        var pixels = CreatePixels((60, 60, 60), (200, 200, 200));

        AdjustmentPipeline.Apply(new BasicAdjustments { Contrast = 50 }, pixels, pixels.Length / 4, 1);

        pixels[0].Should().BeLessThan(60, "暗部はより暗く");
        pixels[4].Should().BeGreaterThan(200, "明部はより明るく");
    }

    [Fact]
    public void Apply_WarmTemperature_ShiftsRedUpBlueDown()
    {
        var pixels = CreatePixels((128, 128, 128));

        AdjustmentPipeline.Apply(new BasicAdjustments { Temperature = 50 }, pixels, pixels.Length / 4, 1);

        pixels[2].Should().BeGreaterThan(128, "Rが上がる");
        pixels[0].Should().BeLessThan(128, "Bが下がる");
    }

    [Fact]
    public void Apply_IsDeterministic()
    {
        var adjustments = new BasicAdjustments
        {
            Exposure = 0.5,
            Contrast = 30,
            Highlights = -20,
            Shadows = 15,
            Temperature = 25,
            Vibrance = 40,
        };
        var pixels1 = CreatePixels((10, 60, 110), (160, 210, 250));
        var pixels2 = (byte[])pixels1.Clone();

        AdjustmentPipeline.Apply(adjustments, pixels1, pixels1.Length / 4, 1);
        AdjustmentPipeline.Apply(adjustments, pixels2, pixels2.Length / 4, 1);

        pixels1.Should().Equal(pixels2, "同一入力・同一設定で同一出力(WYSIWYG)");
    }

    [Fact]
    public void Apply_PreservesAlpha()
    {
        var pixels = new byte[] { 50, 100, 150, 77 };

        AdjustmentPipeline.Apply(new BasicAdjustments { Exposure = 2.0 }, pixels, pixels.Length / 4, 1);

        pixels[3].Should().Be(77);
    }

    [Fact]
    public void Apply_InvalidBufferLength_Throws()
    {
        var act = () =>
            AdjustmentPipeline.Apply(new BasicAdjustments { Exposure = 1 }, new byte[] { 1, 2, 3 }, 1, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apply_Sharpening_AmplifiesEdges()
    {
        // 4x1の境界画像(暗→明)
        var pixels = CreateUniformImage(4, 1, 50);
        SetPixel(pixels, 4, 2, 0, 200, 200, 200);
        SetPixel(pixels, 4, 3, 0, 200, 200, 200);
        var before = (byte[])pixels.Clone();

        AdjustmentPipeline.Apply(new BasicAdjustments { Sharpening = 100 }, pixels, 4, 1);

        // 境界の暗側はより暗く、明側はより明るくなる(オーバーシュート)
        pixels[1 * 4].Should().BeLessThanOrEqualTo(before[1 * 4]);
        pixels[2 * 4].Should().BeGreaterThanOrEqualTo(before[2 * 4]);
        pixels.Should().NotEqual(before);
    }

    [Fact]
    public void Apply_NoiseReduction_SmoothsVariation()
    {
        // 市松模様(強いノイズ相当)
        var pixels = CreateUniformImage(4, 4, 0);
        for (var i = 0; i < 16; i++)
        {
            var v = (byte)((i % 2 == 0) ? 0 : 255);
            SetPixel(pixels, 4, i % 4, i / 4, v, v, v);
        }

        AdjustmentPipeline.Apply(new BasicAdjustments { NoiseReduction = 100 }, pixels, 4, 4);

        // 平滑化により極値(0/255)がなくなる
        var values = Enumerable.Range(0, 16).Select(i => pixels[i * 4]).ToList();
        values.Max().Should().BeLessThan(255);
        values.Min().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Apply_Dehaze_IncreasesContrast()
    {
        var pixels = CreatePixels((80, 80, 80), (180, 180, 180));

        AdjustmentPipeline.Apply(new BasicAdjustments { Dehaze = 50 }, pixels, 2, 1);

        pixels[0].Should().BeLessThan(80, "暗部が引き締まる");
    }

    [Fact]
    public void Apply_Texture_ChangesEdgePixels()
    {
        var pixels = CreateUniformImage(8, 1, 50);
        SetPixel(pixels, 8, 4, 0, 200, 200, 200);
        SetPixel(pixels, 8, 5, 0, 200, 200, 200);
        SetPixel(pixels, 8, 6, 0, 200, 200, 200);
        SetPixel(pixels, 8, 7, 0, 200, 200, 200);
        var before = (byte[])pixels.Clone();

        AdjustmentPipeline.Apply(new BasicAdjustments { Texture = 100 }, pixels, 8, 1);

        pixels.Should().NotEqual(before, "境界を含む画像にテクスチャが作用する");
    }

    [Fact]
    public void Apply_ClarityOnFlatImage_IsNearIdentity()
    {
        var pixels = CreateUniformImage(8, 8, 128);

        AdjustmentPipeline.Apply(new BasicAdjustments { Clarity = 80 }, pixels, 8, 8);

        // 平坦な画像には局所コントラストの影響がほぼない
        for (var i = 0; i < pixels.Length; i += 4)
        {
            ((int)pixels[i]).Should().BeInRange(126, 130);
        }
    }

    private static byte[] CreateUniformImage(int width, int height, byte value)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = value;
            pixels[i + 1] = value;
            pixels[i + 2] = value;
            pixels[i + 3] = 255;
        }

        return pixels;
    }

    private static void SetPixel(byte[] pixels, int width, int x, int y, byte b, byte g, byte r)
    {
        var offset = (y * width + x) * 4;
        pixels[offset] = b;
        pixels[offset + 1] = g;
        pixels[offset + 2] = r;
        pixels[offset + 3] = 255;
    }

    /// <summary>BGRAピクセル列を生成する(タプルは B,G,R 順)。</summary>
    private static byte[] CreatePixels(params (byte B, byte G, byte R)[] colors)
    {
        var pixels = new byte[colors.Length * 4];
        for (var i = 0; i < colors.Length; i++)
        {
            pixels[i * 4] = colors[i].B;
            pixels[i * 4 + 1] = colors[i].G;
            pixels[i * 4 + 2] = colors[i].R;
            pixels[i * 4 + 3] = 255;
        }

        return pixels;
    }
}
