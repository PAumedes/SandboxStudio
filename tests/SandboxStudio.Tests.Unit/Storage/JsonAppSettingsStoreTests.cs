using FluentAssertions;
using SandboxStudio.Core.Storage;
using Xunit;

namespace SandboxStudio.Tests.Unit.Storage;

public sealed class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;
    private readonly JsonAppSettingsStore _sut;

    public JsonAppSettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        _sut = new JsonAppSettingsStore(_settingsPath);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var settings = _sut.Load();

        settings.Should().BeEquivalentTo(new AppSettings());
        settings.LanguageTag.Should().BeNull();
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsLanguageTag()
    {
        _sut.Save(new AppSettings { LanguageTag = "es-ES" });

        _sut.Load().LanguageTag.Should().Be("es-ES");
    }

    [Fact]
    public void Save_CreatesMissingParentDirectory()
    {
        var nested = Path.Combine(_tempDir, "sub", "dir", "settings.json");
        var store = new JsonAppSettingsStore(nested);

        store.Save(new AppSettings { LanguageTag = "en-US" });

        File.Exists(nested).Should().BeTrue();
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_ReturnsDefaults()
    {
        File.WriteAllText(_settingsPath, "{ not json !!!");

        _sut.Load().Should().BeEquivalentTo(new AppSettings());
    }

    [Fact]
    public void Load_WhenFileHasUnknownFields_IgnoresThem()
    {
        File.WriteAllText(_settingsPath, """{"LanguageTag":"es-ES","FutureField":42}""");

        _sut.Load().LanguageTag.Should().Be("es-ES");
    }

    [Fact]
    public void Save_NullLanguageTag_ClearsOverride()
    {
        _sut.Save(new AppSettings { LanguageTag = "es-ES" });
        _sut.Save(new AppSettings { LanguageTag = null });

        _sut.Load().LanguageTag.Should().BeNull();
    }
}
