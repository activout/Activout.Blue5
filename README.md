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

List the contexts available to your API key:

```csharp
foreach (var context in await client.GetContexts())
{
    Console.WriteLine($"{context.Id}: {context.Name}");   // e.g. "l3598: Swedish"
}
```

If queries return no products at all, you are probably using the wrong context. The default is `en`.

## Products

```csharp
Product? product = await client.Products.GetByNumber("A-1001");       // null if not published
IReadOnlyList<Product> some = await client.Products.GetByNumbers(["A-1001", "A-1002"]);
Product? byId = await client.Products.GetById("5f00000000000000000000a1"); // PAPI id, not the product number
IReadOnlyList<Product> byIds = await client.Products.GetByIds(["5f00000000000000000000a1"]);

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

ProductPage page = await client.Products.Find(query, page: 0, pageSize: 50);
Console.WriteLine($"{page.Items.Count} of {page.TotalCount}");

await foreach (var p in client.Products.FindAll(query)) { /* ... */ }
```

Pages are 0-based, like PAPI's `pageNo`, and at most 50 items each (the PAPI limit, `ProductClient.MaxPageSize`).

## Attributes

Define typed keys once. Your domain types need no annotations or reflection. The attribute types live in
the `Activout.Blue5.Attributes` namespace:

```csharp
using Activout.Blue5.Attributes;

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

### Dictionary values

A product's `dictionary` attribute only carries the entries selected for that product. To list every
value defined for the attribute, pass its id (not its number). Pages of 100 are streamed lazily, as
with `GetAll()`:

```csharp
string dictionaryAttributeId = "YOUR-DICTIONARY-ATTRIBUTE-ID";
await foreach (SelectOption value in client.GetDictionaryValues(dictionaryAttributeId, cancellationToken))
{
    Console.WriteLine($"{value.Number} = {value.Value}");
}
```

## Media

`product.Media` lists the product's media assets in PAPI order. Each `MediaAsset` has `Name`,
`Description`, `FileName`, `ContentType`, `Labels`, `CreateDate`/`UpdateDate`, its own `Attributes`, and two
absolute URLs: `DownloadUri` (the original file) and `PreviewUri` (a small rendition). Blue5 only
describes assets; fetch the URLs with your own `HttpClient`.

```csharp
foreach (var image in product.Media.Where(m => m.ContentType.StartsWith("image/")))
    Console.WriteLine($"{image.Name}: {image.DownloadUri}");
```

## Relations, variants and bundles

Besides attributes and media, a `Product` carries `Relations` (links to other products, with
`ProductId`, `Direction` and `Reverse`), `Variants` and `Groups` (child product ids), `Bundles`
(`ProductId` and `Quantity` per bundled product), `Metadata` (id/value entries; non-string values come
back as raw JSON text) and `RelationSortingOrderSource`. The product references are PAPI ids, not product numbers, and Blue5 has no lookup by id yet.

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

blue5 context list                             # contexts you can pass to --context
blue5 product get A-1001                       # details (--format json for machines)
blue5 product list --limit 20                  # table, json or ndjson
blue5 product find --label new --type single --attr colour=Red --page 1
blue5 product find --label new --all --format ndjson
blue5 product attributes A-1001                # attribute table
blue5 attribute get A-1001 colour              # just the value(s)
blue5 attribute values 5f59cf80cff47e000c2ea630   # all values of a dictionary attribute (by id)
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
- `/products/list` `pageNo` is **0-based** (not stated in the OpenAPI document). Blue5 uses the same numbering.
- `/products/list` rejects `itemsOnPage` above **50**. `/products/cursor/all` accepts `limit` up
  to 100.
- The cursor endpoint returns a non-null `nextCursor` even on the last page of data. The end of the
  stream is `{"nextCursor":null,"results":[]}`, so a full `GetAll()` makes one extra, empty request.
- An unrecognised cursor silently restarts from the beginning. Blue5 never makes cursors up.
- `/products/by-numbers` accepts 1–100 numbers and `/products/by-ids` 1–100 ids. `GetByNumbers` and `GetByIds` batch larger inputs.
  Unknown ids are left out of the result rather than failing; `GET /products/{id}` returns 404 for them, which is why Blue5 uses `by-ids`.
- Error bodies are `{"message": "...", "entityId": "..."}` without the documented `status`. A
  missing or wrong API key gives `403 {"message":"Forbidden"}`.
- PAPI does return **429** in practice: a sequential `FindAll()` over ~6,000 products (125 requests)
  was throttled several times. The response is a bare `429 {"message":"Too Many Requests"}` with no
  `Retry-After` and no `x-request-tracker`. Use Activout.Blue5.Resilience (or your own policy) for bulk
  reads.
- Attributes carry two undocumented fields, exposed on `ProductAttribute`: `definingAttribute`
  (`IsDefining`, marks variant-defining attributes) and `valueType` (`ValueType`). A `color` value
  type on a select puts a hex colour in each option's `Metadata`.
- Attribute values depend on the context: the same attribute can have a value in one context and
  `"values": []` in another. Blue5 treats an attribute without values as missing and returns the
  key's `DefaultValue`.
- Media assets are embedded in each product (there are no media endpoints). `downloadUri` and
  `previewUri` point at a separate media host; the preview is a
  `?f=jpg&w=400` rendition of the download URL. Most products have no media (1 in 1,500 in the test tenant).
- In the test tenant `variants`, `bundles` and `relatedProductsRelationSortingOrderSource` occur on live products.
  `relations`, `metadata` and `groups` did not occur in the first 2,000 products, so their mapping follows the
  OpenAPI document only.
- Responses leave out empty collections and include fields the spec doesn't mention (such as
  `publishInfoRef`). Blue5 ignores unknown fields.

## Verifying packages

The packages are published from GitHub Actions with NuGet trusted publishing. Each one carries a
[build provenance attestation](https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations)
that links it to this repository, workflow and commit:

```bash
gh attestation verify Activout.Blue5.0.1.0.nupkg -R activout/Activout.Blue5
```

Symbol packages (`.snupkg`) and SourceLink let you step into the Blue5 source while debugging.

## Links

- [Bluestone PIM Public API reference](https://docs.api.bluestonepim.com/)
- [Attribute types in Public API](https://help.bluestonepim.com/attribute-types-in-public-api)
- [Rate limits, retries and backoff](https://docs.api-us.bluestonepim.com/docs/rate-limits-retries-and-backoff)
- [Bluestone PIM Labs](https://labs.bluestonepim.com/)

## License

MIT, see [LICENSE](LICENSE).
