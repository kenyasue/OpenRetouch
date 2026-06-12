using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using OpenRetouch.Core.Environment;
using OpenRetouch.Core.Settings;
using Xunit;

namespace OpenRetouch.Core.Tests.Settings;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));
    private readonly AppEnvironment _environment;
    private readonly JsonSettingsService _service;

    public JsonSettingsServiceTests()
    {
        _environment = new AppEnvironment(_root);
        _service = new JsonSettingsService(_environment, NullLogger<JsonSettingsService>.Instance);
    }

    [Fact]
    public async Task LoadAsync_FileDoesNotExist_ReturnsDefaults()
    {
        var settings = await _service.LoadAsync();

        settings.CacheLimitGb.Should().Be(20);
        settings.LastViewMode.Should().Be("Library");
        settings.Version.Should().Be(1);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsValues()
    {
        var settings = new AppSettings
        {
            CacheLimitGb = 42,
            LastViewMode = "Settings",
            DefaultImportFolder = @"D:\Photos\Library",
        };

        await _service.SaveAsync(settings);
        var reloaded = await new JsonSettingsService(
            _environment, NullLogger<JsonSettingsService>.Instance).LoadAsync();

        reloaded.CacheLimitGb.Should().Be(42);
        reloaded.LastViewMode.Should().Be("Settings");
        reloaded.DefaultImportFolder.Should().Be(@"D:\Photos\Library");
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_FallsBackToDefaults()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(_environment.SettingsFilePath, "{ not valid json !!");

        var settings = await _service.LoadAsync();

        settings.CacheLimitGb.Should().Be(20);
        settings.LastViewMode.Should().Be("Library");
    }

    [Fact]
    public async Task LoadAsync_UnknownFields_AreIgnored()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(
            _environment.SettingsFilePath,
            """{ "version": 1, "cacheLimitGb": 5, "lastViewMode": "Edit", "unknownFutureField": true }""");

        var settings = await _service.LoadAsync();

        settings.CacheLimitGb.Should().Be(5);
        settings.LastViewMode.Should().Be("Edit");
    }

    [Fact]
    public async Task SaveAsync_UpdatesCurrent()
    {
        var settings = new AppSettings { CacheLimitGb = 7 };

        await _service.SaveAsync(settings);

        _service.Current.CacheLimitGb.Should().Be(7);
    }

    [Fact]
    public async Task SaveAsync_DoesNotLeaveTempFile()
    {
        await _service.SaveAsync(new AppSettings());

        File.Exists(_environment.SettingsFilePath + ".tmp").Should().BeFalse();
        File.Exists(_environment.SettingsFilePath).Should().BeTrue();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
