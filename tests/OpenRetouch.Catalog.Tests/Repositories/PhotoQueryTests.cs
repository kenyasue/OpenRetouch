using FluentAssertions;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Models;
using Xunit;

namespace OpenRetouch.Catalog.Tests.Repositories;

public sealed class PhotoQueryTests : IDisposable
{
    private readonly TestCatalogDatabase _db = new();
    private readonly PhotoRepository _repository;
    private readonly AlbumRepository _albumRepository;
    private readonly FolderRepository _folderRepository;
    private readonly string _folderId;
    private readonly string _folder2Id;

    public PhotoQueryTests()
    {
        _repository = new PhotoRepository(_db.ConnectionFactory);
        _albumRepository = new AlbumRepository(_db.ConnectionFactory);
        _folderRepository = new FolderRepository(_db.ConnectionFactory);

        _folderId = Guid.NewGuid().ToString();
        _folder2Id = Guid.NewGuid().ToString();
        _folderRepository.InsertAsync(new Folder
        {
            Id = _folderId,
            Path = @"C:\Photos",
            Name = "Photos",
            CreatedAt = DateTimeOffset.UtcNow,
        }).Wait();
        _folderRepository.InsertAsync(new Folder
        {
            Id = _folder2Id,
            Path = @"C:\Other",
            Name = "Other",
            CreatedAt = DateTimeOffset.UtcNow,
        }).Wait();
    }

    [Fact]
    public async Task QueryAsync_MinRating_FiltersBelowThreshold()
    {
        await InsertPhotosAsync(
            CreatePhoto("a.jpg", rating: 1),
            CreatePhoto("b.jpg", rating: 3),
            CreatePhoto("c.jpg", rating: 5));

        var result = await _repository.QueryAsync(new PhotoQuery { MinRating = 3 });

        result.Select(p => p.FileName).Should().BeEquivalentTo(["b.jpg", "c.jpg"]);
    }

    [Fact]
    public async Task QueryAsync_FlagPick_ReturnsOnlyPicked()
    {
        await InsertPhotosAsync(
            CreatePhoto("pick.jpg", flag: PhotoFlag.Pick),
            CreatePhoto("reject.jpg", flag: PhotoFlag.Reject),
            CreatePhoto("none.jpg"));

        var result = await _repository.QueryAsync(new PhotoQuery { Flag = PhotoFlag.Pick });

        result.Should().ContainSingle().Which.FileName.Should().Be("pick.jpg");
    }

    [Fact]
    public async Task QueryAsync_FlagNone_ReturnsUnflaggedOnly()
    {
        await InsertPhotosAsync(
            CreatePhoto("pick.jpg", flag: PhotoFlag.Pick),
            CreatePhoto("none.jpg"));

        var result = await _repository.QueryAsync(new PhotoQuery { Flag = PhotoFlag.None });

        result.Should().ContainSingle().Which.FileName.Should().Be("none.jpg");
    }

    [Fact]
    public async Task QueryAsync_ColorLabel_Filters()
    {
        await InsertPhotosAsync(
            CreatePhoto("red.jpg", label: ColorLabel.Red),
            CreatePhoto("blue.jpg", label: ColorLabel.Blue));

        var result = await _repository.QueryAsync(new PhotoQuery { ColorLabel = ColorLabel.Red });

        result.Should().ContainSingle().Which.FileName.Should().Be("red.jpg");
    }

    [Fact]
    public async Task QueryAsync_Extensions_Filters()
    {
        await InsertPhotosAsync(
            CreatePhoto("a.jpg"),
            CreatePhoto("b.png"),
            CreatePhoto("c.tif"));

        var result = await _repository.QueryAsync(new PhotoQuery { Extensions = [".jpg", ".png"] });

        result.Select(p => p.FileName).Should().BeEquivalentTo(["a.jpg", "b.png"]);
    }

    [Fact]
    public async Task QueryAsync_FolderId_Filters()
    {
        await InsertPhotosAsync(
            CreatePhoto("in.jpg"),
            CreatePhoto("out.jpg", folderId: _folder2Id));

        var result = await _repository.QueryAsync(new PhotoQuery { FolderId = _folderId });

        result.Should().ContainSingle().Which.FileName.Should().Be("in.jpg");
    }

