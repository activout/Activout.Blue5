using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

/// <summary>Every attribute type documented at https://help.bluestonepim.com/attribute-types-in-public-api.</summary>
public class AttributeTypeTests
{
    private static readonly ProductAttributes Attributes = ParseProduct(Fixture("all-attribute-types.json")).Attributes;

    [Fact]
    public void Fixture_CoversEveryDocumentedDataType()
    {
        string[] documented =
        [
            AttributeDataTypes.Boolean, AttributeDataTypes.Column, AttributeDataTypes.Date, AttributeDataTypes.DateTime,
            AttributeDataTypes.Decimal, AttributeDataTypes.Dictionary, AttributeDataTypes.FormattedText, AttributeDataTypes.Integer,
            AttributeDataTypes.Matrix, AttributeDataTypes.MultiSelect, AttributeDataTypes.Multiline, AttributeDataTypes.Pattern,
            AttributeDataTypes.SingleSelect, AttributeDataTypes.Text, AttributeDataTypes.Time,
        ];
        Assert.Equal(documented.Order(), Attributes.Select(a => a.DataType!).Distinct().Order());
    }

    [Fact]
    public void Boolean()
    {
        Assert.True(Attributes.Get(new AttributeKey<bool>("boolean")));
        Assert.True(Attributes.Get(new AttributeKey<bool?>("boolean")));
        Assert.Equal("true", Attributes.Get(new AttributeKey<string>("boolean")));
    }

    [Fact]
    public void Integer()
    {
        Assert.Equal(42, Attributes.Get(new AttributeKey<int>("integer")));
        Assert.Equal(42L, Attributes.Get(new AttributeKey<long?>("integer")));
        Assert.Equal(42m, Attributes.Get(new AttributeKey<decimal>("integer")));
    }

    [Fact]
    public void Decimal()
    {
        Assert.Equal(198233.6875m, Attributes.Get(new AttributeKey<decimal?>("decimal")));
        Assert.Equal(198233.6875d, Attributes.Get(new AttributeKey<double>("decimal")));
        Assert.Equal("kg", Attributes.Find("decimal")!.Unit);
    }

    [Fact]
    public void Date() => Assert.Equal(new DateOnly(2020, 1, 1), Attributes.Get(new AttributeKey<DateOnly?>("date")));

    [Fact]
    public void DateTime_HasUnspecifiedKind_BecausePapiSendsNoOffset()
    {
        var value = Attributes.Get(new AttributeKey<DateTime>("date_time"));
        Assert.Equal(new DateTime(2020, 1, 1, 9, 0, 0), value);
        Assert.Equal(DateTimeKind.Unspecified, value.Kind);
    }

    [Fact]
    public void Time() => Assert.Equal(new TimeOnly(11, 5, 31), Attributes.Get(new AttributeKey<TimeOnly>("time")));

    [Theory]
    [InlineData("text", "Example text")]
    [InlineData("multiline", "This text has \nmultiple lines.")]
    [InlineData("pattern", "ABC-123")]
    [InlineData("compound", "42 Example text")]
    [InlineData("single_select", "Value A")]
    public void TextLikeTypes_ReadAsString(string number, string expected) =>
        Assert.Equal(expected, Attributes.Get(new AttributeKey<string>(number, "")));

    [Fact]
    public void Compound_IsFlagged() => Assert.True(Attributes.Find("compound")!.IsCompound);

    [Theory]
    [InlineData("markdown", "text/markdown", "# Header example")]
    [InlineData("html", "text/html", "<h2>Headline</h2>")]
    public void FormattedText_KeepsContentType_AndReadsLossilyAsString(string number, string contentType, string prefix)
    {
        var text = Attributes.Get(new AttributeKey<FormattedText>(number))!;
        Assert.Equal(contentType, text.ContentType);
        Assert.StartsWith(prefix, text.Content);
        Assert.Equal(text.Content, Attributes.Get(new AttributeKey<string>(number)));
    }

    [Fact]
    public void SingleSelect_AsOption()
    {
        Assert.Equal(new SelectOption("s1", "value-a-code", "Value A"), Attributes.Get(new AttributeKey<SelectOption>("single_select")));
        Assert.Equal(["Value A"], Attributes.Get(new AttributeKey<IReadOnlyList<string>>("single_select"))!);
    }

    [Fact]
    public void MultiSelect_AsOptionsAndStrings()
    {
        var options = Attributes.Get(new AttributeKey<IReadOnlyList<SelectOption>>("multi_select"))!;
        Assert.Equal(["value-a-code", "value-b-code"], options.Select(o => o.Number));
        Assert.Equal("""{"hex":"#fff"}""", options[1].Metadata);
        Assert.Equal(["Value A", "Value B"], Attributes.Get(new AttributeKey<IReadOnlyList<string>>("multi_select"))!);
    }

    [Fact]
    public void Dictionary_SharesSelectOptionModel()
    {
        var entries = Attributes.Get(new AttributeKey<IReadOnlyList<SelectOption>>("dictionary"))!;
        Assert.Equal([new SelectOption("d1", "first-code", "First"), new SelectOption("d2", "second-code", "Second")], entries);
        Assert.Equal(["First", "Second"], Attributes.Get(new AttributeKey<IReadOnlyList<string>>("dictionary"))!);
    }

    [Fact]
    public void Column()
    {
        var cells = Attributes.Get(new AttributeKey<IReadOnlyList<AttributeCell>>("column"))!;
        Assert.Equal([new AttributeCell("c1", "First", "First cell"), new AttributeCell("c2", "Second", "Second cell")], cells);
    }

    [Fact]
    public void Matrix()
    {
        var columns = Attributes.Get(new AttributeKey<IReadOnlyList<MatrixColumn>>("matrix"))!;
        Assert.Equal(["First column", "Second column"], columns.Select(c => c.Name));
        Assert.Equal(new AttributeCell("r2", "Second row", "Fourth cell"), columns[1].Rows[1]);
    }
}
