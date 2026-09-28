using System.Net;
using System.Text.Json;
using Activout.Blue5.Internal;
using RichardSzalay.MockHttp;

namespace Activout.Blue5.Tests.Support;

/// <summary>Terse builders for PAPI responses and a Blue5Client wired to MockHttp.</summary>
internal static class Fake
{
    public const string Root = "https://papi.test/v1/";
    public const string ApiKey = "secret-key";

    public static Blue5Client Client(MockHttpMessageHandler mock, string context = "en") =>
        new(mock.ToHttpClient(), new Blue5Options { BaseAddress = new Uri("https://papi.test"), ApiKey = ApiKey, Context = context });

    public static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static string Product(string number, params string[] attributes) =>
        $$"""{"id":"id-{{number}}","type":"SINGLE","name":"Product {{number}}","number":"{{number}}","attributes":[{{string.Join(",", attributes)}}]}""";

    public static string Attribute(string number, string dataType, params string[] values) =>
        $$"""{"id":"attr-{{number}}","number":"{{number}}","dataType":"{{dataType}}","values":[{{string.Join(",", values.Select(v => $"\"{v}\""))}}]}""";

    public static string Results(params string[] products) => $$"""{"results":[{{string.Join(",", products)}}]}""";

    public static string CursorPage(string? nextCursor, params string[] products) =>
        $$"""{"nextCursor":{{(nextCursor is null ? "null" : $"\"{nextCursor}\"")}},"results":[{{string.Join(",", products)}}]}""";

    public static string SearchPage(long totalCount, params string[] products) =>
        $$"""{"totalCount":{{totalCount}},"results":[{{string.Join(",", products)}}]}""";

    public static string[] Products(string prefix, int count) =>
        Enumerable.Range(1, count).Select(i => Product($"{prefix}{i}")).ToArray();

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };

    /// <summary>Maps PAPI product JSON through the real DTO/mapping path, without HTTP.</summary>
    public static Product ParseProduct(string json) => Mapping.ToProduct(JsonSerializer.Deserialize<ProductDto>(json, Papi.Json)!);

    public static Product ProductWith(params string[] attributes) => ParseProduct(Product("P", attributes));
}
