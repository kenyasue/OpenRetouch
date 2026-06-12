namespace OpenRetouch.Core.Editing;

/// <summary>
/// クロップ/ジオメトリ設定。座標はOrientation正規化後の画像に対する正規化座標(0.0-1.0)。
/// 適用順: RotationSteps(90度) → Flip → Straighten → Crop切り出し。
/// </summary>
public sealed class CropSettings
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; } = 1.0;

    public double Height { get; set; } = 1.0;

    /// <summary>角度補正(-45..+45度)。はみ出しは内接ズームで補う。</summary>
    public double Straighten { get; set; }

    /// <summary>90度単位の回転(0-3 = 0/90/180/270度 時計回り)。</summary>
    public int RotationSteps { get; set; }

    public bool FlipHorizontal { get; set; }

    public bool FlipVertical { get; set; }

    /// <summary>アスペクト比ロック("free" | "1:1" | "4:5" | "16:9" | "3:2")。</summary>
    public string AspectRatio { get; set; } = "free";

    // AspectRatioはUI上のロック状態であり画像出力に影響しないため、意図的にIsDefaultの判定から除外している
    public bool IsDefault =>
        X == 0 && Y == 0 && Width == 1.0 && Height == 1.0
        && Straighten == 0 && RotationSteps == 0
        && !FlipHorizontal && !FlipVertical;

    public CropSettings Clone() => (CropSettings)MemberwiseClone();

    /// <summary>全パラメータの値が等しいか。</summary>
    public bool ValuesEqual(CropSettings other) =>
        X == other.X && Y == other.Y && Width == other.Width && Height == other.Height
        && Straighten == other.Straighten && RotationSteps == other.RotationSteps
        && FlipHorizontal == other.FlipHorizontal && FlipVertical == other.FlipVertical
        && AspectRatio == other.AspectRatio;
}
