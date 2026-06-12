using System.Globalization;
using System.Xml.Linq;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Editing;

/// <summary>XMPサイドカーから読み取った内容。</summary>
public sealed record XmpSidecarData(EditSettings Settings, int? Rating, ColorLabel? ColorLabel);

/// <summary>
/// Lightroom互換XMPサイドカーとEditSettingsの相互変換。
/// 本アプリのレンダリングはAdobe Camera Rawと同一ではないため、数値の引き継ぎは近似である。
/// 未対応フィールド(ローカル補正・トーンカーブ等)は読み飛ばし、書き出し時のマージでは保持する。
/// </summary>
public static class XmpConverter
{
    private static readonly XNamespace X = "adobe:ns:meta/";
    private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    private static readonly XNamespace Crs = "http://ns.adobe.com/camera-raw-settings/1.0/";
    private static readonly XNamespace Tiff = "http://ns.adobe.com/tiff/1.0/";
    private static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";

    /// <summary>XMP(XML)を解析してEditSettings等へ変換する。解析不能時はXmlException。</summary>
    public static XmpSidecarData FromXmp(string xml)
    {
        var document = XDocument.Parse(xml);
        var description = document.Descendants(Rdf + "Description").FirstOrDefault()
            ?? throw new System.Xml.XmlException("rdf:Description not found in XMP");

        var settings = new EditSettings();
        var b = settings.Basic;

        b.Exposure = Math.Clamp(GetDouble(description, Crs + "Exposure2012") ?? 0, -5, 5);
        b.Contrast = GetIntClamped(description, Crs + "Contrast2012", -100, 100);
        b.Highlights = GetIntClamped(description, Crs + "Highlights2012", -100, 100);
        b.Shadows = GetIntClamped(description, Crs + "Shadows2012", -100, 100);
        b.Whites = GetIntClamped(description, Crs + "Whites2012", -100, 100);
        b.Blacks = GetIntClamped(description, Crs + "Blacks2012", -100, 100);
        b.Tint = GetIntClamped(description, Crs + "Tint", -100, 100);
        b.Saturation = GetIntClamped(description, Crs + "Saturation", -100, 100);
        b.Vibrance = GetIntClamped(description, Crs + "Vibrance", -100, 100);
        b.Clarity = GetIntClamped(description, Crs + "Clarity2012", -100, 100);
        b.Texture = GetIntClamped(description, Crs + "Texture", -100, 100);
        b.Dehaze = GetIntClamped(description, Crs + "Dehaze", -100, 100);
        b.Sharpening = GetIntClamped(description, Crs + "Sharpness", 0, 150);
        b.NoiseReduction = GetIntClamped(description, Crs + "LuminanceSmoothing", 0, 100);

        // Temperature: RAWはKelvin(>150)、JPEG等は相対値(±100)
        if (GetDouble(description, Crs + "Temperature") is { } temperature)
        {
            b.Temperature = Math.Abs(temperature) <= 150
                ? (int)Math.Clamp(Math.Round(temperature), -100, 100)
                : KelvinToRelative(temperature);
        }

        // クロップ
        if (GetBool(description, Crs + "HasCrop") == true)
        {
            var left = Math.Clamp(GetDouble(description, Crs + "CropLeft") ?? 0, 0, 1);
            var top = Math.Clamp(GetDouble(description, Crs + "CropTop") ?? 0, 0, 1);
            var right = Math.Clamp(GetDouble(description, Crs + "CropRight") ?? 1, 0, 1);
            var bottom = Math.Clamp(GetDouble(description, Crs + "CropBottom") ?? 1, 0, 1);
            settings.Crop.X = left;
            settings.Crop.Y = top;
            settings.Crop.Width = Math.Max(0.01, right - left);
            settings.Crop.Height = Math.Max(0.01, bottom - top);
        }

        settings.Crop.Straighten = Math.Clamp(GetDouble(description, Crs + "CropAngle") ?? 0, -45, 45);

        // 回転・反転(tiff:Orientation)
        if (GetDouble(description, Tiff + "Orientation") is { } orientationValue)
        {
            ApplyTiffOrientation(settings.Crop, (int)orientationValue);
        }

        // 評価・色ラベル
        int? rating = GetDouble(description, Xmp + "Rating") is { } r
            ? (int)Math.Clamp(Math.Round(r), 0, 5)
            : null;
        ColorLabel? label = GetString(description, Xmp + "Label") is { } labelText
            ? ParseLabel(labelText)
            : null;

        return new XmpSidecarData(settings, rating, label);
    }

