using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using SandboxStudio.Core.Localization;

namespace SandboxStudio.Core.Updates;

/// <summary>
/// Queries the GitHub Releases API for the repository's latest release tag and
/// compares it against the running assembly's informational version.
/// </summary>
public sealed class GitHubUpdateChecker : IUpdateChecker
{
    /// <summary>GitHub "latest release" endpoint for this repository.</summary>
    public static readonly Uri LatestReleaseApiUri =
        new("https://api.github.com/repos/PAumedes/SandboxStudio/releases/latest");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // GitHub's release JSON uses snake_case (tag_name, html_url).
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly Func<Uri, CancellationToken, Task<string>> _httpGet;
    private readonly Func<string?> _currentVersion;

    /// <summary>
    /// Initializes a new instance of <see cref="GitHubUpdateChecker"/>.
    /// </summary>
    /// <param name="httpGet">
    /// Fetches the response body for a GET request; tests inject a stub. The default
    /// implementation issues a GET with a descriptive User-Agent (required by the
    /// GitHub API) and throws <see cref="HttpRequestException"/> on non-success.
    /// </param>
    /// <param name="currentVersion">
    /// Returns the running version string (e.g. <c>"0.1.0+abc123"</c>); the default
    /// reads the entry assembly's informational version.
    /// </param>
    public GitHubUpdateChecker(
        Func<Uri, CancellationToken, Task<string>>? httpGet = null,
        Func<string?>? currentVersion = null)
    {
        _httpGet = httpGet ?? DefaultHttpGet;
        _currentVersion = currentVersion ?? (() =>
            Assembly.GetEntryAssembly()
                ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion);
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        string body;
        try
        {
            body = await _httpGet(LatestReleaseApiUri, cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // The releases/latest endpoint 404s when a repository has no releases.
            return UpdateCheckResult.UpToDate();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return UpdateCheckResult.Unavailable(
                "The update check timed out.", MessageKeys.UpdateCheckFailed, ["request timed out"]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return UpdateCheckResult.Unavailable(
                $"Update check failed: {ex.Message}", MessageKeys.UpdateCheckFailed, [ex.Message]);
        }

        try
        {
            var doc = JsonSerializer.Deserialize<LatestReleaseDto>(body, JsonOptions);
            var latest = ParseVersion(doc?.TagName);
            // Only HTTPS release links are ever surfaced — the feed value is
            // remote input and reaches ShellExecute in the UI.
            var releaseUri = doc?.HtmlUrl is { Length: > 0 } url &&
                Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                uri.Scheme == Uri.UriSchemeHttps
                    ? uri
                    : null;
            var current = ParseVersion(_currentVersion());

            if (latest is null || releaseUri is null || current is null)
            {
                return UpdateCheckResult.Unavailable(
                    "The release feed returned an unexpected response.", MessageKeys.UpdateCheckFailed, ["unexpected response"]);
            }

            return latest > current
                ? UpdateCheckResult.Available(latest, releaseUri, doc?.TagName)
                : UpdateCheckResult.UpToDate();
        }
        catch (JsonException ex)
        {
            return UpdateCheckResult.Unavailable(
                $"Update check failed: {ex.Message}", MessageKeys.UpdateCheckFailed, [ex.Message]);
        }
    }

    /// <summary>
    /// Parses a version from a tag or informational-version string: strips a leading
    /// <c>v</c>/<c>V</c>, <c>+metadata</c>, and <c>-prerelease</c> suffixes.
    /// </summary>
    private static Version? ParseVersion(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim().TrimStart('v', 'V');
        var cut = value.IndexOfAny(['+', '-']);
        if (cut >= 0)
        {
            value = value[..cut];
        }

        return Version.TryParse(value, out var version) ? version : null;
    }

    private static readonly HttpClient SharedClient = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SandboxStudio-UpdateChecker");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }

    private static async Task<string> DefaultHttpGet(Uri uri, CancellationToken cancellationToken) =>
        await SharedClient.GetStringAsync(uri, cancellationToken);

    private sealed record LatestReleaseDto(
        string? TagName = null,
        string? HtmlUrl = null);
}
