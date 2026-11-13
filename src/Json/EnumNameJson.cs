using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Json;

/// <summary>
/// The JSON converter that reads and writes an enum value as its C# member name.
/// The JSON form is a string, for example <c>"Linear"</c>.
/// </summary>
/// <remarks>
/// Use this converter for enums that mods cannot extend and that <c>EnumCache</c> does not have.
/// </remarks>
/// <typeparam name="TEnum">The enum type to convert.</typeparam>
public class EnumNameJson<TEnum> : JsonConverter<TEnum> where TEnum : struct, Enum
{
    /// <summary>
    /// Reads a JSON string and gets the enum value with that member name. The name is not case-sensitive.
    /// The method uses <c>Enum.TryParse</c>, so it also accepts numeric strings.
    /// </summary>
    /// <param name="reader">The JSON reader, at a string token.</param>
    /// <param name="typeToConvert">The type to read. The method does not use it.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    /// <returns>The enum value that has the name in the JSON string.</returns>
    /// <exception cref="JsonException">The JSON value is null or is not a member name of <typeparamref name="TEnum"/>. The message shows the valid names.</exception>
    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? enumName = reader.GetString();

        if (enumName != null && Enum.TryParse(enumName, true, out TEnum parsedValue))
        {
            return parsedValue;
        }

        string validNames = string.Join(", ", Enum.GetNames<TEnum>());
        throw new JsonException($"Unknown {typeof(TEnum).Name} value '{enumName}'. Valid values: {validNames}");
    }

    /// <summary>
    /// Writes the enum value as a JSON string with its C# member name.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The enum value to write.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
