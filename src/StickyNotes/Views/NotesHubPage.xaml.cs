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
        await ShowEditNoteDialogAsync(new NoteModel
        {
            Title = "Quick Note",
            Content = "",
            Category = "Work",
            ColorTheme = "yellow"
        }, isNew: true);
    }

    private async void OnNoteItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is NoteModel note)
        {
            await ShowEditNoteDialogAsync(note, isNew: false);
        }
    }

    private async void OnDeleteNoteClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NoteModel note && ViewModel != null)
        {
            await ViewModel.DeleteNoteAsync(note);
        }
    }

    private async void OnTogglePinClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is NoteModel note && ViewModel != null)
        {
            await ViewModel.TogglePinAsync(note);
        }
    }

    private void OnFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && ViewModel != null)
        {
            ViewModel.FilterCategory(tag);
        }
    }

    private void OnCopySnippetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string snippetContent && !string.IsNullOrEmpty(snippetContent))
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(snippetContent);
            Clipboard.SetContent(dataPackage);
        }
    }

    private async Task ShowEditNoteDialogAsync(NoteModel note, bool isNew)
    {
        var titleBox = new TextBox { Header = "Title", Text = note.Title, Margin = new Thickness(0, 0, 0, 12) };
        var contentBox = new TextBox
        {
            Header = "Note Content",
            Text = note.Content,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Height = 120,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var categoryCombo = new ComboBox
        {
            Header = "Category",
            ItemsSource = new[] { "Work", "Dev", "Personal", "Design", "Study" },
            SelectedItem = note.Category,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var snippetLabelBox = new TextBox { Header = "Snippet Label (optional)", PlaceholderText = "e.g. Build Command", Margin = new Thickness(0, 0, 0, 8) };
        var snippetContentBox = new TextBox { Header = "Code / Command Snippet (optional)", PlaceholderText = "e.g. dotnet run", Margin = new Thickness(0, 0, 0, 12) };

        var panel = new StackPanel { Width = 380 };
        panel.Children.Add(titleBox);
        panel.Children.Add(contentBox);
        panel.Children.Add(categoryCombo);
        panel.Children.Add(snippetLabelBox);
        panel.Children.Add(snippetContentBox);

        var dialog = new ContentDialog
        {
            Title = isNew ? "Create New Sticky Note" : "Edit Sticky Note",
            PrimaryButtonText = "Save",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            Content = panel,
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary && ViewModel != null)
        {
            note.Title = string.IsNullOrWhiteSpace(titleBox.Text) ? "Untitled Note" : titleBox.Text;
            note.Content = contentBox.Text;
            note.Category = categoryCombo.SelectedItem?.ToString() ?? "Work";

            if (!string.IsNullOrWhiteSpace(snippetContentBox.Text))
            {
                note.Snippets.Clear();
                note.Snippets.Add(new SnippetBoxModel
                {
                    Label = string.IsNullOrWhiteSpace(snippetLabelBox.Text) ? "Code" : snippetLabelBox.Text,
                    Content = snippetContentBox.Text
                });
            }

            if (isNew)
            {
                await ViewModel.SaveNoteAsync(note);
                ViewModel.AllNotes.Insert(0, note);
                ViewModel.FilterCategory("all");
            }
            else
            {
                await ViewModel.SaveNoteAsync(note);
            }
        }
    }
}
