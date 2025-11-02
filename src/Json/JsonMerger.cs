using Il2CppSystem.IO;
using Newtonsoft.Json.Linq;

namespace PolyMod.Json;

/// <summary>
/// Provides functionality to merge JSON objects, with special handling for arrays.
/// </summary>
public static class JsonMerger
{
    /// <summary>
    /// Merges a patch JObject into an original JObject.
    /// </summary>
    /// <param name="original">The original JObject.</param>
    /// <param name="patch">The patch JObject to merge.</param>
    /// <returns>The merged JObject.</returns>
    public static JObject Merge(JObject original, JObject patch)
    {
        patch = MergeRecursive(original, patch);
        original.Merge(patch, new() { MergeArrayHandling = MergeArrayHandling.Replace, MergeNullValueHandling = MergeNullValueHandling.Merge });
        return original;
    }

    /// <summary>
    /// Recursively applies custom merging logic to arrays.
    /// </summary>
    /// <param name="original"></param>
    /// <param name="patch"></param>
    /// <returns></returns>
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
    /// Merges two JArrays, with special handling for adding and removing elements.
    /// </summary>
    /// <param name="original">The original JArray.</param>
    /// <param name="patch">The patch JArray to merge.</param>
    /// <param name="isSkins">A flag to indicate if the array is a 'skins' array, which has special merging logic.</param>
    /// <returns>The merged JArray.</returns>
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

            if (str.StartsWith("+"))
            {
                string value = str.Substring(1);
                result.Add(value);
                hasCustomValues = true;
            }
            else if (str.StartsWith("-"))
            {
                string value = str.Substring(1);
                var toRemove = result._values.ToArray()
                    .Where(t => t.Type == JTokenType.String && t.ToString() == value)
                    .ToList();
                foreach (var rem in toRemove)
                    result.Remove(rem);
                hasCustomValues = true;
            }
            else if(isSkins)
            {
                result.Add(str);
                hasCustomValues = true;
            }
        }
        if(!hasCustomValues)
        {
            result = new JArray(patch);
        }
        return result;
    }
}
