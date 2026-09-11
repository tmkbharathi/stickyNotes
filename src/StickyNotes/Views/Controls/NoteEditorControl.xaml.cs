using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Note;
using Windows.ApplicationModel.DataTransfer;

namespace StickyNotes.Views.Controls;

public sealed partial class NoteEditorControl : UserControl
{
    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(
            nameof(Note),
            typeof(NoteModel),
            typeof(NoteEditorControl),
            new PropertyMetadata(null, OnNoteChangedStatic));

    public static readonly DependencyProperty IsFloatingModeProperty =
        DependencyProperty.Register(
            nameof(IsFloatingMode),
            typeof(bool),
            typeof(NoteEditorControl),
            new PropertyMetadata(false, OnFloatingModeChangedStatic));

    public NoteModel? Note
    {
        get => (NoteModel?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public bool IsFloatingMode
    {
        get => (bool)GetValue(IsFloatingModeProperty);
        set => SetValue(IsFloatingModeProperty, value);
    }

    public UIElement TitleBarElement => HeaderBar;

    public event EventHandler? NewNoteRequested;
    public event EventHandler? PopoutRequested;
    public event EventHandler? AttachRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? HeaderDragRequested;
    public event EventHandler<NoteModel>? DeleteRequested;
    public event EventHandler<NoteModel>? NoteSaved;

    private System.Threading.Timer? _debounceTimer;

    public NoteEditorControl()
    {
        this.InitializeComponent();
    }

    private void OnHeaderBarPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (IsFloatingMode)
        {
            HeaderDragRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void OnNoteChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteEditorControl ctrl)
        {
            ctrl.Bindings.Update();
        }
    }

    private static void OnFloatingModeChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteEditorControl ctrl)
        {
            ctrl.Bindings.Update();
        }
    }

    private void OnAttachClick(object sender, RoutedEventArgs e)
    {
        AttachRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNewNoteClick(object sender, RoutedEventArgs e)
    {
        NewNoteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTogglePinClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.IsPinned = !Note.IsPinned;
        TriggerAutoSave();
    }

    private void OnPopoutClick(object sender, RoutedEventArgs e)
    {
        PopoutRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (Note != null)
        {
            DeleteRequested?.Invoke(this, Note);
        }
    }

    private void OnAddSnippetBoxClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.Snippets.Add(new SnippetBoxModel
        {
            Type = "CMD",
            Label = "Snippet",
            Content = "// Enter command or snippet...",
            OrderIndex = Note.Snippets.Count
        });
        TriggerAutoSave();
    }

    private void OnSnippetRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SnippetBoxModel snippet && Note != null)
        {
            Note.Snippets.Remove(snippet);
            TriggerAutoSave();
        }
    }

    private void OnColorSelectClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string color && Note != null)
        {
            Note.ColorTheme = color;
            TriggerAutoSave();
        }
    }

    private void OnSnippetCopyClick(object sender, RoutedEventArgs e)
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

    private void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var text = $"{Note.Title}\n\n{Note.Content}";
        if (Note.Snippets.Count > 0)
        {
            text += "\n\n-- Snippets --\n" + string.Join("\n\n", Note.Snippets.Select(s => $"[{s.Type} - {s.Label}]\n{s.Content}"));
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);

        CopyAllText.Text = "Copied!";
        Task.Delay(1500).ContinueWith(_ =>
        {
            App.CurrentAppSynchronizationContext?.Post(__ => CopyAllText.Text = "Copy all blocks", null);
        });
    }

    public void FocusTitle()
    {
        TitleTextBox?.Focus(FocusState.Programmatic);
        TitleTextBox?.SelectAll();
    }

    private void OnContentChanged(object sender, TextChangedEventArgs e)
    {
        TriggerAutoSave();
    }

    private void TriggerAutoSave()
    {
        if (Note == null) return;
        if (SaveStatusLabel != null) SaveStatusLabel.Text = "Saving...";

        _debounceTimer?.Dispose();
        _debounceTimer = new System.Threading.Timer(_ =>
        {
            App.CurrentAppSynchronizationContext?.Post(__ =>
            {
                if (SaveStatusLabel != null) SaveStatusLabel.Text = "✓ Saved";
                if (Note != null)
                {
                    NoteSaved?.Invoke(this, Note);
                }
            }, null);
        }, null, 500, Timeout.Infinite);
    }
}