    /// <summary>
    /// EditSettingsをXMPへ書き出す。existingXmlがあれば本アプリ管理フィールドのみ更新し、
    /// 他の要素・属性(ローカル補正等)は保持する。
    /// </summary>
    public static string ToXmp(EditSettings settings, int rating, ColorLabel label, string? existingXml)
    {
        XDocument document;
        XElement? description = null;

        if (!string.IsNullOrWhiteSpace(existingXml))
        {
            try
            {
                document = XDocument.Parse(existingXml);
                description = document.Descendants(Rdf + "Description").FirstOrDefault();
            }
            catch (System.Xml.XmlException)
            {
                document = CreateEmptyDocument();
            }
        }
        else
        {
            document = CreateEmptyDocument();
        }

        // 新規生成ドキュメント側のDescriptionも拾う(二重生成防止)
        description ??= document.Descendants(Rdf + "Description").FirstOrDefault();

        if (description is null)
        {
            var rdf = document.Descendants(Rdf + "RDF").FirstOrDefault();
            if (rdf is null)
            {
                document = CreateEmptyDocument();
                rdf = document.Descendants(Rdf + "RDF").First();
            }

            description = new XElement(Rdf + "Description", new XAttribute(Rdf + "about", ""));
            rdf.Add(description);
        }

        var b = settings.Basic;
        SetValue(description, Crs + "ProcessVersion", "11.0");
        SetValue(description, Crs + "Exposure2012", FormatSigned(b.Exposure, "0.00"));
        SetValue(description, Crs + "Contrast2012", FormatSigned(b.Contrast));
        SetValue(description, Crs + "Highlights2012", FormatSigned(b.Highlights));
        SetValue(description, Crs + "Shadows2012", FormatSigned(b.Shadows));
        SetValue(description, Crs + "Whites2012", FormatSigned(b.Whites));
        SetValue(description, Crs + "Blacks2012", FormatSigned(b.Blacks));
        SetValue(description, Crs + "Temperature", RelativeToKelvin(b.Temperature).ToString(CultureInfo.InvariantCulture));
        SetValue(description, Crs + "Tint", FormatSigned(b.Tint));
        SetValue(description, Crs + "Saturation", FormatSigned(b.Saturation));
        SetValue(description, Crs + "Vibrance", FormatSigned(b.Vibrance));
        SetValue(description, Crs + "Clarity2012", FormatSigned(b.Clarity));
        SetValue(description, Crs + "Texture", FormatSigned(b.Texture));
        SetValue(description, Crs + "Dehaze", FormatSigned(b.Dehaze));
        SetValue(description, Crs + "Sharpness", b.Sharpening.ToString(CultureInfo.InvariantCulture));
        SetValue(description, Crs + "LuminanceSmoothing", b.NoiseReduction.ToString(CultureInfo.InvariantCulture));

        var crop = settings.Crop;
        var hasCrop = crop.X != 0 || crop.Y != 0 || crop.Width != 1.0 || crop.Height != 1.0 || crop.Straighten != 0;
        SetValue(description, Crs + "HasCrop", hasCrop ? "True" : "False");
        if (hasCrop)
        {
            SetValue(description, Crs + "CropLeft", FormatDouble(crop.X));
            SetValue(description, Crs + "CropTop", FormatDouble(crop.Y));
            SetValue(description, Crs + "CropRight", FormatDouble(crop.X + crop.Width));
            SetValue(description, Crs + "CropBottom", FormatDouble(crop.Y + crop.Height));
            SetValue(description, Crs + "CropAngle", FormatDouble(crop.Straighten));
        }

        SetValue(description, Tiff + "Orientation",
            ToTiffOrientation(crop).ToString(CultureInfo.InvariantCulture));
        SetValue(description, Xmp + "Rating", rating.ToString(CultureInfo.InvariantCulture));
        SetValue(description, Xmp + "Label", label == ColorLabel.None ? "" : label.ToString());

        return document.Declaration is null
            ? document.ToString()
            : document.Declaration + System.Environment.NewLine + document.ToString();
    }

    // ---- Temperature近似変換(往復安定) ----

