namespace StickyNotes.Core.Services.Lifecycle;

/// <summary>
/// Controls application restart, shutdown, and post-update state handover.
/// </summary>
public interface IAppLifecycleManager
{
    bool RestartApplication(string launchArguments = "--after-update");
    void ExitApplication();
    string GetCurrentProcessArchitecture();
    Version GetCurrentPackageVersion();
    string GetCurrentPackageIdentity();
    string GetCurrentPublisher();
}
