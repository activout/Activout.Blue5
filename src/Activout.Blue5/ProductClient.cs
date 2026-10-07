using System.Globalization;
using System.Runtime.CompilerServices;
using Activout.Blue5.Internal;

namespace Activout.Blue5;

/// <summary>Read-only access to PAPI products. Obtain it from <see cref="Blue5Client.Products"/>.</summary>
public sealed class ProductClient
{
    /// <summary>Largest page PAPI accepts for <c>/products/list</c> (observed: "itemsPerPage cannot be greater than the max value of 50").</summary>
    public const int MaxPageSize = 50;

    private const int CursorPageSize = 100;   // PAPI maximum for /products/cursor/all
    private const int BatchSize = 100;        // PAPI: "numbers/ids size must be between 1 and 100"

    private readonly Papi _papi;

    internal ProductClient(Papi papi) => _papi = papi;

    /// <summary>Gets a product by its number, or <see langword="null"/> if it is not published in this context.</summary>
    public async Task<Product?> GetByNumber(string number, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(number);
        var products = await GetByNumbers([number], cancellationToken);
        return products.FirstOrDefault(p => string.Equals(p.Number, number, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the products with the given numbers. Numbers that do not exist are simply absent from the result.
    /// Large inputs are split into batches of 100 (the PAPI limit).
    /// </summary>
    public async Task<IReadOnlyList<Product>> GetByNumbers(IEnumerable<string> numbers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(numbers);
        return await PostInBatches(numbers, "v1/products/by-numbers", batch => new NumbersRequestDto(batch), cancellationToken);
    }

    /// <summary>Gets a product by its PAPI id (<see cref="Product.Id"/>), or <see langword="null"/> if it is not published in this context.</summary>
    public async Task<Product?> GetById(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var products = await GetByIds([id], cancellationToken);
        return products.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the products with the given PAPI ids (<see cref="Product.Id"/>, as found in other products' references).
    /// Ids that do not exist are simply absent from the result. Large inputs are split into batches of 100 (the PAPI limit).
    /// </summary>
    public async Task<IReadOnlyList<Product>> GetByIds(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        return await PostInBatches(ids, "v1/products/by-ids", batch => new IdsRequestDto(batch), cancellationToken);
    }

    private async Task<IReadOnlyList<Product>> PostInBatches(
        IEnumerable<string> keys, string path, Func<string[], object> request, CancellationToken cancellationToken)
    {
        var result = new List<Product>();
        foreach (var batch in keys.Distinct(StringComparer.Ordinal).Chunk(BatchSize))
        {
            var response = await _papi.Post<ResultsDto>(path, request(batch), cancellationToken);
            result.AddRange((response.Results ?? []).Select(Mapping.ToProduct));
        }

        return result;
    }

    /// <summary>
    /// Streams every product in the context using PAPI's cursor API. Pages of 100 are fetched only as enumeration
    /// needs them, so <c>GetAll().Take(10)</c> makes a single request.
    /// </summary>
    public async IAsyncEnumerable<Product> GetAll([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string? cursor = null;
        while (true)
        {
            var page = await _papi.Post<CursorPageDto>("v1/products/cursor/all", new CursorQueryDto(cursor, CursorPageSize), cancellationToken);
            var results = page.Results ?? [];
            foreach (var dto in results)
            {
                yield return Mapping.ToProduct(dto);
            }

            // PAPI returns a non-null cursor on the last data page and ends with {"nextCursor":null,"results":[]}.
            if (results.Count == 0 || string.IsNullOrEmpty(page.NextCursor))
            {
                yield break;
            }

            cursor = page.NextCursor;
        }
    }

    /// <summary>Searches products and returns one page, including the total match count.</summary>
    /// <param name="query">Filters; <see cref="ProductQuery.All"/> for everything.</param>
    /// <param name="page">0-based page number (as PAPI's <c>pageNo</c>).</param>
    /// <param name="pageSize">Page size, 1 to <see cref="MaxPageSize"/>.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    public async Task<ProductPage> Find(ProductQuery query, int page = 0, int pageSize = MaxPageSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        var path = string.Create(CultureInfo.InvariantCulture, $"v1/products/list?pageNo={page}&itemsOnPage={pageSize}");
        if (!string.IsNullOrEmpty(query.Sort))
        {
            path += "&sort=" + Uri.EscapeDataString(query.Sort);
        }

        var response = await _papi.Post<PaginatedDto>(path, ToCriteria(query), cancellationToken);
        var items = (response.Results ?? []).Select(Mapping.ToProduct).ToArray();
        return new ProductPage(items, response.TotalCount ?? items.Length, page, pageSize);
    }

    /// <summary>Streams every product matching <paramref name="query"/>, walking search pages lazily.</summary>
    public async IAsyncEnumerable<Product> FindAll(ProductQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (var page = 0; ; page++)
        {
            var result = await Find(query, page, MaxPageSize, cancellationToken);
            foreach (var product in result.Items)
            {
                yield return product;
            }

            if (result.Items.Count == 0 || !result.HasNextPage)
            {
                yield break;
            }
        }
    }

    private static FilteringCriteriaDto ToCriteria(ProductQuery q) => new(
        q.Numbers,
        q.Names,
        q.Labels,
        q.Categories,
        q.AttributeFilters?.Select(f => new AttributeFilterDto(f.Number, f.Value)).ToArray(),
        q.ProductTypes?.Select(Mapping.ToPapi).ToArray());
}
