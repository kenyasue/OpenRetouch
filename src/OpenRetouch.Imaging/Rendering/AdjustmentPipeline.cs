using OpenRetouch.Core.Editing;

namespace OpenRetouch.Imaging.Rendering;

/// <summary>
/// BGRA8ピクセル列への基本調整の適用(プレビューと書き出しで共通=WYSIWYG保証)。
/// 処理順序: WB → Exposure → Contrast → HL/SH → Whites/Blacks → Dehaze → Vibrance → Saturation
///           → NoiseReduction → Clarity → Texture → Sharpening(空間フィルタ)
/// </summary>
public static class AdjustmentPipeline
{
    /// <summary>
    /// 調整をインプレース適用する。デフォルト設定の場合は何もしない(恒等)。
    /// width/heightは空間フィルタ(Clarity/Texture/Sharpening/NoiseReduction)に使用する。
    /// </summary>
    public static void Apply(BasicAdjustments adjustments, byte[] bgra, int width, int height)
    {
        if (adjustments.IsDefault)
        {
            return;
        }

        if (bgra.Length % 4 != 0 || bgra.Length < width * height * 4)
        {
            throw new ArgumentException("Pixel buffer length must match width*height*4 (BGRA).", nameof(bgra));
        }

        ApplyPerPixel(adjustments, bgra);
        ApplySpatial(adjustments, bgra, width, height);
    }

