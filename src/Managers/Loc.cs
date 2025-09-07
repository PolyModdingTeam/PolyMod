using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Reflection;
using LibCpp2IL;
using Polytopia.Data;

namespace PolyMod.Managers;

/// <summary>
/// Manages localization for PolyMod.
/// </summary>
public static class Loc
{
	public record TermInfo(string term, string localizedString);
	internal static Dictionary<int, string> languagesToAdd = new();

	/// <summary>
	/// Patches the tribe selection popup to correctly display descriptions for custom skins.
	/// </summary>
	[HarmonyPostfix]
	[HarmonyPatch(typeof(SelectTribePopup), nameof(SelectTribePopup.SetDescription))]
	private static void SetDescription(SelectTribePopup __instance)
	{
		if ((int)__instance.SkinType >= Plugin.AUTOIDX_STARTS_FROM)
		{
			string description = Localization.Get(__instance.SkinType.GetLocalizationDescriptionKey());
			if (description == __instance.SkinType.GetLocalizationDescriptionKey())
			{
				description = Localization.Get(__instance.tribeData.description, new Il2CppSystem.Object[]
				{
					Localization.Get(__instance.tribeData.displayName),
				});
			}
			__instance.Description = description + "\n\n" + Localization.GetSkinned(__instance.SkinType, __instance.tribeData.description2, new Il2CppSystem.Object[]
			{
				__instance.tribeName,
				Localization.Get(__instance.startTechSid, Array.Empty<Il2CppSystem.Object>())
			});
		}
	}

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

	/// <summary>
	/// Builds and loads localization data from a dictionary.
	/// </summary>
	/// <param name="localization">A dictionary containing the localization data.</param>
	public static void BuildAndLoadLocalization(Dictionary<string, Dictionary<string, string>> localization)
	{
		foreach (var (key, data) in localization)
		{
			string name = key.Replace("_", ".");
			name = name.Replace("..", "_");
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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(SettingsScreen), nameof(SettingsScreen.LanguageChangedCallback))]
	public static bool LanguageChangedCallback(SettingsScreen __instance, int index)
	{
		Console.Write("LanguageChangedCallback");
		Console.Write(index);
		Il2CppSystem.Nullable<int> il2cppNullable = __instance.languageSelector.GetIdForIndex(index);
		Console.Write(il2cppNullable.HasValue);
		if (!il2cppNullable.HasValue)
		{
			var allLanguages = LocalizationManager.GetAllLanguages();
			string languageName = allLanguages[index];
			string languageCode = LocalizationManager.GetLanguageCode(languageName);
			Console.Write(languageCode);
			SettingsUtils.Language = languageCode;
			LocalizationManager.CurrentLanguage = languageName;
			UINavigationManager.Select(__instance.languageSelector.GetCurrentSelectable());
		}
		else
		{
			Console.Write(il2cppNullable.Value);
		}
		return il2cppNullable.HasValue;
	}
	public static void BuildAndLoadCustomLanguage(string name, Dictionary<string, string> terms)
	{
		LanguageSourceData source = LocalizationManager.Sources[0];
		int languageIndex = source.GetLanguageIndex(name);
		string languageName = terms["language"];
		if (languageIndex == -1)
		{
			source.AddLanguage(terms["language"], name);
			languageIndex = source.GetLanguageIndex(languageName);
			languagesToAdd[languageIndex] = terms["language"];
			Console.Write($"{name} language added.");
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

		// LocalizationManager.CurrentLanguage = name;

		Console.Write($"{name} strings added and language activated!");
	}

	/// <summary>
	/// Initializes the Loc manager by patching the necessary methods.
	/// </summary>
	internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Loc));
	}
}
