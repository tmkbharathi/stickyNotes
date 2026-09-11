using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using StickyNotes.Core.Models.Note;
using StickyNotes.ViewModels;
using Windows.ApplicationModel.DataTransfer;

namespace StickyNotes.Views;

public sealed partial class NotesHubPage : Page
{
    public MainHubViewModel? ViewModel
    {
        get => DataContext as MainHubViewModel;
        set
        {
            DataContext = value;
            this.Bindings.Update();
        }
    }

    public NotesHubPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is MainHubViewModel vm)
        {
            ViewModel = vm;
        }
    }

    private async void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        await ViewModel.CreateNewNoteAsync();
        HubEditorControl.FocusTitle();
    }

    private async void OnSyncUpdatesClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        await ViewModel.UpdateVm.CheckForUpdatesAsync(force: true);
    }

    private void OnToggleFloatingPaneClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel == null) return;
        ViewModel.IsFloatingWindowVisible = !ViewModel.IsFloatingWindowVisible;
    }

    private void OnOpenStandaloneWindowClick(object sender, RoutedEventArgs e)
    {
        OpenActiveNoteInStandaloneWindow();
    }

    private void OnPopoutRequestedFromEditor(object? sender, EventArgs e)
    {
        OpenActiveNoteInStandaloneWindow();
    }

    private void OpenActiveNoteInStandaloneWindow()
    {
        if (ViewModel?.ActiveNote == null) return;
        var noteWin = new NoteWindow(
            ViewModel.ActiveNote,
            geometryService: ViewModel.GeometryService,
            onNoteUpdated: async _ => await ViewModel.SaveNoteAsync(ViewModel.ActiveNote),
            onNewNoteRequested: async _ => await ViewModel.CreateNewNoteAsync());
        noteWin.Activate();
    }

    private void OnFilterNavClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string cat && ViewModel != null)
        {
            ViewModel.FilterCategory(cat);
        }
    }

    private void OnColorFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string color && ViewModel != null)
        {
            ViewModel.FilterByColor(color);
        }
    }

    private void OnNoteCardItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NoteModel note && ViewModel != null)
        {
            ViewModel.OpenNoteInEditor(note);
        }
    }

    private async void OnCardPinClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NoteModel note && ViewModel != null)
        {
            await ViewModel.TogglePinAsync(note);
        }
    }

    private void OnCardCopyAllClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NoteModel note)
        {
            CopyNoteToClipboard(note);

            if (btn.Content is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock tb)
            {
                var orig = tb.Text;
                tb.Text = "Copied!";
                Task.Delay(1500).ContinueWith(_ =>
                {
                    App.CurrentAppSynchronizationContext?.Post(__ => tb.Text = orig, null);
                });
            }
        }
    }

    private void OnSnippetBoxCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string content && !string.IsNullOrEmpty(content))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(content);
            Clipboard.SetContent(dataPackage);

            if (btn.Content is StackPanel sp && sp.Children.Count >= 2 && sp.Children[1] is TextBlock tb)
            {
                var orig = tb.Text;
                tb.Text = "Copied!";
                Task.Delay(1500).ContinueWith(_ =>
                {
                    App.CurrentAppSynchronizationContext?.Post(__ => tb.Text = orig, null);
                });
            }
        }
    }

    private async void OnNewNoteRequestedFromEditor(object? sender, EventArgs e)
    {
        if (ViewModel != null)
        {
            await ViewModel.CreateNewNoteAsync();
            HubEditorControl.FocusTitle();
        }
    }

    private async void OnDeleteActiveNoteFromEditor(object? sender, NoteModel note)
    {
        if (ViewModel != null)
        {
            await ViewModel.DeleteNoteAsync(note);
        }
    }

    private async void OnActiveNoteSavedFromEditor(object? sender, NoteModel note)
    {
        if (ViewModel != null)
        {
            await ViewModel.SaveNoteAsync(note);
        }
    }

    private void OnToggleActiveNotePinClick(object sender, RoutedEventArgs e)
    {
        ViewModel?.ToggleActiveNotePin();
    }

    private async void OnDeleteActiveNoteClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel?.ActiveNote != null)
        {
            await ViewModel.DeleteNoteAsync(ViewModel.ActiveNote);
        }
    }

    private static void CopyNoteToClipboard(NoteModel note)
    {
        var text = $"{note.Title}\n\n{note.Content}";
        if (note.Snippets.Count > 0)
        {
            text += "\n\n-- Snippets --\n" + string.Join("\n\n", note.Snippets.Select(s => $"[{s.Type} - {s.Label}]\n{s.Content}"));
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);
    }
}
