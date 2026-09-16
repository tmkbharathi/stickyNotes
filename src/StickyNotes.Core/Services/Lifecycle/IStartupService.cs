namespace StickyNotes.Core.Services.Lifecycle;

/// <summary>
/// Service for managing application startup with Windows.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Checks whether Sticky Notes is registered to launch at Windows startup.
    /// </summary>
    bool IsStartupEnabled();

    /// <summary>
    /// Enables or disables Sticky Notes from launching at Windows startup.
    /// </summary>
    /// <param name="enable">True to enable autostart, false to disable.</param>
    /// <returns>True if setting was successfully updated; otherwise false.</returns>
    bool SetStartupEnabled(bool enable);

    /// <summary>
    /// Gets the executable path used for startup registration.
    /// </summary>
    string? GetStartupExecutablePath();
}
