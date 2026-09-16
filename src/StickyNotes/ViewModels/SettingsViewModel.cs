using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StickyNotes.Core.Models.Update;
using StickyNotes.Core.Services.Lifecycle;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Core.Services.Update;

namespace StickyNotes.ViewModels;

/// <summary>
/// ViewModel for Settings View, binding Update channel, auto-update switches,
/// autostart with Windows toggle, last checked timestamp, and release notes.
/// </summary>
public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly ISettingsService _settingsService;
    private readonly IUpdateService _updateService;
    private readonly IStartupService _startupService;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public SettingsViewModel(
        ISettingsService settingsService,
        IUpdateService updateService,
        IStartupService? startupService = null)
    {
        _settingsService = settingsService;
        _updateService = updateService;
        _startupService = startupService ?? new WindowsStartupService();

        _updateService.StateChanged += (_, _) =>
        {
            if (App.CurrentAppSynchronizationContext != null)
            {
                App.CurrentAppSynchronizationContext.Post(_ =>
                {
                    OnPropertyChanged(nameof(StatusText));
                    OnPropertyChanged(nameof(FormattedLastCheckedText));
                }, null);
            }
            else
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(FormattedLastCheckedText));
            }
        };
        CheckForUpdatesCommand = new AsyncRelayCommand(async () => await _updateService.CheckForUpdatesAsync(force: true));
    }

    public string CurrentVersion => _updateService.GetCurrentVersion().ToString();
    public string FormattedVersion => $"Version {CurrentVersion}";
    public string StatusText => _updateService.CurrentState switch
    {
        UpdateState.UpToDate => "You're up to date",
        UpdateState.Checking => "Checking for updates...",
        UpdateState.UpdateAvailable => $"Version {_updateService.GetAvailableVersion()} is available",
        UpdateState.Downloading => $"Downloading... {_updateService.DownloadProgress:F0}%",
        UpdateState.RestartRequired => "Restart required",
        UpdateState.Failed => "Update check failed",
        _ => "Up to date"
    };

    public string LastCheckedText
    {
        get
        {
            var last = _settingsService.GetUpdateSettings().LastCheckTimestamp;
            return last.HasValue ? last.Value.ToLocalTime().ToString("MMM dd, yyyy h:mm tt") : "Never";
        }
    }
    public string FormattedLastCheckedText => $"Last checked: {LastCheckedText}";

    public bool StartWithWindows
    {
        get => _startupService.IsStartupEnabled();
        set
        {
            if (_startupService.IsStartupEnabled() != value)
            {
                _startupService.SetStartupEnabled(value);
                OnPropertyChanged();
            }
        }
    }

    public bool AutoCheckUpdates
    {
        get => _settingsService.GetUpdateSettings().AutoCheckUpdates;
        set
        {
            var s = _settingsService.GetUpdateSettings();
            s.AutoCheckUpdates = value;
            _ = _settingsService.SaveUpdateSettingsAsync(s);
            OnPropertyChanged();
        }
    }

    public bool AutoDownloadUpdates
    {
        get => _settingsService.GetUpdateSettings().AutoDownloadUpdates;
        set
        {
            var s = _settingsService.GetUpdateSettings();
            s.AutoDownloadUpdates = value;
            _ = _settingsService.SaveUpdateSettingsAsync(s);
            OnPropertyChanged();
        }
    }

    public bool AutoInstallWhenSafe
    {
        get => _settingsService.GetUpdateSettings().AutoInstallWhenSafe;
        set
        {
            var s = _settingsService.GetUpdateSettings();
            s.AutoInstallWhenSafe = value;
            _ = _settingsService.SaveUpdateSettingsAsync(s);
            OnPropertyChanged();
        }
    }

    public UpdateChannel Channel
    {
        get => _settingsService.GetUpdateSettings().Channel;
        set
        {
            _ = _settingsService.SetUpdateChannelAsync(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedChannelIndex));
        }
    }

    public int SelectedChannelIndex
    {
        get
        {
            for (int i = 0; i < AvailableChannels.Count; i++)
            {
                if (AvailableChannels[i] == Channel) return i;
            }
            return 0;
        }
        set
        {
            if (value >= 0 && value < AvailableChannels.Count)
            {
                Channel = AvailableChannels[value];
            }
        }
    }

    public IReadOnlyList<UpdateChannel> AvailableChannels { get; } = new[]
    {
        UpdateChannel.Stable,
        UpdateChannel.Beta,
        UpdateChannel.Development
    };

    public ICommand CheckForUpdatesCommand { get; }
}
