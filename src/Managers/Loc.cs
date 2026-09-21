using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Reflection;
using LibCpp2IL;
using Polytopia.Data;
using PolytopiaBackendBase.Common;

namespace PolyMod.Managers;

/// <summary>
/// Manages localization for PolyMod.
/// </summary>
public static class Loc
{
	internal static Dictionary<string, Dictionary<string, string>> languagesToAdd = new();
	internal static Dictionary<string, string> buildingsInfoOverrides = new();

	/// <summary>
	/// Patches the localization getter to handle custom enum values.
	/// </summary>
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
				if(!typeMapping.shouldCreateCache)
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
				int index = keys.IndexOf(idx.ToString()!);
				keys[index] = name;
				key = string.Join(".", keys);
			}
		}
		return true;
	}

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

		foreach(var languageCode in languagesToAdd.Keys)
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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(BuildingUtils), nameof(BuildingUtils.GetInfo))]
	private static bool BuildingUtils_GetInfo(ref string __result, SkinType skinOfCurrentLocalPlayer,
		ImprovementData improvementData, ImprovementState improvementState, PlayerState owner, TileData tileData)
	{
		TribeType tribe = TribeType.None;
		if (owner != null)
			tribe = owner.tribe;

		string key = EnumCache<ImprovementData.Type>.GetName(improvementData.type);
		if(buildingsInfoOverrides.ContainsKey(key))
		{
			__result = Localization.GetSkinned(skinOfCurrentLocalPlayer, tribe, buildingsInfoOverrides[key]);
			return false;
		}

		return true;
	}

	/// <summary>
	/// Builds and loads localization data from a dictionary.
	/// </summary>
	/// <param name="localization">A dictionary containing the localization data.</param>
	internal static void BuildAndLoadLocalization(Dictionary<string, Dictionary<string, string>> localization)
	{
		foreach (var (key, data) in localization)
		{
			string name = ReplaceDashesWithDots(key);
			if (name.StartsWith("tribeskins")) name = "TribeSkins/" + name;
			TermData term = LocalizationManager.Sources[0].AddTerm(name);
			List<string> strings = new();
			foreach (string language in LocalizationManager.GetAllLanguages(false))
			{
				strings.Add(data.GetOrDefault(language, data.GetOrDefault("English", term.Term))!);
			}
			term.Languages = new Il2CppStringArray(strings.ToArray());
		}
	}

	internal static string ReplaceDashesWithDots(string key)
	{
		string name = key.Replace("_", ".");
		return name.Replace("..", "_");
	}

	/// <summary>
	/// Initializes the Loc manager by patching the necessary methods.
	/// </summary>
	internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Loc));
	}
}
