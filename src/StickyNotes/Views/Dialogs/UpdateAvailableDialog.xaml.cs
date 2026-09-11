using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Update;
using StickyNotes.ViewModels;

namespace StickyNotes.Views.Dialogs;

public sealed partial class UpdateAvailableDialog : ContentDialog
{
    public UpdateViewModel ViewModel { get; }

    public string VersionSummaryText =>
        $"Sticky Notes {ViewModel.AvailableVersionText} is available.\nYou are currently using version {ViewModel.CurrentVersionText}.";

    public string ReleaseNotesText =>
        ViewModel.ReleaseNotes ?? "• Performance improvements and bug fixes.\n• Enhanced multi-snippet copy engine.\n• Seamless background MSIX update integration.";

    public UpdateAvailableDialog(UpdateViewModel viewModel)
    {
        ViewModel = viewModel;
        this.InitializeComponent();
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await ViewModel.UpdateNowAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.DeferUpdate();
    }
}
