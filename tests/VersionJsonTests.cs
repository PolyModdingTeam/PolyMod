using PolyMod.Json;
using System.Text.Json;

namespace Tests;

public class VersionJsonTests
{
    private static readonly JsonSerializerOptions options = new() { Converters = { new VersionJson() } };

    [Fact]
    public void VersionRoundTripsThroughString()
    {
        var json = JsonSerializer.Serialize(new Version(1, 2, 3), options);
        Assert.Equal("\"1.2.3\"", json);
        Assert.Equal(new Version(1, 2, 3), JsonSerializer.Deserialize<Version>(json, options));
    }
}
