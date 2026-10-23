using System.Net;

namespace SandboxStudio.Core.Sessions;

/// <summary>
/// Result of <c>wsb ip</c>: the session's reported network addresses.
/// </summary>
/// <param name="Addresses">IPv4 addresses reported for the session; empty on failure.</param>
/// <param name="ErrorMessage">Why the query failed, when it did.</param>
public sealed record WsbIpResult(IReadOnlyList<IPAddress> Addresses, string? ErrorMessage)
{
    /// <summary>Factory for a successful result.</summary>
    public static WsbIpResult Success(IReadOnlyList<IPAddress> addresses) => new(addresses, null);

    /// <summary>Factory for a failed result.</summary>
    public static WsbIpResult Failure(string errorMessage) => new([], errorMessage);
}
