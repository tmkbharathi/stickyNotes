using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Update;
using StickyNotes.ViewModels;

namespace StickyNotes.Views.Dialogs;

public sealed partial class UpdateAvailableDialog : ContentDialog, INotifyPropertyChanged
{
    public UpdateViewModel ViewModel { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? prop = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));

    public string VersionSummaryText =>
        $"Sticky Notes {ViewModel.AvailableVersionText} is available.\nYou are currently using version {ViewModel.CurrentVersionText}.";

    public string ReleaseNotesText =>
        !string.IsNullOrWhiteSpace(ViewModel.ReleaseNotes)
            ? ViewModel.ReleaseNotes
            : "• Performance improvements and bug fixes.\n• Windows autostart support.\n• Seamless GitHub Releases update integration.";

    public UpdateAvailableDialog(UpdateViewModel viewModel)
    {
        ViewModel = viewModel;
        ViewModel.PropertyChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(VersionSummaryText));
            OnPropertyChanged(nameof(ReleaseNotesText));
        };
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
