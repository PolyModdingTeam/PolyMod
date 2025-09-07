using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace PolyMod.Json;

/// <summary>
/// Converts a <see cref="Vector2"/> to and from a JSON array of two numbers.
/// </summary>
public class Vector2Json : JsonConverter<Vector2>
{
    /// <summary>
    /// Reads and converts the JSON to a <see cref="Vector2"/>.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        List<float> values = new();
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray) break;
                if (reader.TokenType != JsonTokenType.Number) throw new JsonException();
                values.Add(reader.GetSingle());
            }
        }
        if (values.Count != 2) throw new JsonException();
        return new(values[0], values[1]);
    }

    /// <summary>
    /// Writes a specified value as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteEndArray();
    }
}
