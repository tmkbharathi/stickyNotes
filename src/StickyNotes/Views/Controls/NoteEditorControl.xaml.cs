using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using StickyNotes.Core.Models.Note;

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
        this.Unloaded += (s, e) => _debounceTimer?.Dispose();

        this.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler(OnControlPointerMoved), true);
        this.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnControlPointerReleased), true);
        this.AddHandler(UIElement.PointerCanceledEvent, new PointerEventHandler(OnControlPointerCanceled), true);
        this.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler(OnControlPointerCaptureLost), true);
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
            ctrl.Note?.EnsureUnifiedBlocks();
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
        Note.IsAlwaysOnTop = Note.IsPinned;
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
            Content = string.Empty,
            OrderIndex = Note.Snippets.Count
        });
        TriggerAutoSave();
        Bindings.Update();
    }

    private void OnAddLabelBlockClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.Snippets.Add(new SnippetBoxModel
        {
            Type = "LABEL",
            Label = "Label",
            Content = string.Empty,
            OrderIndex = Note.Snippets.Count
        });
        TriggerAutoSave();
        Bindings.Update();
    }

    private void OnAddDescriptionBlockClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.Snippets.Add(new SnippetBoxModel
        {
            Type = "DESC",
            Label = "Description",
            Content = string.Empty,
            OrderIndex = Note.Snippets.Count
        });
        TriggerAutoSave();
        Bindings.Update();
    }

    private void OnAddSeparatorBlockClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.Snippets.Add(new SnippetBoxModel
        {
            Type = "SEPARATOR",
            Label = "Separator",
            Content = "---",
            OrderIndex = Note.Snippets.Count
        });
        TriggerAutoSave();
        Bindings.Update();
    }

    private SnippetBoxModel? _draggedSnippet;
    private Border? _activeGrip;
    private FrameworkElement? _draggedItemContainer;
    private Point _dragStartPos;
    private bool _isDraggingSnippet;

    // Title / Description swap dragging state
    private bool _isDraggingTitleOrContent;
    private bool _titleGripIsTitle;
    private Point _titleDragStartPos;
    private bool _titleHasSwapped;

    private void OnTitleGripPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            b.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        }
    }

    private void OnTitleGripPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border b)
        {
            b.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            this.ProtectedCursor = null;
        }
    }

    private void OnTitleGripPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsRightButtonPressed)
        {
            if (sender is FrameworkElement fe)
            {
                ShowTitleGripFlyout(fe);
                e.Handled = true;
            }
            return;
        }

        if (!pt.Properties.IsLeftButtonPressed) return;

        _isDraggingTitleOrContent = true;
        _titleGripIsTitle = true;
        _titleDragStartPos = pt.Position;
        _titleHasSwapped = false;
        this.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnContentGripPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsRightButtonPressed)
        {
            if (sender is FrameworkElement fe)
            {
                ShowContentGripFlyout(fe);
                e.Handled = true;
            }
            return;
        }

        if (!pt.Properties.IsLeftButtonPressed) return;

        _isDraggingTitleOrContent = true;
        _titleGripIsTitle = false;
        _titleDragStartPos = pt.Position;
        _titleHasSwapped = false;
        this.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnGripPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 255, 255, 255));
            this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
        }
    }

    private void OnGripPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Border border)
        {
            if (!_isDraggingSnippet || _activeGrip != border)
            {
                border.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
                this.ProtectedCursor = null;
            }
        }
    }

    private void OnGripPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        if (pt.Properties.IsRightButtonPressed)
        {
            if (sender is FrameworkElement fe && fe.Tag is SnippetBoxModel s)
            {
                ShowSnippetGripFlyout(fe, s);
                e.Handled = true;
            }
            return;
        }

        if (!pt.Properties.IsLeftButtonPressed) return;

        if (sender is Border border && border.Tag is SnippetBoxModel snippet)
        {
            _draggedSnippet = snippet;
            _activeGrip = border;
            _dragStartPos = e.GetCurrentPoint(SnippetsItemsControl).Position;
            _isDraggingSnippet = false;
            _draggedItemContainer = SnippetsItemsControl.ContainerFromItem(snippet) as FrameworkElement;

            this.CapturePointer(e.Pointer);
            e.Handled = true;
        }
    }

    private void OnControlPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingTitleOrContent && Note != null)
        {
            var pt = e.GetCurrentPoint(this).Position;
            var deltaY = pt.Y - _titleDragStartPos.Y;
            if (!_titleHasSwapped)
            {
                if ((_titleGripIsTitle && deltaY > 18) || (!_titleGripIsTitle && deltaY < -18))
                {
                    Note.IsContentFirst = !Note.IsContentFirst;
                    _titleHasSwapped = true;
                    TriggerAutoSave();
                }
            }
            return;
        }

        if (_draggedSnippet == null || Note == null) return;

        var curPos = e.GetCurrentPoint(SnippetsItemsControl).Position;
        var delta = curPos.Y - _dragStartPos.Y;

        if (!_isDraggingSnippet)
        {
            if (Math.Abs(delta) > 5)
            {
                _isDraggingSnippet = true;
                this.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
                if (_draggedItemContainer != null)
                {
                    _draggedItemContainer.Opacity = 0.55;
                }
            }
        }

        if (_isDraggingSnippet)
        {
            int currentIdx = Note.Snippets.IndexOf(_draggedSnippet);
            if (currentIdx >= 0)
            {
                // Check neighbor above
                if (currentIdx > 0)
                {
                    var prevContainer = SnippetsItemsControl.ContainerFromIndex(currentIdx - 1) as FrameworkElement;
                    if (prevContainer != null)
                    {
                        var prevCenterY = GetElementCenterY(prevContainer, SnippetsItemsControl);
                        if (curPos.Y < prevCenterY)
                        {
                            Note.Snippets.Move(currentIdx, currentIdx - 1);
                            UpdateSnippetOrderIndices();
                            _draggedItemContainer = SnippetsItemsControl.ContainerFromItem(_draggedSnippet) as FrameworkElement;
                            if (_draggedItemContainer != null) _draggedItemContainer.Opacity = 0.55;
                            return;
                        }
                    }
                }

                // Check neighbor below
                if (currentIdx < Note.Snippets.Count - 1)
                {
                    var nextContainer = SnippetsItemsControl.ContainerFromIndex(currentIdx + 1) as FrameworkElement;
                    if (nextContainer != null)
                    {
                        var nextCenterY = GetElementCenterY(nextContainer, SnippetsItemsControl);
                        if (curPos.Y > nextCenterY)
                        {
                            Note.Snippets.Move(currentIdx, currentIdx + 1);
                            UpdateSnippetOrderIndices();
                            _draggedItemContainer = SnippetsItemsControl.ContainerFromItem(_draggedSnippet) as FrameworkElement;
                            if (_draggedItemContainer != null) _draggedItemContainer.Opacity = 0.55;
                            return;
                        }
                    }
                }
            }

            // Auto-scroll when near top or bottom of EditorScrollViewer
            if (EditorScrollViewer != null)
            {
                var scrollPos = e.GetCurrentPoint(EditorScrollViewer).Position;
                if (scrollPos.Y < 30)
                {
                    EditorScrollViewer.ChangeView(null, Math.Max(0, EditorScrollViewer.VerticalOffset - 8), null, true);
                }
                else if (scrollPos.Y > EditorScrollViewer.ActualHeight - 30)
                {
                    EditorScrollViewer.ChangeView(null, EditorScrollViewer.VerticalOffset + 8, null, true);
                }
            }
        }
    }

    private static double GetElementCenterY(FrameworkElement element, UIElement relativeTo)
    {
        try
        {
            var transform = element.TransformToVisual(relativeTo);
            var pt = transform.TransformPoint(new Point(0, 0));
            return pt.Y + (element.ActualHeight / 2.0);
        }
        catch
        {
            return 0;
        }
    }

    private void OnControlPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingTitleOrContent)
        {
            this.ReleasePointerCapture(e.Pointer);
            this.ProtectedCursor = null;

            if (!_titleHasSwapped && Note != null)
            {
                Note.IsContentFirst = !Note.IsContentFirst;
                TriggerAutoSave();
            }

            _isDraggingTitleOrContent = false;
            _titleHasSwapped = false;
            e.Handled = true;
            return;
        }

        if (_draggedSnippet == null) return;

        this.ReleasePointerCapture(e.Pointer);
        this.ProtectedCursor = null;

        if (_activeGrip != null)
        {
            _activeGrip.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }

        if (_draggedItemContainer != null)
        {
            _draggedItemContainer.Opacity = 1.0;
            _draggedItemContainer = null;
        }

        if (_isDraggingSnippet)
        {
            TriggerAutoSave();
        }
        else
        {
            // Simple click without dragging -> show options flyout!
            if (_activeGrip != null)
            {
                ShowSnippetGripFlyout(_activeGrip, _draggedSnippet);
            }
        }

        _draggedSnippet = null;
        _activeGrip = null;
        _isDraggingSnippet = false;
        e.Handled = true;
    }

    private void OnControlPointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        CleanUpDragState();
    }

    private void OnControlPointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        CleanUpDragState();
    }

    private void CleanUpDragState()
    {
        if (_draggedItemContainer != null)
        {
            _draggedItemContainer.Opacity = 1.0;
            _draggedItemContainer = null;
        }
        if (_activeGrip != null)
        {
            _activeGrip.Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            _activeGrip = null;
        }
        this.ProtectedCursor = null;
        _draggedSnippet = null;
        _isDraggingSnippet = false;
        _isDraggingTitleOrContent = false;
        _titleHasSwapped = false;
    }

    private void ShowSnippetGripFlyout(FrameworkElement targetElement, SnippetBoxModel snippet)
    {
        if (Note == null) return;

        var flyout = new MenuFlyout();

        int idx = Note.Snippets.IndexOf(snippet);

        var moveUpItem = new MenuFlyoutItem
        {
            Text = "Move Up",
            Icon = new FontIcon { Glyph = "\uE70E", FontSize = 11 },
            IsEnabled = idx > 0
        };
        moveUpItem.Click += (s, args) =>
        {
            int i = Note.Snippets.IndexOf(snippet);
            if (i > 0)
            {
                Note.Snippets.Move(i, i - 1);
                UpdateSnippetOrderIndices();
                TriggerAutoSave();
            }
        };
        flyout.Items.Add(moveUpItem);

        var moveDownItem = new MenuFlyoutItem
        {
            Text = "Move Down",
            Icon = new FontIcon { Glyph = "\uE70D", FontSize = 11 },
            IsEnabled = idx >= 0 && idx < Note.Snippets.Count - 1
        };
        moveDownItem.Click += (s, args) =>
        {
            int i = Note.Snippets.IndexOf(snippet);
            if (i >= 0 && i < Note.Snippets.Count - 1)
            {
                Note.Snippets.Move(i, i + 1);
                UpdateSnippetOrderIndices();
                TriggerAutoSave();
            }
        };
        flyout.Items.Add(moveDownItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        string removeText = snippet.IsTitle ? "Delete title" :
                            snippet.IsSeparator ? "Remove separator" :
                            snippet.IsLabel ? "Remove label" :
                            snippet.IsDescription ? "Remove description" : "Remove block";

        var removeItem = new MenuFlyoutItem
        {
            Text = removeText,
            Icon = new FontIcon { Glyph = "\uE711", FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 136, 136)) }
        };
        removeItem.Click += (s, args) =>
        {
            OnSnippetRemoveClick(new Button { Tag = snippet }, new RoutedEventArgs());
        };
        flyout.Items.Add(removeItem);

        flyout.ShowAt(targetElement);
    }

    private void OnSnippetRemoveClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is SnippetBoxModel snippet && Note != null)
        {
            if (snippet.IsTitle)
            {
                Note.HasTitle = false;
                Note.Title = string.Empty;
            }
            else if (snippet.IsDescription)
            {
                Note.HasContent = false;
                Note.Content = string.Empty;
            }

            Note.Snippets.Remove(snippet);
            UpdateSnippetOrderIndices();
            TriggerAutoSave();
            Bindings.Update();
        }
    }

    private void OnSnippetContentChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is SnippetBoxModel snippet)
        {
            snippet.Content = tb.Text;
            if (snippet.IsTitle && Note != null)
            {
                Note.Title = tb.Text;
            }
            else if (snippet.IsDescription && Note != null)
            {
                Note.Content = tb.Text;
            }
            TriggerAutoSave();
        }
    }

    private void OnSnippetMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is SnippetBoxModel snippet && Note != null)
        {
            var index = Note.Snippets.IndexOf(snippet);
            if (index > 0)
            {
                Note.Snippets.Move(index, index - 1);
                UpdateSnippetOrderIndices();
                TriggerAutoSave();
            }
        }
    }

    private void OnSnippetMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is SnippetBoxModel snippet && Note != null)
        {
            var index = Note.Snippets.IndexOf(snippet);
            if (index >= 0 && index < Note.Snippets.Count - 1)
            {
                Note.Snippets.Move(index, index + 1);
                UpdateSnippetOrderIndices();
                TriggerAutoSave();
            }
        }
    }

    private void UpdateSnippetOrderIndices()
    {
        if (Note == null) return;
        for (int i = 0; i < Note.Snippets.Count; i++)
        {
            Note.Snippets[i].OrderIndex = i;
        }
        Note.SyncFromBlocks();
    }

    private void OnSnippetToggleMaskClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is SnippetBoxModel snippet)
        {
            snippet.IsMasked = !snippet.IsMasked;
            if (snippet.IsTitle && Note != null)
            {
                Note.IsTitleMasked = snippet.IsMasked;
            }
            else if (snippet.IsDescription && Note != null)
            {
                Note.IsContentMasked = snippet.IsMasked;
            }
            TriggerAutoSave();
        }
    }

    private void OnSnippetPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && pb.Tag is SnippetBoxModel snippet)
        {
            if (snippet.Content != pb.Password)
            {
                snippet.Content = pb.Password;
                if (snippet.IsTitle && Note != null)
                {
                    Note.Title = pb.Password;
                }
                else if (snippet.IsDescription && Note != null)
                {
                    Note.Content = pb.Password;
                }
                TriggerAutoSave();
            }
        }
    }

    private void OnToggleTitleMaskClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.IsTitleMasked = !Note.IsTitleMasked;
        TriggerAutoSave();
    }

    private void OnToggleContentMaskClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.IsContentMasked = !Note.IsContentMasked;
        TriggerAutoSave();
    }

    private void OnTitlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && Note != null)
        {
            if (Note.Title != pb.Password)
            {
                Note.Title = pb.Password;
                TriggerAutoSave();
            }
        }
    }

    private void OnContentPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && Note != null)
        {
            if (Note.Content != pb.Password)
            {
                Note.Content = pb.Password;
                TriggerAutoSave();
            }
        }
    }

    private void OnSwapTitleAndContentClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        Note.IsContentFirst = !Note.IsContentFirst;
        TriggerAutoSave();
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

    public bool IsNoteEmpty => Note != null && Note.Snippets.Count == 0;

    private void OnDeleteTitleClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var titleSnippet = Note.Snippets.FirstOrDefault(s => s.IsTitle);
        if (titleSnippet != null)
        {
            OnSnippetRemoveClick(new Button { Tag = titleSnippet }, new RoutedEventArgs());
        }
        else
        {
            Note.HasTitle = false;
            Note.Title = string.Empty;
            Bindings.Update();
            TriggerAutoSave();
        }
    }

    private void OnDeleteContentClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var descSnippet = Note.Snippets.FirstOrDefault(s => s.IsDescription);
        if (descSnippet != null)
        {
            OnSnippetRemoveClick(new Button { Tag = descSnippet }, new RoutedEventArgs());
        }
        else
        {
            Note.HasContent = false;
            Note.Content = string.Empty;
            Bindings.Update();
            TriggerAutoSave();
        }
    }

    private void OnAddTitleClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var existingTitle = Note.Snippets.FirstOrDefault(s => s.IsTitle);
        if (existingTitle != null) return;

        var titleBlock = new SnippetBoxModel
        {
            Type = "TITLE",
            Label = "Title",
            Content = string.IsNullOrWhiteSpace(Note.Title) ? "Untitled Note" : Note.Title,
            OrderIndex = 0
        };
        Note.HasTitle = true;
        Note.Title = titleBlock.Content;
        Note.Snippets.Insert(0, titleBlock);
        UpdateSnippetOrderIndices();
        Bindings.Update();
        TriggerAutoSave();
    }

    private void OnAddDescriptionClick(object sender, RoutedEventArgs e)
    {
        if (Note == null) return;
        var descBlock = new SnippetBoxModel
        {
            Type = "DESC",
            Label = "Description",
            Content = string.IsNullOrWhiteSpace(Note.Content) ? string.Empty : Note.Content,
            OrderIndex = Note.Snippets.Count
        };
        Note.HasContent = true;
        Note.Snippets.Add(descBlock);
        UpdateSnippetOrderIndices();
        Bindings.Update();
        TriggerAutoSave();
    }

    public void FocusTitle()
    {
    }

    public void FocusContent()
    {
    }

    private void ShowTitleGripFlyout(FrameworkElement targetElement)
    {
        if (Note == null) return;

        var flyout = new MenuFlyout();

        if (Note.HasContent)
        {
            var swapItem = new MenuFlyoutItem
            {
                Text = "Swap with Description",
                Icon = new FontIcon { Glyph = "\uE8D8", FontSize = 11 }
            };
            swapItem.Click += (s, args) =>
            {
                Note.IsContentFirst = !Note.IsContentFirst;
                TriggerAutoSave();
            };
            flyout.Items.Add(swapItem);
        }

        var toggleMaskItem = new MenuFlyoutItem
        {
            Text = Note.IsTitleMasked ? "Unhide title" : "Hide title",
            Icon = new FontIcon { Glyph = Note.IsTitleMasked ? "\uED1A" : "\uE7B3", FontSize = 11 }
        };
        toggleMaskItem.Click += (s, args) =>
        {
            Note.IsTitleMasked = !Note.IsTitleMasked;
            TriggerAutoSave();
        };
        flyout.Items.Add(toggleMaskItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var deleteItem = new MenuFlyoutItem
        {
            Text = "Delete title",
            Icon = new FontIcon { Glyph = "\uE711", FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 136, 136)) }
        };
        deleteItem.Click += (s, args) =>
        {
            OnDeleteTitleClick(this, new RoutedEventArgs());
        };
        flyout.Items.Add(deleteItem);

        flyout.ShowAt(targetElement);
    }

    private void ShowContentGripFlyout(FrameworkElement targetElement)
    {
        if (Note == null) return;

        var flyout = new MenuFlyout();

        if (Note.HasTitle)
        {
            var swapItem = new MenuFlyoutItem
            {
                Text = "Swap with Title",
                Icon = new FontIcon { Glyph = "\uE8D8", FontSize = 11 }
            };
            swapItem.Click += (s, args) =>
            {
                Note.IsContentFirst = !Note.IsContentFirst;
                TriggerAutoSave();
            };
            flyout.Items.Add(swapItem);
        }

        var toggleMaskItem = new MenuFlyoutItem
        {
            Text = Note.IsContentMasked ? "Unhide description" : "Hide description",
            Icon = new FontIcon { Glyph = Note.IsContentMasked ? "\uED1A" : "\uE7B3", FontSize = 11 }
        };
        toggleMaskItem.Click += (s, args) =>
        {
            Note.IsContentMasked = !Note.IsContentMasked;
            TriggerAutoSave();
        };
        flyout.Items.Add(toggleMaskItem);

        flyout.Items.Add(new MenuFlyoutSeparator());

        var deleteItem = new MenuFlyoutItem
        {
            Text = "Delete description",
            Icon = new FontIcon { Glyph = "\uE711", FontSize = 11, Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 136, 136)) }
        };
        deleteItem.Click += (s, args) =>
        {
            OnDeleteContentClick(this, new RoutedEventArgs());
        };
        flyout.Items.Add(deleteItem);

        flyout.ShowAt(targetElement);
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