    private static void ApplyPerPixel(BasicAdjustments adjustments, Span<byte> bgra)
    {
        // 空間フィルタのみの場合はピクセルループをスキップ
        var hasPerPixelWork =
            adjustments.Exposure != 0 || adjustments.Contrast != 0
            || adjustments.Highlights != 0 || adjustments.Shadows != 0
            || adjustments.Whites != 0 || adjustments.Blacks != 0
            || adjustments.Temperature != 0 || adjustments.Tint != 0
            || adjustments.Saturation != 0 || adjustments.Vibrance != 0
            || adjustments.Dehaze != 0;
        if (!hasPerPixelWork)
        {
            return;
        }


        // ---- パラメータの事前計算(ピクセルループ外) ----

        // WB: R/Bゲイン(±25%)、Tint: Gゲイン(±25%)
        var temp = adjustments.Temperature / 100f;
        var tint = adjustments.Tint / 100f;
        var gainR = 1f + 0.25f * temp;
        var gainB = 1f - 0.25f * temp;
        var gainG = 1f - 0.25f * tint;

        // Exposure: linear × 2^EV
        var exposureGain = MathF.Pow(2f, (float)adjustments.Exposure);

        // Contrast: ミッドグレー基準スケール(sRGB空間)
        var contrast = 1f + 0.8f * (adjustments.Contrast / 100f);

        // Highlights/Shadows: 輝度域別ゲイン
        var highlights = adjustments.Highlights / 100f;   // -1..+1
        var shadows = adjustments.Shadows / 100f;

        // Whites/Blacks: レベル(白点/黒点)移動
        var whitePoint = 1f - 0.25f * (adjustments.Whites / 100f);   // Whites+で白点を下げ明るく
        var blackPoint = -0.25f * (adjustments.Blacks / 100f);       // Blacks+で黒点を下げ(持ち上げ)
        var levelScale = 1f / MathF.Max(0.05f, whitePoint - blackPoint);

        // 彩度系
        var saturation = 1f + adjustments.Saturation / 100f;         // 0..2
        var vibrance = adjustments.Vibrance / 100f;                  // -1..+1

        // かすみ除去(正: コントラスト増+黒点引き締め / 負: かすみ追加)
        var dehaze = adjustments.Dehaze / 100f;

        var applyWb = adjustments.Temperature != 0 || adjustments.Tint != 0;
        var applyExposure = adjustments.Exposure != 0;
        var applyContrast = adjustments.Contrast != 0;
        var applyHlSh = adjustments.Highlights != 0 || adjustments.Shadows != 0;
        var applyLevels = adjustments.Whites != 0 || adjustments.Blacks != 0;
        var applyDehaze = adjustments.Dehaze != 0;
        var applySaturation = adjustments.Saturation != 0;
        var applyVibrance = adjustments.Vibrance != 0;

        for (var i = 0; i < bgra.Length; i += 4)
        {
            // sRGB→linear近似(γ2.2)
            var b = SrgbToLinear[bgra[i]];
            var g = SrgbToLinear[bgra[i + 1]];
            var r = SrgbToLinear[bgra[i + 2]];

            // 1. ホワイトバランス(linear)
            if (applyWb)
            {
                r *= gainR;
                g *= gainG;
                b *= gainB;
            }

            // 2. 露出(linear)
            if (applyExposure)
            {
                r *= exposureGain;
                g *= exposureGain;
                b *= exposureGain;
            }

            // 以降はトーン操作のためsRGB空間(知覚的に均等)で処理する
            r = LinearToSrgbF(r);
            g = LinearToSrgbF(g);
            b = LinearToSrgbF(b);

            // 3. コントラスト(0.5基準)
            if (applyContrast)
            {
                r = (r - 0.5f) * contrast + 0.5f;
                g = (g - 0.5f) * contrast + 0.5f;
                b = (b - 0.5f) * contrast + 0.5f;
            }

            // 4. ハイライト/シャドウ(輝度の重み付きゲイン)
            if (applyHlSh)
            {
                var luma = Luma(r, g, b);
                // 高輝度域: lumaの2乗で重み付け / 低輝度域: (1-luma)の2乗
                var hlWeight = luma * luma;
                var shWeight = (1f - luma) * (1f - luma);
                var gain = 1f + 0.6f * highlights * hlWeight + 0.6f * shadows * shWeight;
                r *= gain;
                g *= gain;
                b *= gain;
            }

            // 5. 白レベル/黒レベル(レベル補正)
            if (applyLevels)
            {
                r = (r - blackPoint) * levelScale;
                g = (g - blackPoint) * levelScale;
                b = (b - blackPoint) * levelScale;
            }

            // 6. かすみ除去
            if (applyDehaze)
            {
                if (dehaze > 0)
                {
                    r = (r - 0.12f * dehaze) * (1f + 0.35f * dehaze);
                    g = (g - 0.12f * dehaze) * (1f + 0.35f * dehaze);
                    b = (b - 0.12f * dehaze) * (1f + 0.35f * dehaze);
                }
                else
                {
                    var lift = -0.3f * dehaze;
                    r = r * (1f - lift) + lift;
                    g = g * (1f - lift) + lift;
                    b = b * (1f - lift) + lift;
                }
            }

            // 7. 自然な彩度(彩度が低い画素ほど強く効く)
            if (applyVibrance)
            {
                var luma = Luma(r, g, b);
                var maxChannel = MathF.Max(r, MathF.Max(g, b));
                var minChannel = MathF.Min(r, MathF.Min(g, b));
                var currentSat = maxChannel - minChannel;
                var amount = 1f + vibrance * (1f - Math.Clamp(currentSat, 0f, 1f));
                r = luma + (r - luma) * amount;
                g = luma + (g - luma) * amount;
                b = luma + (b - luma) * amount;
            }

            // 8. 彩度
            if (applySaturation)
            {
                var luma = Luma(r, g, b);
                r = luma + (r - luma) * saturation;
                g = luma + (g - luma) * saturation;
                b = luma + (b - luma) * saturation;
            }

            bgra[i] = ToByte(b);
            bgra[i + 1] = ToByte(g);
            bgra[i + 2] = ToByte(r);
            // アルファ(i+3)は変更しない
        }
    }

    // ---- 空間フィルタ(NoiseReduction → Clarity → Texture → Sharpening) ----

    private static void ApplySpatial(BasicAdjustments adjustments, byte[] bgra, int width, int height)
    {
        // 1. ノイズ軽減: 小半径ぼかしとのブレンド
        if (adjustments.NoiseReduction != 0)
        {
            var blurred = BoxBlur(bgra, width, height, radius: 1);
            var amount = 0.7f * adjustments.NoiseReduction / 100f;
            BlendInPlace(bgra, blurred, amount);
        }

        // 2. 明瞭度: 大半径アンシャープ(ミッドトーンコントラスト近似)
        if (adjustments.Clarity != 0)
        {
            UnsharpInPlace(bgra, width, height, radius: 6, amount: 0.6f * adjustments.Clarity / 100f);
        }

        // 3. テクスチャ: 中半径アンシャープ
        if (adjustments.Texture != 0)
        {
            UnsharpInPlace(bgra, width, height, radius: 2, amount: 0.8f * adjustments.Texture / 100f);
        }

        // 4. シャープ: 小半径アンシャープ
        if (adjustments.Sharpening != 0)
        {
            UnsharpInPlace(bgra, width, height, radius: 1, amount: 1.0f * adjustments.Sharpening / 100f);
        }
    }

