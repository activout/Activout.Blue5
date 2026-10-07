namespace Activout.Blue5;

/// <summary>One product in a bundle.</summary>
/// <param name="ProductId">Id of the bundled product.</param>
/// <param name="Quantity">How many of it the bundle contains.</param>
public sealed record ProductBundleItem(string ProductId, decimal Quantity);
