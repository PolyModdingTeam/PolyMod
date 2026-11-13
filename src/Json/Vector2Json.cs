using System.Text.Json;
using System.Text.Json.Serialization;
using UnityEngine;

namespace PolyMod.Json;

/// <summary>
/// The JSON converter that reads and writes a <see cref="Vector2"/>.
/// The JSON form is an array of two numbers, x and y, for example <c>[0.5, -1]</c>.
/// </summary>
public class Vector2Json : JsonConverter<Vector2>
{
    /// <summary>
    /// Reads a JSON array of two numbers and makes a <see cref="Vector2"/> from them.
    /// </summary>
    /// <param name="reader">The JSON reader, at the start of the array.</param>
    /// <param name="typeToConvert">The type to read. The method does not use it.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    /// <returns>The vector with the first number as x and the second number as y.</returns>
    /// <exception cref="JsonException">The JSON value is not an array, an item is not a number, or the array does not have exactly two items.</exception>
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
    /// Writes a <see cref="Vector2"/> as a JSON array of two numbers, x and y.
    /// </summary>
    /// <param name="writer">The JSON writer.</param>
    /// <param name="value">The vector to write.</param>
    /// <param name="options">The serializer options. The method does not use them.</param>
    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.x);
        writer.WriteNumberValue(value.y);
        writer.WriteEndArray();
    }
}
