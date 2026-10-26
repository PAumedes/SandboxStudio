using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SandboxStudio.Core.Capabilities;
using SandboxStudio.Core.Execution;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Scripts;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Sessions;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Updates;
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
    public void DependencyInjection_RegistersAndResolvesAllCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddSingleton<IWsbSerializer, WsbSerializer>();
        services.AddSingleton<IWsbValidator, WsbValidator>();
        services.AddSingleton<IProfileRepository, JsonProfileRepository>();
        services.AddSingleton<WsbExportService>();
        services.AddSingleton<WsbImportService>();
        services.AddSingleton<ProfileDuplicationService>();
        services.AddSingleton<ILogonScriptService, LogonScriptService>();
        services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        services.AddSingleton<ISandboxLauncher, SandboxLauncher>();
        services.AddSingleton<IHostCapabilities, HostCapabilities>();
        services.AddSingleton<ISandboxSessionManager, WsbSessionManager>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();

        using var provider = services.BuildServiceProvider();

        // Act & Assert
        provider.GetRequiredService<IWsbSerializer>().Should().NotBeNull();
        provider.GetRequiredService<IWsbValidator>().Should().NotBeNull();
        provider.GetRequiredService<IProfileRepository>().Should().NotBeNull();
        provider.GetRequiredService<ISandboxLauncher>().Should().NotBeNull();
        provider.GetRequiredService<IHostCapabilities>().Should().NotBeNull();
        provider.GetRequiredService<WsbExportService>().Should().NotBeNull();
        provider.GetRequiredService<WsbImportService>().Should().NotBeNull();
        provider.GetRequiredService<ProfileDuplicationService>().Should().NotBeNull();
        provider.GetRequiredService<ILogonScriptService>().Should().NotBeNull();
        provider.GetRequiredService<IAppSettingsStore>().Should().NotBeNull();
        provider.GetRequiredService<ISandboxSessionManager>().Should().NotBeNull();
        provider.GetRequiredService<IUpdateChecker>().Should().NotBeNull();
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
