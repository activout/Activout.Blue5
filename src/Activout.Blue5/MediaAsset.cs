using Activout.Blue5.Attributes;

namespace Activout.Blue5;

/// <summary>A media asset (image, document, ...) attached to a product.</summary>
/// <remarks>Blue5 only describes the asset. Fetch <see cref="DownloadUri"/> yourself to get the file.</remarks>
public sealed record MediaAsset
{
    /// <summary>Asset identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Asset business key. Always a string.</summary>
    public string? Number { get; init; }

    /// <summary>Asset name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Asset description, if any.</summary>
    public string? Description { get; init; }

    /// <summary>Original file name, if known.</summary>
    public string? FileName { get; init; }

    /// <summary>MIME type, such as <c>image/webp</c>.</summary>
    public string ContentType { get; init; } = "";

    /// <summary>URL of the original file.</summary>
    public required Uri DownloadUri { get; init; }

    /// <summary>URL of a small preview rendition (a 400px-wide JPEG for images).</summary>
    public required Uri PreviewUri { get; init; }

    /// <summary>Label names.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>When the asset was created.</summary>
    public DateTimeOffset? CreateDate { get; init; }

    /// <summary>When the asset was last updated.</summary>
    public DateTimeOffset? UpdateDate { get; init; }

    /// <summary>The asset's own attribute values, indexed by attribute number.</summary>
    public ProductAttributes Attributes { get; init; } = ProductAttributes.Empty;
}
