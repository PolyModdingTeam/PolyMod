using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Json;

/// <summary>
/// Converts fixed (non-extensible) enums to and from their plain string names.
/// Use this for enums that are not registered in EnumCache.
/// </summary>
public class EnumNameJson<EnumType> : JsonConverter<EnumType> where EnumType : struct, Enum
{
    public override EnumType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string? enumName = reader.GetString();

        if (enumName != null && Enum.TryParse(enumName, true, out EnumType parsedValue))
        {
            return parsedValue;
        }

        string validNames = string.Join(", ", Enum.GetNames(typeof(EnumType)));
        throw new JsonException($"Unknown {typeof(EnumType).Name} value '{enumName}'. Valid values: {validNames}");
    }

    public override void Write(Utf8JsonWriter writer, EnumType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}