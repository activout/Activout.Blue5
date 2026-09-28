using Activout.Blue5.Internal;

namespace Activout.Blue5;

/// <summary>
/// Entry point for the Bluestone PIM Public API. Thread-safe; create one per <see cref="HttpClient"/> and use
/// <see cref="WithContext"/> for other languages/publications.
/// </summary>
public sealed class Blue5Client
{
    private readonly Papi _papi;

    /// <summary>Creates a client. Blue5 does not change <paramref name="httpClient"/>'s settings, so it can be shared.</summary>
    public Blue5Client(HttpClient httpClient, Blue5Options options)
        : this(CreatePapi(httpClient, options))
    {
    }

    private Blue5Client(Papi papi)
    {
        _papi = papi;
        Products = new ProductClient(papi);
    }

    /// <summary>The PAPI context this client reads from.</summary>
    public string Context => _papi.Context;

    /// <summary>Product operations.</summary>
    public ProductClient Products { get; }

    /// <summary>Returns a client for another PAPI context that shares this client's <see cref="HttpClient"/>.</summary>
    public Blue5Client WithContext(string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        return new Blue5Client(_papi.WithContext(context));
    }

    private static Papi CreatePapi(HttpClient httpClient, Blue5Options options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.BaseAddress);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ApiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Context);
        if (!options.BaseAddress.IsAbsoluteUri)
        {
            throw new ArgumentException("BaseAddress must be an absolute URI.", nameof(options));
        }

        var root = options.BaseAddress.AbsoluteUri.EndsWith('/') ? options.BaseAddress : new Uri(options.BaseAddress.AbsoluteUri + "/");
        return new Papi(httpClient, root, options.ApiKey, options.Context);
    }
}
