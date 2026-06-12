namespace OpenRetouch.Core.Editing;

/// <summary>
/// 輝度ヒストグラムからの自動トーン補正(Auto Tone)の算出。
/// パーセンタイルベースの決定的アルゴリズムで、トーン6項目
/// (Exposure/Contrast/Highlights/Shadows/Whites/Blacks)のみを設定する。
/// 色(Temperature/Tint/Saturation等)とディテール項目は変更しない。
/// </summary>
public static class AutoToneCalculator
{
    /// <summary>中央値をこの輝度(0-255)へ寄せる(18%グレー相当よりやや明るめ)。</summary>
    private const double TargetMedian = 118.0;

    /// <summary>露出の最大補正量(EV)。極端な補正を避けるため±5.0より狭くする。</summary>
    private const double MaxExposure = 2.5;

    /// <summary>中央値→目標への寄せ係数(1.0だと完全一致でやり過ぎになるため減衰)。</summary>
    private const double ExposureDamping = 0.7;

    /// <summary>
    /// 256bin輝度ヒストグラムから自動トーン補正を算出する。
    /// ヒストグラムが空・極端(全画素同輝度)の場合は補正なし(全て0)を返す。
    /// </summary>
    /// <param name="histogram">輝度0-255の度数(256要素)。</param>
    public static BasicAdjustments Calculate(IReadOnlyList<long> histogram)
    {
        ArgumentNullException.ThrowIfNull(histogram);
        if (histogram.Count != 256)
        {
            throw new ArgumentException("Histogram must contain exactly 256 elements.", nameof(histogram));
        }

        var result = new BasicAdjustments();
        var total = histogram.Sum();
        if (total == 0)
        {
            return result;
        }

        var p01 = Percentile(histogram, total, 0.01);
        var p05 = Percentile(histogram, total, 0.05);
        var median = Percentile(histogram, total, 0.50);
        var p95 = Percentile(histogram, total, 0.95);
        var p99 = Percentile(histogram, total, 0.99);

        // 全画素がほぼ同輝度(単色画像等)は補正しない(スプレッドが情報を持たない)
        if (p99 - p01 < 4)
        {
            return result;
        }

        // 露出: 中央値を目標へ寄せる(対数比、減衰+クランプ)
        var safeMedian = Math.Max(median, 1.0);
        result.Exposure = Math.Round(
            Math.Clamp(ExposureDamping * Math.Log2(TargetMedian / safeMedian), -MaxExposure, MaxExposure),
            2);

        // ブラック: 下端が黒(16)から浮いていれば引き締め、深く潰れていれば持ち上げ
        if (p01 > 16)
        {
            result.Blacks = (int)Math.Clamp(-(p01 - 16) * 1.5, -60, 0);
        }
        else if (p01 < 4)
        {
            result.Blacks = (int)Math.Clamp((4 - p01) * 5.0, 0, 30);
        }

        // ホワイト: 上端が白(240)に届かなければ持ち上げ、飽和気味なら抑える
        if (p99 < 240)
        {
            result.Whites = (int)Math.Clamp((240 - p99) * 0.8, 0, 60);
        }
        else if (p99 > 252)
        {
            result.Whites = (int)Math.Clamp(-(p99 - 252) * 5.0, -30, 0);
        }

        // ハイライト: 明部に画素が集中(白飛び傾向)していれば回復方向
        if (p95 > 250)
        {
            result.Highlights = (int)Math.Clamp(-(p95 - 250) * 8.0, -60, 0);
        }

        // シャドウ: 暗部が潰れ気味なら持ち上げ
        if (p05 < 8)
        {
            result.Shadows = (int)Math.Clamp((8 - p05) * 5.0, 0, 50);
        }

        // コントラスト: スプレッド(p95-p05)が狭い眠い画像は持ち上げる
        var spread = p95 - p05;
        if (spread < 180)
        {
            result.Contrast = (int)Math.Clamp((180 - spread) * 0.25, 0, 40);
        }

        return result;
    }

    /// <summary>
    /// 算出したトーン6項目をtargetへ上書きする(色・ディテール・クロップは変更しない)。
    /// </summary>
    public static void ApplyTone(BasicAdjustments target, BasicAdjustments tone)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(tone);
        target.Exposure = tone.Exposure;
        target.Contrast = tone.Contrast;
        target.Highlights = tone.Highlights;
        target.Shadows = tone.Shadows;
        target.Whites = tone.Whites;
        target.Blacks = tone.Blacks;
    }

    /// <summary>累積度数が指定割合に達する輝度値(0-255)を返す。</summary>
    private static int Percentile(IReadOnlyList<long> histogram, long total, double fraction)
    {
        var threshold = (long)Math.Ceiling(total * fraction);
        long cumulative = 0;
        for (var i = 0; i < histogram.Count; i++)
        {
            cumulative += histogram[i];
            if (cumulative >= threshold)
            {
                return i;
            }
        }

        return 255;
    }
}
