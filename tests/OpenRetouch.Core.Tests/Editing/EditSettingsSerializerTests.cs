using System.Text.Json;
using FluentAssertions;
using OpenRetouch.Core.Editing;
using Xunit;

namespace OpenRetouch.Core.Tests.Editing;

public sealed class EditSettingsSerializerTests
{
    [Fact]
    public void SerializeThenDeserialize_RoundTripsAllValues()
    {
        var settings = new EditSettings
        {
            Basic = new BasicAdjustments
            {
                Exposure = 0.35,
                Contrast = 12,
                Highlights = -30,
                Shadows = 20,
                Whites = 5,
                Blacks = -8,
                Temperature = 15,
                Tint = -4,
                Saturation = 5,
                Vibrance = 18,
            },
        };

        var json = EditSettingsSerializer.Serialize(settings);
        var restored = EditSettingsSerializer.Deserialize(json);

        restored.Version.Should().Be(1);
        restored.Basic.Should().BeEquivalentTo(settings.Basic);
    }

    [Fact]
    public void Serialize_UsesCamelCase()
    {
        var json = EditSettingsSerializer.Serialize(new EditSettings());

        json.Should().Contain("\"version\"");
        json.Should().Contain("\"basic\"");
        json.Should().Contain("\"exposure\"");
    }

    [Fact]
    public void Deserialize_UnknownFields_AreIgnored()
    {
        const string json = """
            { "version": 1, "basic": { "exposure": 1.5, "futureParam": 99 }, "unknownSection": {} }
            """;

        var settings = EditSettingsSerializer.Deserialize(json);

        settings.Basic.Exposure.Should().Be(1.5);
    }

    [Fact]
    public void Deserialize_MissingFields_UseDefaults()
    {
        const string json = """{ "version": 1 }""";

        var settings = EditSettingsSerializer.Deserialize(json);

        settings.Basic.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_CorruptedJson_Throws()
    {
        var act = () => EditSettingsSerializer.Deserialize("{ broken !!");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_LegacyJsonWithoutCrop_UsesDefaultCrop()
    {
        // M3時点のスキーマ(cropフィールドなし)
        const string json = """{ "version": 1, "basic": { "exposure": 0.5 } }""";

        var settings = EditSettingsSerializer.Deserialize(json);

        settings.Crop.Should().NotBeNull();
        settings.Crop.IsDefault.Should().BeTrue();
        settings.Basic.Exposure.Should().Be(0.5);
    }

    [Fact]
    public void Deserialize_ExplicitNullSections_FallBackToDefaults()
    {
        const string json = """{ "version": 1, "basic": null, "crop": null }""";

        var settings = EditSettingsSerializer.Deserialize(json);

        settings.Basic.Should().NotBeNull();
        settings.Crop.Should().NotBeNull();
    }

    [Fact]
    public void SerializeThenDeserialize_WithCrop_RoundTrips()
    {
        var settings = new EditSettings();
        settings.Crop.X = 0.1;
        settings.Crop.Y = 0.05;
        settings.Crop.Width = 0.8;
        settings.Crop.Height = 0.7;
        settings.Crop.Straighten = 2.5;
        settings.Crop.RotationSteps = 1;
        settings.Crop.FlipHorizontal = true;
        settings.Crop.AspectRatio = "4:5";
        settings.Basic.Clarity = 20;
        settings.Basic.Sharpening = 50;
        settings.Basic.NoiseReduction = 10;

        var restored = EditSettingsSerializer.Deserialize(EditSettingsSerializer.Serialize(settings));

        restored.Crop.Should().BeEquivalentTo(settings.Crop);
        restored.Basic.Clarity.Should().Be(20);
        restored.Basic.Sharpening.Should().Be(50);
        restored.Basic.NoiseReduction.Should().Be(10);
    }
}
