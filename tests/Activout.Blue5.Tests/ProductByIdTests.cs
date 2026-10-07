using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class ProductByIdTests
{
    [Fact]
    public async Task GetById_PostsIdAndReturnsMatch()
    {
        var mock = new MockHttpMessageHandler();
        mock.Expect(HttpMethod.Post, Root + "products/by-ids")
            .WithHeaders("x-api-key", ApiKey)
            .WithContent("""{"ids":["id-A"]}""")
            .Respond("application/json", Results(Product("A")));

        var product = await Client(mock).Products.GetById("id-A");

        Assert.Equal("A", product?.Number);
        mock.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotPublished()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/by-ids").Respond("application/json", Results());

        Assert.Null(await Client(mock).Products.GetById("missing"));
    }

    [Fact]
    public async Task GetByIds_DeduplicatesAndBatchesByHundred()
    {
        var mock = new MockHttpMessageHandler();
        var bodies = new List<string>();
        mock.When(HttpMethod.Post, Root + "products/by-ids").Respond(async request =>
        {
            bodies.Add(await request.Content!.ReadAsStringAsync());
            return Json(Results(Product("X")));
        });
        var ids = Enumerable.Range(0, 250).Select(i => $"I{i}").Concat(["I0", "I1"]);

        var products = await Client(mock).Products.GetByIds(ids);

        Assert.Equal(3, bodies.Count);
        Assert.Equal(3, products.Count);
        Assert.StartsWith("""{"ids":["I0","I1",""", bodies[0]);
        Assert.StartsWith("""{"ids":["I200",""", bodies[2]);
    }

    [Fact]
    public async Task GetByIds_Empty_MakesNoRequest()
    {
        var mock = new MockHttpMessageHandler(); // any request would throw: nothing is mocked

        Assert.Empty(await Client(mock).Products.GetByIds([]));
    }
}
