using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Smoke;

public sealed class SanitySmokeTests : IDisposable
{
    private readonly string _smokeTempDir = Path.Combine(Path.GetTempPath(), "SandboxStudioSmoke", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_smokeTempDir))
        {
            try
            {
                Directory.Delete(_smokeTempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }

    [Fact]
    public void DefaultPresets_AreSyntacticallyValid()
    {
        // Arrange
        var repo = new JsonProfileRepository(_smokeTempDir);
        var validator = new WsbValidator();

        // Act
        var presets = repo.GetDefaultPresets();

        // Assert
        presets.Should().NotBeEmpty();
        foreach (var preset in presets)
        {
            var result = validator.Validate(preset.Configuration);
            result.IsValid.Should().BeTrue($"Preset '{preset.Name}' must be valid according to WsbValidator.");
        }
    }

    [Fact]
    public void DependencyInjection_RegistersAndResolvesAllCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddSingleton<IWsbSerializer, WsbSerializer>();
        services.AddSingleton<IWsbValidator, WsbValidator>();
        services.AddSingleton<IProfileRepository>(_ => new JsonProfileRepository(_smokeTempDir));
        services.AddSingleton<ISandboxLauncher, SandboxLauncher>();

        using var provider = services.BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<IWsbSerializer>().Should().NotBeNull();
        provider.GetRequiredService<IWsbValidator>().Should().NotBeNull();
        provider.GetRequiredService<IProfileRepository>().Should().NotBeNull();
        provider.GetRequiredService<ISandboxLauncher>().Should().NotBeNull();
    }

    [Fact]
    public void WsbConfiguration_DefaultState_IsInstantiableWithoutExceptions()
    {
        // Act
        var config = new WsbConfiguration();

        // Assert
        config.MappedFolders.Should().NotBeNull();
        config.MappedFolders.Should().BeEmpty();
        config.LogonCommand.Should().BeNull();
    }
}
