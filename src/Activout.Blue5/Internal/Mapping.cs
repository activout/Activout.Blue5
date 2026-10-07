using System.Text.Json;
using Activout.Blue5.Attributes;

namespace Activout.Blue5.Internal;

/// <summary>DTO → public model mapping. The only place that knows both shapes.</summary>
internal static class Mapping
{
    public static Product ToProduct(ProductDto dto) => new()
    {
        Id = dto.Id ?? throw Invalid("product without id", dto),
        Number = dto.Number ?? throw Invalid("product without number", dto),
        Name = dto.Name ?? "",
        Description = dto.Description,
        Type = ToProductType(dto.Type),
        Labels = dto.Labels ?? [],
        Categories = dto.Categories ?? [],
        VariantParentId = dto.VariantParentId,
        GroupParentId = dto.GroupParentId,
        LastUpdate = ToTimestamp(dto.LastUpdate),
        CreateDate = ToTimestamp(dto.CreateDate),
        Attributes = new ProductAttributes((dto.Attributes ?? []).Select(ToAttribute)),
    };

    public static ProductAttribute ToAttribute(AttributeDto dto) => new()
    {
        Number = dto.Number ?? dto.Id ?? throw new JsonException("PAPI returned an attribute without number and id."),
        Id = dto.Id,
        Name = dto.Name,
        DataType = dto.DataType,
        ContentType = dto.ContentType,
        Unit = dto.Unit,
        GroupNumber = dto.GroupNumber,
        GroupName = dto.GroupName,
        IsCompound = dto.IsCompound ?? false,
        IsDefining = dto.DefiningAttribute ?? false,
        ValueType = dto.ValueType,
        Values = (dto.Values ?? []).OfType<string>().ToArray(),
        Select = (dto.Select ?? []).Select(ToOption).ToArray(),
        Dictionary = (dto.Dictionary ?? []).Select(ToOption).ToArray(),
        Column = (dto.Column ?? []).Select(ToCell).ToArray(),
        Matrix = (dto.Matrix ?? []).Select(m => new MatrixColumn(m.Id ?? "", m.Name, (m.Rows ?? []).Select(ToCell).ToArray())).ToArray(),
    };

    public static ContextInfo ToContext(ContextDto dto) =>
        new(dto.Context ?? throw new JsonException("PAPI returned a context without id."), dto.ContextName);

    public static string ToPapi(ProductType type) => type switch
    {
        ProductType.Single => "SINGLE",
        ProductType.Variant => "VARIANT",
        ProductType.Group => "GROUP",
        ProductType.Bundle => "BUNDLE",
        ProductType.Family => "FAMILY",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Only known product types can be used in a query."),
    };

    private static ProductType ToProductType(string? type) => type switch
    {
        "SINGLE" => ProductType.Single,
        "VARIANT" => ProductType.Variant,
        "GROUP" => ProductType.Group,
        "BUNDLE" => ProductType.Bundle,
        "FAMILY" => ProductType.Family,
        _ => ProductType.Unknown,
    };

    public static SelectOption ToOption(SelectDto dto) => new(
        dto.Id ?? "",
        dto.Number,
        dto.Value,
        dto.Metadata is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } m
            ? m.ValueKind == JsonValueKind.String ? m.GetString() : m.GetRawText()
            : null);

    private static AttributeCell ToCell(CellDto dto) => new(dto.Id ?? "", dto.Name, dto.Value);

    private static DateTimeOffset? ToTimestamp(double? epochMilliseconds) =>
        epochMilliseconds is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds((long)ms) : null;

    private static JsonException Invalid(string what, ProductDto dto) =>
        new($"PAPI returned a {what} (id '{dto.Id}', number '{dto.Number}').");
}
