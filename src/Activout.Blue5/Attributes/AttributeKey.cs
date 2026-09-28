namespace Activout.Blue5.Attributes;

/// <summary>
/// A typed reference to a Bluestone attribute, used with <see cref="ProductAttributes.Get{T}"/>.
/// Define these once as static fields and reuse them.
/// </summary>
/// <typeparam name="T">
/// Target type: <see cref="string"/>, <see cref="bool"/>, <see cref="int"/>, <see cref="long"/>, <see cref="decimal"/>,
/// <see cref="double"/>, <see cref="DateOnly"/>, <see cref="DateTime"/>, <see cref="TimeOnly"/> (or their nullable forms),
/// <see cref="FormattedText"/>, <see cref="SelectOption"/>, <c>IReadOnlyList&lt;string&gt;</c>,
/// <c>IReadOnlyList&lt;SelectOption&gt;</c>, <c>IReadOnlyList&lt;AttributeCell&gt;</c> or <c>IReadOnlyList&lt;MatrixColumn&gt;</c>.
/// </typeparam>
/// <param name="Number">The Bluestone attribute number (compared ordinally).</param>
/// <param name="DefaultValue">Returned when the product has no value for the attribute.</param>
public readonly record struct AttributeKey<T>(string Number, T? DefaultValue = default);
