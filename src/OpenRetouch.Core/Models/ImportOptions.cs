namespace OpenRetouch.Core.Models;

/// <summary>インポート時の取り込み方法。</summary>
public enum ImportMode
{
    /// <summary>開いたフォルダーをそのまま登録する(コピーなし・従来動作)。</summary>
    RegisterInPlace,

    /// <summary>設定済みのデフォルトフォルダーへコピーして取り込む。</summary>
    CopyToDefaultFolder,

    /// <summary>その場で指定したフォルダーへコピーして取り込む。</summary>
    CopyToCustomFolder,
}

/// <summary>インポート実行時のオプション。</summary>
public sealed class ImportOptions
{
    /// <summary>取り込み元フォルダー(SDカード等)。</summary>
    public required string SourceFolder { get; init; }

    /// <summary>取り込み方法。</summary>
    public ImportMode Mode { get; init; } = ImportMode.RegisterInPlace;

    /// <summary>
    /// コピー先ルートフォルダー。Copy系モードでは必須
    /// (CopyToDefaultFolderでも呼び出し側が設定値を解決して渡す)。
    /// </summary>
    public string? DestinationFolder { get; init; }

    /// <summary>コピー時にYYYY/MM/DDの日付フォルダー階層へ振り分けるか。</summary>
    public bool UseDateFolders { get; init; } = true;

    /// <summary>サブフォルダーも取り込み対象にするか。</summary>
    public bool Recursive { get; init; } = true;

    /// <summary>コピーを伴うモードか。</summary>
    public bool IsCopyMode => Mode is ImportMode.CopyToDefaultFolder or ImportMode.CopyToCustomFolder;
}
