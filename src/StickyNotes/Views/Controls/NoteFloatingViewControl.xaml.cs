using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StickyNotes.Core.Models.Note;

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
            ctrl.Note?.EnsureUnifiedBlocks();
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

    private void OnMinimizeClick(object sender, RoutedEventArgs e)
    {
        MinimizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
