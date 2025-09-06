using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyMod.Json;

public class EnumCacheJson<T> : JsonConverter<T> where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return EnumCache<T>.GetType(reader.GetString());
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(EnumCache<T>.GetName(value));
    }
}
