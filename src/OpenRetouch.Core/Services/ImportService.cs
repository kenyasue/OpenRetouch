using Microsoft.Extensions.Logging;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;

namespace OpenRetouch.Core.Services;

/// <inheritdoc cref="IImportService"/>
public sealed class ImportService : IImportService
{
    /// <summary>対応する画像拡張子(小文字)。JPEG/PNG/TIFF+RAW形式。</summary>
    public static readonly IReadOnlySet<string> SupportedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".tif", ".tiff" }
            .Concat(RawFileTypes.Extensions)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private const int InsertBatchSize = 500;
    private const int MaxRecursionDepth = 32;

    private readonly IPhotoRepository _photoRepository;
    private readonly IFolderRepository _folderRepository;
    private readonly IEditRepository _editRepository;
    private readonly IPhotoMetadataReader _metadataReader;
    private readonly IXmpSidecarService _xmpSidecarService;
    private readonly IJobQueue _jobQueue;
    private readonly ICatalogService _catalogService;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IPhotoRepository photoRepository,
        IFolderRepository folderRepository,
        IEditRepository editRepository,
        IPhotoMetadataReader metadataReader,
        IXmpSidecarService xmpSidecarService,
        IJobQueue jobQueue,
        ICatalogService catalogService,
        ILogger<ImportService> logger)
    {
        _photoRepository = photoRepository;
        _folderRepository = folderRepository;
        _editRepository = editRepository;
        _metadataReader = metadataReader;
        _xmpSidecarService = xmpSidecarService;
        _jobQueue = jobQueue;
        _catalogService = catalogService;
        _logger = logger;
    }

    public event EventHandler<ImportCompletedEventArgs>? ImportCompleted;

    public string ImportFolder(string folderPath, bool recursive) =>
        Import(new ImportOptions { SourceFolder = folderPath, Recursive = recursive });

    public string Import(ImportOptions options)
    {
        if (options.IsCopyMode && string.IsNullOrWhiteSpace(options.DestinationFolder))
        {
            throw new ArgumentException(
                "DestinationFolder must be specified for copy import.", nameof(options));
        }

        DelegateJob? job = null;
        job = new DelegateJob(
            $"Import: {Path.GetFileName(options.SourceFolder)}",
            (progress, ct) => RunImportAsync(options, progress, ct, job!.Id));
        return _jobQueue.Enqueue(job);
    }

    internal Task RunImportAsync(
        string folderPath,
        bool recursive,
        IProgress<(int Done, int Total)> progress,
        CancellationToken ct,
        string jobId = "") =>
        RunImportAsync(
            new ImportOptions { SourceFolder = folderPath, Recursive = recursive },
            progress, ct, jobId);

    internal async Task RunImportAsync(
        ImportOptions options,
        IProgress<(int Done, int Total)> progress,
        CancellationToken ct,
        string jobId = "")
    {
        // 1. スキャン(登録パスの形式を揃えるため、フォルダパスは正規化して使う)
        var sourceFolder = Path.GetFullPath(options.SourceFolder);
        var files = ScanFiles(sourceFolder, options.Recursive);
        _logger.LogInformation(
            "Import scan found {Count} files in {Folder}", files.Count, sourceFolder);

        // 1.5. コピー系モード: コピー先へコピーし、以降はコピー先パスを取り込み対象にする
        var folderPath = sourceFolder;
        var copyFailedFiles = new List<string>();
        if (options.IsCopyMode)
        {
            var destinationRoot = Path.GetFullPath(options.DestinationFolder!);
            if (PathsEqual(destinationRoot, sourceFolder))
            {
                // コピー先がソースと同一の場合はコピーせずそのまま登録する
                _logger.LogInformation(
                    "Copy destination equals source. Falling back to in-place import: {Folder}",
                    destinationRoot);
            }
            else
            {
                var plan = ImportCopyPlanner.Plan(
                    files, destinationRoot, options.UseDateFolders, ResolveCaptureDate);
                files = CopyFiles(plan, copyFailedFiles, progress, ct);
                folderPath = destinationRoot;
            }
        }

        // 2. 重複除外
        var existingPaths = await _photoRepository.GetExistingFilePathsAsync(ct);
        var newFiles = files.Where(f => !existingPaths.Contains(f)).ToList();
        var skipped = files.Count - newFiles.Count;

        // 3. フォルダ登録
        var folder = await GetOrCreateFolderAsync(folderPath, ct);

        // 4. メタデータ読み込み+Photo生成(+RAWはXMPサイドカー読込)→5. バッチ登録
        var imported = 0;
        var failedFiles = new List<string>(copyFailedFiles);
        var batch = new List<Photo>(InsertBatchSize);
        var sidecarEdits = new List<(string PhotoId, Editing.EditSettings Settings)>();

        foreach (var (filePath, index) in newFiles.Select((f, i) => (f, i)))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var photo = CreatePhoto(filePath, folder.Id);

                // Lightroom互換XMPサイドカー: 現像設定・評価・色ラベルを取り込む
                if (RawFileTypes.IsRaw(photo.FileExtension)
                    && await _xmpSidecarService.TryReadAsync(photo, ct) is { } sidecar)
                {
                    if (sidecar.Rating is { } rating)
                    {
                        photo.Rating = rating;
                    }

                    if (sidecar.ColorLabel is { } label)
                    {
                        photo.ColorLabel = label;
                    }

                    if (!sidecar.Settings.IsDefault)
                    {
                        sidecarEdits.Add((photo.Id, sidecar.Settings));
                    }
                }

                batch.Add(photo);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Failed to read file during import: {Path}", filePath);
                failedFiles.Add(filePath);
            }

            if (batch.Count >= InsertBatchSize || index == newFiles.Count - 1)
            {
                if (batch.Count > 0)
                {
                    // batchは再利用するため、呼び出し先へはスナップショットを渡す
                    await _photoRepository.InsertBatchAsync(batch.ToList(), ct);
                    imported += batch.Count;
                    batch.Clear();
                }

                // サイドカー由来の編集は写真INSERT後に保存(FK制約)。サムネイル生成前に行う
                foreach (var (photoId, settings) in sidecarEdits)
                {
                    await _editRepository.UpsertCurrentAsync(photoId, settings, ct);
                }

                sidecarEdits.Clear();
                progress.Report((index + 1, newFiles.Count));
            }
        }

        // 6. サムネイル生成ジョブ投入
        _catalogService.EnqueueThumbnailGeneration();

        // 7. 完了通知
        _logger.LogInformation(
            "Import completed: imported={Imported}, skipped={Skipped}, failed={Failed}",
            imported, skipped, failedFiles.Count);
        ImportCompleted?.Invoke(
            this,
            new ImportCompletedEventArgs(jobId, imported, skipped, failedFiles));
    }

    /// <summary>
    /// コピー計画を実行し、取り込み対象となるコピー先パスの一覧を返す。
    /// コピー先に同名ファイルが既にある場合は二重コピーせず、既存ファイルを取り込み対象とする。
    /// 失敗したファイルはfailedFilesへ記録して続行する。コピー元は変更しない。
    /// </summary>
    private List<string> CopyFiles(
        IReadOnlyList<ImportCopyItem> plan,
        List<string> failedFiles,
        IProgress<(int Done, int Total)> progress,
        CancellationToken ct)
    {
        var copiedPaths = new List<string>(plan.Count);
        var done = 0;
        foreach (var item in plan)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(item.DestinationPath)!);
                if (!File.Exists(item.DestinationPath))
                {
                    File.Copy(item.SourcePath, item.DestinationPath);
                }

                copiedPaths.Add(item.DestinationPath);

                // サイドカーのコピー失敗で本体の取り込みを失敗扱いにしない
                // (本体は既にコピー済みのため、登録しないと孤立ファイルになる)
                if (item.SidecarSourcePath is not null
                    && item.SidecarDestinationPath is not null
                    && !File.Exists(item.SidecarDestinationPath))
                {
                    try
                    {
                        File.Copy(item.SidecarSourcePath, item.SidecarDestinationPath);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _logger.LogWarning(
                            ex, "Failed to copy XMP sidecar: {Source}", item.SidecarSourcePath);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(
                    ex, "Failed to copy file during import: {Source} -> {Destination}",
                    item.SourcePath, item.DestinationPath);
                failedFiles.Add(item.SourcePath);
            }

            done++;
            progress.Report((done, plan.Count));
        }

        return copiedPaths;
    }

    /// <summary>日付フォルダ振り分け用の日付(撮影日時、なければファイル更新日時)を解決する。</summary>
    private DateTimeOffset ResolveCaptureDate(string filePath)
    {
        try
        {
            return _metadataReader.Read(filePath).CapturedAt ?? File.GetLastWriteTimeUtc(filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return File.GetLastWriteTimeUtc(filePath);
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(
            a.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    internal static List<string> ScanFiles(string folderPath, bool recursive)
    {
        var results = new List<string>();
        Scan(folderPath, recursive ? MaxRecursionDepth : 0, results);
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;

        static void Scan(string path, int remainingDepth, List<string> results)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(path))
                {
                    if (SupportedExtensions.Contains(Path.GetExtension(file)))
                    {
                        results.Add(file);
                    }
                }

                if (remainingDepth > 0)
                {
                    foreach (var dir in Directory.EnumerateDirectories(path))
                    {
                        Scan(dir, remainingDepth - 1, results);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // アクセス不可ディレクトリはスキップ
            }
        }
    }

    private Photo CreatePhoto(string filePath, string folderId)
    {
        var fileInfo = new FileInfo(filePath);
        var metadata = _metadataReader.Read(filePath);

        return new Photo
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = folderId,
            FilePath = filePath,
            FileName = fileInfo.Name,
            FileExtension = fileInfo.Extension.ToLowerInvariant(),
            FileSize = fileInfo.Length,
            ImportedAt = DateTimeOffset.UtcNow,
            CapturedAt = metadata.CapturedAt ?? fileInfo.LastWriteTimeUtc,
            Width = metadata.Width,
            Height = metadata.Height,
            Orientation = metadata.Orientation,
            Exif = metadata.Exif,
        };
    }

    private async Task<Folder> GetOrCreateFolderAsync(string folderPath, CancellationToken ct)
    {
        var existing = await _folderRepository.GetByPathAsync(folderPath, ct);
        if (existing is not null)
        {
            return existing;
        }

        var folder = new Folder
        {
            Id = Guid.NewGuid().ToString(),
            Path = folderPath,
            Name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar)),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await _folderRepository.InsertAsync(folder, ct);
        return folder;
    }
}
