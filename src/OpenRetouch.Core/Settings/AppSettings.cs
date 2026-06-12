namespace OpenRetouch.Core.Settings;

/// <summary>
/// settings.json に永続化されるアプリ設定。
/// 未知のフィールドは無視し、欠落フィールドはデフォルト値で補完する(前方互換)。
/// </summary>
public sealed class AppSettings
{
    /// <summary>設定スキーマバージョン。</summary>
    public int Version { get; init; } = 1;

    /// <summary>キャッシュ(サムネイル/プレビュー)の上限サイズ(GB)。</summary>
    public int CacheLimitGb { get; set; } = 20;

    /// <summary>最後に表示していた画面モード(再起動時の復元用)。</summary>
    public string LastViewMode { get; set; } = "Library";

    /// <summary>インポート時のデフォルトコピー先フォルダー(未設定は空文字)。</summary>
    public string DefaultImportFolder { get; set; } = "";
}
