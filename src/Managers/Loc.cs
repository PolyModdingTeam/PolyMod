using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Reflection;
using LibCpp2IL;

namespace PolyMod.Managers;

/// <summary>
/// Manages localization for PolyMod.
/// </summary>
public static class Loc
{
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

	/// <summary>
	/// Initializes the Loc manager by patching the necessary methods.
	/// </summary>
	internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Loc));
	}
}
