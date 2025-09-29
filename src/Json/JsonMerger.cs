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
        foreach (var property in patch.Properties().ToArray().ToList())
        {
            if (property == null || property.Name == null)
                continue;

            string propName = property.Name;
            JToken? originalValue = original[propName];
            JToken patchValue = property.Value;

            if (originalValue == null)
            {
                JArray? patchArray = patchValue.TryCast<JArray>();
                if (patchArray != null)
                {
                    bool isSkins = propName.Equals("skins", StringComparison.OrdinalIgnoreCase);
                    JArray merged = MergeArrays(new JArray(), patchArray, isSkins);
                    original[propName] = merged;
                }
                else
                {
                    original[propName] = patchValue;
                }
                continue;
            }

            JObject? originalObj = originalValue.TryCast<JObject>();
            JObject? patchObj = patchValue.TryCast<JObject>();
            if (originalObj != null && patchObj != null)
            {
                Merge(originalObj, patchObj);
                continue;
            }

            JArray? originalArr = originalValue.TryCast<JArray>();
            JArray? patchArr = patchValue.TryCast<JArray>();
            if (originalArr != null && patchArr != null)
            {
                bool isSkins = propName.Equals("skins", StringComparison.OrdinalIgnoreCase);
                JArray merged = MergeArrays(originalArr, patchArr, isSkins);
                original[propName] = merged;
                continue;
            }
            original[propName] = patchValue;
        }

        return original;
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
        if (patch.Count == 0)
            return new JArray();

        var result = new JArray(original);
        var patchList = patch._values.ToArray().ToList();

        bool hasDirectValues = patchList.Any(v =>
            v.Type == JTokenType.String &&
            !v.ToString().StartsWith("+") &&
            !v.ToString().StartsWith("-"));

        if (!isSkins && hasDirectValues)
        {
            result = new JArray();
        }

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
                if (!result._values.ToArray().Any(t => t.Type == JTokenType.String && t.ToString() == value))
                {
                    result.Add(value);
                }
            }
            else if (str.StartsWith("-"))
            {
                string value = str.Substring(1);
                var toRemove = result._values.ToArray()
                    .Where(t => t.Type == JTokenType.String && t.ToString() == value)
                    .ToList();
                foreach (var rem in toRemove)
                    result.Remove(rem);
            }
            else
            {
                if (isSkins)
                {
                    if (!result._values.ToArray().Any(t => t.Type == JTokenType.String && t.ToString() == str))
                    {
                        result.Add(str);
                    }
                }
                else
                {
                    result.Add(str);
                }
            }
        }

        return result;
    }
}
