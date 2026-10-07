namespace Activout.Blue5;

/// <summary>A relation from a product to another product.</summary>
/// <param name="Id">Relation definition id.</param>
/// <param name="Name">Relation name, if any.</param>
/// <param name="Number">Relation business key, if any.</param>
/// <param name="ProductId">Id of the related product.</param>
/// <param name="Reverse">True when this is the reverse side of a two-way relation.</param>
/// <param name="Direction">Whether the relation is one-way or two-way.</param>
public sealed record ProductRelation(string Id, string? Name, string? Number, string ProductId, bool Reverse, RelationDirection Direction);

/// <summary>Direction of a <see cref="ProductRelation"/>.</summary>
public enum RelationDirection
{
    /// <summary>A direction this version of Blue5 does not know about.</summary>
    Unknown = 0,

    /// <summary><c>ONE_WAY</c></summary>
    OneWay,

    /// <summary><c>TWO_WAY</c></summary>
    TwoWay,
}

/// <summary>Where the order of a product's related products comes from.</summary>
public enum RelationSortingOrderSource
{
    /// <summary>Not supplied, or a value this version of Blue5 does not know about.</summary>
    Unknown = 0,

    /// <summary><c>RELATION_DEFINITION</c></summary>
    RelationDefinition,

    /// <summary><c>PRODUCT</c></summary>
    Product,
}
