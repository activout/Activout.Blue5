using RichardSzalay.MockHttp;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class ToleranceTests
{
    [Fact]
    public async Task UnknownJsonProperties_AtEveryLevel_AreIgnored()
    {
        var mock = new MockHttpMessageHandler();
        mock.When(HttpMethod.Post, Root + "products/cursor/all").Respond("application/json",
            """
            {"nextCursor":null,"futureRoot":{"x":1},"results":[
              {"id":"p1","type":"SINGLE","name":"N","number":"A","futureField":[1,2],"publishInfoRef":"x",
               "attributes":[{"id":"a1","number":"sel","dataType":"single_select","values":["V"],"futureAttr":true,
                 "select":[{"id":"s1","number":"v","value":"V","metadata":{"nested":1},"futureOption":"x"}]}]}
            ]}
            """);

        var product = Assert.Single(await Client(mock).Products.GetAll().ToListAsync());

        Assert.Equal("V", product.Attributes.Get(new AttributeKey<string>("sel")));
        Assert.Equal("""{"nested":1}""", product.Attributes.Get(new AttributeKey<SelectOption>("sel"))!.Metadata);
    }

    [Fact]
    public void OmittedCollections_BecomeEmpty()
    {
        var product = ParseProduct("""{"id":"p1","number":"A","attributes":[{"id":"a1","number":"b","dataType":"boolean"}]}""");

        Assert.Empty(product.Labels);
        Assert.Equal("", product.Name);
        Assert.True(product.Attributes.Find("b")!.IsEmpty);
    }

    [Fact]
    public void UnknownProductType_IsUnknown() =>
        Assert.Equal(ProductType.Unknown, ParseProduct("""{"id":"p1","number":"A","type":"KIT"}""").Type);

    [Fact]
    public void UnknownDataType_IsStillReadable()
    {
        var attributes = ProductWith(Attribute("future", "hologram", "3D")).Attributes;

        Assert.Equal("hologram", attributes.Find("future")!.DataType);
        Assert.Equal("3D", attributes.Get(new AttributeKey<string>("future")));
    }

    [Fact]
    public void ProductWithoutNumber_IsAnExplicitError() =>
        Assert.Throws<System.Text.Json.JsonException>(() => ParseProduct("""{"id":"p1","type":"SINGLE"}"""));
}
