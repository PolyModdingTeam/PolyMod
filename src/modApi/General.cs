using PolytopiaBackendBase.Game;
using UnityEngine;

namespace PolyMod.modApi;

public static class General
{
	/// <summary>
	/// Adds a button to the "select game mode" screen
	/// </summary>
	/// <param name="id">The text to display on the button</param>
	/// <param name="action">The action to take when the button is pressed</param>
	/// <param name="sprite">The image on the button.</param>
	public static void AddGameModeButton(string id, UIButtonBase.ButtonAction action, Sprite? sprite)
	{
		EnumCache<GameMode>.AddMapping(id, (GameMode)Registry.gameModesAutoidx);
		EnumCache<GameMode>.AddMapping(id, (GameMode)Registry.gameModesAutoidx);
		Loader.gamemodes.Add(new Loader.GameModeButtonsInformation(Registry.gameModesAutoidx, action, null, sprite));
		Registry.gameModesAutoidx++;
	}
	/// <summary>
	/// Adds a custom type to the gld.
	/// </summary>
	/// <param name="typeId"></param>
	/// <param name="type"></param>
	public static void AddPatchDataType(string typeId, Type type)
	{
		if (!Loader.typeMappings.ContainsKey(typeId))
			Loader.typeMappings.Add(typeId, type);
	}
}
