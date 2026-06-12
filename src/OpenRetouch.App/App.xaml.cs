using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenRetouch.App.Services;
using OpenRetouch.App.ViewModels;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Abstractions;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Services;
using OpenRetouch.Core.Settings;
using OpenRetouch.Imaging.Export;
using OpenRetouch.Imaging.Metadata;
using OpenRetouch.Imaging.Raw;
using OpenRetouch.Imaging.Rendering;
using OpenRetouch.Imaging.Thumbnails;
using Serilog;

namespace OpenRetouch.App;

/// <summary>
/// Application entry point and composition root.
/// Startup sequence: initialize logging → build DI → create folders → load settings → initialize DB → show MainWindow.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private Microsoft.UI.Xaml.Window? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;

        var environment = AppEnvironment.CreateDefault();
        environment.EnsureDirectories();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(environment.LogsPath, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)   // Allow all instances to write logs when multiple instances run
            .CreateLogger();

        Services = ConfigureServices(environment);
    }

    /// <summary>The current App instance.</summary>
    public static new App Current => (App)Microsoft.UI.Xaml.Application.Current;

    /// <summary>The DI container. Views resolve their ViewModels from here.</summary>
    public IServiceProvider Services { get; }

    /// <summary>The main window (used for HWND association of FolderPicker etc.).</summary>
    public Microsoft.UI.Xaml.Window? Window => _window;

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Application launching");

        try
        {
            await Services.GetRequiredService<ISettingsService>().LoadAsync();
            await Services.GetRequiredService<ICatalogInitializer>().InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Startup initialization failed");
            Log.CloseAndFlush();
            throw;
        }

        _window = new MainWindow();
        _window.Activate();

        logger.LogInformation("Application launched");

        // Automatically start batch generation for photos missing thumbnails
        // (resumes after the app was closed during a large import; startup continues on failure)
        _ = Services.GetRequiredService<ICatalogService>()
            .EnqueueThumbnailGenerationIfMissingAsync()
            .ContinueWith(
                t => logger.LogError(t.Exception, "Startup thumbnail check failed"),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

        // For development/verification: "--import <folder>" automatically starts a folder import
        var commandLineArgs = System.Environment.GetCommandLineArgs();
        var importIndex = Array.IndexOf(commandLineArgs, "--import");
        if (importIndex >= 0 && importIndex + 1 < commandLineArgs.Length)
        {
            var folder = commandLineArgs[importIndex + 1];
            logger.LogInformation("Auto-import requested: {Folder}", folder);
            Services.GetRequiredService<IImportService>().ImportFolder(folder, recursive: true);
        }

#if DEBUG
        // For development/verification: "--goto-edit" selects the first photo and goes to the Edit screen;
        // "--smoke-exposure <EV>" additionally sets the exposure value automatically (for smoke tests)
        if (commandLineArgs.Contains("--goto-edit"))
        {
            RunGotoEditSmoke(commandLineArgs);
        }

        // For development/verification: "--goto-settings" automatically navigates to the Settings screen (for smoke tests)
        if (commandLineArgs.Contains("--goto-settings"))
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                _window?.DispatcherQueue.TryEnqueue(() =>
                    Services.GetRequiredService<INavigationService>().NavigateTo(ViewMode.Settings));
            });
        }

        // For development/verification: "--smoke-export <folder>" exports all displayed photos with default settings
        var exportIndex = Array.IndexOf(commandLineArgs, "--smoke-export");
        if (exportIndex >= 0 && exportIndex + 1 < commandLineArgs.Length)
        {
            RunExportSmoke(commandLineArgs[exportIndex + 1]);
        }

        // For development/verification: "--cleanup-smoke" removes smoke-test photos (paths containing lps-smoke) from the catalog and exits
        if (commandLineArgs.Contains("--cleanup-smoke"))
        {
            RunSmokeCleanup(logger);
            Exit();
            return;
        }
#endif
    }

