using System.Globalization;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Reflection;
using LibCpp2IL;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyMod.Managers;

/// <summary>
/// The manager for localization. It adds mod terms and mod languages to the localization of the game.
/// </summary>
public static class Loc
{
    /// <summary>
    /// The languages that mods add, keyed by language code.
    /// Each value maps term keys to the text in that language.
    /// The <c>language</c> key gives the name of the language.
    /// </summary>
    internal static Dictionary<string, Dictionary<string, string>> languagesToAdd = new();
    /// <summary>
    /// The localization keys that replace the info text of improvements, keyed by the enum name of the improvement.
    /// </summary>
    internal static Dictionary<string, string> buildingsInfoOverrides = new();

    /// <summary>
    /// A Harmony prefix on <c>Localization.Get(string, Il2CppReferenceArray)</c>.
    /// It finds a custom enum value in the parts of the key, which are separated by dots.
    /// A custom enum value is a number that is not less than <see cref="Plugin.AUTOIDX_STARTS_FROM"/>.
    /// The patch gets the enum name of this value from the enum caches of the type mappings in <see cref="Loader.typeMappings"/>.
    /// If more than one enum type has a name for the value, the patch uses the last of these types in <see cref="Loader.typeMappings"/>.
    /// Then it replaces the number in the key with the enum name.
    /// If the key has more than one custom value, the patch uses only the last value.
    /// </summary>
    /// <param name="key">The localization key. The patch can change it.</param>
    /// <param name="args">The format arguments of the localized text. The patch does not use them.</param>
    /// <returns>Always true, which runs the original method.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(Il2CppReferenceArray<Il2CppSystem.Object>))]
    private static bool Localization_Get(ref string key, Il2CppReferenceArray<Il2CppSystem.Object> args)
    {
        List<string> keys = key.Split('.').ToList();
        int? idx = null;
        string? name = null;

        // Find any custom enum indices in the localization key
        foreach (string item in keys)
        {
            if (int.TryParse(item, out int parsedIdx))
            {
                if (parsedIdx >= Plugin.AUTOIDX_STARTS_FROM)
                {
                    idx = parsedIdx;
                }
            }
        }

        // If a custom enum index is found, try to resolve its name
        if (idx != null)
        {
            foreach (var typeMapping in Loader.typeMappings.Values)
            {
                if (!typeMapping.shouldCreateCache)
                    continue;

                MethodInfo? methodInfo = typeof(EnumCache<>).MakeGenericType(typeMapping.type).GetMethod("TryGetName");
                if (methodInfo != null)
                {
                    object?[] parameters = { idx, null };
                    object? methodInvokeResult = methodInfo.Invoke(null, parameters);
                    if (methodInvokeResult != null)
                    {
                        if ((bool)methodInvokeResult)
                        {
                            name = (string?)parameters[1];
                        }
                    }
                }
            }

            // If the name was resolved, replace the index with the name in the key
            if (name != null && idx != null)
            {
                int index = keys.IndexOf(idx.Value.ToString(CultureInfo.InvariantCulture));
                keys[index] = name;
                key = string.Join(".", keys);
            }
        }
        return true;
    }

    /// <summary>
    /// A Harmony prefix on <c>Localization.Init</c>.
    /// If the localization is already initialized, the patch does nothing.
    /// If not, it sets the missing language code of Elyrion in the first language source.
    /// Then it adds each language in <see cref="languagesToAdd"/> to the source, if the source does not have that language code.
    /// It adds the missing terms and sets the text of each term for that language.
    /// </summary>
    /// <returns>Always true, which runs the original method.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Localization), nameof(Localization.Init))]
    private static bool Localization_Init()
    {
        if (Localization.initialized)
            return true;

        if (LocalizationManager.Sources.Count == 0)
            LocalizationManager.UpdateSources();

        LanguageSourceData source = LocalizationManager.Sources[0];

        // Elyrion has no language code. I am almost completely sure it is a bug, by looking at the code.
        LanguageData elyrionLanguage = source.GetLanguageData("Elyrion");
        elyrionLanguage.Code = Localization.LANG_CODE_ELYRION;

        foreach (var languageCode in languagesToAdd.Keys)
        {
            Dictionary<string, string> terms = languagesToAdd[languageCode];

            int languageIndex = source.GetLanguageIndexFromCode(languageCode);
            string languageName = terms["language"];
            if (languageIndex == -1)
            {
                source.AddLanguage(languageName, languageCode);
                languageIndex = source.GetLanguageIndex(languageName);
            }

            foreach (var kvp in terms)
            {
                TermData term = source.GetTermData(kvp.Key);
                if (term == null)
                {
                    source.AddTerm(kvp.Key);
                    term = source.GetTermData(kvp.Key);
                }

                term.Languages[languageIndex] = kvp.Value;
            }

            LocalizationManager.UpdateSources();

            Plugin.logger.LogInfo($"{languageCode} language terms added.");
        }
        return true;
    }

