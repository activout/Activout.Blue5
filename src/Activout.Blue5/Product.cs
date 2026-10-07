using Activout.Blue5.Attributes;

namespace Activout.Blue5;

/// <summary>A product as published to PAPI in one context.</summary>
/// <remarks>Properties are <c>init</c> so you can build products yourself in tests.</remarks>
public sealed record Product
{
    /// <summary>PIM-generated identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Unique business key. Always a string; never treat it as numeric.</summary>
    public required string Number { get; init; }

    /// <summary>Product name in the current context.</summary>
    public string Name { get; init; } = "";

    /// <summary>Product description, if any.</summary>
    public string? Description { get; init; }

    /// <summary>Product type.</summary>
    public ProductType Type { get; init; }

    /// <summary>Label names.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Ids of the categories the product is assigned to.</summary>
    public IReadOnlyList<string> Categories { get; init; } = [];

    /// <summary>Id of the parent product when this is a variant.</summary>
    public string? VariantParentId { get; init; }

    /// <summary>Id of the parent product when this belongs to a group.</summary>
    public string? GroupParentId { get; init; }

    /// <summary>When the product was last updated.</summary>
    public DateTimeOffset? LastUpdate { get; init; }

    /// <summary>When the product was created.</summary>
    public DateTimeOffset? CreateDate { get; init; }

    /// <summary>The product's attribute values, indexed by attribute number.</summary>
    public ProductAttributes Attributes { get; init; } = ProductAttributes.Empty;

    /// <summary>Media assets (images, documents, ...) attached to the product, in PAPI order.</summary>
    public IReadOnlyList<MediaAsset> Media { get; init; } = [];
}

/// <summary>Bluestone product type.</summary>
public enum ProductType
{
    /// <summary>A type this version of Blue5 does not know about.</summary>
    Unknown = 0,

    /// <summary><c>SINGLE</c></summary>
    Single,

    /// <summary><c>VARIANT</c></summary>
    Variant,

    /// <summary><c>GROUP</c></summary>
    Group,

    /// <summary><c>BUNDLE</c></summary>
    Bundle,

    /// <summary><c>FAMILY</c></summary>
    Family,
}
