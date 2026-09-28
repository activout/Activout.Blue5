namespace Activout.Blue5;

/// <summary>Connection settings for the Bluestone PIM Public API (PAPI).</summary>
public sealed class Blue5Options
{
    /// <summary>
    /// Environment root, e.g. <c>https://api.bluestonepim.com/</c> or <c>https://api.test.bluestonepim.com/</c>.
    /// Blue5 appends the <c>v1/</c> PAPI path itself.
    /// </summary>
    public required Uri BaseAddress { get; init; }

    /// <summary>PAPI key, sent as the <c>x-api-key</c> header. Never logged or echoed.</summary>
    public required string ApiKey { get; init; }

    /// <summary>Default PAPI context (language/publication), sent as the <c>context</c> header.</summary>
    public string Context { get; init; } = "en";
}
