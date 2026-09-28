namespace Activout.Blue5;

/// <summary>
/// Filters for <see cref="ProductClient.Find"/> and <see cref="ProductClient.FindAll"/>. Unset filters are not sent;
/// set filters are combined by PAPI.
/// </summary>
public sealed record ProductQuery
{
    /// <summary>Matches everything.</summary>
    public static readonly ProductQuery All = new();

    /// <summary>Product numbers the products must equal.</summary>
    public IReadOnlyList<string>? Numbers { get; init; }

    /// <summary>Strings the product name must contain.</summary>
    public IReadOnlyList<string>? Names { get; init; }

    /// <summary>Labels the products must have.</summary>
    public IReadOnlyList<string>? Labels { get; init; }

    /// <summary>Category ids the products must be in.</summary>
    public IReadOnlyList<string>? Categories { get; init; }

    /// <summary>Product types to include.</summary>
    public IReadOnlyList<ProductType>? ProductTypes { get; init; }

    /// <summary>Attribute number/value pairs the products must have.</summary>
    public IReadOnlyList<AttributeFilter>? AttributeFilters { get; init; }

    /// <summary>PAPI sort expression: <c>number</c>, <c>name</c>, <c>name:desc</c>, …</summary>
    public string? Sort { get; init; }
}

/// <summary>An attribute filter for <see cref="ProductQuery.AttributeFilters"/>.</summary>
/// <param name="Number">Attribute number.</param>
/// <param name="Value">Value the attribute must have.</param>
public readonly record struct AttributeFilter(string Number, string Value);

/// <summary>One page of <see cref="ProductClient.Find"/> results.</summary>
/// <param name="Items">Products on this page.</param>
/// <param name="TotalCount">Total number of matches reported by PAPI.</param>
/// <param name="Page">0-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
public sealed record ProductPage(IReadOnlyList<Product> Items, long TotalCount, int Page, int PageSize)
{
    /// <summary>Whether a following page has results.</summary>
    public bool HasNextPage => (Page + 1L) * PageSize < TotalCount;
}
