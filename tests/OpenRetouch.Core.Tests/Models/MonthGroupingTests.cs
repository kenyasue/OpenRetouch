using FluentAssertions;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Core.Tests.Models;

public sealed class MonthGroupingTests
{
    private static DateTimeOffset? Local(int year, int month, int day = 1) =>
        new DateTimeOffset(new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Local));

    [Fact]
    public void Segment_Empty_ReturnsEmpty()
    {
        MonthGrouping.Segment([]).Should().BeEmpty();
    }

    [Fact]
    public void Segment_SingleMonth_ReturnsOneSegment()
    {
        var result = MonthGrouping.Segment([Local(2026, 5, 1), Local(2026, 5, 14), Local(2026, 5, 31)]);

        result.Should().ContainSingle().Which.Should().Be(new MonthSegment(2026, 5, 0, 3));
    }

    [Fact]
    public void Segment_MultipleMonths_SplitsAtBoundaries()
    {
        var result = MonthGrouping.Segment(
            [Local(2026, 6), Local(2026, 6), Local(2026, 5), Local(2025, 12)]);

        result.Should().Equal(
            new MonthSegment(2026, 6, 0, 2),
            new MonthSegment(2026, 5, 2, 1),
            new MonthSegment(2025, 12, 3, 1));
    }

    [Fact]
    public void Segment_SameMonthDifferentYear_Splits()
    {
        var result = MonthGrouping.Segment([Local(2026, 5), Local(2025, 5)]);

        result.Should().HaveCount(2);
        result[0].Year.Should().Be(2026);
        result[1].Year.Should().Be(2025);
    }

    [Fact]
    public void Segment_NullDates_GroupAsUnknown()
    {
        var result = MonthGrouping.Segment([Local(2026, 5), null, null]);

        result.Should().Equal(
            new MonthSegment(2026, 5, 0, 1),
            new MonthSegment(0, 0, 1, 2));
    }

    [Fact]
    public void Segment_NullAtHead_StartsWithUnknownSegment()
    {
        var result = MonthGrouping.Segment([null, null, Local(2026, 5)]);

        result.Should().Equal(
            new MonthSegment(0, 0, 0, 2),
            new MonthSegment(2026, 5, 2, 1));
    }

    [Fact]
    public void Segment_AllNull_ReturnsSingleUnknownSegment()
    {
        var result = MonthGrouping.Segment([null, null, null]);

        result.Should().ContainSingle().Which.Should().Be(new MonthSegment(0, 0, 0, 3));
    }

    [Fact]
    public void Segment_NonChronologicalOrder_CreatesRunsInDisplayOrder()
    {
        // ファイル名ソート等で月が行き来するケース: 表示順のまま区切る(並べ替えない)
        var result = MonthGrouping.Segment([Local(2026, 5), Local(2026, 4), Local(2026, 5)]);

        result.Should().HaveCount(3, "同じ月でも連続しなければ別セグメントになる");
        result[2].Should().Be(new MonthSegment(2026, 5, 2, 1));
    }

    [Fact]
    public void Segment_UsesLocalTimeForMonthBoundary()
    {
        // ローカル月境界をまたぐUTC時刻: ローカル時刻基準で判定される
        var utc = new DateTimeOffset(2026, 5, 31, 23, 30, 0, TimeSpan.Zero);
        var expectedLocal = utc.ToLocalTime();

        var result = MonthGrouping.Segment([utc]);

        result.Should().ContainSingle().Which.Month.Should().Be(expectedLocal.Month);
        result[0].Year.Should().Be(expectedLocal.Year);
    }

    [Fact]
    public void Segment_StartIndexAndCount_CoverAllItems()
    {
        var dates = new DateTimeOffset?[]
        {
            Local(2026, 6), Local(2026, 6), Local(2026, 5), null, Local(2026, 5),
        };

        var result = MonthGrouping.Segment(dates);

        result.Sum(s => s.Count).Should().Be(dates.Length);
        result[0].StartIndex.Should().Be(0);
        for (var i = 1; i < result.Count; i++)
        {
            result[i].StartIndex.Should().Be(result[i - 1].StartIndex + result[i - 1].Count);
        }
    }
}
