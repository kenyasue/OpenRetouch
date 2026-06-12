using FluentAssertions;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Core.Tests.Editing;

public sealed class XmpConverterTests
{
    /// <summary>Lightroom Classicが生成する属性形式のサンプル(抜粋)。</summary>
    private const string LightroomSampleXmp = """
        <x:xmpmeta xmlns:x="adobe:ns:meta/" x:xmptk="Adobe XMP Core 7.0-c000">
         <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
          <rdf:Description rdf:about=""
            xmlns:xmp="http://ns.adobe.com/xap/1.0/"
            xmlns:tiff="http://ns.adobe.com/tiff/1.0/"
            xmlns:crs="http://ns.adobe.com/camera-raw-settings/1.0/"
            xmp:Rating="3"
            xmp:Label="Blue"
            tiff:Orientation="6"
            crs:ProcessVersion="11.0"
            crs:WhiteBalance="Custom"
            crs:Temperature="11000"
            crs:Tint="+12"
            crs:Exposure2012="+0.85"
            crs:Contrast2012="+25"
            crs:Highlights2012="-40"
            crs:Shadows2012="+30"
            crs:Whites2012="+5"
            crs:Blacks2012="-10"
            crs:Texture="+15"
            crs:Clarity2012="+20"
            crs:Dehaze="+8"
            crs:Vibrance="+18"
            crs:Saturation="-5"
            crs:Sharpness="60"
            crs:LuminanceSmoothing="25"
            crs:HasCrop="True"
            crs:CropTop="0.1"
            crs:CropLeft="0.2"
            crs:CropBottom="0.9"
            crs:CropRight="0.8"
            crs:CropAngle="2.5"
            crs:ToneCurveName2012="Linear"
            crs:CameraProfile="Adobe Color"/>
         </rdf:RDF>
        </x:xmpmeta>
        """;

    [Fact]
    public void FromXmp_LightroomSample_MapsAllBasicAdjustments()
    {
        var data = XmpConverter.FromXmp(LightroomSampleXmp);
        var b = data.Settings.Basic;

        b.Exposure.Should().Be(0.85);
        b.Contrast.Should().Be(25);
        b.Highlights.Should().Be(-40);
        b.Shadows.Should().Be(30);
        b.Whites.Should().Be(5);
        b.Blacks.Should().Be(-10);
        b.Tint.Should().Be(12);
        b.Texture.Should().Be(15);
        b.Clarity.Should().Be(20);
        b.Dehaze.Should().Be(8);
        b.Vibrance.Should().Be(18);
        b.Saturation.Should().Be(-5);
        b.Sharpening.Should().Be(60);
        b.NoiseReduction.Should().Be(25);
    }

    [Fact]
    public void FromXmp_KelvinTemperature_ConvertsToWarmRelative()
    {
        var data = XmpConverter.FromXmp(LightroomSampleXmp);

        // 11000K(5500Kの2倍) → +83(暖色方向)
        data.Settings.Basic.Temperature.Should().Be(83);
    }

    [Fact]
    public void FromXmp_RelativeTemperature_IsUsedDirectly()
    {
        const string xml = """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
             <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description rdf:about=""
                xmlns:crs="http://ns.adobe.com/camera-raw-settings/1.0/"
                crs:Temperature="-35"/>
             </rdf:RDF>
            </x:xmpmeta>
            """;

        XmpConverter.FromXmp(xml).Settings.Basic.Temperature.Should().Be(-35);
    }

    [Fact]
    public void FromXmp_Crop_MapsToNormalizedRect()
    {
        var crop = XmpConverter.FromXmp(LightroomSampleXmp).Settings.Crop;

        crop.X.Should().Be(0.2);
        crop.Y.Should().Be(0.1);
        crop.Width.Should().BeApproximately(0.6, 1e-9);
        crop.Height.Should().BeApproximately(0.8, 1e-9);
        crop.Straighten.Should().Be(2.5);
    }

    [Fact]
    public void FromXmp_Orientation6_MapsToRotate90()
    {
        var crop = XmpConverter.FromXmp(LightroomSampleXmp).Settings.Crop;

        crop.RotationSteps.Should().Be(1);
        crop.FlipHorizontal.Should().BeFalse();
    }

    [Theory]
    [InlineData(1, 0, false, false)]
    [InlineData(2, 0, true, false)]
    [InlineData(3, 2, false, false)]
    [InlineData(4, 0, false, true)]
    [InlineData(5, 1, true, false)]
    [InlineData(6, 1, false, false)]
    [InlineData(7, 3, true, false)]
    [InlineData(8, 3, false, false)]
    public void FromXmp_AllTiffOrientations_Map(int orientation, int steps, bool flipH, bool flipV)
    {
        var xml = $"""
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
             <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description rdf:about="" xmlns:tiff="http://ns.adobe.com/tiff/1.0/"
                tiff:Orientation="{orientation}"/>
             </rdf:RDF>
            </x:xmpmeta>
            """;

        var crop = XmpConverter.FromXmp(xml).Settings.Crop;

        crop.RotationSteps.Should().Be(steps);
        crop.FlipHorizontal.Should().Be(flipH);
        crop.FlipVertical.Should().Be(flipV);
    }

