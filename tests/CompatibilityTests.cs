using System.Text;
using PolyMod;
using PolyMod.Managers;

namespace Tests;

public class CompatibilityTests
{
    private static Mod MakeModWithFiles(string id, Version version, params (string Name, string Content)[] files)
    {
        List<Mod.File> modFiles = new();
        foreach ((string name, string content) in files)
            modFiles.Add(new Mod.File(name, Encoding.UTF8.GetBytes(content)));

        Mod.Manifest manifest = new(id, null, null, version, new[] { "author" }, null);
        return new Mod(manifest, Mod.Status.Success, modFiles);
    }

    // The launcher computes this same value from the mod's .polymod archive. If this literal has to
    // change, PolyModLauncher's ModChecksum must change with it or multiplayer checks will disagree.
    [Fact]
    public void ModChecksumMatchesTheLauncher()
    {
        Mod mod = MakeModWithFiles(
            "testmod",
            new Version(1, 2, 3),
            ("sprites.json", "{\"a\":1}"),
            ("nested/thing.txt", "hello world"),
            ("code.dll", "not really a dll"));

        Assert.Equal(
            "34A44D3156F856C4A6CACB16379B529E797230E7F1DB9A32E1E3682D416405AC",
            Compatibility.ComputeModChecksum(mod));
    }

    [Fact]
    public void ModChecksumIgnoresFileOrder()
    {
        Mod ordered = MakeModWithFiles(
            "testmod",
            new Version(1, 0, 0),
            ("a.txt", "first"),
            ("b.txt", "second"));
        Mod reversed = MakeModWithFiles(
            "testmod",
            new Version(1, 0, 0),
            ("b.txt", "second"),
            ("a.txt", "first"));

        Assert.Equal(
            Compatibility.ComputeModChecksum(ordered),
            Compatibility.ComputeModChecksum(reversed));
    }

    [Fact]
    public void ModChecksumChangesWithFileContents()
    {
        Mod original = MakeModWithFiles("testmod", new Version(1, 0, 0), ("a.txt", "first"));
        Mod edited = MakeModWithFiles("testmod", new Version(1, 0, 0), ("a.txt", "firsT"));

        Assert.NotEqual(
            Compatibility.ComputeModChecksum(original),
            Compatibility.ComputeModChecksum(edited));
    }

    [Fact]
    public void ModChecksumChangesWithVersion()
    {
        Mod original = MakeModWithFiles("testmod", new Version(1, 0, 0), ("a.txt", "first"));
        Mod bumped = MakeModWithFiles("testmod", new Version(1, 0, 1), ("a.txt", "first"));

        Assert.NotEqual(
            Compatibility.ComputeModChecksum(original),
            Compatibility.ComputeModChecksum(bumped));
    }

    [Fact]
    public void GlobalChecksumIgnoresClientOnlyMods()
    {
        Mod shared = MakeModWithFiles("sharedmod", new Version(1, 0, 0), ("a.txt", "first"));

        Dictionary<string, Mod> withoutClient = new() { { shared.id, shared } };
        Compatibility.ComputeChecksums(withoutClient);
        string before = Compatibility.checksum;

        Mod clientOnly = MakeModWithFiles("clientmod", new Version(1, 0, 0), ("b.txt", "second"));
        clientOnly.client = true;

        Dictionary<string, Mod> withClient = new()
        {
            { shared.id, shared },
            { clientOnly.id, clientOnly },
        };
        Compatibility.ComputeChecksums(withClient);

        Assert.Equal(before, Compatibility.checksum);
    }

    // The game deletes saves through APIs that hand the id over as a Guid in some places and as a
    // string in others. If those produced different keys the stored checksum would never be purged.
    [Fact]
    public void GameIdNormalizesAcrossTheFormsTheGameUses()
    {
        Guid gameId = Guid.Parse("3F2504E0-4F89-11D3-9A0C-0305E82C3301");
        string expected = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

        Assert.Equal(expected, Compatibility.NormalizeGameId(gameId));
        Assert.Equal(expected, Compatibility.NormalizeGameId(gameId.ToString()));
        Assert.Equal(expected, Compatibility.NormalizeGameId(gameId.ToString().ToUpperInvariant()));
        Assert.Equal(expected, Compatibility.NormalizeGameId(gameId.ToString("B")));
    }

    [Fact]
    public void GlobalChecksumIgnoresModOrder()
    {
        Mod first = MakeModWithFiles("alpha", new Version(1, 0, 0), ("a.txt", "first"));
        Mod second = MakeModWithFiles("beta", new Version(1, 0, 0), ("b.txt", "second"));

        Compatibility.ComputeChecksums(new Dictionary<string, Mod>
        {
            { first.id, first },
            { second.id, second },
        });
        string ordered = Compatibility.checksum;

        Compatibility.ComputeChecksums(new Dictionary<string, Mod>
        {
            { second.id, second },
            { first.id, first },
        });

        Assert.Equal(ordered, Compatibility.checksum);
    }
}
