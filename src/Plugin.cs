using System.Reflection;
using System.Text.Json;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using PolyMod.Loaders;
using PolyMod.Managers;

namespace PolyMod
{
	[BepInPlugin("com.polymod", "PolyMod", VERSION)]
	public class Plugin : BepInEx.Unity.IL2CPP.BasePlugin
	{
		internal record PolyConfig(
			bool debug = false
		);

		internal const string VERSION = Props.VERSION;
		internal const int AUTOIDX_STARTS_FROM = 1000;
		public static readonly string BASE_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "..");
		public static readonly string MODS_PATH = Path.Combine(BASE_PATH, "Mods");
		public static readonly string DUMPED_DATA_PATH = Path.Combine(BASE_PATH, "DumpedData");
		internal static readonly string CONFIG_PATH = Path.Combine(BASE_PATH, "PolyMod.json");
		internal static readonly Il2CppSystem.Version POLYTOPIA_VERSION = new(2, 12, 0);

#pragma warning disable CS8618
		internal static PolyConfig config;
		internal static ManualLogSource logger;
#pragma warning restore CS8618

		public override void Load()
		{
			try
			{
				config = JsonSerializer.Deserialize<PolyConfig>(File.ReadAllText(CONFIG_PATH))!;
			}
			catch
			{
				config = new();
				File.WriteAllText(CONFIG_PATH, JsonSerializer.Serialize(config));
			}
			if (!config.debug) ConsoleManager.DetachConsole();
			logger = Log;
			ConfigFile.CoreConfig[new("Logging.Disk", "WriteUnityLog")].BoxedValue = true;

			CompatibilityManager.Init();

			AudioClipLoader.Init();
			LocalizationLoader.Init();
			SpritesLoader.Init();
			VisualManager.Init();

			ModManager.Init();
		}

		internal static Stream GetResource(string id)
		{
			return Assembly.GetExecutingAssembly().GetManifestResourceStream(
				$"{typeof(Plugin).Namespace}.resources.{id}"
			)!;
		}
	}
}
