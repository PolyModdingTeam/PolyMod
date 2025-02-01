using HarmonyLib;
using I2.Loc;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using LibCpp2IL;

namespace PolyMod.Loaders
{
	public static class LocalizationLoader
	{
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
			Harmony.CreateAndPatchAll(typeof(LocalizationLoader));
		}
	}
}