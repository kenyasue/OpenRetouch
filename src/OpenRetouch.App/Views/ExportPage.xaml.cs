using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using OpenRetouch.App.ViewModels;

namespace OpenRetouch.App.Views;

public sealed partial class ExportPage : Page
{
    public ExportPage()
    {
        ViewModel = App.Current.Services.GetRequiredService<ExportViewModel>();
        InitializeComponent();
    }

    public ExportViewModel ViewModel { get; }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshTargetSummary();
    }
}
