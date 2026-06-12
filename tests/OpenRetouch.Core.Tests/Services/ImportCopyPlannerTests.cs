using FluentAssertions;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Services;

public sealed class ImportCopyPlannerTests
{
    private static readonly DateTimeOffset CapturedAt =
        new DateTimeOffset(2026, 5, 14, 10, 30, 0, TimeSpan.Zero).ToLocalTime();

    [Fact]
    public void Plan_WithDateFolders_BuildsYearMonthDayHierarchy()
    {
        var items = ImportCopyPlanner.Plan(
            [@"E:\DCIM\100CANON\IMG_0001.JPG"],
            @"D:\Photos",
            useDateFolders: true,
            _ => CapturedAt,
            sidecarExists: _ => false);

        var local = CapturedAt.ToLocalTime();
        var expected = Path.Combine(
            @"D:\Photos",
            local.Year.ToString("D4"),
            local.Month.ToString("D2"),
            local.Day.ToString("D2"),
            "IMG_0001.JPG");
        items.Should().ContainSingle().Which.DestinationPath.Should().Be(expected);
    }

    [Fact]
    public void Plan_WithoutDateFolders_PlacesFilesDirectlyUnderRoot()
    {
        var items = ImportCopyPlanner.Plan(
            [@"E:\DCIM\100CANON\IMG_0001.JPG", @"E:\DCIM\100CANON\IMG_0002.CR3"],
            @"D:\Photos",
            useDateFolders: false,
            _ => CapturedAt,
            sidecarExists: _ => false);

        items.Should().HaveCount(2);
        items[0].DestinationPath.Should().Be(@"D:\Photos\IMG_0001.JPG");
        items[1].DestinationPath.Should().Be(@"D:\Photos\IMG_0002.CR3");
    }

    [Fact]
    public void Plan_RawWithSidecar_IncludesSidecarPair()
    {
        var items = ImportCopyPlanner.Plan(
            [@"E:\DCIM\100CANON\IMG_0002.CR3"],
            @"D:\Photos",
            useDateFolders: true,
            _ => CapturedAt,
            sidecarExists: path => path.EndsWith(".xmp", StringComparison.OrdinalIgnoreCase));

        var item = items.Should().ContainSingle().Subject;
        item.SidecarSourcePath.Should().Be(@"E:\DCIM\100CANON\IMG_0002.xmp");
        item.SidecarDestinationPath.Should().Be(
            Path.ChangeExtension(item.DestinationPath, ".xmp"));
    }

    [Fact]
    public void Plan_RawWithoutSidecar_HasNoSidecarPaths()
    {
        var items = ImportCopyPlanner.Plan(
            [@"E:\DCIM\100CANON\IMG_0002.CR3"],
            @"D:\Photos",
            useDateFolders: true,
            _ => CapturedAt,
            sidecarExists: _ => false);

        var item = items.Should().ContainSingle().Subject;
        item.SidecarSourcePath.Should().BeNull();
        item.SidecarDestinationPath.Should().BeNull();
    }

    [Fact]
    public void Plan_NonRawFile_NeverChecksSidecar()
    {
        var items = ImportCopyPlanner.Plan(
            [@"E:\DCIM\100CANON\IMG_0001.JPG"],
            @"D:\Photos",
            useDateFolders: false,
            _ => CapturedAt,
            sidecarExists: _ => true);

        items.Should().ContainSingle().Which.SidecarSourcePath.Should().BeNull();
    }

    [Fact]
    public void Plan_FilesWithDifferentDates_GoToDifferentFolders()
    {
        var dates = new Dictionary<string, DateTimeOffset>
        {
            [@"E:\a.jpg"] = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero),
            [@"E:\b.jpg"] = new(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
        };

        var items = ImportCopyPlanner.Plan(
            [@"E:\a.jpg", @"E:\b.jpg"],
            @"D:\Photos",
            useDateFolders: true,
            path => dates[path],
            sidecarExists: _ => false);

        Path.GetDirectoryName(items[0].DestinationPath)
            .Should().NotBe(Path.GetDirectoryName(items[1].DestinationPath));
    }

    [Fact]
    public void Plan_EmptyDestinationRoot_Throws()
    {
        var act = () => ImportCopyPlanner.Plan(
            [@"E:\a.jpg"], "", useDateFolders: true, _ => CapturedAt, _ => false);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FormatDateFolder_PadsSingleDigitMonthAndDay()
    {
        // ローカル時刻基準のため、ローカル時刻で日付を構成する
        var date = new DateTimeOffset(new DateTime(2026, 3, 7, 12, 0, 0, DateTimeKind.Local));

        ImportCopyPlanner.FormatDateFolder(date)
            .Should().Be(Path.Combine("2026", "03", "07"));
    }
}
