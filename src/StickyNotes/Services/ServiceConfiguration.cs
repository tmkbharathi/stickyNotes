using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using StickyNotes.Core.Services.Lifecycle;
using StickyNotes.Core.Services.Logging;
using StickyNotes.Core.Services.Persistence;
using StickyNotes.Core.Services.Update;
using StickyNotes.ViewModels;

namespace StickyNotes.Services;

/// <summary>
/// Configures dependency injection registrations for the application.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        var storageDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyNotes");
        Directory.CreateDirectory(storageDir);

        // Core Infrastructure & Logging
        services.AddSingleton<IUpdateLogger>(_ => new UpdateLogger(storageDir));
        services.AddSingleton<ISettingsService>(sp => new SettingsService(storageDir, sp.GetRequiredService<IUpdateLogger>()));
        services.AddSingleton<INotePersistenceService>(sp => new JsonNotePersistenceService(storageDir, sp.GetRequiredService<IUpdateLogger>()));
        services.AddSingleton<IWindowStateManager>(sp => new WindowStateManager(storageDir, sp.GetRequiredService<IUpdateLogger>()));
        services.AddSingleton<IAppLifecycleManager>(sp => new AppLifecycleManager(sp.GetRequiredService<IUpdateLogger>()));
        services.AddSingleton<IWindowGeometryService>(sp => new WindowGeometryService(storageDir, sp.GetRequiredService<IUpdateLogger>()));
        services.AddSingleton<IStartupService>(sp => new WindowsStartupService(sp.GetRequiredService<IUpdateLogger>()));

        // HTTP Client Singleton
        services.AddSingleton<HttpClient>(_ =>
        {
            var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StickyNotes-App", "1.0"));
            return client;
        });

        // Deployment & Update Services
        services.AddSingleton<IPackageDeploymentProvider>(sp =>
            new MsixPackageManagerDeploymentProvider(
                sp.GetRequiredService<HttpClient>(),
                sp.GetRequiredService<IUpdateLogger>()));

        services.AddSingleton<IUpdateService>(sp =>
            new UpdateService(
                sp.GetRequiredService<IPackageDeploymentProvider>(),
                sp.GetRequiredService<INotePersistenceService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IWindowStateManager>(),
                sp.GetRequiredService<IAppLifecycleManager>(),
                sp.GetRequiredService<IUpdateLogger>()));

        // ViewModels
        services.AddSingleton<MainHubViewModel>(sp =>
            new MainHubViewModel(
                sp.GetRequiredService<INotePersistenceService>(),
                sp.GetRequiredService<IUpdateService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IWindowGeometryService>()));

        services.AddSingleton<SettingsViewModel>(sp =>
            new SettingsViewModel(
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IUpdateService>(),
                sp.GetRequiredService<IStartupService>()));

        services.AddSingleton<UpdateViewModel>(sp =>
            new UpdateViewModel(sp.GetRequiredService<IUpdateService>()));

        return services.BuildServiceProvider();
    }
}
