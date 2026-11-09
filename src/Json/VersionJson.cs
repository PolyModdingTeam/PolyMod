using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Json;

/// <summary>
/// The JSON converter that reads and writes a <see cref="Version"/>.
/// The JSON form is a version string, for example <c>"1.2.3"</c>.
/// </summary>
public class VersionJson : JsonConverter<Version>
{
    /// <summary>
    /// Reads a JSON string and makes a <see cref="Version"/> from it.
    /// The <see cref="Version"/> constructor throws an exception if the string is null or is not a valid version.
    /// </summary>
    /// <param name="reader">The JSON reader, at a string token.</param>
    /// <param name="typeToConvert">The type to read. The method does not use it.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    /// <returns>The version from the JSON string.</returns>
    public override Version? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new(reader.GetString()!);
    }

    /// <summary>
    /// Writes a <see cref="Version"/> as a JSON string, for example <c>"1.2.3"</c>.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The version to write.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
