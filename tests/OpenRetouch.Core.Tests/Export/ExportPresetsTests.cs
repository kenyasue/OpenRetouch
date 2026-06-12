using FluentAssertions;
using OpenRetouch.Core.Export;
using Xunit;

namespace OpenRetouch.Core.Tests.Export;

public sealed class ExportPresetsTests
{
    [Fact]
    public void BuiltIn_ContainsAllSevenPresets()
    {
        ExportPresets.BuiltIn.Should().HaveCount(7);
        ExportPresets.BuiltIn.Select(p => p.Name).Should().BeEquivalentTo(
        [
            "Original Size JPEG",
            "Web Optimized JPEG",
            "Instagram Square",
            "Instagram 4:5",
            "YouTube Thumbnail",
            "EC Product Image",
            "Print TIFF",
        ]);
    }

    [Fact]
    public void BuiltIn_PrintTiff_UsesTiffFormatWithoutResize()
    {
        var (_, settings) = ExportPresets.BuiltIn.Single(p => p.Name == "Print TIFF");

        settings.Format.Should().Be(ExportFormat.Tiff);
        settings.ResizeMode.Should().Be(ResizeMode.None);
    }

    [Fact]
    public void BuiltIn_ResizePresets_HaveResizeValues()
    {
        foreach (var (name, settings) in ExportPresets.BuiltIn.Where(p => p.Settings.ResizeMode != ResizeMode.None))
        {
            settings.ResizeValue.Should().NotBeNull($"{name} はリサイズ値を持つべき");
            settings.ResizeValue!.Value.Should().BeGreaterThan(0);
        }
    }
}
