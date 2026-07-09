namespace PolyMod.Multiplayer.ViewModels;
public class ModdedGameStateViewModel
{
	public string gameId { get; set; } = "";
    public string lobbyId { get; set; } = "";

	public byte[] serializedGameState { get; set; } = new byte[0];
	public byte[] serializedGameSummary { get; set; } = new byte[0];

	public string gameSettingsJson { get; set; } = "";
	public string currentPlayerId { get; set; } = "";
	public bool IsEndTurnCommand { get; set; } = false;
}