    [Fact]
    public async Task QueryAsync_AlbumId_ReturnsAlbumMembersOnly()
    {
        var inAlbum = CreatePhoto("member.jpg");
        var notInAlbum = CreatePhoto("other.jpg");
        await InsertPhotosAsync(inAlbum, notInAlbum);
        var album = await _albumRepository.InsertAsync("My Album");
        await _albumRepository.AddPhotosAsync(album.Id, [inAlbum.Id]);

        var result = await _repository.QueryAsync(new PhotoQuery { AlbumId = album.Id });

        result.Should().ContainSingle().Which.FileName.Should().Be("member.jpg");
    }

    [Fact]
    public async Task QueryAsync_CombinedFilters_AreAndCombined()
    {
        await InsertPhotosAsync(
            CreatePhoto("match.jpg", rating: 4, flag: PhotoFlag.Pick),
            CreatePhoto("low-rating.jpg", rating: 2, flag: PhotoFlag.Pick),
            CreatePhoto("no-flag.jpg", rating: 5));

        var result = await _repository.QueryAsync(new PhotoQuery
        {
            MinRating = 3,
            Flag = PhotoFlag.Pick,
        });

        result.Should().ContainSingle().Which.FileName.Should().Be("match.jpg");
    }

    [Fact]
    public async Task QueryAsync_SortByFileNameAscending_Orders()
    {
        await InsertPhotosAsync(
            CreatePhoto("charlie.jpg"),
            CreatePhoto("alpha.jpg"),
            CreatePhoto("Bravo.jpg"));

        var result = await _repository.QueryAsync(new PhotoQuery
        {
            SortBy = PhotoSortField.FileName,
            SortDescending = false,
        });

        result.Select(p => p.FileName).Should().ContainInOrder("alpha.jpg", "Bravo.jpg", "charlie.jpg");
    }