    /// <summary>アンシャープマスク: src += amount * (src - blur)。負のamountはソフト化。</summary>
    private static void UnsharpInPlace(byte[] bgra, int width, int height, int radius, float amount)
    {
        var blurred = BoxBlur(bgra, width, height, radius);
        for (var i = 0; i < bgra.Length; i++)
        {
            if (i % 4 == 3)
            {
                continue; // アルファは変更しない
            }

            var value = bgra[i] + amount * (bgra[i] - blurred[i]);
            bgra[i] = (byte)Math.Clamp((int)(value + 0.5f), 0, 255);
        }
    }

    private static void BlendInPlace(byte[] bgra, byte[] other, float amount)
    {
        for (var i = 0; i < bgra.Length; i++)
        {
            if (i % 4 == 3)
            {
                continue;
            }

            bgra[i] = (byte)Math.Clamp((int)(bgra[i] + (other[i] - bgra[i]) * amount + 0.5f), 0, 255);
        }
    }

    /// <summary>分離型ボックスぼかし(移動平均、O(n))。BGRAの全チャンネルに適用(アルファ含むが上書きしない用途)。</summary>
    internal static byte[] BoxBlur(byte[] src, int width, int height, int radius)
    {
        var temp = new byte[src.Length];
        var dst = new byte[src.Length];

        // 水平パス
        for (var y = 0; y < height; y++)
        {
            for (var c = 0; c < 4; c++)
            {
                var rowOffset = y * width * 4 + c;
                var sum = 0;
                var count = 0;
                for (var x = -radius; x <= radius; x++)
                {
                    var cx = Math.Clamp(x, 0, width - 1);
                    sum += src[rowOffset + cx * 4];
                    count++;
                }

                for (var x = 0; x < width; x++)
                {
                    temp[rowOffset + x * 4] = (byte)(sum / count);
                    var addX = Math.Clamp(x + radius + 1, 0, width - 1);
                    var removeX = Math.Clamp(x - radius, 0, width - 1);
                    sum += src[rowOffset + addX * 4] - src[rowOffset + removeX * 4];
                }
            }
        }

        // 垂直パス
        for (var x = 0; x < width; x++)
        {
            for (var c = 0; c < 4; c++)
            {
                var colOffset = x * 4 + c;
                var stride = width * 4;
                var sum = 0;
                var count = 0;
                for (var y = -radius; y <= radius; y++)
                {
                    var cy = Math.Clamp(y, 0, height - 1);
                    sum += temp[colOffset + cy * stride];
                    count++;
                }

                for (var y = 0; y < height; y++)
                {
                    dst[colOffset + y * stride] = (byte)(sum / count);
                    var addY = Math.Clamp(y + radius + 1, 0, height - 1);
                    var removeY = Math.Clamp(y - radius, 0, height - 1);
                    sum += temp[colOffset + addY * stride] - temp[colOffset + removeY * stride];
                }
            }
        }

        return dst;
    }

    // ---- ヘルパー ----

    private static readonly float[] SrgbToLinear = BuildSrgbToLinearLut();

    private static float[] BuildSrgbToLinearLut()
    {
        var lut = new float[256];
        for (var i = 0; i < 256; i++)
        {
            lut[i] = MathF.Pow(i / 255f, 2.2f);
        }

        return lut;
    }

    private static float LinearToSrgbF(float linear) =>
        linear <= 0f ? 0f : MathF.Pow(linear, 1f / 2.2f);

    private static float Luma(float r, float g, float b) =>
        0.2126f * r + 0.7152f * g + 0.0722f * b;

    private static byte ToByte(float srgb) =>
        (byte)Math.Clamp((int)(srgb * 255f + 0.5f), 0, 255);
}
