using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using StickyNotes.Core.Services.Logging;

namespace StickyNotes.Core.Services.Lifecycle;

/// <summary>
/// Windows implementation of IStartupService using the HKCU Run registry key.
/// </summary>
public sealed class WindowsStartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppStartupValueName = "StickyNotes";
    private readonly IUpdateLogger? _logger;
    private readonly string? _customExecutablePath;

    public WindowsStartupService(IUpdateLogger? logger = null, string? customExecutablePath = null)
    {
        _logger = logger;
        _customExecutablePath = customExecutablePath;
    }

    [SupportedOSPlatform("windows")]
    public bool IsStartupEnabled()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(AppStartupValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to read autostart status from registry.", ex);
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    public bool SetStartupEnabled(bool enable)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                _logger?.LogError($"Failed to open registry key: {RunKeyPath}");
                return false;
            }

            if (enable)
            {
                var exePath = GetStartupExecutablePath();
                if (string.IsNullOrWhiteSpace(exePath))
                {
                    _logger?.LogError("Could not determine application executable path for startup registration.");
                    return false;
                }

                var formattedCommand = $"\"{exePath}\" --startup";
                key.SetValue(AppStartupValueName, formattedCommand, RegistryValueKind.String);
                _logger?.LogInfo($"Registered Sticky Notes for Windows autostart: {formattedCommand}");
            }
            else
            {
                if (key.GetValue(AppStartupValueName) != null)
                {
                    key.DeleteValue(AppStartupValueName, throwOnMissingValue: false);
                    _logger?.LogInfo("Unregistered Sticky Notes from Windows autostart.");
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to {(enable ? "enable" : "disable")} autostart in registry.", ex);
            return false;
        }
    }

    public string? GetStartupExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_customExecutablePath))
        {
            return _customExecutablePath;
        }

        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                return processPath;
            }

            using var currentProcess = Process.GetCurrentProcess();
            return currentProcess.MainModule?.FileName;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to resolve current process executable path.", ex);
            return null;
        }
    }
}
