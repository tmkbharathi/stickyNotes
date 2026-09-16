using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Update;

namespace StickyNotes.ViewModels;

/// <summary>
/// MVVM ViewModel orchestrating UI state, notifications, download progress, and user actions for updates.
/// </summary>
public sealed class UpdateViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IUpdateService _updateService;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public UpdateViewModel(IUpdateService updateService)
    {
        _updateService = updateService;
        _updateService.StateChanged += OnUpdateStateChanged;
        _updateService.DownloadProgressChanged += OnDownloadProgressChanged;

        CheckForUpdatesCommand = new AsyncRelayCommand(async () => await CheckForUpdatesAsync(force: true));
        UpdateNowCommand = new AsyncRelayCommand(async () => await UpdateNowAsync());
        DownloadUpdateCommand = new AsyncRelayCommand(async () => await DownloadUpdateAsync());
        LaterCommand = new RelayCommand(DeferUpdate);
    }

    public string CurrentVersionText => $"v{_updateService.GetCurrentVersion()}";
    public string AvailableVersionText => _updateService.GetAvailableVersion() != null ? $"v{_updateService.GetAvailableVersion()}" : string.Empty;

    public bool IsUpdateAvailable => _updateService.IsUpdateAvailable();
    public bool IsChecking => _updateService.CurrentState == UpdateState.Checking;
    public bool IsDownloading => _updateService.CurrentState == UpdateState.Downloading;
    public bool IsInstalling => _updateService.CurrentState == UpdateState.Installing;
    public bool IsRestartRequired => _updateService.CurrentState == UpdateState.RestartRequired;
    public bool HasFailed => _updateService.CurrentState == UpdateState.Failed;
    public bool IsBannerVisible => IsUpdateAvailable && _updateService.CurrentState != UpdateState.Deferred;
    public Microsoft.UI.Xaml.Visibility DownloadingVisibility => IsDownloading ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string StatusText => _updateService.CurrentState switch
    {
        UpdateState.UpToDate => "You're up to date",
        UpdateState.Checking => "Checking for updates...",
        UpdateState.UpdateAvailable => $"Sticky Notes {_updateService.GetAvailableVersion()} is available",
        UpdateState.Downloading => $"Downloading update... {DownloadProgressPercentage:F0}%",
        UpdateState.Installing => "Installing update...",
        UpdateState.RestartRequired => "Restart required to complete update",
        UpdateState.Deferred => "Update postponed",
        UpdateState.Failed => StatusErrorMessage ?? "We couldn't check for updates.",
        _ => "Idle"
    };

    public double DownloadProgressPercentage => _updateService.DownloadProgress;
    public string? ReleaseNotes => _updateService.CurrentUpdateInfo?.ReleaseMetadata?.ReleaseNotes;
    public string? StatusErrorMessage => _updateService.CurrentUpdateInfo?.ErrorMessage;

    public ICommand CheckForUpdatesCommand { get; }
    public ICommand UpdateNowCommand { get; }
    public ICommand DownloadUpdateCommand { get; }
    public ICommand LaterCommand { get; }

    public async Task CheckForUpdatesAsync(bool force = false)
    {
        await _updateService.CheckForUpdatesAsync(force);
    }

    public async Task DownloadUpdateAsync()
    {
        await _updateService.DownloadUpdateAsync();
    }

    public async Task UpdateNowAsync()
    {
        // Direct install or download then install
        if (_updateService.CurrentState == UpdateState.UpdateAvailable)
        {
            var downloaded = await _updateService.DownloadUpdateAsync();
            if (downloaded)
            {
                await _updateService.InstallUpdateAsync(restartAfterInstall: true);
            }
        }
        else if (_updateService.CurrentState == UpdateState.RestartRequired)
        {
            await _updateService.InstallUpdateAsync(restartAfterInstall: true);
        }
    }

    public void DeferUpdate()
    {
        _updateService.DeferUpdate("Postponed by user in banner");
        RefreshAllProperties();
    }

    private void OnUpdateStateChanged(object? sender, UpdateStateChangedEventArgs e)
    {
        if (App.CurrentAppSynchronizationContext != null)
        {
            App.CurrentAppSynchronizationContext.Post(_ => RefreshAllProperties(), null);
        }
        else
        {
            RefreshAllProperties();
        }
    }

    private void OnDownloadProgressChanged(object? sender, double progress)
    {
        if (App.CurrentAppSynchronizationContext != null)
        {
            App.CurrentAppSynchronizationContext.Post(_ =>
            {
                OnPropertyChanged(nameof(DownloadProgressPercentage));
                OnPropertyChanged(nameof(StatusText));
            }, null);
        }
        else
        {
            OnPropertyChanged(nameof(DownloadProgressPercentage));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    private void RefreshAllProperties()
    {
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(AvailableVersionText));
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(IsChecking));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsInstalling));
        OnPropertyChanged(nameof(IsRestartRequired));
        OnPropertyChanged(nameof(HasFailed));
        OnPropertyChanged(nameof(IsBannerVisible));
        OnPropertyChanged(nameof(DownloadingVisibility));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DownloadProgressPercentage));
        OnPropertyChanged(nameof(ReleaseNotes));
        OnPropertyChanged(nameof(StatusErrorMessage));
    }

    public void Dispose()
    {
        _updateService.StateChanged -= OnUpdateStateChanged;
        _updateService.DownloadProgressChanged -= OnDownloadProgressChanged;
    }
}

public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private bool _isExecuting;
    public event EventHandler? CanExecuteChanged;

    public AsyncRelayCommand(Func<Task> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public bool CanExecute(object? parameter) => !_isExecuting;

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        try
        {
            _isExecuting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            await _execute();
        }
        finally
        {
            _isExecuting = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