    [Fact]
    public void FromXmp_RatingAndLabel_AreMapped()
    {
        var data = XmpConverter.FromXmp(LightroomSampleXmp);

        data.Rating.Should().Be(3);
        data.ColorLabel.Should().Be(ColorLabel.Blue);
    }

    [Fact]
    public void FromXmp_ElementFormValues_AreSupported()
    {
        const string xml = """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
             <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description rdf:about="" xmlns:crs="http://ns.adobe.com/camera-raw-settings/1.0/">
                <crs:Exposure2012>+1.20</crs:Exposure2012>
                <crs:Contrast2012>-15</crs:Contrast2012>
              </rdf:Description>
             </rdf:RDF>
            </x:xmpmeta>
            """;

        var b = XmpConverter.FromXmp(xml).Settings.Basic;

        b.Exposure.Should().Be(1.20);
        b.Contrast.Should().Be(-15);
    }

    [Fact]
    public void FromXmp_CorruptedXml_Throws()
    {
        var act = () => XmpConverter.FromXmp("<not valid xmp");
        act.Should().Throw<System.Xml.XmlException>();
    }

    [Fact]
    public void ToXmpThenFromXmp_RoundTripsSettings()
    {
        var settings = new EditSettings();
        settings.Basic.Exposure = -1.25;
        settings.Basic.Contrast = 40;
        settings.Basic.Temperature = 50;
        settings.Basic.Sharpening = 80;
        settings.Basic.NoiseReduction = 30;
        settings.Crop.X = 0.1;
        settings.Crop.Y = 0.2;
        settings.Crop.Width = 0.5;
        settings.Crop.Height = 0.6;
        settings.Crop.Straighten = -3.0;
        settings.Crop.RotationSteps = 1;

        var xml = XmpConverter.ToXmp(settings, rating: 4, ColorLabel.Red, existingXml: null);
        var restored = XmpConverter.FromXmp(xml);

        restored.Settings.Basic.Exposure.Should().Be(-1.25);
        restored.Settings.Basic.Contrast.Should().Be(40);
        restored.Settings.Basic.Temperature.Should().Be(50, "Kelvin経由の往復が安定する");
        restored.Settings.Basic.Sharpening.Should().Be(80);
        restored.Settings.Basic.NoiseReduction.Should().Be(30);
        restored.Settings.Crop.X.Should().BeApproximately(0.1, 1e-6);
        restored.Settings.Crop.Width.Should().BeApproximately(0.5, 1e-6);
        restored.Settings.Crop.Straighten.Should().Be(-3.0);
        restored.Settings.Crop.RotationSteps.Should().Be(1);
        restored.Rating.Should().Be(4);
        restored.ColorLabel.Should().Be(ColorLabel.Red);
    }

    [Fact]
    public void ToXmp_MergeWithExisting_PreservesUnknownFields()
    {
        var settings = new EditSettings();
        settings.Basic.Exposure = 2.0;

        var merged = XmpConverter.ToXmp(settings, rating: 0, ColorLabel.None, LightroomSampleXmp);

        merged.Should().Contain("ToneCurveName2012", "未対応フィールドは保持される");
        merged.Should().Contain("Adobe Color", "CameraProfileは保持される");
        merged.Should().Contain("WhiteBalance", "他のcrsフィールドも保持される");
        XmpConverter.FromXmp(merged).Settings.Basic.Exposure.Should().Be(2.0, "管理フィールドは更新される");
    }

    [Fact]
    public void ToXmp_ExistingXmlCorrupted_CreatesFreshDocument()
    {
        var settings = new EditSettings();
        settings.Basic.Contrast = 10;

        var xml = XmpConverter.ToXmp(settings, 0, ColorLabel.None, "<broken xml!!");

        XmpConverter.FromXmp(xml).Settings.Basic.Contrast.Should().Be(10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(-50)]
    [InlineData(100)]
    [InlineData(-100)]
    public void KelvinConversion_RoundTripsStably(int relative)
    {
        var kelvin = XmpConverter.RelativeToKelvin(relative);
        XmpConverter.KelvinToRelative(kelvin).Should().Be(relative);
    }

    [Fact]
    public void ToTiffOrientation_BothFlips_NormalizesToRotation()
    {
        var crop = new CropSettings { FlipHorizontal = true, FlipVertical = true };

        XmpConverter.ToTiffOrientation(crop).Should().Be(3, "flipH+flipV = 180度回転");
    }
}
