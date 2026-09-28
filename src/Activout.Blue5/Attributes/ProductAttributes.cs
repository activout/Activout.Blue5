using System.Collections;

namespace Activout.Blue5;

/// <summary>A product's attributes, indexed once by attribute number (ordinal comparison).</summary>
public sealed class ProductAttributes : IReadOnlyCollection<ProductAttribute>
{
    /// <summary>No attributes.</summary>
    public static readonly ProductAttributes Empty = new([]);

    private readonly IReadOnlyList<ProductAttribute> _attributes;
    private readonly Dictionary<string, ProductAttribute> _byNumber;

    /// <summary>Indexes <paramref name="attributes"/> by number. If a number repeats, the first one wins for lookups.</summary>
    public ProductAttributes(IEnumerable<ProductAttribute> attributes)
    {
        _attributes = attributes.ToArray();
        _byNumber = new Dictionary<string, ProductAttribute>(_attributes.Count, StringComparer.Ordinal);
        foreach (var attribute in _attributes)
        {
            _byNumber.TryAdd(attribute.Number, attribute);
        }
    }

    /// <summary>Number of attributes.</summary>
    public int Count => _attributes.Count;

    /// <summary>
    /// Returns the attribute value converted to <typeparamref name="T"/>, or <see cref="AttributeKey{T}.DefaultValue"/>
    /// when the product has no value for it.
    /// </summary>
    /// <exception cref="AttributeConversionException">The value exists but cannot be converted to <typeparamref name="T"/>.</exception>
    public T? Get<T>(AttributeKey<T> key)
    {
        var attribute = Find(key.Number);
        return attribute is null || attribute.IsEmpty ? key.DefaultValue : AttributeConverter.Convert<T>(attribute);
    }

    /// <summary>The raw attribute with the given number, or <see langword="null"/>.</summary>
    public ProductAttribute? Find(string number) => _byNumber.GetValueOrDefault(number);

    /// <summary>Whether the product has an attribute with the given number.</summary>
    public bool Contains(string number) => _byNumber.ContainsKey(number);

    /// <inheritdoc />
    public IEnumerator<ProductAttribute> GetEnumerator() => _attributes.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
