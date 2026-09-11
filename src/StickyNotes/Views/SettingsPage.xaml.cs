using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StickyNotes.ViewModels;

namespace StickyNotes.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel? ViewModel
    {
        get => DataContext as SettingsViewModel;
        set
        {
            DataContext = value;
            this.Bindings.Update();
        }
    }

    public SettingsPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is SettingsViewModel vm)
        {
            ViewModel = vm;
        }
    }
}
