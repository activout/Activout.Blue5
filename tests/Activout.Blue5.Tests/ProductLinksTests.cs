using System.Text.Json;
using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class ProductLinksTests
{
    private static readonly Product Full = ParseProduct(Fixture("all-attribute-types.json"));

    [Fact]
    public void Relations()
    {
        Assert.Equal(new ProductRelation("r1", "Accessories", "accessories", "p-acc", false, RelationDirection.OneWay), Full.Relations[0]);
        Assert.Equal(new ProductRelation("r2", null, null, "p-sim", true, RelationDirection.TwoWay), Full.Relations[1]);
    }

    [Fact]
    public void Metadata_StringAsIs_OtherJsonAsRawText()
    {
        Assert.Equal(new ProductMetadata("origin", "SE"), Full.Metadata[0]);
        Assert.Equal(new ProductMetadata("weights", "[1, 2]"), Full.Metadata[1]);
    }

    [Fact]
    public void Bundles() => Assert.Equal([new ProductBundleItem("p-part", 2m)], Full.Bundles);

    [Fact]
    public void VariantsAndGroups()
    {
        Assert.Equal(["v1", "v2"], Full.Variants);
        Assert.Equal(["g1"], Full.Groups);
    }

    [Fact]
    public void SortingOrderSource() => Assert.Equal(RelationSortingOrderSource.RelationDefinition, Full.RelationSortingOrderSource);

    [Fact]
    public void Absent_GivesEmptyListsAndUnknown()
    {
        var p = ProductWith();
        Assert.Empty(p.Relations);
        Assert.Empty(p.Metadata);
        Assert.Empty(p.Bundles);
        Assert.Empty(p.Variants);
        Assert.Empty(p.Groups);
        Assert.Equal(RelationSortingOrderSource.Unknown, p.RelationSortingOrderSource);
    }

    [Fact]
    public void RelationWithoutProductId_Throws()
    {
        var json = Product("P").Replace("\"attributes\"", "\"relations\":[{\"id\":\"r\"}],\"attributes\"");
        Assert.Throws<JsonException>(() => ParseProduct(json));
    }
}
