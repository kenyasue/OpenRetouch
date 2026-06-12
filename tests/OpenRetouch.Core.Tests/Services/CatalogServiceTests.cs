using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Abstractions.Repositories;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using Xunit;

namespace OpenRetouch.Core.Tests.Services;

public sealed class CatalogServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    private readonly IPhotoRepository _photoRepository = Substitute.For<IPhotoRepository>();
    private readonly IThumbnailCacheRepository _thumbnailCacheRepository =
        Substitute.For<IThumbnailCacheRepository>();
    private readonly IThumbnailGenerator _thumbnailGenerator = Substitute.For<IThumbnailGenerator>();
    private readonly IEditRepository _editRepository = Substitute.For<IEditRepository>();
    private readonly IFolderRepository _folderRepository = Substitute.For<IFolderRepository>();
    private readonly IJobQueue _jobQueue = Substitute.For<IJobQueue>();
    private readonly CatalogService _service;

    public CatalogServiceTests()
    {
        Directory.CreateDirectory(_root);
        _thumbnailCacheRepository.GetAllThumbPathsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string>());

        _editRepository.GetCurrentAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((OpenRetouch.Core.Editing.EditSettings?)null);
        _service = new CatalogService(
            _photoRepository,
            _folderRepository,
            Substitute.For<IAlbumRepository>(),
            _thumbnailCacheRepository,
            _editRepository,
            _thumbnailGenerator,
            _jobQueue,
            NullLogger<CatalogService>.Instance);
    }

    [Fact]
    public async Task SetRatingAsync_OutOfRange_Throws()
    {
        var act = () => _service.SetRatingAsync(["p1"], 6);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();

        var actNegative = () => _service.SetRatingAsync(["p1"], -1);
        await actNegative.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SetRatingAsync_ValidRating_DelegatesToRepository()
    {
        await _service.SetRatingAsync(["p1", "p2"], 4);

        await _photoRepository.Received(1).UpdateRatingAsync(
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 2), 4, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetFlagAndColorLabel_DelegateToRepository()
    {
        await _service.SetFlagAsync(["p1"], PhotoFlag.Pick);
        await _service.SetColorLabelAsync(["p1"], ColorLabel.Blue);

        await _photoRepository.Received(1).UpdateFlagAsync(
            Arg.Any<IReadOnlyList<string>>(), PhotoFlag.Pick, Arg.Any<CancellationToken>());
        await _photoRepository.Received(1).UpdateColorLabelAsync(
            Arg.Any<IReadOnlyList<string>>(), ColorLabel.Blue, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateMissingThumbnails_AllCached_GeneratesNothing()
    {
        var photo = CreatePhotoWithSourceFile("p1");
        var cachedThumb = CreateFile("thumb-p1.jpg");
        _photoRepository.QueryAsync(Arg.Any<PhotoQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Photo> { photo });
        _thumbnailCacheRepository.GetAllThumbPathsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { [photo.Id] = cachedThumb });

        await _service.GenerateMissingThumbnailsAsync(new NoopProgress(), CancellationToken.None);

        await _thumbnailGenerator.DidNotReceive().GenerateAsync(Arg.Any<Photo>(), Arg.Any<OpenRetouch.Core.Editing.EditSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateMissingThumbnails_CachedFileMissing_Regenerates()
    {
        var photo = CreatePhotoWithSourceFile("p1");
        _photoRepository.QueryAsync(Arg.Any<PhotoQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Photo> { photo });
        _thumbnailCacheRepository.GetAllThumbPathsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { [photo.Id] = Path.Combine(_root, "deleted.jpg") });
        _thumbnailGenerator.GenerateAsync(photo, Arg.Any<OpenRetouch.Core.Editing.EditSettings>(), Arg.Any<CancellationToken>())
            .Returns(Path.Combine(_root, "new-thumb.jpg"));

        await _service.GenerateMissingThumbnailsAsync(new NoopProgress(), CancellationToken.None);

        await _thumbnailGenerator.Received(1).GenerateAsync(photo, Arg.Any<OpenRetouch.Core.Editing.EditSettings>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateMissingThumbnails_OneFails_ContinuesWithRemaining()
    {
        var photo1 = CreatePhotoWithSourceFile("p1");
        var photo2 = CreatePhotoWithSourceFile("p2");
        _photoRepository.QueryAsync(Arg.Any<PhotoQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Photo> { photo1, photo2 });
        _thumbnailGenerator.GenerateAsync(photo1, Arg.Any<OpenRetouch.Core.Editing.EditSettings>(), Arg.Any<CancellationToken>())
            .Returns<Task<string>>(_ => throw new InvalidOperationException("decode failed"));
        _thumbnailGenerator.GenerateAsync(photo2, Arg.Any<OpenRetouch.Core.Editing.EditSettings>(), Arg.Any<CancellationToken>())
            .Returns(Path.Combine(_root, "thumb-p2.jpg"));

        var readyEvents = new List<ThumbnailReadyEventArgs>();
        _service.ThumbnailReady += (_, e) => readyEvents.Add(e);

        await _service.GenerateMissingThumbnailsAsync(new NoopProgress(), CancellationToken.None);

        readyEvents.Should().ContainSingle().Which.PhotoId.Should().Be(photo2.Id);
        await _thumbnailCacheRepository.Received(1).UpsertAsync(
            photo2.Id, Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateMissingThumbnails_RaisesThumbnailReadyPerPhoto()
    {
        var photo = CreatePhotoWithSourceFile("p1");
        var thumbPath = Path.Combine(_root, "thumb-p1.jpg");
        _photoRepository.QueryAsync(Arg.Any<PhotoQuery>(), Arg.Any<CancellationToken>())
            .Returns(new List<Photo> { photo });
        _thumbnailGenerator.GenerateAsync(photo, Arg.Any<OpenRetouch.Core.Editing.EditSettings>(), Arg.Any<CancellationToken>()).Returns(thumbPath);

        var readyEvents = new List<ThumbnailReadyEventArgs>();
        _service.ThumbnailReady += (_, e) => readyEvents.Add(e);

        await _service.GenerateMissingThumbnailsAsync(new NoopProgress(), CancellationToken.None);

        readyEvents.Should().ContainSingle();
        readyEvents[0].PhotoId.Should().Be(photo.Id);
        readyEvents[0].ThumbnailPath.Should().Be(thumbPath);
    }

    [Fact]
    public async Task RefreshThumbnailsAsync_DeletesOldFileAndClearsCache()
    {
        var oldThumb = CreateFile("old-thumb.jpg");
        _thumbnailCacheRepository.GetAllThumbPathsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["p1"] = oldThumb });

        await _service.RefreshThumbnailsAsync(["p1"]);

        File.Exists(oldThumb).Should().BeFalse("旧サムネイルファイルが削除される");
        await _thumbnailCacheRepository.Received(1).RemoveAsync(
            Arg.Is<IReadOnlyList<string>>(ids => ids.Contains("p1")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshThumbnailsAsync_EmptyList_DoesNothing()
    {
        await _service.RefreshThumbnailsAsync([]);

        await _thumbnailCacheRepository.DidNotReceive().RemoveAsync(
            Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueThumbnailGenerationIfMissing_MissingThumbnails_EnqueuesJob()
    {
        _thumbnailCacheRepository.CountPhotosWithoutThumbnailAsync(Arg.Any<CancellationToken>())
            .Returns(3L);

        await _service.EnqueueThumbnailGenerationIfMissingAsync();

        _jobQueue.Received(1).Enqueue(Arg.Any<IJob>());
        // SQLカウントで判定できた場合は全パス取得を行わない
        await _thumbnailCacheRepository.DidNotReceive().GetAllThumbPathsAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnqueueThumbnailGenerationIfMissing_AllCached_DoesNotEnqueue()
    {
        var cachedThumb = CreateFile("thumb-p1.jpg");
        _thumbnailCacheRepository.CountPhotosWithoutThumbnailAsync(Arg.Any<CancellationToken>())
            .Returns(0L);
        _thumbnailCacheRepository.GetAllThumbPathsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["p1"] = cachedThumb });

        await _service.EnqueueThumbnailGenerationIfMissingAsync();

        _jobQueue.DidNotReceive().Enqueue(Arg.Any<IJob>());
    }

    [Fact]
    public async Task EnqueueThumbnailGenerationIfMissing_CachedFileDeleted_EnqueuesJob()
    {
        _thumbnailCacheRepository.CountPhotosWithoutThumbnailAsync(Arg.Any<CancellationToken>())
            .Returns(0L);
        _thumbnailCacheRepository.GetAllThumbPathsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["p1"] = Path.Combine(_root, "deleted.jpg") });

        await _service.EnqueueThumbnailGenerationIfMissingAsync();

        _jobQueue.Received(1).Enqueue(Arg.Any<IJob>());
    }

    [Fact]
    public async Task RemoveFolderFromCatalogAsync_DeletesRowsAndThumbnailFiles()
    {
        var thumb = CreateFile("thumb-f1.jpg");
        _folderRepository.DeleteCascadeAsync("f1", Arg.Any<CancellationToken>())
            .Returns(new List<string> { thumb });

        await _service.RemoveFolderFromCatalogAsync("f1");

        await _folderRepository.Received(1).DeleteCascadeAsync("f1", Arg.Any<CancellationToken>());
        File.Exists(thumb).Should().BeFalse("カタログ解除時にサムネイルファイルも削除される");
    }

    [Fact]
    public async Task ClearThumbnailCacheAsync_RemovesRowsAndFilesThenEnqueuesRegeneration()
    {
        var thumb = CreateFile("thumb-p1.jpg");
        _thumbnailCacheRepository.GetAllThumbPathsAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, string> { ["p1"] = thumb });

        await _service.ClearThumbnailCacheAsync();

        await _thumbnailCacheRepository.Received(1).RemoveAllAsync(Arg.Any<CancellationToken>());
        File.Exists(thumb).Should().BeFalse("キャッシュクリアでファイルも削除される");
        _jobQueue.Received(1).Enqueue(Arg.Any<IJob>());
    }

    private Photo CreatePhotoWithSourceFile(string id)
    {
        var sourcePath = CreateFile(id + ".jpg");
        return new Photo
        {
            Id = id,
            FolderId = "f1",
            FilePath = sourcePath,
            FileName = id + ".jpg",
            FileExtension = ".jpg",
            ImportedAt = DateTimeOffset.UtcNow,
        };
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "x");
        return path;
    }

    private sealed class NoopProgress : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value)
        {
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
