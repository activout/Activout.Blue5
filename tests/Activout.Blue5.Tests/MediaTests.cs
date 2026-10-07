using Activout.Blue5.Attributes;
using System.Text.Json;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class MediaTests
{
    private static readonly Product Product = ParseProduct(Fixture("all-attribute-types.json"));

    [Fact]
    public void MapsEveryField()
    {
        var m = Product.Media[0];
        Assert.Equal("m1", m.Id);
        Assert.Equal("m-1", m.Number);
        Assert.Equal("Front view", m.Name);
        Assert.Equal("Front of the product", m.Description);
        Assert.Equal("front.webp", m.FileName);
        Assert.Equal("image/webp", m.ContentType);
        Assert.Equal(new Uri("https://media.example.test/m1/front.webp"), m.DownloadUri);
        Assert.Equal("f=jpg&w=400", m.PreviewUri.Query.TrimStart('?'));
        Assert.Equal(["web"], m.Labels);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1711534898057), m.CreateDate);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1774870853227), m.UpdateDate);
        Assert.Equal("654321", m.Attributes.Get(new AttributeKey<string>("SKUS")));
    }

    [Fact]
    public void OptionalFieldsMayBeMissing()
    {
        var m = Product.Media[1];
        Assert.Null(m.Number);
        Assert.Null(m.FileName);
        Assert.Null(m.CreateDate);
        Assert.Empty(m.Attributes);
    }

    [Fact]
    public void ProductWithoutMedia_HasEmptyList() => Assert.Empty(ProductWith().Media);

    [Fact]
    public void InvalidDownloadUri_Throws()
    {
        var json = Product("P").Replace("\"attributes\"", "\"media\":[{\"id\":\"x\",\"name\":\"n\",\"contentType\":\"a/b\",\"previewUri\":\"https://x.test/p\"}],\"attributes\"");
        Assert.Throws<JsonException>(() => ParseProduct(json));
    }
}
