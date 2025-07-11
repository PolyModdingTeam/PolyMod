using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Reflection;
using LibCpp2IL;
using Polytopia.Data;

namespace PolyMod.Managers;

public static class Loc
{
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

	[HarmonyPrefix]
	[HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(Il2CppReferenceArray<Il2CppSystem.Object>))]
	private static bool Localization_Get(ref string key, Il2CppReferenceArray<Il2CppSystem.Object> args)
	{
		List<string> keys = key.Split('.').ToList();
		int? idx = null;
		string? name = null;
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
		if (idx != null)
		{
			foreach (var targetType in Loader.typeMappings.Values)
			{
				MethodInfo? methodInfo = typeof(EnumCache<>).MakeGenericType(targetType).GetMethod("TryGetName");
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
			if (name != null && idx != null)
			{
				int index = keys.IndexOf(idx.ToString()!);
				keys[index] = name;
				key = string.Join(".", keys);
			}
		}
		return true;
	}

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

	internal static void Init()
	{
		Harmony.CreateAndPatchAll(typeof(Loc));
	}
}
