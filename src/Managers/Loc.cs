using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Reflection;
using LibCpp2IL;
using Polytopia.Data;
using Cpp2IL.Core.Il2CppApiFunctions;

namespace PolyMod.Managers;
public static class Loc
{
	[HarmonyPostfix]
	[HarmonyPatch(typeof(SelectTribePopup), nameof(SelectTribePopup.SetDescription))]
	private static void SetDescription(SelectTribePopup __instance)
	{
		if ((int)__instance.SkinType >= Plugin.AUTOIDX_STARTS_FROM)
		{
			__instance.Description = Localization.Get(__instance.SkinType.GetLocalizationDescriptionKey()) + "\n\n" + Localization.GetSkinned(__instance.SkinType, __instance.tribeData.description2, new Il2CppSystem.Object[]
			{
				__instance.tribeName,
				Localization.Get(__instance.startTechSid, Array.Empty<Il2CppSystem.Object>())
			});
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(Il2CppReferenceArray<Il2CppSystem.Object>))]
	private static void Localization_Get(Localization __instance, string __result, string key, Il2CppReferenceArray<Il2CppSystem.Object> args)
	{
		Console.Write("/////////////////////////////");
		Console.Write(key);
		List<string> keys = key.Split('.').ToList();
		int? idx = null;
		string name = null;
		foreach (string item in keys)
		{
			Console.Write(item);
			if(int.TryParse(item, out int parsedIdx))
			{
				idx = parsedIdx;
				Console.Write(idx);
				Console.Write("Parsed correctly");
			}
		}
		if(idx != null)
		{
			foreach (var targetType in Loader.typeMappings.Values)
			{
				Console.Write(targetType);
				MethodInfo? methodInfo = typeof(EnumCache<>).MakeGenericType(targetType).GetMethod("TryGetName");
				if (methodInfo != null)
				{
					object methodInvokeResult = methodInfo.Invoke(null, new object[] { idx, name});
					if((bool)methodInvokeResult)
					{
						Console.Write("INVOKED AND GOT THE NAME");
					}
				}
			}
			if(name != null)
			{
				int index = keys.IndexOf(idx.ToString());
				keys[index] = name;
				string newKey = string.Join(".", keys);
				Console.Write("Returned new result");
				__result = Localization.Get(newKey, args);
			}
		}
		Console.Write("/////////////////////////////");
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
