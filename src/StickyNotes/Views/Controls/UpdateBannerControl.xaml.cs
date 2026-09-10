using Microsoft.UI.Xaml.Controls;
using StickyNotes.ViewModels;

namespace StickyNotes.Views.Controls;

public sealed partial class UpdateBannerControl : UserControl
{
    public UpdateViewModel? ViewModel
    {
        get => DataContext as UpdateViewModel;
        set => DataContext = value;
    }

    public UpdateBannerControl()
    {
        this.InitializeComponent();
    }
}
