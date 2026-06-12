namespace OpenRetouch.Core.Settings;

/// <summary>
/// アプリ設定(settings.json)の読み込み・保存を行う。
/// </summary>
public interface ISettingsService
{
    /// <summary>現在の設定。LoadAsync前はデフォルト値。</summary>
    AppSettings Current { get; }

    /// <summary>
    /// 設定を読み込む。ファイルが存在しない・破損している場合はデフォルト値で復旧する。
    /// </summary>
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    /// <summary>設定をアトミックに保存する。</summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
}
