namespace Activout.Blue5;

/// <summary>A metadata entry on a product.</summary>
/// <param name="Id">Metadata key.</param>
/// <param name="Value">The value as PAPI supplied it (raw JSON text when not a string), or null.</param>
public sealed record ProductMetadata(string Id, string? Value);
