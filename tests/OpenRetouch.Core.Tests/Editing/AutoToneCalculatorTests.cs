using FluentAssertions;
using OpenRetouch.Core.Editing;
using Xunit;

namespace OpenRetouch.Core.Tests.Editing;

public sealed class AutoToneCalculatorTests
{
    /// <summary>指定の平均・広がりを持つ正規分布風ヒストグラムを作る。</summary>
    private static long[] CreateHistogram(int center, int halfWidth)
    {
        var histogram = new long[256];
        for (var i = 0; i < 256; i++)
        {
            var distance = Math.Abs(i - center);
            histogram[i] = distance <= halfWidth ? (halfWidth - distance + 1) * 100 : 0;
        }

        return histogram;
    }

    [Fact]
    public void Calculate_DarkImage_IncreasesExposure()
    {
        var result = AutoToneCalculator.Calculate(CreateHistogram(center: 40, halfWidth: 30));

        result.Exposure.Should().BeGreaterThan(0.3, "暗い画像は露出をプラス補正する");
    }

    [Fact]
    public void Calculate_BrightImage_DecreasesExposure()
    {
        var result = AutoToneCalculator.Calculate(CreateHistogram(center: 220, halfWidth: 25));

        result.Exposure.Should().BeLessThan(-0.3, "明るい画像は露出をマイナス補正する");
    }

    [Fact]
    public void Calculate_WellExposedImage_AppliesMinimalExposure()
    {
        var result = AutoToneCalculator.Calculate(CreateHistogram(center: 118, halfWidth: 100));

        Math.Abs(result.Exposure).Should().BeLessThan(0.2, "適正露出の画像はほぼ補正しない");
    }

    [Fact]
    public void Calculate_LowContrastImage_IncreasesContrast()
    {
        // 100-156に集中した眠い画像
        var result = AutoToneCalculator.Calculate(CreateHistogram(center: 128, halfWidth: 28));

        result.Contrast.Should().BeGreaterThan(10, "スプレッドが狭い画像はコントラストを上げる");
    }

    [Fact]
    public void Calculate_FullRangeImage_DoesNotBoostContrast()
    {
        var histogram = new long[256];
        for (var i = 0; i < 256; i++)
        {
            histogram[i] = 100; // 全域フラット
        }

        var result = AutoToneCalculator.Calculate(histogram);

        result.Contrast.Should().Be(0, "フルレンジの画像はコントラストを上げない");
    }

    [Fact]
    public void Calculate_CrushedShadows_LiftsShadows()
    {
        // 暗部0-3に大量の画素+残りは中間
        var histogram = CreateHistogram(center: 128, halfWidth: 60);
        histogram[0] = 50_000;
        histogram[1] = 30_000;

        var result = AutoToneCalculator.Calculate(histogram);

        result.Shadows.Should().BeGreaterThan(0, "黒潰れ傾向はシャドウを持ち上げる");
    }

    [Fact]
    public void Calculate_BlownHighlights_RecoversHighlights()
    {
        // 中間+白飛び(253-255)に大量の画素
        var histogram = CreateHistogram(center: 128, halfWidth: 60);
        histogram[254] = 60_000;
        histogram[255] = 60_000;

        var result = AutoToneCalculator.Calculate(histogram);

        result.Highlights.Should().BeLessThan(0, "白飛び傾向はハイライトを回復方向に補正する");
    }

    [Fact]
    public void Calculate_LiftedBlacks_TightensBlacks()
    {
        // 下端が60から始まる(締まりのない)画像
        var result = AutoToneCalculator.Calculate(CreateHistogram(center: 120, halfWidth: 60));

        result.Blacks.Should().BeLessThan(0, "黒が浮いている画像はブラックを締める");
    }

    [Fact]
    public void Calculate_EmptyHistogram_ReturnsDefaults()
    {
        var result = AutoToneCalculator.Calculate(new long[256]);

        result.IsDefault.Should().BeTrue("空ヒストグラムは補正しない");
    }

    [Fact]
    public void Calculate_SingleToneImage_ReturnsDefaults()
    {
        var histogram = new long[256];
        histogram[128] = 1_000_000;

        var result = AutoToneCalculator.Calculate(histogram);

        result.IsDefault.Should().BeTrue("単色画像は補正しない");
    }

    [Fact]
    public void Calculate_ExtremelyDarkImage_ClampsExposure()
    {
        var histogram = new long[256];
        histogram[1] = 100_000;
        histogram[2] = 100_000;
        histogram[40] = 5_000; // スプレッド確保(p99がここに届く度数にする)

        var result = AutoToneCalculator.Calculate(histogram);

        result.Exposure.Should().BeLessThanOrEqualTo(2.5, "露出補正は±2.5EVにクランプされる");
        result.Exposure.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Calculate_SaturatedWhites_ReducesWhites()
    {
        // 大半の画素が255近辺に張り付いた画像(下端にも分布を持たせて単色ガードを回避)
        var histogram = new long[256];
        histogram[255] = 100_000;
        histogram[254] = 50_000;
        histogram[100] = 5_000;

        var result = AutoToneCalculator.Calculate(histogram);

        result.Whites.Should().BeLessThan(0, "白飽和気味はホワイトを抑える");
    }

    [Fact]
    public void ApplyTone_OverwritesToneAndKeepsColor()
    {
        var target = new BasicAdjustments { Temperature = 30, Saturation = 20, Exposure = -1.0 };
        var tone = new BasicAdjustments { Exposure = 0.5, Contrast = 15, Shadows = 25 };

        AutoToneCalculator.ApplyTone(target, tone);

        target.Exposure.Should().Be(0.5);
        target.Contrast.Should().Be(15);
        target.Shadows.Should().Be(25);
        target.Temperature.Should().Be(30, "色設定は維持される");
        target.Saturation.Should().Be(20);
    }

    [Fact]
    public void Calculate_OnlyAdjustsToneParameters()
    {
        var result = AutoToneCalculator.Calculate(CreateHistogram(center: 40, halfWidth: 30));

        result.Temperature.Should().Be(0, "色は変更しない");
        result.Tint.Should().Be(0);
        result.Saturation.Should().Be(0);
        result.Vibrance.Should().Be(0);
        result.Clarity.Should().Be(0);
        result.Texture.Should().Be(0);
        result.Dehaze.Should().Be(0);
        result.Sharpening.Should().Be(0);
        result.NoiseReduction.Should().Be(0);
    }

    [Fact]
    public void Calculate_WrongBinCount_Throws()
    {
        var act = () => AutoToneCalculator.Calculate(new long[100]);

        act.Should().Throw<ArgumentException>();
    }
}
