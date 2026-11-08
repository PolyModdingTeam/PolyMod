using PolyMod;

namespace Tests;

public class UtilTests
{
    [Theory]
    [InlineData("1.0.0", "2.0.0", true)]                 // older core
    [InlineData("2.0.0", "1.0.0", false)]                // newer core
    [InlineData("2.0.0", "2.0.0", true)]                 // equal
    [InlineData("2.0", "2.0.0", true)]                   // shorter version sorts first
    [InlineData("2.0.0.1", "2.0.0", false)]              // revision makes it newer
    [InlineData("2.0.0", "2.0.0-beta", false)]           // release is newer than its pre-release
    [InlineData("2.0.0-beta", "2.0.0", true)]
    [InlineData("2.0.0-alpha", "2.0.0-beta", true)]      // alphanumeric identifiers compare lexically
    [InlineData("2.0.0-rc.9", "2.0.0-rc.10", true)]      // numeric identifiers compare numerically
    [InlineData("2.0.0-rc.10", "2.0.0-rc.9", false)]
    [InlineData("2.0.0-9", "2.0.0-10", true)]
    [InlineData("2.0.0-1", "2.0.0-alpha", true)]         // numeric sorts before alphanumeric
    [InlineData("2.0.0-pre", "2.0.0-pre.1", true)]       // more identifiers mean a newer pre-release
    [InlineData("2.0.0-pre.1", "2.0.0-pre", false)]
    [InlineData("2.0.0-alpha-1", "2.0.0-alpha-2", true)] // everything after the first dash is the tag
    [InlineData("2.0.0-alpha-2", "2.0.0-alpha-1", false)]
    public void VersionComparisons(string version, string other, bool expected) =>
        Assert.Equal(expected, version.IsVersionOlderOrEqual(other));

    [Fact]
    public void HashProducesUppercaseSha256Hex()
    {
        Assert.Equal(
            "9F86D081884C7D659A2FEAA0C55AD015A3BF4F1B2B0B822CD15D6C15B0F00A08",
            Util.Hash("test"));
    }
}
