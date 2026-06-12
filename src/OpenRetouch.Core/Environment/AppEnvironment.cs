using IOPath = System.IO.Path;

namespace OpenRetouch.Core.Environment;

/// <inheritdoc cref="IAppEnvironment"/>
public sealed class AppEnvironment : IAppEnvironment
{
    public const string AppFolderName = "OpenRetouch";

    public AppEnvironment(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        RootPath = rootPath;
    }

    /// <summary>既定のAppDataルート(%AppData%\OpenRetouch)を使う環境を生成する。</summary>
    public static AppEnvironment CreateDefault()
    {
        var appData = System.Environment.GetFolderPath(
            System.Environment.SpecialFolder.ApplicationData,
            System.Environment.SpecialFolderOption.Create);
        return new AppEnvironment(IOPath.Combine(appData, AppFolderName));
    }

    public string RootPath { get; }

    public string CatalogDatabasePath => IOPath.Combine(RootPath, "catalog.db");

    public string SettingsFilePath => IOPath.Combine(RootPath, "settings.json");

    public string ThumbnailsPath => IOPath.Combine(RootPath, "thumbnails");

    public string PreviewsPath => IOPath.Combine(RootPath, "previews");

    public string MasksPath => IOPath.Combine(RootPath, "masks");

    public string PresetsPath => IOPath.Combine(RootPath, "presets");

    public string LogsPath => IOPath.Combine(RootPath, "logs");

    public string BackupsPath => IOPath.Combine(RootPath, "backups");

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(ThumbnailsPath);
        Directory.CreateDirectory(PreviewsPath);
        Directory.CreateDirectory(MasksPath);
        Directory.CreateDirectory(PresetsPath);
        Directory.CreateDirectory(LogsPath);
        Directory.CreateDirectory(BackupsPath);
    }
}
