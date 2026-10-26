using FluentAssertions;
using SandboxStudio.Core.Models;
using SandboxStudio.Core.Serialization;
using SandboxStudio.Core.Storage;
using SandboxStudio.Core.Validation;
using Xunit;

namespace SandboxStudio.Tests.Integration;

public sealed class ProfileLifecycleIntegrationTests : IDisposable
{
    private readonly string _testRoot;
    private readonly JsonProfileRepository _profileRepo;
    private readonly WsbSerializer _serializer = new();
    private readonly WsbValidator _validator = new();

    public ProfileLifecycleIntegrationTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "SandboxStudioIntegration", Guid.NewGuid().ToString("N"));
        _profileRepo = new JsonProfileRepository(
            Path.Combine(_testRoot, "Profiles"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }

    [Fact]
    public async Task FullProfileLifecycle_FromStorageToWsbFile_MaintainsIntegrity()
    {
        // 1. Create Profile
        var profile = new SandboxProfile
        {
            Name = "End-to-End Test Profile",
            Description = "Integration testing profile persistence and WSB generation.",
            Tags = new() { "E2E", "Integration" },
            Configuration = new WsbConfiguration
            {
                Networking = "Disable",
                VGpu = "Enable",
                MemoryInMB = 4096,
                ProtectedClient = "Enable",
                MappedFolders = new()
                {
                    new WsbMappedFolder
                    {
                        HostFolder = @"C:\TestSource",
                        SandboxFolder = @"C:\SandboxTarget",
                        IsReadOnly = true
                    }
                },
                LogonCommand = new WsbLogonCommand
                {
                    Command = "powershell.exe -ExecutionPolicy Bypass -Command Write-Host 'Hello Sandbox'"
                }
            }
        };

        // 2. Persist in Repository
        await _profileRepo.SaveAsync(profile);
        var loadedProfile = await _profileRepo.GetByIdAsync(profile.Id);
        loadedProfile.Should().NotBeNull();

        // 3. Validate
        var validation = _validator.Validate(loadedProfile!.Configuration);
        validation.IsValid.Should().BeTrue();

        // 4. Export to .wsb file on disk
        var wsbPath = Path.Combine(_testRoot, "Output", "TestProfile.wsb");
        await _serializer.SerializeToFileAsync(loadedProfile.Configuration, wsbPath);
        File.Exists(wsbPath).Should().BeTrue();

        // 5. Read back from disk and verify
        var roundtripped = await _serializer.DeserializeFromFileAsync(wsbPath);
        roundtripped.Networking.Should().Be("Disable");
        roundtripped.VGpu.Should().Be("Enable");
        roundtripped.MemoryInMB.Should().Be(4096);
        roundtripped.MappedFolders.Should().HaveCount(1);
        roundtripped.MappedFolders[0].HostFolder.Should().Be(@"C:\TestSource");
        roundtripped.MappedFolders[0].IsReadOnly.Should().BeTrue();
    }
}
