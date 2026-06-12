namespace OpenRetouch.App.Services;

/// <summary>The app's view modes.</summary>
public enum ViewMode
{
    Library,
    Edit,
    Export,
    Settings,
}

/// <summary>Provides navigation via the Frame inside the Shell.</summary>
public interface INavigationService
{
    ViewMode Current { get; }

    event EventHandler<ViewMode>? Navigated;

    void NavigateTo(ViewMode mode);
}
