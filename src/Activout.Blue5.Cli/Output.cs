using System.Text.Json;
using System.Text.Json.Serialization;
using Activout.Blue5.Attributes;

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

    public static Task WriteLine(TextWriter writer, string text, CancellationToken cancellationToken) =>
        writer.WriteLineAsync(text.AsMemory(), cancellationToken);

    public static Task WriteJson<T>(TextWriter output, T value, bool indented, CancellationToken cancellationToken) =>
        WriteLine(output, JsonSerializer.Serialize(value, indented ? Indented : Compact), cancellationToken);

    /// <summary>Writes products as they arrive; nothing is buffered, so exports of any size stream.</summary>
    public static async Task WriteProducts(TextWriter output, IAsyncEnumerable<Product> products, string format, CancellationToken cancellationToken)
    {
        var first = true;
        if (format == "json") await output.WriteAsync("[".AsMemory(), cancellationToken);
        if (format == "table") await WriteLine(output, $"{"NUMBER",-30} {"TYPE",-8} NAME", cancellationToken);
        await foreach (var product in products.WithCancellation(cancellationToken))
        {
            await (format switch
            {
                "ndjson" => WriteLine(output, JsonSerializer.Serialize(product, Compact), cancellationToken),
                "json" => output.WriteAsync(((first ? "\n" : ",\n") + JsonSerializer.Serialize(product, Compact)).AsMemory(), cancellationToken),
                _ => WriteLine(output, $"{product.Number,-30} {product.Type,-8} {product.Name}", cancellationToken),
            });
            first = false;
        }

        if (format == "json") await WriteLine(output, first ? "]" : "\n]", cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    public static async Task WriteProductDetails(TextWriter output, Product p, CancellationToken cancellationToken)
    {
        List<(string Name, object? Value)> rows = [("Number", p.Number), ("Id", p.Id), ("Name", p.Name), ("Type", p.Type)];
        if (!string.IsNullOrEmpty(p.Description)) rows.Add(("Description", OneLine(p.Description, 100)));
        if (p.Labels.Count > 0) rows.Add(("Labels", string.Join(", ", p.Labels)));
        if (p.Categories.Count > 0) rows.Add(("Categories", string.Join(", ", p.Categories)));
        if (p.VariantParentId is not null) rows.Add(("VariantParentId", p.VariantParentId));
        if (p.GroupParentId is not null) rows.Add(("GroupParentId", p.GroupParentId));
        if (p.CreateDate is not null) rows.Add(("Created", p.CreateDate.Value.ToString("u")));
        if (p.LastUpdate is not null) rows.Add(("LastUpdate", p.LastUpdate.Value.ToString("u")));
        rows.Add(("Attributes", p.Attributes.Count));
        foreach (var (name, value) in rows) await WriteLine(output, $"{name,-16} {value}", cancellationToken);
    }

    public static async Task WriteContextTable(TextWriter output, IReadOnlyList<ContextInfo> contexts, CancellationToken cancellationToken)
    {
        var width = Math.Max("CONTEXT".Length, contexts.Count == 0 ? 0 : contexts.Max(c => c.Id.Length));
        await WriteLine(output, $"{"CONTEXT".PadRight(width)}  NAME", cancellationToken);
        foreach (var context in contexts) await WriteLine(output, $"{context.Id.PadRight(width)}  {context.Name}", cancellationToken);
    }

    public static async Task WriteAttributeTable(TextWriter output, IEnumerable<ProductAttribute> attributes, CancellationToken cancellationToken)
    {
        var rows = attributes.Select(a => (a.Number, Type: a.DataType ?? "", Value: OneLine(Display(a), 80))).ToList();
        var numberWidth = Math.Max("NUMBER".Length, rows.Count == 0 ? 0 : rows.Max(r => r.Number.Length));
        var typeWidth = Math.Max("TYPE".Length, rows.Count == 0 ? 0 : rows.Max(r => r.Type.Length));
        await WriteLine(output, $"{"NUMBER".PadRight(numberWidth)}  {"TYPE".PadRight(typeWidth)}  VALUE", cancellationToken);
        foreach (var row in rows)
        {
            await WriteLine(output, $"{row.Number.PadRight(numberWidth)}  {row.Type.PadRight(typeWidth)}  {row.Value}", cancellationToken);
        }
    }

    /// <summary>Plain value output: one value per line; column cells as name/value and matrix cells as column/row/value, tab-separated.</summary>
    public static async Task WriteAttributeText(TextWriter output, ProductAttribute a, CancellationToken cancellationToken)
    {
        foreach (var cell in a.Column) await WriteLine(output, $"{cell.Name}\t{cell.Value}", cancellationToken);
        foreach (var column in a.Matrix)
        foreach (var cell in column.Rows)
            await WriteLine(output, $"{column.Name}\t{cell.Name}\t{cell.Value}", cancellationToken);
        foreach (var value in DisplayValues(a)) await WriteLine(output, value, cancellationToken);
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
