namespace OpenRetouch.Core.Services;

/// <summary>インポート完了の結果サマリ。</summary>
public sealed record ImportCompletedEventArgs(
    string JobId,
    int Imported,
    int Skipped,
    IReadOnlyList<string> FailedFiles);

/// <summary>写真のインポート(フォルダスキャン→(コピー)→カタログ登録→サムネイル生成ジョブ投入)。</summary>
public interface IImportService
{
    /// <summary>
    /// フォルダのインポートをバックグラウンドジョブとして開始し、ジョブIDを返す(コピーなし・そのまま登録)。
    /// </summary>
    string ImportFolder(string folderPath, bool recursive);

    /// <summary>
    /// オプション指定のインポートをバックグラウンドジョブとして開始し、ジョブIDを返す。
    /// コピー系モードではソースをコピー先(日付フォルダ任意)へコピーし、コピー先のパスで登録する。
    /// </summary>
    /// <exception cref="ArgumentException">コピー系モードでDestinationFolderが未指定。</exception>
    string Import(Models.ImportOptions options);

    /// <summary>インポート完了時に発火(ワーカースレッドから)。</summary>
    event EventHandler<ImportCompletedEventArgs>? ImportCompleted;
}