#if DEBUG
    private void RunGotoEditSmoke(string[] commandLineArgs)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(4000); // Wait for the Library's initial load and import to complete
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                var library = Services.GetRequiredService<LibraryViewModel>();
                library.SelectedPhoto = library.Photos.FirstOrDefault();
                Services.GetRequiredService<INavigationService>().NavigateTo(ViewMode.Edit);
            });

            var exposureIndex = Array.IndexOf(commandLineArgs, "--smoke-exposure");
            if (exposureIndex >= 0 && exposureIndex + 1 < commandLineArgs.Length
                && double.TryParse(commandLineArgs[exposureIndex + 1], out var ev))
            {
                await Task.Delay(3000);
                _window?.DispatcherQueue.TryEnqueue(() =>
                {
                    Services.GetRequiredService<EditViewModel>().Exposure = ev;
                });
            }
        });
    }

    private void RunSmokeCleanup(ILogger<App> logger)
    {
        var connectionFactory = Services.GetRequiredService<ConnectionFactory>();
        using var connection = connectionFactory.Open();

        // Delete thumbnail files of the target photos
        using (var select = connection.CreateCommand())
        {
            select.CommandText = """
                SELECT tc.thumb_path FROM thumbnail_cache tc
                JOIN photos p ON p.id = tc.photo_id
                WHERE p.file_path LIKE '%lps-smoke%'
                """;
            using var reader = select.ExecuteReader();
            while (reader.Read())
            {
                var thumbPath = reader.GetString(0);
                try
                {
                    File.Delete(thumbPath);
                }
                catch (IOException)
                {
                }
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM thumbnail_cache WHERE photo_id IN (SELECT id FROM photos WHERE file_path LIKE '%lps-smoke%');
            DELETE FROM edits WHERE photo_id IN (SELECT id FROM photos WHERE file_path LIKE '%lps-smoke%');
            DELETE FROM photo_album_map WHERE photo_id IN (SELECT id FROM photos WHERE file_path LIKE '%lps-smoke%');
            DELETE FROM export_job_items WHERE photo_id IN (SELECT id FROM photos WHERE file_path LIKE '%lps-smoke%');
            DELETE FROM export_jobs WHERE id NOT IN (SELECT DISTINCT job_id FROM export_job_items);
            DELETE FROM photos WHERE file_path LIKE '%lps-smoke%';
            DELETE FROM folders WHERE path LIKE '%lps-smoke%' AND id NOT IN (SELECT DISTINCT folder_id FROM photos);
            """;
        var removed = command.ExecuteNonQuery();
        logger.LogInformation("Smoke cleanup removed {Count} rows", removed);
    }

    private void RunExportSmoke(string outputFolder)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(6000); // Wait for import and Library load to complete
            _window?.DispatcherQueue.TryEnqueue(() =>
            {
                var exportVm = Services.GetRequiredService<ExportViewModel>();
                exportVm.OutputFolder = outputFolder;
                Services.GetRequiredService<INavigationService>().NavigateTo(ViewMode.Export);
                exportVm.StartExportCommand.Execute(null);
            });
        });
    }
#endif

    private static ServiceProvider ConfigureServices(IAppEnvironment environment)
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        // Core
        services.AddSingleton(environment);
        services.AddSingleton<ISettingsService, JsonSettingsService>();

        // Catalog
        services.AddSingleton<ConnectionFactory>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<ICatalogInitializer, CatalogInitializer>();
        services.AddSingleton<IPhotoRepository, PhotoRepository>();
        services.AddSingleton<IFolderRepository, FolderRepository>();
        services.AddSingleton<IAlbumRepository, AlbumRepository>();
        services.AddSingleton<IThumbnailCacheRepository, ThumbnailCacheRepository>();
        services.AddSingleton<IEditRepository, EditRepository>();
        services.AddSingleton<IPresetRepository, PresetRepository>();
        services.AddSingleton<IExportJobRepository, ExportJobRepository>();

        // Imaging
        services.AddSingleton<LibRawDecoder>();
        services.AddSingleton<IPhotoMetadataReader, PhotoMetadataReader>();
        services.AddSingleton<IThumbnailGenerator, WicThumbnailGenerator>();
        services.AddSingleton<IPreviewRenderer, PreviewRenderer>();
        services.AddSingleton<IAutoToneService, AutoToneService>();
        services.AddSingleton<IExportPipeline, WicExportPipeline>();

        // Core services
        services.AddSingleton<IJobQueue>(sp =>
            new JobQueue(sp.GetRequiredService<ILogger<JobQueue>>(), maxConcurrency: 2));
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<IImportService, ImportService>();
        services.AddSingleton<IEditService, EditService>();
        services.AddSingleton<IPresetService, PresetService>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddSingleton<IXmpSidecarService, XmpSidecarService>();

        // App services
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<IImportOptionsDialogService, ImportOptionsDialogService>();

        // ViewModels
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<LibraryViewModel>();
        services.AddSingleton<EditViewModel>();
        services.AddSingleton<ExportViewModel>();
        services.AddTransient<SettingsViewModel>();

        return services.BuildServiceProvider();
    }

    private void OnUnhandledException(
        object sender,
        Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Log.Fatal(e.Exception, "Unhandled exception: {Message}", e.Message);
        Log.CloseAndFlush();
        // e.Handled is intentionally not set: after an unhandled exception the state is untrustworthy,
        // so let the process terminate after logging. A crash report dialog is planned for M9.
    }
}
