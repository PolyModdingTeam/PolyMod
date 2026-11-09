using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;

namespace PolyMod;

/// <summary>
/// The helper methods that PolyMod uses for types, hashes, JSON tokens, tasks and versions.
/// </summary>
internal static class Util
{
    /// <summary>
    /// Gets the Il2Cpp type of a managed type.
    /// If the managed type is not registered in Il2Cpp, the method registers it first.
    /// </summary>
    /// <typeparam name="T">The managed type.</typeparam>
    /// <returns>The <c>Il2CppSystem.Type</c> of <typeparamref name="T"/>.</returns>
    internal static Il2CppSystem.Type WrapType<T>() where T : class
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return Il2CppType.From(typeof(T));
    }

    /// <summary>
    /// Gets the SHA-256 hash of the string value of an object.
    /// The method uses <c>ToString</c> on the object and UTF-8 to make the bytes.
    /// </summary>
    /// <param name="data">The object to hash.</param>
    /// <returns>The hash as an uppercase hexadecimal string.</returns>
    internal static string Hash(object data)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString()!)));
    }

    /// <summary>
    /// Gets the SHA-256 hash of a byte array.
    /// </summary>
    /// <param name="data">The bytes to hash.</param>
    /// <returns>The hash as an uppercase hexadecimal string.</returns>
    internal static string HashBytes(byte[] data)
    {
        return Convert.ToHexString(SHA256.HashData(data));
    }

    /// <summary>
    /// Gets one part of the path of a JSON token.
    /// The method splits the path at each dot and counts the parts from the end.
    /// </summary>
    /// <param name="token">The JSON token.</param>
    /// <param name="n">The position of the part from the end of the path. The value 1 gives the last part.</param>
    /// <returns>The part of the path at position <paramref name="n"/> from the end.</returns>
    internal static string GetJTokenName(JToken token, int n = 1)
    {
        return token.Path.Split('.')[^n];
    }

    /// <summary>
    /// Changes an <c>Il2CppSystem.Version</c> to a <see cref="Version"/>.
    /// The method parses the string value of the Il2Cpp version.
    /// </summary>
    /// <param name="self">The Il2Cpp version to change.</param>
    /// <returns>A <see cref="Version"/> with the same value.</returns>
    internal static Version Cast(this Il2CppSystem.Version self)
    {
        return new(self.ToString());
    }

    /// <summary>
    /// Waits for a task to complete and gets its result.
    /// The method blocks the thread that calls it.
    /// </summary>
    /// <typeparam name="T">The type of the result of the task.</typeparam>
    /// <param name="self">The task to wait for.</param>
    /// <returns>The result of the task.</returns>
    internal static T UnwrapAsync<T>(this Task<T> self)
    {
        return self.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Makes a copy of a version without the revision part.
    /// </summary>
    /// <param name="self">The version to copy.</param>
    /// <returns>A new version with the same major, minor and build parts, and no revision part.</returns>
    internal static Version CutRevision(this Version self)
    {
        return new(self.Major, self.Minor, self.Build);
    }

    /// <summary>
    /// Compares two version strings.
    /// The method first compares the core versions, which are the parts before the first dash.
    /// If the core versions are equal, it compares the pre-release tags with semantic version rules.
    /// A version with a pre-release tag is older than the same version without a tag.
    /// </summary>
    /// <param name="version1">The first version string.</param>
    /// <param name="version2">The second version string.</param>
    /// <returns>True if <paramref name="version1"/> is older than or equal to <paramref name="version2"/>, false if it is newer.</returns>
    internal static bool IsVersionOlderOrEqual(this string version1, string version2)
    {
        Version core1 = ParseVersion(version1, out string? pre1);
        Version core2 = ParseVersion(version2, out string? pre2);

        if (core1 != core2)
            return core1 < core2;

        return ComparePreRelease(pre1, pre2) <= 0;
    }

    /// <summary>
    /// Splits a version string into a core version and a pre-release tag.
    /// The pre-release tag is the text after the first dash.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <param name="preRelease">The pre-release tag, or null if there is no dash or no text after the dash.</param>
    /// <returns>The core version, which is the text before the first dash.</returns>
    private static Version ParseVersion(string version, out string? preRelease)
    {
        int dash = version.IndexOf('-');
        if (dash < 0)
        {
            preRelease = null;
            return new Version(version);
        }
        preRelease = version[(dash + 1)..];
        if (preRelease.Length == 0)
            preRelease = null;
        return new Version(version[..dash]);
    }

    /// <summary>
    /// Compares two pre-release tags with semantic version rules.
    /// A null tag (no pre-release) is newer than a tag that is not null.
    /// The method splits each tag at the dots and compares the identifiers in sequence.
    /// If all shared identifiers are equal, the tag with more identifiers is newer.
    /// </summary>
    /// <param name="left">The first pre-release tag, or null.</param>
    /// <param name="right">The second pre-release tag, or null.</param>
    /// <returns>A negative value if <paramref name="left"/> is older, zero if the tags are equal, or a positive value if <paramref name="left"/> is newer.</returns>
    private static int ComparePreRelease(string? left, string? right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return 1;
        if (right == null) return -1;

        string[] leftIds = left.Split('.');
        string[] rightIds = right.Split('.');
        int shared = Math.Min(leftIds.Length, rightIds.Length);
        for (int i = 0; i < shared; i++)
        {
            int comparison = CompareIdentifier(leftIds[i], rightIds[i]);
            if (comparison != 0)
                return comparison;
        }
        return leftIds.Length.CompareTo(rightIds.Length);
    }

    /// <summary>
    /// Compares two identifiers of a pre-release tag.
    /// The method compares two numeric identifiers as numbers.
    /// A numeric identifier is older than an identifier that contains other characters.
    /// The method compares two identifiers that are not numeric by ordinal string order.
    /// </summary>
    /// <param name="left">The first identifier.</param>
    /// <param name="right">The second identifier.</param>
    /// <returns>A negative value if <paramref name="left"/> is older, zero if the identifiers are equal, or a positive value if <paramref name="left"/> is newer.</returns>
    private static int CompareIdentifier(string left, string right)
    {
        bool leftNumeric = left.Length > 0 && left.All(char.IsDigit);
        bool rightNumeric = right.Length > 0 && right.All(char.IsDigit);
        if (leftNumeric && rightNumeric)
            return long.Parse(left, CultureInfo.InvariantCulture).CompareTo(long.Parse(right, CultureInfo.InvariantCulture));
        if (leftNumeric) return -1;
        if (rightNumeric) return 1;
        return string.Compare(left, right, StringComparison.Ordinal);
    }
}
