using static Activout.Blue5.Tests.Support.Fake;

namespace Activout.Blue5.Tests;

public class ConversionErrorTests
{
    [Fact]
    public void InvalidScalar_ReportsNumberTargetTypeDataTypeAndValue()
    {
        var attributes = ProductWith(Attribute("qty", "text", "abc")).Attributes;

        var ex = Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<int?>("qty")));

        Assert.Equal(("qty", typeof(int?), "text", "'abc'"), (ex.AttributeNumber, ex.TargetType, ex.DataType, ex.RawValue));
        Assert.Equal("Cannot convert attribute 'qty' (dataType 'text', value 'abc') to Nullable<Int32>: 'abc' is not a valid Int32", ex.Message);
    }

    [Theory]
    [InlineData("decimal", "12,5")]
    [InlineData("date", "01/02/2020")]
    [InlineData("boolean", "yes")]
    [InlineData("time", "25:00:00")]
    public void UnparseableValues_Throw(string dataType, string value)
    {
        var attributes = ProductWith(Attribute("a", dataType, value)).Attributes;

        Assert.Throws<AttributeConversionException>(() => dataType switch
        {
            "decimal" => attributes.Get(new AttributeKey<decimal>("a")),
            "date" => attributes.Get(new AttributeKey<DateOnly>("a")),
            "boolean" => attributes.Get(new AttributeKey<bool>("a")),
            _ => (object)attributes.Get(new AttributeKey<TimeOnly>("a")),
        });
    }

    [Fact]
    public void MultipleValues_ToScalar_Throws()
    {
        var attributes = ParseProduct(Fixture("all-attribute-types.json")).Attributes;

        var ex = Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<string>("multi_select")));
        Assert.Contains("it has 2 values", ex.Message);
        Assert.Equal("['Value A', 'Value B']", ex.RawValue);
        Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<SelectOption>("multi_select")));
    }

    [Theory]
    [InlineData("column")]
    [InlineData("matrix")]
    public void StructuredTypes_DoNotPretendToBeScalars(string number)
    {
        var attributes = ParseProduct(Fixture("all-attribute-types.json")).Attributes;

        Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<string>(number)));
        Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<IReadOnlyList<string>>(number)));
    }

    [Fact]
    public void IncompatibleStructuredTargets_Throw()
    {
        var attributes = ParseProduct(Fixture("all-attribute-types.json")).Attributes;

        Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<IReadOnlyList<SelectOption>>("text")));
        Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<IReadOnlyList<AttributeCell>>("matrix")));
        Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<IReadOnlyList<MatrixColumn>>("column")));
    }

    [Fact]
    public void UnsupportedTargetType_Throws()
    {
        var attributes = ProductWith(Attribute("id", "text", "8c1f0b8e-0000-0000-0000-000000000000")).Attributes;

        var ex = Assert.Throws<AttributeConversionException>(() => attributes.Get(new AttributeKey<Guid>("id")));
        Assert.Contains("unsupported target type", ex.Message);
    }
}
