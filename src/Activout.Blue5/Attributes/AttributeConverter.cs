using System.Globalization;

namespace Activout.Blue5.Attributes;

/// <summary>
/// Converts a non-empty <see cref="ProductAttribute"/> to a requested type. Scalars are parsed from the single
/// display value (the select/dictionary value for those types), so e.g. a numeric text attribute can be read as
/// <see cref="int"/>. Column and matrix values only convert to their structured types.
/// </summary>
internal static class AttributeConverter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static T Convert<T>(ProductAttribute attribute)
    {
        var target = typeof(T);
        var type = Nullable.GetUnderlyingType(target) ?? target;
        return (T)ConvertTo(attribute, type, target);
    }

    private static object ConvertTo(ProductAttribute a, Type type, Type target)
    {
        if (type == typeof(IReadOnlyList<AttributeCell>))
            return a.Column.Count > 0 ? a.Column : throw Fail(a, target, "it is not a column attribute");
        if (type == typeof(IReadOnlyList<MatrixColumn>))
            return a.Matrix.Count > 0 ? a.Matrix : throw Fail(a, target, "it is not a matrix attribute");
        if (type == typeof(IReadOnlyList<SelectOption>))
            return Options(a) is { Count: > 0 } options ? options : throw Fail(a, target, "it is not a select or dictionary attribute");
        if (type == typeof(SelectOption))
            return Options(a) switch
            {
                [var single] => single,
                [] => throw Fail(a, target, "it is not a select or dictionary attribute"),
                _ => throw Fail(a, target, "it has more than one selected option"),
            };

        if (a.Column.Count > 0 || a.Matrix.Count > 0)
            throw Fail(a, target, "column and matrix attributes only convert to IReadOnlyList<AttributeCell> or IReadOnlyList<MatrixColumn>");

        var values = DisplayValues(a);
        if (type == typeof(IReadOnlyList<string>))
            return values;

        if (values.Count != 1)
            throw Fail(a, target, $"it has {values.Count} values; use IReadOnlyList<string> or IReadOnlyList<SelectOption>");

        var s = values[0];
        object? result = type switch
        {
            _ when type == typeof(string) => s,
            _ when type == typeof(FormattedText) => new FormattedText(s, a.ContentType),
            _ when type == typeof(bool) => bool.TryParse(s, out var v) ? v : null,
            _ when type == typeof(int) => int.TryParse(s, NumberStyles.Integer, Invariant, out var v) ? v : null,
            _ when type == typeof(long) => long.TryParse(s, NumberStyles.Integer, Invariant, out var v) ? v : null,
            _ when type == typeof(decimal) => decimal.TryParse(s, NumberStyles.Float, Invariant, out var v) ? v : null,
            _ when type == typeof(double) => double.TryParse(s, NumberStyles.Float, Invariant, out var v) ? v : null,
            _ when type == typeof(DateOnly) => DateOnly.TryParseExact(s, "yyyy-MM-dd", Invariant, DateTimeStyles.None, out var v) ? v : null,
            _ when type == typeof(TimeOnly) => TimeOnly.TryParse(s, Invariant, DateTimeStyles.None, out var v) ? v : null,
            // PAPI date_time values carry no offset ("2020-01-01 9:00:00"), so the result has DateTimeKind.Unspecified.
            _ when type == typeof(DateTime) => DateTime.TryParse(s, Invariant, DateTimeStyles.None, out var v) ? v : null,
            _ => throw Fail(a, target, "unsupported target type"),
        };
        return result ?? throw Fail(a, target, $"'{s}' is not a valid {type.Name}");
    }

    private static IReadOnlyList<SelectOption> Options(ProductAttribute a) => a.Dictionary.Count > 0 ? a.Dictionary : a.Select;

    /// <summary>Select/dictionary display values when present, otherwise the raw values.</summary>
    private static IReadOnlyList<string> DisplayValues(ProductAttribute a) =>
        Options(a) is { Count: > 0 } options ? options.Select(o => o.Value ?? o.Number ?? o.Id).ToArray() : a.Values;

    private static AttributeConversionException Fail(ProductAttribute a, Type target, string reason) =>
        new(a.Number, target, a.DataType, Raw(a), reason);

    private static string Raw(ProductAttribute a)
    {
        if (a.Column.Count > 0) return $"column[{a.Column.Count}]";
        if (a.Matrix.Count > 0) return $"matrix[{a.Matrix.Count}]";
        var values = DisplayValues(a);
        var text = string.Join(", ", values.Select(v => $"'{(v.Length > 80 ? v[..80] + "…" : v)}'"));
        return values.Count == 1 ? text : $"[{text}]";
    }
}
