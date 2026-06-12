using FluentAssertions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Core.Tests.Editing;

public sealed class PresetMergerTests
{
    [Fact]
    public void Merge_OverwritesBasicAndKeepsCrop()
    {
        var current = new EditSettings();
        current.Basic.Exposure = 2.0;
        current.Crop.X = 0.1;
        current.Crop.Width = 0.5;
        current.Crop.AspectRatio = "1:1";

        var preset = new Preset
        {
            Id = "p1",
            Name = "Preset",
            Settings = new BasicAdjustments { Contrast = 40, Vibrance = 20 },
        };

        var merged = PresetMerger.Merge(current, preset);

        merged.Basic.Contrast.Should().Be(40);
        merged.Basic.Vibrance.Should().Be(20);
        merged.Basic.Exposure.Should().Be(0, "プリセットのBasicで全体が置き換わる");
        merged.Crop.X.Should().Be(0.1, "クロップは維持される");
        merged.Crop.Width.Should().Be(0.5);
        merged.Crop.AspectRatio.Should().Be("1:1");
    }

    [Fact]
    public void Merge_ReturnsIndependentCopies()
    {
        var current = new EditSettings();
        var preset = new Preset
        {
            Id = "p1",
            Name = "Preset",
            Settings = new BasicAdjustments { Contrast = 10 },
        };

        var merged = PresetMerger.Merge(current, preset);
        merged.Basic.Contrast = 99;
        merged.Crop.X = 0.9;

        preset.Settings.Contrast.Should().Be(10, "マージ結果の変更がプリセットへ波及しない");
        current.Crop.X.Should().Be(0, "マージ結果の変更が元の編集へ波及しない");
    }
}
