using FluentAssertions;
using OpenRetouch.Core.Environment;
using Xunit;

namespace OpenRetouch.Core.Tests.Environment;

public sealed class AppEnvironmentTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "OpenRetouch.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Constructor_EmptyPath_Throws()
    {
        var act = () => new AppEnvironment("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Paths_AreResolvedUnderRoot()
    {
        var env = new AppEnvironment(_root);

        env.RootPath.Should().Be(_root);
        env.CatalogDatabasePath.Should().Be(Path.Combine(_root, "catalog.db"));
        env.SettingsFilePath.Should().Be(Path.Combine(_root, "settings.json"));
        env.ThumbnailsPath.Should().Be(Path.Combine(_root, "thumbnails"));
        env.PreviewsPath.Should().Be(Path.Combine(_root, "previews"));
        env.MasksPath.Should().Be(Path.Combine(_root, "masks"));
        env.PresetsPath.Should().Be(Path.Combine(_root, "presets"));
        env.LogsPath.Should().Be(Path.Combine(_root, "logs"));
        env.BackupsPath.Should().Be(Path.Combine(_root, "backups"));
    }

    [Fact]
    public void EnsureDirectories_CreatesAllFolders()
    {
        var env = new AppEnvironment(_root);

        env.EnsureDirectories();

        Directory.Exists(env.RootPath).Should().BeTrue();
        Directory.Exists(env.ThumbnailsPath).Should().BeTrue();
        Directory.Exists(env.PreviewsPath).Should().BeTrue();
        Directory.Exists(env.MasksPath).Should().BeTrue();
        Directory.Exists(env.PresetsPath).Should().BeTrue();
        Directory.Exists(env.LogsPath).Should().BeTrue();
        Directory.Exists(env.BackupsPath).Should().BeTrue();
    }

    [Fact]
    public void EnsureDirectories_CalledTwice_DoesNotThrow()
    {
        var env = new AppEnvironment(_root);

        env.EnsureDirectories();
        var act = env.EnsureDirectories;

        act.Should().NotThrow();
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
