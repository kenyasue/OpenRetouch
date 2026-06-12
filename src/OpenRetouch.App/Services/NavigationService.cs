using Microsoft.UI.Xaml.Controls;
using OpenRetouch.App.Views;

namespace OpenRetouch.App.Services;

/// <inheritdoc cref="INavigationService"/>
public sealed class NavigationService : INavigationService
{
    private static readonly IReadOnlyDictionary<ViewMode, Type> PageMap = new Dictionary<ViewMode, Type>
    {
        [ViewMode.Library] = typeof(LibraryPage),
        [ViewMode.Edit] = typeof(EditPage),
        [ViewMode.Export] = typeof(ExportPage),
        [ViewMode.Settings] = typeof(SettingsPage),
    };

    private Frame? _frame;

    public ViewMode Current { get; private set; } = ViewMode.Library;

    public event EventHandler<ViewMode>? Navigated;

    /// <summary>Registers the Shell's Frame (called exactly once during MainWindow initialization).</summary>
    public void Initialize(Frame frame)
    {
        _frame = frame;
    }

    public void NavigateTo(ViewMode mode)
    {
        if (_frame is null)
        {
            throw new InvalidOperationException("NavigationService is not initialized with a Frame.");
        }

        if (_frame.Content is not null && Current == mode)
        {
            return;
        }

        _frame.Navigate(PageMap[mode]);
        Current = mode;
        Navigated?.Invoke(this, mode);
    }
}
