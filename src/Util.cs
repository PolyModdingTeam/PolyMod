using System.Security.Cryptography;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Newtonsoft.Json.Linq;

namespace PolyMod;

/// <summary>
/// A collection of utility methods for the PolyMod framework.
/// </summary>
internal static class Util
{
    /// <summary>
    /// Wraps a managed type for use in the Il2Cpp runtime.
    /// </summary>
    /// <typeparam name="T">The type to wrap.</typeparam>
    /// <returns>The Il2Cpp representation of the type.</returns>
    internal static Il2CppSystem.Type WrapType<T>() where T : class
    {
        if (!ClassInjector.IsTypeRegisteredInIl2Cpp<T>())
            ClassInjector.RegisterTypeInIl2Cpp<T>();
        return Il2CppType.From(typeof(T));
    }

    /// <summary>
    /// Computes the SHA256 hash of an object's string representation.
    /// </summary>
    /// <param name="data">The object to hash.</param>
    /// <returns>The hexadecimal string representation of the hash.</returns>
    internal static string Hash(object data)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data.ToString()!)));
    }

    /// <summary>
    /// Gets the name of a JToken from its path.
    /// </summary>
    /// <param name="token">The JToken.</param>
    /// <param name="n">The part of the path to retrieve, from the end.</param>
    /// <returns>The name of the JToken.</returns>
    internal static string GetJTokenName(JToken token, int n = 1)
    {
        return token.Path.Split('.')[^n];
    }

    /// <summary>
    /// Converts an Il2CppSystem.Version to a System.Version.
    /// </summary>
    /// <param name="self">The Il2CppSystem.Version to convert.</param>
    /// <returns>The equivalent System.Version.</returns>
    internal static Version Cast(this Il2CppSystem.Version self)
    {
        return new(self.ToString());
    }

    /// <summary>
    /// Synchronously unwraps the result of a Task.
    /// </summary>
    /// <typeparam name="T">The result type of the Task.</typeparam>
    /// <param name="self">The Task to unwrap.</param>
    /// <returns>The result of the Task.</returns>
    internal static T UnwrapAsync<T>(this Task<T> self)
    {
        return self.GetAwaiter().GetResult();
    }

    /// <summary>
    /// Removes the revision component from a Version.
    /// </summary>
    /// <param name="self">The Version to modify.</param>
    /// <returns>A new Version with the revision set to -1.</returns>
    internal static Version CutRevision(this Version self)
    {
        return new(self.Major, self.Minor, self.Build);
    }

    /// <summary>
    /// Compares two version strings to see if the first is older or equal to the second.
    /// Pre-release tags follow semantic-version rules: the part after the first dash is
    /// split on dots, purely numeric identifiers compare numerically and sort before
    /// alphanumeric ones, and a longer identifier list is newer. A version with a
    /// pre-release tag is always older than the same version without one.
    /// </summary>
    /// <param name="version1">The first version string.</param>
    /// <param name="version2">The second version string.</param>
    /// <returns>True if version1 is older or equal to version2, false otherwise.</returns>
    internal static bool IsVersionOlderOrEqual(this string version1, string version2)
    {
        Version core1 = ParseVersion(version1, out string? pre1);
        Version core2 = ParseVersion(version2, out string? pre2);

        if (core1 != core2)
            return core1 < core2;

        return ComparePreRelease(pre1, pre2) <= 0;
    }

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

    private static int CompareIdentifier(string left, string right)
    {
        bool leftNumeric = left.Length > 0 && left.All(char.IsDigit);
        bool rightNumeric = right.Length > 0 && right.All(char.IsDigit);
        if (leftNumeric && rightNumeric)
            return long.Parse(left).CompareTo(long.Parse(right));
        if (leftNumeric) return -1;
        if (rightNumeric) return 1;
        return string.Compare(left, right, StringComparison.Ordinal);
    }
}