    /// <summary>
    /// A Harmony prefix on <c>BuildingUtils.GetInfo</c>.
    /// If <see cref="buildingsInfoOverrides"/> has a key for the improvement, the patch gives the localized text of that key.
    /// The localization uses the skin of the local player and the tribe of the owner.
    /// </summary>
    /// <param name="__result">The info text of the improvement. The patch sets it if an override exists.</param>
    /// <param name="skinOfCurrentLocalPlayer">The skin of the local player.</param>
    /// <param name="improvementData">The data of the improvement.</param>
    /// <param name="improvementState">The state of the improvement. The patch does not use it.</param>
    /// <param name="owner">The player that owns the improvement, or null. If null, the patch uses no tribe.</param>
    /// <param name="tileData">The tile of the improvement. The patch does not use it.</param>
    /// <returns>False if an override exists, which skips the original method. True if not.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(BuildingUtils), nameof(BuildingUtils.GetInfo))]
    private static bool BuildingUtils_GetInfo(ref string __result, SkinType skinOfCurrentLocalPlayer,
        ImprovementData improvementData, ImprovementState improvementState, PlayerState owner, TileData tileData)
    {
        TribeType tribe = TribeType.None;
        if (owner != null)
            tribe = owner.tribe;

        string key = EnumCache<ImprovementData.Type>.GetName(improvementData.type);
        if (buildingsInfoOverrides.TryGetValue(key, out string? buildingInfoOverride))
        {
            __result = Localization.GetSkinned(skinOfCurrentLocalPlayer, tribe, buildingInfoOverride);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds the terms of a mod localization file to the first language source of the game.
    /// The method changes each key with <see cref="ReplaceDashesWithDots"/>.
    /// It adds the <c>TribeSkins/</c> prefix to a key that starts with <c>tribeskins</c>.
    /// For each game language, the term gets the text in that language.
    /// If that text does not exist, the term gets the English text, or else the term name.
    /// </summary>
    /// <param name="localization">The terms, keyed by term key. Each value maps language names to text.</param>
    internal static void BuildAndLoadLocalization(Dictionary<string, Dictionary<string, string>> localization)
    {
        foreach (var (key, data) in localization)
        {
            string name = ReplaceDashesWithDots(key);
            if (name.StartsWith("tribeskins", StringComparison.Ordinal)) name = "TribeSkins/" + name;
            TermData term = LocalizationManager.Sources[0].AddTerm(name);
            List<string> strings = new();
            foreach (string language in LocalizationManager.GetAllLanguages(false))
            {
                strings.Add(data.GetOrDefault(language, data.GetOrDefault("English", term.Term))!);
            }
            term.Languages = new Il2CppStringArray(strings.ToArray());
        }
    }

    /// <summary>
    /// Changes a key from a mod into a localization key.
    /// The method replaces each underscore with a dot. Then it replaces each pair of dots with one underscore.
    /// Thus, one underscore becomes a dot, and two underscores become one underscore.
    /// The method does not change dashes.
    /// </summary>
    /// <param name="key">The key from the mod.</param>
    /// <returns>The localization key.</returns>
    internal static string ReplaceDashesWithDots(string key)
    {
        string name = key.Replace("_", ".");
        return name.Replace("..", "_");
    }

    /// <summary>
    /// Applies all Harmony patches in the <see cref="Loc"/> class.
    /// </summary>
    internal static void Init()
    {
        Harmony.CreateAndPatchAll(typeof(Loc));
    }
}
