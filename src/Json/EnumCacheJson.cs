using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Json;

/// <summary>
/// Converts an <see cref="Enum"/> to and from a JSON string using the <see cref="EnumCache{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the enum.</typeparam>
internal class EnumCacheJson<T> : JsonConverter<T> where T : struct, Enum
{
    /// <summary>
    /// Reads and converts the JSON to an enum value.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return EnumCache<T>.GetType(reader.GetString());
    }

    /// <summary>
    /// Writes a specified value as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(EnumCache<T>.GetName(value));
    }
}
