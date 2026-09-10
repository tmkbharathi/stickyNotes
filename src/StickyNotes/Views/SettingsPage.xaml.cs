using Microsoft.UI.Xaml.Controls;
using StickyNotes.ViewModels;

namespace StickyNotes.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel? ViewModel
    {
        get => DataContext as SettingsViewModel;
        set => DataContext = value;
    }

    public SettingsPage()
    {
        this.InitializeComponent();
    }
}
