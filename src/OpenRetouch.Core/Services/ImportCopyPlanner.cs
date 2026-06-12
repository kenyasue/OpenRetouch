using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <summary>コピーインポートの1ファイル分の計画。</summary>
/// <param name="SourcePath">コピー元の画像ファイル。</param>
/// <param name="DestinationPath">コピー先の画像ファイル。</param>
/// <param name="SidecarSourcePath">同名XMPサイドカーのコピー元(存在しない場合null)。</param>
/// <param name="SidecarDestinationPath">同名XMPサイドカーのコピー先(存在しない場合null)。</param>
public sealed record ImportCopyItem(
    string SourcePath,
    string DestinationPath,
    string? SidecarSourcePath,
    string? SidecarDestinationPath);

/// <summary>
/// コピーインポートの宛先パスを決定する純粋ロジック。
/// 日付フォルダー有効時は「コピー先ルート/YYYY/MM/DD/ファイル名」の階層を作る。
/// </summary>
public static class ImportCopyPlanner
{
    /// <summary>
    /// 各ソースファイルのコピー先を決定する。
    /// RAWファイルは同名の.xmpサイドカーが存在すれば一緒に計画へ含める。
    /// </summary>
    /// <param name="sourceFiles">コピー元の画像ファイル一覧。</param>
    /// <param name="destinationRoot">コピー先ルートフォルダー。</param>
    /// <param name="useDateFolders">YYYY/MM/DDの日付フォルダーへ振り分けるか。</param>
    /// <param name="dateResolver">ファイルパス→日付(撮影日時、なければファイル更新日時)の解決。</param>
    /// <param name="sidecarExists">サイドカーパスの存在判定(テスト差し替え用。省略時はFile.Exists)。</param>
    public static IReadOnlyList<ImportCopyItem> Plan(
        IReadOnlyList<string> sourceFiles,
        string destinationRoot,
        bool useDateFolders,
        Func<string, DateTimeOffset> dateResolver,
        Func<string, bool>? sidecarExists = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationRoot);
        sidecarExists ??= File.Exists;

        var items = new List<ImportCopyItem>(sourceFiles.Count);
        foreach (var sourcePath in sourceFiles)
        {
            var directory = useDateFolders
                ? Path.Combine(destinationRoot, FormatDateFolder(dateResolver(sourcePath)))
                : destinationRoot;
            var destinationPath = Path.Combine(directory, Path.GetFileName(sourcePath));

            string? sidecarSource = null;
            string? sidecarDestination = null;
            if (RawFileTypes.IsRaw(Path.GetExtension(sourcePath)))
            {
                var candidate = Path.ChangeExtension(sourcePath, ".xmp");
                if (sidecarExists(candidate))
                {
                    sidecarSource = candidate;
                    sidecarDestination = Path.ChangeExtension(destinationPath, ".xmp");
                }
            }

            items.Add(new ImportCopyItem(sourcePath, destinationPath, sidecarSource, sidecarDestination));
        }

        return items;
    }

    /// <summary>日付をYYYY/MM/DDの3階層の相対パスに変換する(ローカル時刻基準)。</summary>
    internal static string FormatDateFolder(DateTimeOffset date)
    {
        var local = date.ToLocalTime();
        return Path.Combine(
            local.Year.ToString("D4"),
            local.Month.ToString("D2"),
            local.Day.ToString("D2"));
    }
}
