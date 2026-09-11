using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Update;
using StickyNotes.ViewModels;

namespace StickyNotes.Views.Dialogs;

public sealed partial class UpdateAvailableDialog : ContentDialog
{
    private readonly UpdateViewModel _viewModel;

    public string VersionSummaryText =>
        $"Sticky Notes {_viewModel.AvailableVersionText} is available.\nYou are currently using version {_viewModel.CurrentVersionText}.";

    public string ReleaseNotesText =>
        _viewModel.ReleaseNotes ?? "• Performance improvements and bug fixes.\n• Enhanced multi-snippet copy engine.\n• Seamless background MSIX update integration.";

    public bool IsProgressVisible => _viewModel.IsDownloading;
    public Microsoft.UI.Xaml.Visibility ProgressVisibility => IsProgressVisible ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
    public double ProgressValue => _viewModel.DownloadProgressPercentage;
    public string ProgressText => $"{_viewModel.DownloadProgressPercentage:F0}%";

    public UpdateAvailableDialog(UpdateViewModel viewModel)
    {
        _viewModel = viewModel;
        this.InitializeComponent();
    }

    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await _viewModel.UpdateNowAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        _viewModel.DeferUpdate();
    }
}
