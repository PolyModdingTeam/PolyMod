using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Json;

/// <summary>
/// A custom JSON converter for enum types that integrates with EnumCache.
/// Supports serializing and deserializing enums by their cached string names,
/// and works with both single enum values and property names.
/// </summary>
/// <typeparam name="T">The enum type being converted.</typeparam>
public class EnumCacheJson<T> : JsonConverter<T> where T : struct, Enum
{
    /// <summary>
    /// Determines whether the converter can handle the given type.
    /// Supports both the enum type <typeparamref name="T"/> and <see cref="List{T}"/>.
    /// </summary>
    /// <param name="typeToConvert">The type to check for compatibility.</param>
    /// <returns>True if the type can be converted; otherwise false.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(T) || typeToConvert == typeof(List<T>);
    }

    /// <summary>
    /// Reads a JSON string and converts it into the corresponding enum value
    /// using the EnumCache lookup.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type to convert into.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The enum value represented by the JSON string.</returns>
    /// <exception cref="NotSupportedException">Thrown if the type is not supported.</exception>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(T))
        {
            return EnumCache<T>.GetType(reader.GetString());
        }

        throw new NotSupportedException("EnumCacheJson does not support reading this type directly.");
    }


    /// <summary>
    /// Writes the enum value as its cached string representation.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The enum value to write.</param>
    /// <param name="options">Serialization options.</param>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(EnumCache<T>.GetName(value));
    }

    /// <summary>
    /// Reads a JSON property name and converts it into the corresponding enum value.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The target type.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>The enum value represented by the property name.</returns>
    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Read(ref reader, typeToConvert, options);
    }

    /// <summary>
    /// Writes the enum value as a property name using its cached string representation.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The enum value to write.</param>
    /// <param name="options">Serialization options.</param>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(EnumCache<T>.GetName(value));
    }
}

/// <summary>
/// A custom JSON converter for lists of enum values that integrates with EnumCache.
/// Serializes enum values as strings and deserializes them back into a list of enums.
/// </summary>
/// <typeparam name="T">The enum type being converted.</typeparam>
public class EnumCacheListJson<T> : JsonConverter<List<T>> where T : struct, Enum
{
    /// <summary>
    /// Inner converter used to handle individual enum values.
    /// </summary>
    private readonly EnumCacheJson<T> _inner = new();

    /// <summary>
    /// Reads a JSON array of enum string values and converts them into a list of enum values.
    /// </summary>
    /// <param name="reader">The JSON reader.</param>
    /// <param name="typeToConvert">The type to convert into.</param>
    /// <param name="options">Serialization options.</param>
    /// <returns>A list of enum values.</returns>
    /// <exception cref="JsonException">Thrown if the JSON is not an array.</exception>
    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<T>();

        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected StartArray token for enum list");

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
                break;

            list.Add(_inner.Read(ref reader, typeof(T), options));
        }

        return list;
    }

    /// <summary>
    /// Writes a list of enum values as a JSON array of strings.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The list of enum values to write.</param>
    /// <param name="options">Serialization options.</param>
    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();

        foreach (var item in value)
        {
            _inner.Write(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
