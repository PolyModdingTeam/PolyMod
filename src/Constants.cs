namespace PolyMod;

/// <summary>
/// useful constant values.
/// </summary>
public partial class Constants
{
	/// <summary>
	/// Path of the polytopia folder
	/// </summary>
	public static readonly string BASE_PATH = Path.Combine(BepInEx.Paths.BepInExRootPath, "..");
	/// <summary>
	/// path of the mods folder
	/// </summary>
	public static readonly string MODS_PATH = Path.Combine(BASE_PATH, "Mods");
	internal static readonly string CONFIG_PATH = Path.Combine(BASE_PATH, "PolyMod.json");
	internal static readonly string DISCORD_LINK = "https://discord.gg/eWPdhWtfVy";
	internal const int AUTOIDX_STARTS_FROM = 1000;
	internal static readonly List<string> LOG_MESSAGES_IGNORE = new()
	{
		"Failed to find atlas",
		"Could not find sprite",
		"Couldn't find prefab for type",
		"MARKET: id:",
		"Missing name for value",
	};
	/// <summary>
	/// kFilename of the dumped data
	/// </summary>
	public static readonly string DUMPED_DATA_PATH = Path.Combine(BASE_PATH, "DumpedData");
	internal static readonly string CHECKSUM_PATH
		= Path.Combine(BASE_PATH, "CHECKSUM");
	internal const string INCOMPATIBILITY_WARNING_LAST_VERSION_KEY
		= "INCOMPATIBILITY_WARNING_LAST_VERSION";
}
