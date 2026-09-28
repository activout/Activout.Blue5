# Activout.Blue5

An idiomatic, **read-only** .NET 10 client for the [Bluestone PIM](https://www.bluestonepim.com/)
Public API (PAPI). It gives you typed product attributes and lazy, cursor-based streaming of the
whole catalogue. You never deal with Bluestone JSON, DTOs or pagination.

> **Unofficial.** Activout.Blue5 is a community project maintained by Activout. It is not affiliated
> with, endorsed by, or supported by Bluestone PIM.

| Package | Purpose |
|---|---|
| `Activout.Blue5` | Core client: products, typed attributes, cursor streaming, search. Depends only on `Microsoft.Extensions.Http`. Never retries. |
| `Activout.Blue5.Resilience` | Optional retry/backoff that follows Bluestone's guidance (429, transient 5xx, network errors) |
| `Activout.Blue5.Cli` | The `blue5` dotnet tool: inspect products and attributes, export NDJSON |

## Installation

```bash
dotnet add package Activout.Blue5
dotnet add package Activout.Blue5.Resilience   # optional, recommended
dotnet tool install -g Activout.Blue5.Cli       # optional CLI
```

## Getting started

### Direct construction (console apps, scripts, tests)

```csharp
using Activout.Blue5;

var client = new Blue5Client(new HttpClient(), new Blue5Options
{
    BaseAddress = new Uri("https://api.bluestonepim.com"), // environment root; Blue5 adds /v1
    ApiKey = Environment.GetEnvironmentVariable("BLUE5_API_KEY")!,
    Context = "en",                                        // default context
});
```

### Dependency injection

```csharp
using Activout.Blue5;
using Activout.Blue5.Resilience;

builder.Services
    .AddBlue5(builder.Configuration.GetSection("Blue5").Get<Blue5Options>()!)
    .AddBlue5Resilience();   // optional

// then inject Blue5Client
```

`AddBlue5` registers `Blue5Client` as a typed `HttpClient` client and returns the
`IHttpClientBuilder`, so you can chain your own handlers or resilience strategy instead.

### Contexts

A PAPI context is a language or publication, sent as the `context` header. `WithContext` returns a
lightweight client that shares the same `HttpClient`. The original client is left unchanged.

```csharp
var swedish = client.WithContext("sv");
var product = await swedish.Products.GetByNumber("A-1001");
```

`GET {BaseAddress}/v1/contexts` lists the contexts available to your API key.

## Products

```csharp
Product? product = await client.Products.GetByNumber("A-1001");       // null if not published
IReadOnlyList<Product> some = await client.Products.GetByNumbers(["A-1001", "A-1002"]);

await foreach (var p in client.Products.GetAll(cancellationToken))    // cursor streaming
{
    Console.WriteLine($"{p.Number}: {p.Name}");
}
```

The product number is a **string** business key and is never parsed as a number.

`GetAll()` reads the catalogue through PAPI's cursor endpoint in pages of 100. It fetches the next
page only when enumeration reaches it, so the whole catalogue is never loaded into memory. It works
with .NET 10's built-in async LINQ:

```csharp
var articles = client.Products
    .GetAll()
    .Where(p => p.Type == ProductType.Variant)
    .Select(p => new Article(
        p.Number,
        p.Attributes.Get(Attributes.Name),
        p.Attributes.Get(Attributes.Price)))
    .Take(10);   // one HTTP request

await foreach (var article in articles) { /* ... */ }
```

### Search

`Find` returns a single page, including the total number of matches. `FindAll` streams every
match, fetching pages lazily.

```csharp
var query = new ProductQuery
{
    Labels = ["new"],
    ProductTypes = [ProductType.Single],
    AttributeFilters = [new AttributeFilter("colour", "Red")],
    Sort = "name:desc",
};

ProductPage page = await client.Products.Find(query, page: 1, pageSize: 50);
Console.WriteLine($"{page.Items.Count} of {page.TotalCount}");

await foreach (var p in client.Products.FindAll(query)) { /* ... */ }
```

Pages are 1-based and at most 50 items each (the PAPI limit, `ProductClient.MaxPageSize`).

## Attributes

Define typed keys once. Your domain types need no annotations or reflection:

```csharp
public static class Attributes
{
    public static readonly AttributeKey<string> Name = new("name", string.Empty);
    public static readonly AttributeKey<string> Colour = new("colour", string.Empty);
    public static readonly AttributeKey<decimal?> Weight = new("weight");
    public static readonly AttributeKey<decimal> Price = new("price", 0m);
    public static readonly AttributeKey<IReadOnlyList<SelectOption>> Sizes = new("sizes", []);
}

string colour = product.Attributes.Get(Attributes.Colour);   // "" when absent
decimal? weight = product.Attributes.Get(Attributes.Weight); // null when absent
```

- When an attribute is **missing** (or has no value), `Get` returns the key's `DefaultValue`.
- When a value is **present but can't be converted**, `Get` throws `AttributeConversionException`.
  The exception carries the attribute number, target type, PAPI data type and raw value. Blue5
  never silently turns bad data into a default.
- Lookups are indexed once per product by attribute number, using ordinal (case-sensitive)
  comparison.

### Supported conversions

| PAPI `dataType` | Typed access |
|---|---|
| `text`, `multiline`, `pattern`, compound | `string` (or any scalar below, if the text parses) |
| `boolean` | `bool`, `string` |
| `integer` | `int`, `long`, `decimal`, `double`, `string` |
| `decimal` | `decimal`, `double`, `string` |
| `date` (`2020-01-01`) | `DateOnly`, `DateTime`, `string` |
| `date_time` (`2020-01-01 9:00:00`) | `DateTime` (`DateTimeKind.Unspecified`: PAPI sends no offset), `string` |
| `time` (`11:05:31`) | `TimeOnly`, `string` |
| `formatted_text` | `FormattedText` (content + `text/markdown` / `text/html`), `string` |
| `single_select` | `SelectOption`, `IReadOnlyList<SelectOption>`, `string` (display value) |
| `multi_select` | `IReadOnlyList<SelectOption>`, `IReadOnlyList<string>` |
| `dictionary` | same as select |
| `column` | `IReadOnlyList<AttributeCell>` |
| `matrix` | `IReadOnlyList<MatrixColumn>` (each with `AttributeCell` rows) |

Scalar types also work in their nullable form, e.g. `int?` or `DateOnly?`. Every non-structured
value also converts to `IReadOnlyList<string>`. Asking for a single scalar from an attribute that
holds several values throws; ask for a list instead.

### Raw attributes

For data no typed conversion covers, including attribute types Bluestone may add later, inspect the
raw value directly:

```csharp
ProductAttribute? raw = product.Attributes.Find("colour");
if (raw is not null)
{
    Console.WriteLine($"{raw.Number} ({raw.DataType}, unit {raw.Unit}): {string.Join(", ", raw.Values)}");
    foreach (var option in raw.Select) Console.WriteLine($"{option.Number} = {option.Value}");
}

foreach (var attribute in product.Attributes) { /* all attributes, in PAPI order */ }
```

`Product`, `ProductAttributes` and `ProductAttribute` have public constructors and `init`
properties, so your unit tests can build products without any HTTP.

## Errors and rate limits

Any non-success PAPI response throws `BluestoneException`. It carries `StatusCode`,
`PapiMessage`, `EntityId`/`EntityIds` (for example the unknown product id or context) and
`RequestId` (the `x-request-tracker` header, useful when you contact Bluestone support).

PAPI can return **HTTP 429**. The core package reports it (`ex.IsRateLimited`) and **does not
retry**. That choice is yours:

```csharp
services.AddBlue5(options).AddBlue5Resilience();

// or tune it
services.AddBlue5(options).AddBlue5Resilience(retry =>
{
    retry.MaxRetryAttempts = 2;
    retry.Delay = TimeSpan.FromMilliseconds(500);
});
```

Defaults follow Bluestone's
[rate limits, retries and backoff](https://docs.api-us.bluestonepim.com/docs/rate-limits-retries-and-backoff) guidance:
- Retries HTTP 429, 500, 502, 503, 504 and network failures.
- Exponential backoff from 1 s (factor 2) with jitter.
- Up to 5 attempts in total.

Bluestone publishes a numeric rate limit only for the Management API, not for PAPI. The policy
handles 429 because PAPI has been seen to return it in practice.

## CLI

```bash
export BLUE5_BASE_URL=https://api.bluestonepim.com
export BLUE5_API_KEY=...          # never echoed
export BLUE5_CONTEXT=en           # optional

blue5 product get A-1001                       # details (--format json for machines)
blue5 product list --limit 20                  # table, json or ndjson
blue5 product find --label new --type single --attr colour=Red --page 2
blue5 product find --label new --all --format ndjson
blue5 product attributes A-1001                # attribute table
blue5 attribute get A-1001 colour              # just the value(s)
blue5 product export --format ndjson > catalogue.ndjson   # streams; constant memory
```

`--base-url`, `--api-key` and `--context` override the environment variables. `--verbose` logs
each HTTP request (never its headers) and full error details to stderr. The CLI retries using
Activout.Blue5.Resilience. Exit codes: `0` success, `1` not found or API error, `2` invalid
usage or configuration.

## PAPI notes

These behaviours were observed on the live API and differ from, or are missing from, the OpenAPI
document. Each is covered by a test.

- The API lives under `/v1` of the environment root (`https://api.bluestonepim.com/v1`).
  `BaseAddress` is the root.
- `/products/list` `pageNo` is **0-based**. Blue5 exposes 1-based pages.
- `/products/list` rejects `itemsOnPage` above **50**. `/products/cursor/all` accepts `limit` up
  to 100.
- The cursor endpoint returns a non-null `nextCursor` even on the last page of data. The end of the
  stream is `{"nextCursor":null,"results":[]}`, so a full `GetAll()` makes one extra, empty request.
- An unrecognised cursor silently restarts from the beginning. Blue5 never makes cursors up.
- `/products/by-numbers` accepts 1–100 numbers. `GetByNumbers` batches larger inputs.
- Error bodies are `{"message": "...", "entityId": "..."}` without the documented `status`. A
  missing or wrong API key gives `403 {"message":"Forbidden"}`.
- Responses leave out empty collections and include fields the spec doesn't mention (such as
  `publishInfoRef`). Blue5 ignores unknown fields.

## Links

- [Bluestone PIM Public API reference](https://docs.api.bluestonepim.com/)
- [Attribute types in Public API](https://help.bluestonepim.com/attribute-types-in-public-api)
- [Rate limits, retries and backoff](https://docs.api-us.bluestonepim.com/docs/rate-limits-retries-and-backoff)
- [Bluestone PIM Labs](https://labs.bluestonepim.com/)

## License

MIT, see [LICENSE](LICENSE).