    [Fact]
    public async Task QueryAsync_SortByImportedAtDescending_Orders()
    {
        var older = CreatePhoto("older.jpg", importedAt: DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        var newer = CreatePhoto("newer.jpg", importedAt: DateTimeOffset.Parse("2026-06-01T00:00:00Z"));
        await InsertPhotosAsync(older, newer);

        var result = await _repository.QueryAsync(new PhotoQuery
        {
            SortBy = PhotoSortField.ImportedAt,
            SortDescending = true,
        });

        result.Select(p => p.FileName).Should().ContainInOrder("newer.jpg", "older.jpg");
    }

    [Fact]
    public async Task QueryAsync_AlbumAndFolderCombined_AppliesBothConditions()
    {
        var inBoth = CreatePhoto("both.jpg");
        var albumOnly = CreatePhoto("album-other-folder.jpg", folderId: _folder2Id);
        var folderOnly = CreatePhoto("folder-no-album.jpg");
        await InsertPhotosAsync(inBoth, albumOnly, folderOnly);
        var album = await _albumRepository.InsertAsync("Combo");
        await _albumRepository.AddPhotosAsync(album.Id, [inBoth.Id, albumOnly.Id]);

        var result = await _repository.QueryAsync(new PhotoQuery
        {
            AlbumId = album.Id,
            FolderId = _folderId,
        });

        result.Should().ContainSingle().Which.FileName.Should().Be("both.jpg");
    }

    [Fact]
    public async Task QueryAsync_DefaultQuery_ReturnsAll()
    {
        await InsertPhotosAsync(CreatePhoto("a.jpg"), CreatePhoto("b.jpg"));

        var result = await _repository.QueryAsync(new PhotoQuery());

        result.Should().HaveCount(2);
    }

    // ---- RAW+JPGペアのJPG除外 ----

    [Fact]
    public async Task QueryAsync_ExcludeJpegWithRawPair_HidesPairedJpeg()
    {
        await InsertPhotosAsync(
            CreatePhoto("AP5A0001.CR3"),
            CreatePhoto("AP5A0001.JPG"),
            CreatePhoto("solo.jpg"),
            CreatePhoto("raw-only.nef"));

        var result = await _repository.QueryAsync(new PhotoQuery { ExcludeJpegWithRawPair = true });

        result.Select(p => p.FileName).Should().BeEquivalentTo(
            ["AP5A0001.CR3", "solo.jpg", "raw-only.nef"],
            "RAWペアのあるJPGのみ非表示になる");
    }

    [Fact]
    public async Task QueryAsync_ExcludeJpegWithRawPair_IsCaseInsensitiveOnBaseName()
    {
        await InsertPhotosAsync(
            CreatePhoto("img_0001.cr3"),
            CreatePhoto("IMG_0001.JPG"));

        var result = await _repository.QueryAsync(new PhotoQuery { ExcludeJpegWithRawPair = true });

        result.Should().ContainSingle().Which.FileName.Should().Be("img_0001.cr3");
    }

    [Fact]
    public async Task QueryAsync_ExcludeJpegWithRawPair_DifferentFolder_DoesNotHide()
    {
        await InsertPhotosAsync(
            CreatePhoto("AP5A0001.CR3", folderId: _folder2Id),
            CreatePhoto("AP5A0001.JPG"));

        var result = await _repository.QueryAsync(new PhotoQuery { ExcludeJpegWithRawPair = true });

        result.Should().HaveCount(2, "別フォルダの同名RAWはペアとみなさない");
    }

    [Fact]
    public async Task QueryAsync_ExcludeJpegWithRawPair_Disabled_ShowsAll()
    {
        await InsertPhotosAsync(
            CreatePhoto("AP5A0001.CR3"),
            CreatePhoto("AP5A0001.JPG"));

        var result = await _repository.QueryAsync(new PhotoQuery { ExcludeJpegWithRawPair = false });

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task QueryAsync_ExcludeJpegWithRawPair_AlsoHidesJpegExtension()
    {
        await InsertPhotosAsync(
            CreatePhoto("AP5A0001.CR3"),
            CreatePhoto("AP5A0001.jpeg"));

        var result = await _repository.QueryAsync(new PhotoQuery { ExcludeJpegWithRawPair = true });

        result.Should().ContainSingle().Which.FileName.Should().Be("AP5A0001.CR3", ".jpegも除外対象");
    }

    [Fact]
    public async Task QueryAsync_ExcludeJpegWithRawPair_WithFolderFilter_StillHidesPair()
    {
        await InsertPhotosAsync(
            CreatePhoto("AP5A0001.CR3"),
            CreatePhoto("AP5A0001.JPG"),
            CreatePhoto("other.jpg", folderId: _folder2Id));

        var result = await _repository.QueryAsync(new PhotoQuery
        {
            ExcludeJpegWithRawPair = true,
            FolderId = _folderId,
        });

        result.Should().ContainSingle().Which.FileName.Should().Be("AP5A0001.CR3");
    }

    [Fact]
    public async Task QueryAsync_ExcludeJpegWithRawPair_DoesNotHidePngOrTiff()
    {
        await InsertPhotosAsync(
            CreatePhoto("AP5A0001.CR3"),
            CreatePhoto("AP5A0001.png"),
            CreatePhoto("AP5A0001.tif"));

        var result = await _repository.QueryAsync(new PhotoQuery { ExcludeJpegWithRawPair = true });

        result.Should().HaveCount(3, "除外対象はJPEGのみ");
    }

    private Task InsertPhotosAsync(params Photo[] photos) => _repository.InsertBatchAsync(photos);

    private Photo CreatePhoto(
        string fileName,
        int rating = 0,
        PhotoFlag flag = PhotoFlag.None,
        ColorLabel label = ColorLabel.None,
        string? folderId = null,
        DateTimeOffset? importedAt = null) => new()
        {
            Id = Guid.NewGuid().ToString(),
            FolderId = folderId ?? _folderId,
            FilePath = @"C:\Photos\" + Guid.NewGuid().ToString("N") + "_" + fileName,
            FileName = fileName,
            FileExtension = Path.GetExtension(fileName).ToLowerInvariant(),
            ImportedAt = importedAt ?? DateTimeOffset.UtcNow,
            Rating = rating,
            Flag = flag,
            ColorLabel = label,
        };

    public void Dispose() => _db.Dispose();
}
