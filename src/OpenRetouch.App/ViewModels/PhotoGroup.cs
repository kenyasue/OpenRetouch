using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace OpenRetouch.App.ViewModels;

/// <summary>
/// Year/month group for the grid (for CollectionViewSource IsSourceGrouped).
/// The group itself is the collection of photo items.
/// </summary>
public sealed class PhotoGroup : ObservableCollection<PhotoItemViewModel>
{
    public PhotoGroup(int year, int month, IEnumerable<PhotoItemViewModel> items)
        : base(items)
    {
        Year = year;
        Month = month;
    }

    /// <summary>Year (0 for the unknown-capture-date group, -1 for a headerless group).</summary>
    public int Year { get; }

    /// <summary>Month 1-12 (0 if unknown).</summary>
    public int Month { get; }

    /// <summary>Whether to show the header (false for the single group used with non-date sorting).</summary>
    public bool HasHeader => Year >= 0;

    /// <summary>The header's large month label ("May" / "Unknown Date").</summary>
    public string MonthTitle => Year < 0 ? "" : Month > 0
        ? System.Globalization.CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(Month)
        : "Unknown Date";

    /// <summary>The header's small year label ("2025"; empty for the unknown group).</summary>
    public string YearTitle => Year > 0 ? Year.ToString() : "";
}

/// <summary>One timeline bar entry (1:1 with a group).</summary>
public sealed partial class TimelineEntryViewModel : ObservableObject
{
    public TimelineEntryViewModel(string yearLabel, string monthLabel, int groupIndex)
    {
        YearLabel = yearLabel;
        MonthLabel = monthLabel;
        GroupIndex = groupIndex;
    }

    /// <summary>Year heading (empty = hidden when same as the previous entry's year).</summary>
    public string YearLabel { get; }

    /// <summary>Month label ("5" / "?").</summary>
    public string MonthLabel { get; }

    /// <summary>Index into the corresponding PhotoGroups (for click-to-jump).</summary>
    public int GroupIndex { get; }

    public bool HasYearLabel => YearLabel.Length > 0;

    /// <summary>Whether this is the month currently shown (scroll-follow highlight).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthBrush))]
    public partial bool IsCurrent { get; set; }

    // Same colors as AccentColor/TextDisabledColor in DarkTheme.xaml (same pattern as PhotoItemViewModel's color brushes)
    private static readonly SolidColorBrush CurrentBrush =
        new(Windows.UI.Color.FromArgb(255, 0x4C, 0xC2, 0xFF));

    private static readonly SolidColorBrush NormalBrush =
        new(Windows.UI.Color.FromArgb(255, 0x80, 0x80, 0x80));

    /// <summary>Color of the month label (the current month uses the accent color).</summary>
    public SolidColorBrush MonthBrush => IsCurrent ? CurrentBrush : NormalBrush;
}
