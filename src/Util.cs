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
    /// Handles pre-release identifiers.
    /// </summary>
    /// <param name="version1">The first version string.</param>
    /// <param name="version2">The second version string.</param>
    /// <returns>True if version1 is older or equal to version2, false otherwise.</returns>
    internal static bool IsVersionOlderOrEqual(this string version1, string version2)
    {
        Version version1_ = new(version1.Split('-')[0]);
        Version version2_ = new(version2.Split('-')[0]);

        if (version1_ < version2_) return true;
        if (version1_ > version2_) return false;

        string pre1 = version1.Contains('-') ? version1.Split('-')[1] : "";
        string pre2 = version2.Contains('-') ? version2.Split('-')[1] : "";

        if (string.IsNullOrEmpty(pre1) && !string.IsNullOrEmpty(pre2)) return false;
        if (!string.IsNullOrEmpty(pre1) && string.IsNullOrEmpty(pre2)) return true;

        return string.Compare(pre1, pre2, StringComparison.Ordinal) <= 0;
    }
}
