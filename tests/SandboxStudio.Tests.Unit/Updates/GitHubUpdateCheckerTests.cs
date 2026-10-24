using System.Net;
using FluentAssertions;
using SandboxStudio.Core.Localization;
using SandboxStudio.Core.Updates;
using Xunit;

namespace SandboxStudio.Tests.Unit.Updates;

public sealed class GitHubUpdateCheckerTests
{
    private static GitHubUpdateChecker CreateSut(
        string response = """{"tag_name":"v0.2.0","html_url":"https://example.test/releases/v0.2.0"}""",
        string currentVersion = "0.1.0+abc123") =>
        new(
            httpGet: (_, _) => Task.FromResult(response),
            currentVersion: () => currentVersion);

    [Fact]
    public async Task CheckAsync_WhenLatestTagIsNewer_ReturnsAvailable()
    {
        var result = await CreateSut().CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Available);
        result.LatestVersion.Should().Be(new Version(0, 2, 0));
        result.ReleaseUri.Should().Be(new Uri("https://example.test/releases/v0.2.0"));
    }

    [Theory]
    [InlineData("v0.1.0")] // identical
    [InlineData("v0.0.9")] // older
    public async Task CheckAsync_WhenLatestTagIsNotNewer_ReturnsUpToDate(string tag)
    {
        var result = await CreateSut(response: $$"""{"tag_name":"{{tag}}","html_url":"https://example.test/r"}""").CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpToDate);
        result.LatestVersion.Should().BeNull();
    }

    [Fact]
    public async Task CheckAsync_WhenTagHasNoVPrefix_StillParses()
    {
        var result = await CreateSut(response: """{"tag_name":"0.2.0","html_url":"https://example.test/r"}""").CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Available);
    }

    [Fact]
    public async Task CheckAsync_WhenTagIsPrerelease_ComparesNumericPart()
    {
        var result = await CreateSut(response: """{"tag_name":"v0.3.0-beta.1","html_url":"https://example.test/r"}""").CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Available);
        result.LatestVersion.Should().Be(new Version(0, 3, 0));
    }

    [Fact]
    public async Task CheckAsync_WhenServerHasNoReleases_ReturnsUpToDate()
    {
        var sut = new GitHubUpdateChecker(
            httpGet: (_, _) => throw new HttpRequestException("404", null, HttpStatusCode.NotFound),
            currentVersion: () => "0.1.0");

        var result = await sut.CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.UpToDate);
    }

    [Fact]
    public async Task CheckAsync_WhenRequestFails_ReturnsUnavailableWithErrorKey()
    {
        var sut = new GitHubUpdateChecker(
            httpGet: (_, _) => throw new HttpRequestException("boom", null, HttpStatusCode.InternalServerError),
            currentVersion: () => "0.1.0");

        var result = await sut.CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Unavailable);
        result.ErrorKey.Should().Be(MessageKeys.UpdateCheckFailed);
        result.ErrorMessage.Should().Contain("boom");
    }

    [Fact]
    public async Task CheckAsync_WhenResponseIsMalformed_ReturnsUnavailable()
    {
        var result = await CreateSut(response: "not json").CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Unavailable);
        result.ErrorKey.Should().Be(MessageKeys.UpdateCheckFailed);
    }

    [Fact]
    public async Task CheckAsync_WhenFieldsMissing_ReturnsUnavailable()
    {
        var result = await CreateSut(response: """{"tag_name":"v0.2.0"}""").CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Unavailable);
    }

    [Fact]
    public async Task CheckAsync_WhenTagUnparseable_ReturnsUnavailable()
    {
        var result = await CreateSut(response: """{"tag_name":"latest","html_url":"https://example.test/r"}""").CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Unavailable);
    }

    [Fact]
    public async Task CheckAsync_QueriesTheLatestReleaseEndpoint()
    {
        Uri? requested = null;
        var sut = new GitHubUpdateChecker(
            httpGet: (uri, _) =>
            {
                requested = uri;
                return Task.FromResult("""{"tag_name":"v0.1.0","html_url":"https://example.test/r"}""");
            },
            currentVersion: () => "0.1.0");

        await sut.CheckAsync();

        requested.Should().Be(GitHubUpdateChecker.LatestReleaseApiUri);
    }

    [Theory]
    [InlineData("file:///C:/evil.bat")]   // executes files via ShellExecute
    [InlineData("javascript:alert(1)")]   // protocol-handler dispatch
    [InlineData("http://example.test/r")] // silent TLS downgrade
    [InlineData("/relative/path")]        // not absolute
    public async Task CheckAsync_WhenReleaseUrlIsUnsafeOrInvalid_ReturnsUnavailable(string url)
    {
        var result = await CreateSut(
            response: $$"""{"tag_name":"v0.2.0","html_url":"{{url}}"}""").CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Unavailable);
        result.ReleaseUri.Should().BeNull("only HTTPS release links are surfaced");
    }

    [Fact]
    public async Task CheckAsync_WhenFetchThrowsTaskCanceled_ReturnsUnavailable()
    {
        // HttpClient timeouts surface as TaskCanceledException without a user token.
        var sut = new GitHubUpdateChecker(
            httpGet: (_, _) => throw new TaskCanceledException("timed out"),
            currentVersion: () => "0.1.0");

        var result = await sut.CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Unavailable);
        result.ErrorKey.Should().Be(MessageKeys.UpdateCheckFailed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public async Task CheckAsync_WhenCurrentVersionUnparseable_ReturnsUnavailable(string? current)
    {
        var result = await CreateSut(currentVersion: current!).CheckAsync();

        result.Status.Should().Be(UpdateCheckStatus.Unavailable);
    }

    [Fact]
    public async Task CheckAsync_WhenAvailable_CarriesRawTagForDisplay()
    {
        var result = await CreateSut(
            response: """{"tag_name":"v0.3.0-beta.1","html_url":"https://example.test/r"}""").CheckAsync();

        result.LatestTag.Should().Be("v0.3.0-beta.1", "the raw tag preserves prerelease detail the Version drops");
    }
}
