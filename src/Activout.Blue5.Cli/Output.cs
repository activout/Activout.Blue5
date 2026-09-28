using System.Text.Json;
using System.Text.Json.Serialization;

namespace Activout.Blue5.Cli;

/// <summary>Formatting of public Blue5 models for the terminal.</summary>
internal static class Output
{
    private static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions Indented = new(Compact) { WriteIndented = true };

    public static void WriteJson<T>(TextWriter output, T value, bool indented) =>
        output.WriteLine(JsonSerializer.Serialize(value, indented ? Indented : Compact));

    /// <summary>Writes products as they arrive; nothing is buffered, so exports of any size stream.</summary>
    public static async Task WriteProducts(TextWriter output, IAsyncEnumerable<Product> products, string format, CancellationToken cancellationToken)
    {
        var first = true;
        if (format == "json") output.Write('[');
        if (format == "table") output.WriteLine($"{"NUMBER",-30} {"TYPE",-8} NAME");
        await foreach (var product in products.WithCancellation(cancellationToken))
        {
            switch (format)
            {
                case "ndjson":
                    output.WriteLine(JsonSerializer.Serialize(product, Compact));
                    break;
                case "json":
                    output.Write(first ? "\n" : ",\n");
                    output.Write(JsonSerializer.Serialize(product, Compact));
                    break;
                default:
                    output.WriteLine($"{product.Number,-30} {product.Type,-8} {product.Name}");
                    break;
            }

            first = false;
        }

        if (format == "json") output.WriteLine(first ? "]" : "\n]");
        await output.FlushAsync(cancellationToken);
    }

    public static void WriteProductDetails(TextWriter output, Product p)
    {
        void Row(string name, object? value) => output.WriteLine($"{name,-16} {value}");
        Row("Number", p.Number);
        Row("Id", p.Id);
        Row("Name", p.Name);
        Row("Type", p.Type);
        if (!string.IsNullOrEmpty(p.Description)) Row("Description", OneLine(p.Description, 100));
        if (p.Labels.Count > 0) Row("Labels", string.Join(", ", p.Labels));
        if (p.Categories.Count > 0) Row("Categories", string.Join(", ", p.Categories));
        if (p.VariantParentId is not null) Row("VariantParentId", p.VariantParentId);
        if (p.GroupParentId is not null) Row("GroupParentId", p.GroupParentId);
        if (p.CreateDate is not null) Row("Created", p.CreateDate.Value.ToString("u"));
        if (p.LastUpdate is not null) Row("LastUpdate", p.LastUpdate.Value.ToString("u"));
        Row("Attributes", p.Attributes.Count);
    }

    public static void WriteAttributeTable(TextWriter output, IEnumerable<ProductAttribute> attributes)
    {
        var rows = attributes.Select(a => (a.Number, Type: a.DataType ?? "", Value: OneLine(Display(a), 80))).ToList();
        var numberWidth = Math.Max("NUMBER".Length, rows.Count == 0 ? 0 : rows.Max(r => r.Number.Length));
        var typeWidth = Math.Max("TYPE".Length, rows.Count == 0 ? 0 : rows.Max(r => r.Type.Length));
        output.WriteLine($"{"NUMBER".PadRight(numberWidth)}  {"TYPE".PadRight(typeWidth)}  VALUE");
        foreach (var row in rows)
        {
            output.WriteLine($"{row.Number.PadRight(numberWidth)}  {row.Type.PadRight(typeWidth)}  {row.Value}");
        }
    }

    /// <summary>Plain value output: one value per line; column cells as name/value and matrix cells as column/row/value, tab-separated.</summary>
    public static void WriteAttributeText(TextWriter output, ProductAttribute a)
    {
        foreach (var cell in a.Column) output.WriteLine($"{cell.Name}\t{cell.Value}");
        foreach (var column in a.Matrix)
        foreach (var cell in column.Rows)
            output.WriteLine($"{column.Name}\t{cell.Name}\t{cell.Value}");
        foreach (var value in DisplayValues(a)) output.WriteLine(value);
    }

    private static string Display(ProductAttribute a)
    {
        if (a.Column.Count > 0) return string.Join("; ", a.Column.Select(c => $"{c.Name}={c.Value}"));
        if (a.Matrix.Count > 0) return string.Join("; ", a.Matrix.SelectMany(c => c.Rows.Select(r => $"{c.Name}/{r.Name}={r.Value}")));
        return string.Join(", ", DisplayValues(a));
    }

    private static IEnumerable<string> DisplayValues(ProductAttribute a)
    {
        if (a.Column.Count > 0 || a.Matrix.Count > 0) return [];
        var options = a.Dictionary.Count > 0 ? a.Dictionary : a.Select;
        return options.Count > 0 ? options.Select(o => o.Value ?? o.Number ?? o.Id) : a.Values;
    }

    private static string OneLine(string value, int max)
    {
        var line = value.ReplaceLineEndings("⏎");
        return line.Length > max ? line[..(max - 1)] + "…" : line;
    }
}
