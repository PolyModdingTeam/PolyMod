using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Json;

/// <summary>
/// The JSON converter that reads and writes a game enum value as its name in <c>EnumCache</c>.
/// The JSON form is a string, for example <c>"imperius"</c>. The converter also reads and writes the value as a property name.
/// </summary>
/// <remarks>
/// Use this converter for enums that mods can extend, because <c>EnumCache</c> also has the names of the values that mods add.
/// </remarks>
/// <typeparam name="T">The enum type to convert.</typeparam>
public class EnumCacheJson<T> : JsonConverter<T> where T : struct, Enum
{
    /// <summary>
    /// Finds if the converter accepts a type.
    /// The method gives true for <typeparamref name="T"/> and for <see cref="List{T}"/> of <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// The <see cref="Read"/> method can read only <typeparamref name="T"/>. For lists, use <see cref="EnumCacheListJson{T}"/>.
    /// </remarks>
    /// <param name="typeToConvert">The type to check.</param>
    /// <returns>True if the type is <typeparamref name="T"/> or <see cref="List{T}"/> of <typeparamref name="T"/>, otherwise false.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(T) || typeToConvert == typeof(List<T>);
    }

    /// <summary>
    /// Reads a JSON string and gets the enum value with that name from <c>EnumCache</c>.
    /// If <c>EnumCache</c> does not have the name, <c>EnumCache</c> throws an exception.
    /// </summary>
    /// <param name="reader">The JSON reader, at a string token or a property name token.</param>
    /// <param name="typeToConvert">The type to read. It must be <typeparamref name="T"/>.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    /// <returns>The enum value that has the name in the JSON string.</returns>
    /// <exception cref="NotSupportedException"><paramref name="typeToConvert"/> is not <typeparamref name="T"/>.</exception>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(T))
        {
            return EnumCache<T>.GetType(reader.GetString());
        }

        throw new NotSupportedException("EnumCacheJson does not support reading this type directly.");
    }


    /// <summary>
    /// Writes the enum value as a JSON string with its name from <c>EnumCache</c>.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The enum value to write.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(EnumCache<T>.GetName(value));
    }

    /// <summary>
    /// Reads a JSON property name and gets the enum value with that name from <c>EnumCache</c>.
    /// The method calls <see cref="Read"/>.
    /// </summary>
    /// <param name="reader">The JSON reader, at a property name token.</param>
    /// <param name="typeToConvert">The type to read. It must be <typeparamref name="T"/>.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    /// <returns>The enum value that has the name in the property name.</returns>
    public override T ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Read(ref reader, typeToConvert, options);
    }

    /// <summary>
    /// Writes the enum value as a JSON property name with its name from <c>EnumCache</c>.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The enum value to write.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    public override void WriteAsPropertyName(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(EnumCache<T>.GetName(value));
    }
}

/// <summary>
/// The JSON converter that reads and writes a list of game enum values as their names in <c>EnumCache</c>.
/// The JSON form is an array of strings, for example <c>["imperius", "bardur"]</c>.
/// </summary>
/// <typeparam name="T">The enum type of the list items.</typeparam>
public class EnumCacheListJson<T> : JsonConverter<List<T>> where T : struct, Enum
{
    /// <summary>
    /// The converter that reads and writes each item of the list.
    /// </summary>
    private readonly EnumCacheJson<T> _inner = new();

    /// <summary>
    /// Reads a JSON array of strings and gets the enum value for each string from <c>EnumCache</c>.
    /// </summary>
    /// <param name="reader">The JSON reader, at the start of the array.</param>
    /// <param name="typeToConvert">The type to read. The method does not use it.</param>
    /// <param name="options">The serializer options. The method gives them to the item converter.</param>
    /// <returns>The list of enum values, in the same order as the array.</returns>
    /// <exception cref="JsonException">The JSON value is not an array.</exception>
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
    /// Writes a list of enum values as a JSON array of strings with their names from <c>EnumCache</c>.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The list of enum values to write.</param>
    /// <param name="options">The serializer options. The method gives them to the item converter.</param>
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
