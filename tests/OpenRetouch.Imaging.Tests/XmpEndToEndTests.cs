using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenRetouch.Catalog.Database;
using OpenRetouch.Catalog.Repositories;
using OpenRetouch.Core.Abstractions.Imaging;
using OpenRetouch.Core.Editing;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Jobs;
using OpenRetouch.Core.Models;
using OpenRetouch.Core.Services;
using OpenRetouch.Imaging.Metadata;
using OpenRetouch.Imaging.Raw;
using OpenRetouch.Imaging.Thumbnails;
using Xunit;

namespace OpenRetouch.Imaging.Tests;

/// <summary>
/// XMPサイドカーのE2E統合テスト。
/// 「DNG+XMPインポート→現像状態サムネイル」と「編集→XMP生成→RAW不変」を実コンポーネントで検証する。
/// </summary>
public sealed class XmpEndToEndTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _photosDir;
    private readonly AppEnvironment _environment;
    private readonly PhotoRepository _photoRepository;
    private readonly EditRepository _editRepository;
    private readonly ThumbnailCacheRepository _thumbnailCacheRepository;
    private readonly XmpSidecarService _xmpService;
    private readonly ImportService _importService;
    private readonly CatalogService _catalogService;
    private readonly EditService _editService;

    public XmpEndToEndTests()
    {
        _photosDir = Path.Combine(_root, "photos");
        Directory.CreateDirectory(_photosDir);
        _environment = new AppEnvironment(Path.Combine(_root, "appdata"));
        _environment.EnsureDirectories();

        var connectionFactory = new ConnectionFactory(_environment.CatalogDatabasePath);
        using (var connection = connectionFactory.Open())
        {
            new MigrationRunner(NullLogger<MigrationRunner>.Instance).Apply(connection);
        }

        _photoRepository = new PhotoRepository(connectionFactory);
        _editRepository = new EditRepository(connectionFactory);
        _thumbnailCacheRepository = new ThumbnailCacheRepository(connectionFactory);
        _xmpService = new XmpSidecarService(NullLogger<XmpSidecarService>.Instance);

        var rawDecoder = new LibRawDecoder(NullLogger<LibRawDecoder>.Instance);
        var folderRepository = new FolderRepository(connectionFactory);
        var jobQueue = Substitute.For<IJobQueue>();

        _catalogService = new CatalogService(
            _photoRepository,
            folderRepository,
            new AlbumRepository(connectionFactory),
            _thumbnailCacheRepository,
            _editRepository,
            new WicThumbnailGenerator(_environment, rawDecoder, NullLogger<WicThumbnailGenerator>.Instance),
            jobQueue,
            NullLogger<CatalogService>.Instance);

        _importService = new ImportService(
            _photoRepository,
            folderRepository,
            _editRepository,
            new PhotoMetadataReader(NullLogger<PhotoMetadataReader>.Instance),
            _xmpService,
            jobQueue,
            _catalogService,
            NullLogger<ImportService>.Instance);

        _editService = new EditService(
            _editRepository, _photoRepository, _xmpService, NullLogger<EditService>.Instance);
    }

    [Fact]
    public async Task ImportDngWithLightroomSidecar_AppliesDevelopSettingsToThumbnail()
    {
        // DNG2枚(同一内容)+片方にだけ露出+2のLightroom風XMP
        var plainPath = Path.Combine(_photosDir, "plain.dng");
        var editedPath = Path.Combine(_photosDir, "edited.dng");
        TestDngFactory.Create(plainPath, 320, 240);
        TestDngFactory.Create(editedPath, 320, 240);
        await File.WriteAllTextAsync(Path.Combine(_photosDir, "edited.xmp"), """
            <x:xmpmeta xmlns:x="adobe:ns:meta/">
             <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
              <rdf:Description rdf:about=""
                xmlns:xmp="http://ns.adobe.com/xap/1.0/"
                xmlns:crs="http://ns.adobe.com/camera-raw-settings/1.0/"
                xmp:Rating="5"
                xmp:Label="Red"
                crs:Exposure2012="+2.00"/>
             </rdf:RDF>
            </x:xmpmeta>
            """);

        await _importService.RunImportAsync(
            _photosDir, recursive: false, new NoopProgress(), CancellationToken.None);

        // 評価・色ラベル・編集がDBへ取り込まれている
        var photos = await _photoRepository.QueryAsync(new PhotoQuery());
        var edited = photos.Single(p => p.FileName == "edited.dng");
        var plain = photos.Single(p => p.FileName == "plain.dng");
        edited.Rating.Should().Be(5);
        edited.ColorLabel.Should().Be(ColorLabel.Red);
        var storedEdit = await _editRepository.GetCurrentAsync(edited.Id);
        storedEdit.Should().NotBeNull();
        storedEdit!.Basic.Exposure.Should().Be(2.00);
        (await _editRepository.GetCurrentAsync(plain.Id)).Should().BeNull();

        // サムネイルが現像状態(露出+2で明るい)を反映する
        await _catalogService.GenerateMissingThumbnailsAsync(new NoopProgress(), CancellationToken.None);
        var thumbs = await _thumbnailCacheRepository.GetAllThumbPathsAsync();
        var editedLuma = await AverageLumaOfFileAsync(thumbs[edited.Id]);
        var plainLuma = await AverageLumaOfFileAsync(thumbs[plain.Id]);
        editedLuma.Should().BeGreaterThan(plainLuma + 10,
            "XMPの露出+2が適用されたサムネイルは未編集より明るい");
    }

    [Fact]
    public async Task EditRawPhoto_WritesSidecarAndKeepsRawUntouched()
    {
        var rawPath = Path.Combine(_photosDir, "towrite.dng");
        TestDngFactory.Create(rawPath, 64, 64);
        var rawBytes = await File.ReadAllBytesAsync(rawPath);

        await _importService.RunImportAsync(
            _photosDir, recursive: false, new NoopProgress(), CancellationToken.None);
        var photo = (await _photoRepository.QueryAsync(new PhotoQuery()))
            .Single(p => p.FileName == "towrite.dng");

        var settings = new EditSettings();
        settings.Basic.Contrast = 42;
        settings.Crop.Width = 0.8;
        settings.Crop.Height = 0.8;
        await _editService.SaveEditAsync(photo.Id, settings);

        // サイドカーが生成され、内容が読み戻せる
        var sidecarPath = Path.Combine(_photosDir, "towrite.xmp");
        File.Exists(sidecarPath).Should().BeTrue();
        var restored = XmpConverter.FromXmp(await File.ReadAllTextAsync(sidecarPath));
        restored.Settings.Basic.Contrast.Should().Be(42);
        restored.Settings.Crop.Width.Should().BeApproximately(0.8, 1e-6);

        // RAW元ファイルは不変
        (await File.ReadAllBytesAsync(rawPath)).Should().Equal(rawBytes);
    }

    private static async Task<double> AverageLumaOfFileAsync(string path)
    {
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
        using var stream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        var pixels = (await decoder.GetPixelDataAsync()).DetachPixelData();
        double sum = 0;
        for (var i = 0; i < pixels.Length; i += 4)
        {
            sum += 0.114 * pixels[i] + 0.587 * pixels[i + 1] + 0.299 * pixels[i + 2];
        }

        return sum / (pixels.Length / 4);
    }

    private sealed class NoopProgress : IProgress<(int Done, int Total)>
    {
        public void Report((int Done, int Total) value)
        {
        }
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
