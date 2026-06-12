namespace OpenRetouch.Core.Editing;

/// <summary>
/// 基本調整パラメータ。値域はUIスライダーと1:1対応。
/// Temperatureは非RAW画像のため絶対色温度(K)ではなく相対補正(-100..+100)とする。
/// </summary>
public sealed class BasicAdjustments
{
    public double Exposure { get; set; }       // -5.0 .. +5.0 (EV)

    public int Contrast { get; set; }          // -100 .. +100

    public int Highlights { get; set; }        // -100 .. +100

    public int Shadows { get; set; }           // -100 .. +100

    public int Whites { get; set; }            // -100 .. +100

    public int Blacks { get; set; }            // -100 .. +100

    public int Temperature { get; set; }       // -100(寒色) .. +100(暖色)

    public int Tint { get; set; }              // -100(緑) .. +100(マゼンタ)

    public int Saturation { get; set; }        // -100 .. +100

    public int Vibrance { get; set; }          // -100 .. +100

    public int Clarity { get; set; }           // -100 .. +100

    public int Texture { get; set; }           // -100 .. +100

    public int Dehaze { get; set; }            // -100 .. +100

    public int Sharpening { get; set; }        // 0 .. 150

    public int NoiseReduction { get; set; }    // 0 .. 100

    public bool IsDefault =>
        Exposure == 0 && Contrast == 0 && Highlights == 0 && Shadows == 0
        && Whites == 0 && Blacks == 0 && Temperature == 0 && Tint == 0
        && Saturation == 0 && Vibrance == 0
        && Clarity == 0 && Texture == 0 && Dehaze == 0
        && Sharpening == 0 && NoiseReduction == 0;

    public BasicAdjustments Clone() => (BasicAdjustments)MemberwiseClone();

    /// <summary>全パラメータをスライダー値域にクランプする(プリセットインポート等の外部入力用)。</summary>
    public void ClampToValidRange()
    {
        Exposure = Math.Clamp(Exposure, -5.0, 5.0);
        Contrast = Math.Clamp(Contrast, -100, 100);
        Highlights = Math.Clamp(Highlights, -100, 100);
        Shadows = Math.Clamp(Shadows, -100, 100);
        Whites = Math.Clamp(Whites, -100, 100);
        Blacks = Math.Clamp(Blacks, -100, 100);
        Temperature = Math.Clamp(Temperature, -100, 100);
        Tint = Math.Clamp(Tint, -100, 100);
        Saturation = Math.Clamp(Saturation, -100, 100);
        Vibrance = Math.Clamp(Vibrance, -100, 100);
        Clarity = Math.Clamp(Clarity, -100, 100);
        Texture = Math.Clamp(Texture, -100, 100);
        Dehaze = Math.Clamp(Dehaze, -100, 100);
        Sharpening = Math.Clamp(Sharpening, 0, 150);
        NoiseReduction = Math.Clamp(NoiseReduction, 0, 100);
    }

    /// <summary>全パラメータの値が等しいか。</summary>
    public bool ValuesEqual(BasicAdjustments other) =>
        Exposure == other.Exposure && Contrast == other.Contrast
        && Highlights == other.Highlights && Shadows == other.Shadows
        && Whites == other.Whites && Blacks == other.Blacks
        && Temperature == other.Temperature && Tint == other.Tint
        && Saturation == other.Saturation && Vibrance == other.Vibrance
        && Clarity == other.Clarity && Texture == other.Texture
        && Dehaze == other.Dehaze && Sharpening == other.Sharpening
        && NoiseReduction == other.NoiseReduction;
}
