namespace Activout.Blue5.Attributes;

/// <summary>An attribute value exists but cannot be converted to the requested type.</summary>
public sealed class AttributeConversionException : Exception
{
    /// <summary>Creates the exception.</summary>
    public AttributeConversionException(string attributeNumber, Type targetType, string? dataType, string rawValue, string reason)
        : base($"Cannot convert attribute '{attributeNumber}' (dataType '{dataType ?? "?"}', value {rawValue}) to {Describe(targetType)}: {reason}")
    {
        AttributeNumber = attributeNumber;
        TargetType = targetType;
        DataType = dataType;
        RawValue = rawValue;
    }

    /// <summary>The attribute number.</summary>
    public string AttributeNumber { get; }

    /// <summary>The requested type.</summary>
    public Type TargetType { get; }

    /// <summary>The PAPI data type of the attribute.</summary>
    public string? DataType { get; }

    /// <summary>A readable rendering of the source value.</summary>
    public string RawValue { get; }

    private static string Describe(Type type) =>
        type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;
}
