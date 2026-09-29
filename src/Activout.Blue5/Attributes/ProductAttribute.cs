using System.Text.Json.Serialization;

namespace Activout.Blue5.Attributes;

/// <summary>
/// One attribute value on a product, close to what PAPI returns. Use it to inspect attributes that
/// no typed conversion covers; prefer <see cref="ProductAttributes.Get{T}"/> otherwise.
/// </summary>
public sealed record ProductAttribute
{
    /// <summary>Attribute number (the key used by <see cref="ProductAttributes"/>). Falls back to the id if PAPI omits it.</summary>
    public required string Number { get; init; }

    /// <summary>Attribute definition id.</summary>
    public string? Id { get; init; }

    /// <summary>Attribute display name.</summary>
    public string? Name { get; init; }

    /// <summary>PAPI data type such as <c>text</c> or <c>single_select</c>; see <see cref="AttributeDataTypes"/>.</summary>
    public string? DataType { get; init; }

    /// <summary>Content type for formatted text: <c>text/markdown</c> or <c>text/html</c>.</summary>
    public string? ContentType { get; init; }

    /// <summary>Unit, if the attribute has one.</summary>
    public string? Unit { get; init; }

    /// <summary>Attribute group number.</summary>
    public string? GroupNumber { get; init; }

    /// <summary>Attribute group name.</summary>
    public string? GroupName { get; init; }

    /// <summary>Whether this is a compound attribute (its value is composed from other attributes).</summary>
    public bool IsCompound { get; init; }

    /// <summary>Whether this attribute defines the variants of a product group (PAPI <c>definingAttribute</c>).</summary>
    public bool IsDefining { get; init; }

    /// <summary>
    /// PAPI <c>valueType</c>, a hint on how to interpret the value. Observed: <c>color</c> on selects, where each
    /// <see cref="SelectOption.Metadata"/> holds a hex colour such as <c>#ff0000</c>.
    /// </summary>
    public string? ValueType { get; init; }

    /// <summary>Raw string values. Scalar types have at most one; select types repeat the display values.</summary>
    public IReadOnlyList<string> Values { get; init; } = [];

    /// <summary>Selected options for <c>single_select</c>/<c>multi_select</c>.</summary>
    public IReadOnlyList<SelectOption> Select { get; init; } = [];

    /// <summary>Selected entries for <c>dictionary</c>.</summary>
    public IReadOnlyList<SelectOption> Dictionary { get; init; } = [];

    /// <summary>Cells for <c>column</c>.</summary>
    public IReadOnlyList<AttributeCell> Column { get; init; } = [];

    /// <summary>Columns (each with rows) for <c>matrix</c>.</summary>
    public IReadOnlyList<MatrixColumn> Matrix { get; init; } = [];

    /// <summary><see langword="true"/> when the attribute carries no value of any kind.</summary>
    [JsonIgnore]
    public bool IsEmpty => Values.Count == 0 && Select.Count == 0 && Dictionary.Count == 0 && Column.Count == 0 && Matrix.Count == 0;
}

/// <summary>A selected option of a select or dictionary attribute.</summary>
/// <param name="Id">Option id.</param>
/// <param name="Number">Option number (business key), if any.</param>
/// <param name="Value">Display value in the current context.</param>
/// <param name="Metadata">Option metadata as supplied by PAPI (raw JSON when not a string).</param>
public sealed record SelectOption(string Id, string? Number, string? Value, string? Metadata = null);

/// <summary>A named cell: one entry of a <c>column</c> attribute, or one row of a <see cref="MatrixColumn"/>.</summary>
/// <param name="Id">Cell (column or row) id.</param>
/// <param name="Name">Column or row name.</param>
/// <param name="Value">Cell value.</param>
public sealed record AttributeCell(string Id, string? Name, string? Value);

/// <summary>One column of a <c>matrix</c> attribute.</summary>
/// <param name="Id">Column id.</param>
/// <param name="Name">Column name.</param>
/// <param name="Rows">The column's cells, one per row.</param>
public sealed record MatrixColumn(string Id, string? Name, IReadOnlyList<AttributeCell> Rows);

/// <summary>A <c>formatted_text</c> value.</summary>
/// <param name="Content">Markdown or HTML source.</param>
/// <param name="ContentType"><c>text/markdown</c>, <c>text/html</c>, or <see langword="null"/> if not supplied.</param>
public sealed record FormattedText(string Content, string? ContentType)
{
    /// <summary>Returns <see cref="Content"/>.</summary>
    public override string ToString() => Content;
}

/// <summary>PAPI attribute <c>dataType</c> values documented by Bluestone.</summary>
public static class AttributeDataTypes
{
    /// <summary><c>boolean</c></summary>
    public const string Boolean = "boolean";
    /// <summary><c>column</c></summary>
    public const string Column = "column";
    /// <summary><c>date</c> (<c>yyyy-MM-dd</c>)</summary>
    public const string Date = "date";
    /// <summary><c>date_time</c> (e.g. <c>2020-01-01 9:00:00</c>, no offset)</summary>
    public const string DateTime = "date_time";
    /// <summary><c>decimal</c></summary>
    public const string Decimal = "decimal";
    /// <summary><c>dictionary</c></summary>
    public const string Dictionary = "dictionary";
    /// <summary><c>formatted_text</c> (Markdown or HTML)</summary>
    public const string FormattedText = "formatted_text";
    /// <summary><c>integer</c></summary>
    public const string Integer = "integer";
    /// <summary><c>matrix</c></summary>
    public const string Matrix = "matrix";
    /// <summary><c>multi_select</c></summary>
    public const string MultiSelect = "multi_select";
    /// <summary><c>multiline</c></summary>
    public const string Multiline = "multiline";
    /// <summary><c>pattern</c> (regular expression constrained text)</summary>
    public const string Pattern = "pattern";
    /// <summary><c>single_select</c></summary>
    public const string SingleSelect = "single_select";
    /// <summary><c>text</c> (also used for compound attributes)</summary>
    public const string Text = "text";
    /// <summary><c>time</c> (<c>HH:mm:ss</c>)</summary>
    public const string Time = "time";
}
