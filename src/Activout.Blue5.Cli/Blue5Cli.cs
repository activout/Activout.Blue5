using System.CommandLine;
using System.Diagnostics;
using Activout.Blue5.Attributes;
using Activout.Blue5.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace Activout.Blue5.Cli;

/// <summary>The <c>blue5</c> command tree. A thin consumer of the public Activout.Blue5 API.</summary>
/// <param name="output">Standard output.</param>
/// <param name="error">Standard error.</param>
/// <param name="primaryHandler">Test seam: replaces the network handler (resilience still applies).</param>
/// <param name="environment">Test seam: environment variable lookup.</param>
internal sealed class Blue5Cli(
    TextWriter output,
    TextWriter error,
    HttpMessageHandler? primaryHandler = null,
    Func<string, string?>? environment = null)
{
    private readonly Func<string, string?> _environment = environment ?? Environment.GetEnvironmentVariable;

    private readonly Option<string?> _baseUrl = new("--base-url") { Description = "PAPI environment root, e.g. https://api.bluestonepim.com (env: BLUE5_BASE_URL)", Recursive = true };
    private readonly Option<string?> _apiKey = new("--api-key") { Description = "PAPI API key (env: BLUE5_API_KEY). Never echoed.", Recursive = true };
    private readonly Option<string?> _context = new("--context") { Description = "PAPI context/language (env: BLUE5_CONTEXT, default: en)", Recursive = true };
    private readonly Option<bool> _verbose = new("--verbose", "-v") { Description = "Log HTTP requests and full errors to stderr", Recursive = true };

    public Task<int> Run(string[] args, CancellationToken cancellationToken = default)
    {
        var root = new RootCommand("blue5: read products from the Bluestone PIM Public API (unofficial)")
        {
            _baseUrl, _apiKey, _context, _verbose,
        };
        root.Subcommands.Add(ProductCommand());
        root.Subcommands.Add(AttributeCommand());
        root.Subcommands.Add(ContextCommand());
        return root.Parse(args).InvokeAsync(new InvocationConfiguration { Output = output, Error = error }, cancellationToken);
    }

    private Command ProductCommand()
    {
        var product = new Command("product", "Read products");

        var number = new Argument<string>("number") { Description = "Product number" };
        var getFormat = Format("table", "json");
        var get = new Command("get", "Show one product") { number, getFormat };
        get.SetAction((parse, ct) => Execute(parse, ct, async client =>
        {
            var p = await client.Products.GetByNumber(parse.GetValue(number)!, ct);
            if (p is null) return NotFound($"Product '{parse.GetValue(number)}'");
            if (parse.GetValue(getFormat) == "json") Output.WriteJson(output, p, indented: true);
            else Output.WriteProductDetails(output, p);
            return 0;
        }));

        var limit = new Option<int?>("--limit", "-n") { Description = "Stop after this many products" };
        var listFormat = Format("table", "json", "ndjson");
        var list = new Command("list", "List products (cursor streaming)") { limit, listFormat };
        list.SetAction((parse, ct) => Execute(parse, ct, async client =>
        {
            var products = client.Products.GetAll(ct);
            if (parse.GetValue(limit) is { } n) products = products.Take(n);
            await Output.WriteProducts(output, products, parse.GetValue(listFormat)!, ct);
            return 0;
        }));

        var numbers = new Option<string[]>("--number") { Description = "Product number (repeatable)" };
        var names = new Option<string[]>("--name") { Description = "Name must contain (repeatable)" };
        var labels = new Option<string[]>("--label") { Description = "Label (repeatable)" };
        var categories = new Option<string[]>("--category") { Description = "Category id (repeatable)" };
        var types = new Option<ProductType[]>("--type") { Description = "Product type: Single, Variant, Group, Bundle, Family (repeatable)" };
        var attrs = new Option<string[]>("--attr") { Description = "Attribute filter <number>=<value> (repeatable)" };
        var sort = new Option<string?>("--sort") { Description = "Sort, e.g. number or name:desc" };
        var page = new Option<int>("--page") { Description = "0-based page", DefaultValueFactory = _ => 0 };
        var pageSize = new Option<int>("--page-size") { Description = $"Page size (max {ProductClient.MaxPageSize})", DefaultValueFactory = _ => ProductClient.MaxPageSize };
        var all = new Option<bool>("--all") { Description = "Stream all pages instead of one" };
        var findFormat = Format("table", "json", "ndjson");
        var find = new Command("find", "Search products") { numbers, names, labels, categories, types, attrs, sort, page, pageSize, all, findFormat };
        find.SetAction((parse, ct) => Execute(parse, ct, async client =>
        {
            var filters = new List<AttributeFilter>();
            foreach (var attr in parse.GetValue(attrs) ?? [])
            {
                var eq = attr.IndexOf('=');
                if (eq <= 0) throw new UsageException($"--attr must be <number>=<value>, got '{attr}'.");
                filters.Add(new AttributeFilter(attr[..eq], attr[(eq + 1)..]));
            }

            var query = new ProductQuery
            {
                Numbers = NullIfEmpty(parse.GetValue(numbers)),
                Names = NullIfEmpty(parse.GetValue(names)),
                Labels = NullIfEmpty(parse.GetValue(labels)),
                Categories = NullIfEmpty(parse.GetValue(categories)),
                ProductTypes = NullIfEmpty(parse.GetValue(types)),
                AttributeFilters = NullIfEmpty(filters.ToArray()),
                Sort = parse.GetValue(sort),
            };
            var format = parse.GetValue(findFormat)!;
            if (parse.GetValue(all))
            {
                await Output.WriteProducts(output, client.Products.FindAll(query, ct), format, ct);
                return 0;
            }

            var result = await client.Products.Find(query, parse.GetValue(page), parse.GetValue(pageSize), ct);
            if (format == "json")
            {
                Output.WriteJson(output, result, indented: true);
            }
            else
            {
                await Output.WriteProducts(output, result.Items.ToAsyncEnumerable(), format, ct);
                if (format == "table") error.WriteLine($"Page {result.Page}: {result.Items.Count} of {result.TotalCount} matches.");
            }

            return 0;
        }));

        var attributesFormat = Format("table", "json");
        var attributes = new Command("attributes", "Show a product's attributes") { number, attributesFormat };
        attributes.SetAction((parse, ct) => Execute(parse, ct, async client =>
        {
            var p = await client.Products.GetByNumber(parse.GetValue(number)!, ct);
            if (p is null) return NotFound($"Product '{parse.GetValue(number)}'");
            if (parse.GetValue(attributesFormat) == "json") Output.WriteJson(output, p.Attributes, indented: true);
            else Output.WriteAttributeTable(output, p.Attributes);
            return 0;
        }));

        var exportFormat = Format("ndjson", "json");
        var export = new Command("export", "Stream all products as NDJSON or a JSON array") { exportFormat };
        export.SetAction((parse, ct) => Execute(parse, ct, async client =>
        {
            await Output.WriteProducts(output, client.Products.GetAll(ct), parse.GetValue(exportFormat)!, ct);
            return 0;
        }));

        product.Subcommands.Add(get);
        product.Subcommands.Add(list);
        product.Subcommands.Add(find);
        product.Subcommands.Add(attributes);
        product.Subcommands.Add(export);
        return product;
    }

    private Command AttributeCommand()
    {
        var productNumber = new Argument<string>("product-number") { Description = "Product number" };
        var attributeNumber = new Argument<string>("attribute-number") { Description = "Attribute number" };
        var format = Format("text", "json");
        var get = new Command("get", "Show one attribute value of a product") { productNumber, attributeNumber, format };
        get.SetAction((parse, ct) => Execute(parse, ct, async client =>
        {
            var p = await client.Products.GetByNumber(parse.GetValue(productNumber)!, ct);
            if (p is null) return NotFound($"Product '{parse.GetValue(productNumber)}'");
            var attribute = p.Attributes.Find(parse.GetValue(attributeNumber)!);
            if (attribute is null) return NotFound($"Attribute '{parse.GetValue(attributeNumber)}' on product '{p.Number}'");
            if (parse.GetValue(format) == "json") Output.WriteJson(output, attribute, indented: true);
            else Output.WriteAttributeText(output, attribute);
            return 0;
        }));

        return new Command("attribute", "Read attribute values") { get };
    }

    private Command ContextCommand()
    {
        var format = Format("table", "json");
        var list = new Command("list", "List the contexts (languages/publications) available to the API key") { format };
        list.SetAction((parse, ct) => Execute(parse, ct, async client =>
        {
            var contexts = await client.GetContexts(ct);
            if (parse.GetValue(format) == "json") Output.WriteJson(output, contexts, indented: true);
            else Output.WriteContextTable(output, contexts);
            return 0;
        }));

        return new Command("context", "Read PAPI contexts") { list };
    }

    private static Option<string> Format(params string[] formats)
    {
        var option = new Option<string>("--format", "-f")
        {
            Description = $"Output format: {string.Join(", ", formats)}",
            DefaultValueFactory = _ => formats[0],
        };
        option.AcceptOnlyFromAmong(formats);
        return option;
    }

    private static T[]? NullIfEmpty<T>(T[]? values) => values is { Length: > 0 } ? values : null;

    private int NotFound(string what)
    {
        error.WriteLine($"{what} not found.");
        return 1;
    }

    private async Task<int> Execute(ParseResult parse, CancellationToken cancellationToken, Func<Blue5Client, Task<int>> action)
    {
        var verbose = parse.GetValue(_verbose);
        try
        {
            await using var services = CreateServices(parse, verbose);
            return await action(services.GetRequiredService<Blue5Client>());
        }
        catch (UsageException ex)
        {
            error.WriteLine(ex.Message);
            return 2;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
        catch (Exception ex) when (ex is BluestoneException or AttributeConversionException or HttpRequestException)
        {
            error.WriteLine(verbose ? ex.ToString() : $"Error: {ex.Message}");
            if (ex is BluestoneException { RequestId: { } id }) error.WriteLine($"Request id: {id}");
            return 1;
        }
    }

    private ServiceProvider CreateServices(ParseResult parse, bool verbose)
    {
        var baseUrl = parse.GetValue(_baseUrl) ?? _environment("BLUE5_BASE_URL");
        var apiKey = parse.GetValue(_apiKey) ?? _environment("BLUE5_API_KEY");
        var context = parse.GetValue(_context) ?? _environment("BLUE5_CONTEXT");
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseAddress))
            throw new UsageException("Missing or invalid base URL: use --base-url or BLUE5_BASE_URL.");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new UsageException("Missing API key: use --api-key or BLUE5_API_KEY.");

        var services = new ServiceCollection();
        var http = services
            .AddBlue5(new Blue5Options
            {
                BaseAddress = baseAddress,
                ApiKey = apiKey,
                Context = string.IsNullOrWhiteSpace(context) ? "en" : context,
            })
            .AddBlue5Resilience();
        if (verbose) http.AddHttpMessageHandler(() => new VerboseHandler(error));
        if (primaryHandler is not null) http.ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        return services.BuildServiceProvider();
    }

    private sealed class UsageException(string message) : Exception(message);

    /// <summary>Logs method, URL, status and timing. Headers (and thus the API key) are never logged.</summary>
    private sealed class VerboseHandler(TextWriter error) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            var response = await base.SendAsync(request, cancellationToken);
            error.WriteLine($"{request.Method} {request.RequestUri} -> {(int)response.StatusCode} ({stopwatch.ElapsedMilliseconds} ms)");
            return response;
        }
    }
}
