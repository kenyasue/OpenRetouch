using Microsoft.Extensions.DependencyInjection;
using OpenRetouch.App.Services;
using OpenRetouch.App.ViewModels;

namespace OpenRetouch.App;

public sealed partial class MainWindow : Microsoft.UI.Xaml.Window
{
    public MainWindow()
    {
        ViewModel = App.Current.Services.GetRequiredService<ShellViewModel>();
        InitializeComponent();
        Title = "Open Retouch";

        var navigation = (NavigationService)App.Current.Services.GetRequiredService<INavigationService>();
        navigation.Initialize(ContentFrame);
        navigation.NavigateTo(ViewMode.Library);
    }

    public ShellViewModel ViewModel { get; }
}
