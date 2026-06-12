namespace OpenRetouch.Core.Models;

/// <summary>
/// 表示順の連続区間としての年月グループ。Year=0(Month=0)は撮影日時不明。
/// </summary>
/// <param name="Year">年(不明は0)。</param>
/// <param name="Month">月1-12(不明は0)。</param>
/// <param name="StartIndex">元リスト内の開始インデックス。</param>
/// <param name="Count">区間の要素数。</param>
public sealed record MonthSegment(int Year, int Month, int StartIndex, int Count);

/// <summary>
/// 写真一覧の月グループ化(Google Photos風タイムライン用)の純粋ロジック。
/// 表示順を保ったまま「連続する同一年月」をひとつの区間にまとめる(ラン長方式)。
/// 撮影日時ソートでは完全な月グループに、その他のソートでも自然な区切りになる。
/// </summary>
public static class MonthGrouping
{
    /// <summary>
    /// 撮影日時リスト(表示順)を年月セグメントに分割する。
    /// 年月はローカル時刻基準。nullは「日付不明」(Year=0)として扱う。
    /// </summary>
    public static IReadOnlyList<MonthSegment> Segment(IReadOnlyList<DateTimeOffset?> capturedAt)
    {
        ArgumentNullException.ThrowIfNull(capturedAt);

        var segments = new List<MonthSegment>();
        var currentYear = -1;
        var currentMonth = -1;
        var start = 0;

        for (var i = 0; i < capturedAt.Count; i++)
        {
            var (year, month) = GetYearMonth(capturedAt[i]);
            if (i == 0)
            {
                (currentYear, currentMonth) = (year, month);
                continue;
            }

            if (year != currentYear || month != currentMonth)
            {
                segments.Add(new MonthSegment(currentYear, currentMonth, start, i - start));
                (currentYear, currentMonth) = (year, month);
                start = i;
            }
        }

        if (capturedAt.Count > 0)
        {
            segments.Add(new MonthSegment(currentYear, currentMonth, start, capturedAt.Count - start));
        }

        return segments;
    }

    private static (int Year, int Month) GetYearMonth(DateTimeOffset? capturedAt)
    {
        if (capturedAt is not { } value)
        {
            return (0, 0);
        }

        var local = value.ToLocalTime();
        return (local.Year, local.Month);
    }
}
