using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace StickyNotes.Views.Controls;

public sealed partial class CopyIconButton : UserControl
{
    public static readonly DependencyProperty TextToCopyProperty =
        DependencyProperty.Register(
            nameof(TextToCopy),
            typeof(string),
            typeof(CopyIconButton),
            new PropertyMetadata(string.Empty));

    public string TextToCopy
    {
        get => (string)GetValue(TextToCopyProperty);
        set => SetValue(TextToCopyProperty, value);
    }

    private CancellationTokenSource? _cts;

    public CopyIconButton()
    {
        this.InitializeComponent();
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(TextToCopy)) return;

        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(TextToCopy);
            Clipboard.SetContent(dataPackage);
        }
        catch
        {
            // Clipboard fallback
        }

        AnimateSuccess();
    }

    public void AnimateSuccess()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        ResetStoryboard.Stop();
        ShowSuccessStoryboard.Begin();

        Task.Delay(1200, token).ContinueWith(t =>
        {
            if (t.IsCanceled) return;
            App.CurrentAppSynchronizationContext?.Post(_ =>
            {
                if (!token.IsCancellationRequested)
                {
                    ResetStoryboard.Begin();
                }
            }, null);
        }, token);
    }
}
