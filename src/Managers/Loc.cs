using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LibCpp2IL;
using Polytopia.Data;

namespace PolyMod.Managers
{
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
		public static void BuildAndLoadLocalization(Dictionary<string, Dictionary<string, string>> localization)
		{
			foreach (var (key, data) in localization)
			{
				string name = key.Replace("_", ".");
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
}