using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Note;
using Windows.ApplicationModel.DataTransfer;

namespace StickyNotes.Views.Controls;

public sealed partial class NoteFloatingViewControl : UserControl
{
    public static readonly DependencyProperty NoteProperty =
        DependencyProperty.Register(
            nameof(Note),
            typeof(NoteModel),
            typeof(NoteFloatingViewControl),
            new PropertyMetadata(null, OnNoteChangedStatic));

    public NoteModel? Note
    {
        get => (NoteModel?)GetValue(NoteProperty);
        set => SetValue(NoteProperty, value);
    }

    public event EventHandler? PinToggleRequested;
    public event EventHandler? MinimizeRequested;
    public event EventHandler? CloseRequested;
    public event EventHandler? HeaderDragRequested;

    public NoteFloatingViewControl()
    {
        this.InitializeComponent();
    }

    private static void OnNoteChangedStatic(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteFloatingViewControl ctrl)
        {
            ctrl.Bindings.Update();
        }
    }

    private void OnHeaderBarPointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        HeaderDragRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnTogglePinClick(object sender, RoutedEventArgs e)
    {
        PinToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCopyAllClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;

        var text = $"{Note.DisplayTitle}\n\n{Note.Content}";
        if (Note.Snippets.Count > 0)
        {
            text += "\n\n" + string.Join("\n\n", Note.Snippets.Select(s => s.Content));
        }

        var dataPackage = new DataPackage();
        dataPackage.SetText(text);
        Clipboard.SetContent(dataPackage);

        CopyFeedbackText.Text = "✓ Copied";
        CopyFeedbackText.Visibility = Visibility.Visible;
        CopyIcon.Glyph = "\uE73E"; // Checkmark

        Task.Delay(1500).ContinueWith(_ =>
        {
            App.CurrentAppSynchronizationContext?.Post(__ =>
            {
                CopyFeedbackText.Visibility = Visibility.Collapsed;
                CopyIcon.Glyph = "\uE8C8"; // Copy icon
            }, null);
        });
    }

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
