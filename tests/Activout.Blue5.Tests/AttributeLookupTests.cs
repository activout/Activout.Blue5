using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class AttributeLookupTests
{
    private static readonly AttributeKey<string> Colour = new("colour", string.Empty);
    private static readonly AttributeKey<decimal?> Weight = new("weight");

    [Fact]
    public void Absent_ReturnsDefault_IncludingStringEmpty()
    {
        var attributes = ProductWith().Attributes;

        Assert.Equal(string.Empty, attributes.Get(Colour));
        Assert.Null(attributes.Get(Weight));
        Assert.Equal(7, attributes.Get(new AttributeKey<int>("qty", 7)));
    }

    [Fact]
    public void PresentWithoutValues_ReturnsDefault()
    {
        var attributes = ProductWith(Attribute("colour", "text")).Attributes;

        Assert.True(attributes.Contains("colour"));
        Assert.Equal(string.Empty, attributes.Get(Colour));
    }

    [Fact]
    public void Present_ReturnsValue()
    {
        var attributes = ProductWith(Attribute("colour", "text", "Red"), Attribute("weight", "decimal", "1.5")).Attributes;

        Assert.Equal("Red", attributes.Get(Colour));
        Assert.Equal(1.5m, attributes.Get(Weight));
    }

    [Fact]
    public void Lookup_IsOrdinal()
    {
        var attributes = ProductWith(Attribute("Colour", "text", "Red")).Attributes;

        Assert.Equal(string.Empty, attributes.Get(Colour));
        Assert.Null(attributes.Find("colour"));
        Assert.NotNull(attributes.Find("Colour"));
    }

    [Fact]
    public void DuplicateNumbers_FirstWinsForLookup_AllKeptForEnumeration()
    {
        var attributes = ProductWith(Attribute("colour", "text", "Red"), Attribute("colour", "text", "Blue")).Attributes;

        Assert.Equal("Red", attributes.Get(Colour));
        Assert.Equal(2, attributes.Count);
    }

    [Fact]
    public void Find_ExposesRawAttribute()
    {
        var raw = ProductWith("""{"id":"a1","number":"colour","name":"Colour","dataType":"text","groupNumber":"g","groupName":"G","values":["Red"]}""")
            .Attributes.Find("colour")!;

        Assert.Equal(("a1", "Colour", "text", "g", "G"), (raw.Id, raw.Name, raw.DataType, raw.GroupNumber, raw.GroupName));
        Assert.Equal(["Red"], raw.Values);
    }

    [Fact]
    public void ConsumersCanBuildProductsForTheirOwnTests()
    {
        var product = new Product
        {
            Id = "1",
            Number = "A",
            Attributes = new ProductAttributes([new ProductAttribute { Number = "colour", Values = ["Red"] }]),
        };

        Assert.Equal("Red", product.Attributes.Get(Colour));
    }
}
