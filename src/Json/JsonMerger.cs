using Il2CppSystem.IO;
using Newtonsoft.Json.Linq;

namespace PolyMod.Json;

/// <summary>
/// The merge of a JSON patch into a JSON object, for example a GLD patch into the game logic data.
/// </summary>
/// <remarks>
/// <para>
/// Objects merge recursively. A value in the patch replaces the value in the original, and a null value in the patch sets the value to null.
/// </para>
/// <para>
/// Arrays have special rules. A string that starts with <c>+</c> adds the string without the <c>+</c> to the original array.
/// A string that starts with <c>-</c> removes all equal strings from the original array.
/// In an array with the name <c>skins</c>, a plain string also adds the string.
/// If an array has none of these items, the patch array replaces the original array.
/// If an array has some of these items, the merge ignores the other plain strings in it.
/// </para>
/// </remarks>
public static class JsonMerger
{
    /// <summary>
    /// Merges a patch into an original JSON object. The method changes <paramref name="original"/> and <paramref name="patch"/>.
    /// </summary>
    /// <param name="original">The JSON object to change.</param>
    /// <param name="patch">The JSON object with the changes.</param>
    /// <returns>The <paramref name="original"/> object, after the merge.</returns>
    public static JObject Merge(JObject original, JObject patch)
    {
        patch = MergeRecursive(original, patch);
        original.Merge(patch, new() { MergeArrayHandling = MergeArrayHandling.Replace, MergeNullValueHandling = MergeNullValueHandling.Merge });
        return original;
    }

    /// <summary>
    /// Replaces each array in the patch with the result of <see cref="MergeArrays"/>, at all levels of the patch.
    /// If the original does not have a matching array or object, the method uses an empty one.
    /// After this method, a normal merge with array replacement gives the correct arrays.
    /// </summary>
    /// <param name="original">The JSON object that the patch applies to.</param>
    /// <param name="patch">The JSON object with the changes. The method changes this object.</param>
    /// <returns>The <paramref name="patch"/> object, with the merged arrays.</returns>
    private static JObject MergeRecursive(JObject original, JObject patch)
    {
        foreach (var property in patch.Properties().ToArray().ToList())
        {
            if (property == null || property.Name == null)
                continue;

            string propName = property.Name;
            JToken? originalValue = original[propName];
            JToken patchValue = property.Value;

            if (patchValue.Type == JTokenType.Array)
            {
                if (originalValue == null)
                    originalValue = new JArray();

                JArray originalArr = originalValue.Cast<JArray>();
                JArray patchArr = patchValue.Cast<JArray>();
                bool isSkins = propName.Equals("skins", StringComparison.OrdinalIgnoreCase);
                JArray merged = MergeArrays(originalArr, patchArr, isSkins);
                patch[propName] = merged;
            }
            else if (patchValue.Type == JTokenType.Object)
            {
                if (originalValue == null || originalValue.Type != JTokenType.Object)
                    originalValue = new JObject();

                MergeRecursive(originalValue.Cast<JObject>(), patchValue.Cast<JObject>());
            }
        }
        return patch;
    }

    /// <summary>
    /// Merges a patch array into a copy of an original array.
    /// A string that starts with <c>+</c> adds the string without the <c>+</c>. A string that starts with <c>-</c> removes all equal strings.
    /// The method adds all items that are not strings. It ignores plain strings, but adds them if <paramref name="isSkins"/> is true.
    /// If the patch has no <c>+</c> strings, no <c>-</c> strings, and no plain strings in a skins array, the method gives a copy of the patch array.
    /// </summary>
    /// <param name="original">The original array. The method does not change it.</param>
    /// <param name="patch">The patch array.</param>
    /// <param name="isSkins">True if the array has the name <c>skins</c>. Then plain strings add to the original array.</param>
    /// <returns>A new array with the merged items.</returns>
    private static JArray MergeArrays(JArray original, JArray patch, bool isSkins)
    {
        var result = new JArray(original);
        var patchList = patch._values.ToArray().ToList();

        bool hasCustomValues = false;
        foreach (var token in patchList)
        {
            if (token.Type != JTokenType.String)
            {
                result.Add(token);
                continue;
            }

            string str = token.ToString();

            if (str.StartsWith('+'))
            {
                string value = str.Substring(1);
                result.Add(value);
                hasCustomValues = true;
            }
            else if (str.StartsWith('-'))
            {
                string value = str.Substring(1);
                var toRemove = result._values.ToArray()
                    .Where(t => t.Type == JTokenType.String && t.ToString() == value)
                    .ToList();
                foreach (var rem in toRemove)
                    result.Remove(rem);
                hasCustomValues = true;
            }
            else if (isSkins)
            {
                result.Add(str);
                hasCustomValues = true;
            }
        }
        if (!hasCustomValues)
        {
            result = new JArray(patch);
        }
        return result;
    }
}
