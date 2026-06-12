namespace OpenRetouch.Core.Environment;

/// <summary>
/// アプリのローカルデータ(AppData配下)のパスを一元的に提供する。
/// 元画像はユーザーフォルダに置かれたまま参照されるため、ここには含まれない。
/// </summary>
public interface IAppEnvironment
{
    /// <summary>AppDataルート(例: %AppData%\OpenRetouch)。</summary>
    string RootPath { get; }

    /// <summary>カタログDBファイルのパス(catalog.db)。</summary>
    string CatalogDatabasePath { get; }

    /// <summary>設定ファイルのパス(settings.json)。</summary>
    string SettingsFilePath { get; }

    string ThumbnailsPath { get; }
    string PreviewsPath { get; }
    string MasksPath { get; }
    string PresetsPath { get; }
    string LogsPath { get; }
    string BackupsPath { get; }

    /// <summary>AppData配下のフォルダ構成を作成する(存在する場合は何もしない)。</summary>
    void EnsureDirectories();
}
