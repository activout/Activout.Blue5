using Activout.Blue5.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class HttpContractTests
{
    [Fact]
    public async Task GetByNumber_PostsNumberWithAuthAndContextHeaders_AndMapsProduct()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Root + "products/by-numbers")
            .WithHeaders("x-api-key", ApiKey)
            .WithHeaders("context", "en")
            .WithContent("""{"numbers":["00123"]}""")
            .Respond("application/json", Results(
                """
                {"id":"p1","type":"VARIANT","name":"Shirt","number":"00123","description":"Nice","labels":["new"],
                 "categories":["c1"],"variantParentId":"p0","lastUpdate":1767225600000,"createDate":1735689600000,"attributes":[]}
                """));

        var product = await Client(mock).Products.GetByNumber("00123");

        mock.VerifyNoOutstandingExpectation();
        Assert.NotNull(product);
        Assert.Equal(("p1", "00123", "Shirt", "Nice", ProductType.Variant, "p0"),
            (product.Id, product.Number, product.Name, product.Description, product.Type, product.VariantParentId));
        Assert.Equal(["new"], product.Labels);
        Assert.Equal(["c1"], product.Categories);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1767225600000), product.LastUpdate);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1735689600000), product.CreateDate);
    }

    [Fact]
    public async Task GetByNumber_ReturnsNull_WhenNotPublished()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/by-numbers").Respond("application/json", Results());

        Assert.Null(await Client(mock).Products.GetByNumber("missing"));
    }

    [Fact]
    public async Task GetByNumbers_DeduplicatesAndBatchesByHundred()
    {
        var mock = new MockHttpMessageHandler();
        var bodies = new List<string>();
        mock.When(HttpMethod.Post, Root + "products/by-numbers").Respond(async request =>
        {
            bodies.Add(await request.Content!.ReadAsStringAsync());
            return Json(Results(Product("X")));
        });
        var numbers = Enumerable.Range(0, 250).Select(i => $"N{i}").Concat(["N0", "N1"]);

        var products = await Client(mock).Products.GetByNumbers(numbers);

        Assert.Equal(3, bodies.Count);
        Assert.Equal(3, products.Count);
        Assert.StartsWith("""{"numbers":["N0","N1",""", bodies[0]);
        Assert.StartsWith("""{"numbers":["N200",""", bodies[2]);
    }

    [Fact]
    public async Task GetByNumbers_EmptyInput_MakesNoRequest()
    {
        var mock = new MockHttpMessageHandler();
        Assert.Empty(await Client(mock).Products.GetByNumbers([]));
    }

    [Fact]
    public async Task WithContext_SendsOtherContext_WithoutChangingOriginal()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Root + "products/by-numbers").WithHeaders("context", "sv").Respond("application/json", Results());
        mock.Expect(HttpMethod.Post, Root + "products/by-numbers").WithHeaders("context", "en").Respond("application/json", Results());
        var client = Client(mock);

        var swedish = client.WithContext("sv");
        await swedish.Products.GetByNumber("A");
        await client.Products.GetByNumber("A");

        mock.VerifyNoOutstandingExpectation();
        Assert.Equal(("sv", "en"), (swedish.Context, client.Context));
    }

    [Theory]
    [InlineData("https://papi.test")]
    [InlineData("https://papi.test/")]
    public async Task BaseAddress_WithOrWithoutTrailingSlash_GetsV1Appended(string baseAddress)
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, "https://papi.test/v1/products/by-numbers").Respond("application/json", Results());
        var client = new Blue5Client(mock.ToHttpClient(), new Blue5Options { BaseAddress = new Uri(baseAddress), ApiKey = "k" });

        await client.Products.GetByNumber("A");

        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task AddBlue5_RegistersTypedClient()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Root + "products/by-numbers").WithHeaders("context", "no").Respond("application/json", Results(Product("A")));
        var services = new ServiceCollection();
        services.AddBlue5(new Blue5Options { BaseAddress = new Uri("https://papi.test"), ApiKey = ApiKey, Context = "no" })
            .ConfigurePrimaryHttpMessageHandler(() => mock);
        await using var provider = services.BuildServiceProvider();

        var product = await provider.GetRequiredService<Blue5Client>().Products.GetByNumber("A");

        Assert.Equal("A", product?.Number);
    }

    [Fact]
    public void Constructor_RejectsMissingApiKey() =>
        Assert.Throws<ArgumentException>(() => new Blue5Client(new HttpClient(), new Blue5Options { BaseAddress = new Uri("https://papi.test"), ApiKey = " " }));
}
