using System.Text.Json;

namespace Activout.Blue5.Internal;

// Hand-written PAPI DTOs covering only what Blue5 uses. Unknown JSON properties are ignored
// (System.Text.Json default), and every collection is nullable because PAPI omits empty ones.

internal sealed record ProductDto(
    string? Id,
    string? Type,
    string? Name,
    string? Number,
    string? Description,
    List<AttributeDto>? Attributes,
    List<string>? Labels,
    List<string>? Categories,
    string? VariantParentId,
    string? GroupParentId,
    double? LastUpdate,
    double? CreateDate);

internal sealed record AttributeDto(
    string? Id,
    string? Name,
    string? Number,
    string? Unit,
    string? GroupName,
    string? GroupNumber,
    string? DataType,
    string? ContentType,
    bool? IsCompound,
    List<string?>? Values,
    List<SelectDto>? Select,
    List<SelectDto>? Dictionary,
    List<CellDto>? Column,
    List<MatrixColumnDto>? Matrix);

internal sealed record SelectDto(string? Id, string? Number, string? Value, JsonElement? Metadata);

internal sealed record CellDto(string? Id, string? Name, string? Value);

internal sealed record MatrixColumnDto(string? Id, string? Name, List<CellDto>? Rows);

internal sealed record CursorPageDto(string? NextCursor, List<ProductDto>? Results);

internal sealed record PaginatedDto(long? TotalCount, List<ProductDto>? Results);

internal sealed record ResultsDto(List<ProductDto>? Results);

internal sealed record ContextDto(string? Context, string? ContextName);

internal sealed record ErrorDto(string? Message, string? EntityId, List<string>? EntityIds);

internal sealed record CursorQueryDto(string? Cursor, int Limit);

internal sealed record NumbersRequestDto(IReadOnlyList<string> Numbers);

internal sealed record FilteringCriteriaDto(
    IReadOnlyList<string>? Numbers,
    IReadOnlyList<string>? Names,
    IReadOnlyList<string>? Labels,
    IReadOnlyList<string>? Categories,
    IReadOnlyList<AttributeFilterDto>? AttributeFilters,
    IReadOnlyList<string>? ProductTypes);

internal sealed record AttributeFilterDto(string Number, string Value);
