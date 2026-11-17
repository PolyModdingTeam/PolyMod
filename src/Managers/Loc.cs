using System.Globalization;
using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
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
    /// The localization keys that the game makes from an enum value with <c>ToString</c>, by key prefix.
    /// For a custom enum value, <c>ToString</c> gives the number, so the key has the number in place of the name.
    /// Each entry gets the name of a value from the <c>EnumCache</c> of the enum type, or null if the value has no name.
    /// <c>action.info.clear</c> must come before <c>action.info.</c>, because both prefixes match the same keys.
    /// </summary>
    private static readonly (string prefix, Func<int, string?> getName)[] enumValueKeyPrefixes =
    {
        ("action.info.clear", value => EnumCache<TileData.EffectType>.TryGetName((TileData.EffectType)value, out string name) ? name : null),
        ("action.info.", value => EnumCache<CommandType>.TryGetName((CommandType)value, out string name) ? name : null),
        ("actionbox.unit.", value => EnumCache<UnitEffect>.TryGetName((UnitEffect)value, out string name) ? name : null),
        ("actionbox.building.", value => EnumCache<ImprovementEffect>.TryGetName((ImprovementEffect)value, out string name) ? name : null),
        ("wcontroller.reward.", value => EnumCache<CityReward>.TryGetName((CityReward)value, out string name) ? name : null),
        ("tribes.", value => EnumCache<TribeType>.TryGetName((TribeType)value, out string name) ? name : null),
    };

    /// <summary>
    /// Replaces the number of a custom enum value in a localization key with the name of the value.
    /// </summary>
    /// <remarks>
    /// The method only changes keys that start with a prefix in <see cref="enumValueKeyPrefixes"/>, directly followed by a number that is not less than <see cref="Plugin.AUTOIDX_STARTS_FROM"/>.
    /// After the number, the key can end, or continue with <c>_</c> or <c>.</c>, for example for the skin suffix that <c>Localization.GetSkinned</c> adds.
    /// For example, it changes <c>action.info.1000</c> to <c>action.info.mycommand</c>, and <c>action.info.1000_swamp</c> to <c>action.info.mycommand_swamp</c>.
    /// </remarks>
    /// <param name="key">The localization key.</param>
    /// <returns>The key with the name of the value, or the key without changes.</returns>
    private static string ReplaceEnumValueInKey(string key)
    {
        foreach (var (prefix, getName) in enumValueKeyPrefixes)
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                continue;

            int numberEnd = prefix.Length;
            while (numberEnd < key.Length && key[numberEnd] >= '0' && key[numberEnd] <= '9')
                numberEnd++;

            bool numberIsWholePart = numberEnd == key.Length || key[numberEnd] == '_' || key[numberEnd] == '.';
            if (numberEnd == prefix.Length || !numberIsWholePart
                || !int.TryParse(key.AsSpan(prefix.Length, numberEnd - prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                || value < Plugin.AUTOIDX_STARTS_FROM)
                return key;

            string? name = getName(value);
            return name == null ? key : string.Concat(prefix, name, key.AsSpan(numberEnd));
        }
        return key;
    }

    /// <summary>
    /// A Harmony prefix on <c>LocalizationManager.TryGetTranslation</c> of I2 Localization.
    /// It changes the key with <see cref="ReplaceEnumValueInKey"/>.
    /// </summary>
    /// <remarks>
    /// All lookups of localized text use this method: <c>Localization.Get</c>, the skin lookup of <c>Localization.GetSkinned</c>, and the direct lookups of the game, for example the tribe names in the stats rows.
    /// </remarks>
    /// <param name="Term">The localization key. The patch can change it.</param>
    /// <returns>Always true, which runs the original method.</returns>
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.TryGetTranslation))]
    private static bool LocalizationManager_TryGetTranslation(ref string Term)
    {
        if (!string.IsNullOrEmpty(Term))
            Term = ReplaceEnumValueInKey(Term);
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
