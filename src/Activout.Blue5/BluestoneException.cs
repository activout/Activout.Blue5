using System.Net;

namespace Activout.Blue5;

/// <summary>
/// A non-success PAPI response, including HTTP 429 Too Many Requests. Activout.Blue5 never retries;
/// add Activout.Blue5.Resilience (or your own policy) for that.
/// </summary>
public sealed class BluestoneException : Exception
{
    /// <summary>Creates the exception.</summary>
    public BluestoneException(
        HttpStatusCode statusCode,
        string message,
        string? papiMessage = null,
        string? entityId = null,
        IReadOnlyList<string>? entityIds = null,
        string? requestId = null)
        : base(message)
    {
        StatusCode = statusCode;
        PapiMessage = papiMessage;
        EntityId = entityId;
        EntityIds = entityIds ?? [];
        RequestId = requestId;
    }

    /// <summary>HTTP status returned by PAPI.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>The <c>message</c> from the PAPI error body, if any.</summary>
    public string? PapiMessage { get; }

    /// <summary>The <c>entityId</c> from the PAPI error body, e.g. the missing product id or unknown context.</summary>
    public string? EntityId { get; }

    /// <summary>The <c>entityIds</c> from the PAPI error body; empty when not supplied.</summary>
    public IReadOnlyList<string> EntityIds { get; }

    /// <summary>The <c>x-request-tracker</c> response header, useful when contacting Bluestone support.</summary>
    public string? RequestId { get; }

    /// <summary><see langword="true"/> for HTTP 429 Too Many Requests.</summary>
    public bool IsRateLimited => StatusCode == HttpStatusCode.TooManyRequests;
}
