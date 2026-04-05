
using Tesla;

namespace PolyMod.Multiplayer.ViewModels;
public class SetupGameStateViewModel : IMonoServerResponseData
{
	public string gameId { get; set; } = "";
    public string lobbyId { get; set; } = "";

	public byte[] serializedGameState { get; set; } = new byte[0];
	public byte[] serializedGameSummary { get; set; } = new byte[0];

	public string gameSettingsJson { get; set; } = "";
}