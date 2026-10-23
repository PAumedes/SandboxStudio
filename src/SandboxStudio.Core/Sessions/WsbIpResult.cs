using System.Net;

namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of <c>wsb ip</c>: the session's reported network addresses.
/// </summary>
/// <param name="Addresses">IPv4 addresses reported for the session; empty on failure.</param>
/// <param name="ErrorMessage">Why the query failed, when it did.</param>
/// <param name="ErrorKey">Optional <see cref="Localization.MessageKeys"/> entry for localized display.</param>
/// <param name="ErrorArgs">Optional format arguments paired with <paramref name="ErrorKey"/>.</param>
public sealed record WsbIpResult(IReadOnlyList<IPAddress> Addresses, string? ErrorMessage, string? ErrorKey = null, IReadOnlyList<object?>? ErrorArgs = null)
{
    /// <summary>Factory for a successful result.</summary>
    public static WsbIpResult Success(IReadOnlyList<IPAddress> addresses) => new(addresses, null);

    /// <summary>Factory for a failed result.</summary>
    public static WsbIpResult Failure(string errorMessage, string? errorKey = null, IReadOnlyList<object?>? errorArgs = null) => new([], errorMessage, errorKey, errorArgs);
}