    /// <summary>Kelvin → 相対値(-100..+100)。5500Kを中立とする対数近似。</summary>
    internal static int KelvinToRelative(double kelvin) =>
        (int)Math.Clamp(Math.Round(83.0 * Math.Log2(Math.Clamp(kelvin, 1000, 60000) / 5500.0)), -100, 100);

    /// <summary>相対値 → Kelvin(書き出し用)。</summary>
    internal static int RelativeToKelvin(int relative) =>
        (int)Math.Round(5500.0 * Math.Pow(2.0, relative / 83.0));

    // ---- Orientationマッピング ----

    private static void ApplyTiffOrientation(CropSettings crop, int orientation)
    {
        (crop.RotationSteps, crop.FlipHorizontal, crop.FlipVertical) = orientation switch
        {
            2 => (0, true, false),
            3 => (2, false, false),
            4 => (0, false, true),
            5 => (1, true, false),
            6 => (1, false, false),
            7 => (3, true, false),
            8 => (3, false, false),
            _ => (0, false, false),
        };
    }

    internal static int ToTiffOrientation(CropSettings crop)
    {
        var steps = ((crop.RotationSteps % 4) + 4) % 4;
        var flipH = crop.FlipHorizontal;
        var flipV = crop.FlipVertical;

        // flipH+flipV は180度回転と等価なので正規化する
        if (flipH && flipV)
        {
            flipH = false;
            flipV = false;
            steps = (steps + 2) % 4;
        }

        // flipVは「flipH+180度」へ正規化(TIFFは4で表現できるが分岐削減のため統一)
        if (flipV)
        {
            flipV = false;
            flipH = true;
            steps = (steps + 2) % 4;
        }

        return (steps, flipH) switch
        {
            (0, false) => 1,
            (1, false) => 6,
            (2, false) => 3,
            (3, false) => 8,
            (0, true) => 2,
            (1, true) => 5,
            (2, true) => 4,   // flipH+180 = flipV
            _ => 7,           // (3, true)
        };
    }

    // ---- XMLヘルパー(属性形式・子要素形式の両対応で読む。書き込みは属性) ----

    private static string? GetString(XElement description, XName name) =>
        description.Attribute(name)?.Value ?? description.Element(name)?.Value;

    private static double? GetDouble(XElement description, XName name)
    {
        var text = GetString(description, name)?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        // Lightroomは "+0.50" 形式を使う
        return double.TryParse(text.TrimStart('+'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static int GetIntClamped(XElement description, XName name, int min, int max) =>
        GetDouble(description, name) is { } value ? (int)Math.Clamp(Math.Round(value), min, max) : 0;

    private static bool? GetBool(XElement description, XName name) =>
        GetString(description, name)?.Trim().ToLowerInvariant() switch
        {
            "true" or "1" => true,
            "false" or "0" => false,
            _ => null,
        };

    private static void SetValue(XElement description, XName name, string value)
    {
        // 子要素形式で存在する場合はそちらを更新(形式を維持)、それ以外は属性
        var element = description.Element(name);
        if (element is not null)
        {
            element.Value = value;
            return;
        }

        description.SetAttributeValue(name, value);
    }

    private static string FormatSigned(double value, string format = "0") =>
        value > 0
            ? "+" + value.ToString(format, CultureInfo.InvariantCulture)
            : value.ToString(format, CultureInfo.InvariantCulture);

    private static string FormatDouble(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static ColorLabel? ParseLabel(string text) =>
        Enum.TryParse<ColorLabel>(text.Trim(), ignoreCase: true, out var label) ? label : ColorLabel.None;

    private static XDocument CreateEmptyDocument()
    {
        // 名前空間プレフィックスを明示宣言(自動採番のp1:等を避け、外部ツールとの互換性を高める)
        var description = new XElement(
            Rdf + "Description",
            new XAttribute(Rdf + "about", ""),
            new XAttribute(XNamespace.Xmlns + "crs", Crs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "tiff", Tiff.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xmp", Xmp.NamespaceName));
        return new XDocument(
            new XElement(
                X + "xmpmeta",
                new XAttribute(XNamespace.Xmlns + "x", X.NamespaceName),
                new XElement(
                    Rdf + "RDF",
                    new XAttribute(XNamespace.Xmlns + "rdf", Rdf.NamespaceName),
                    description)));
    }
}
