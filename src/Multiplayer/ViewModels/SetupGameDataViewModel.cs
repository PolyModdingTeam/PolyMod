
namespace PolyMod.Multiplayer.ViewModels;
public class SetupGameDataViewModel : IMonoServerResponseData
{
    public string lobbyId { get; set; } = string.Empty;

	public byte[] serializedGameState { get; set; } = Array.Empty<byte>();

	public byte[] serializedGameSummary { get; set; } = Array.Empty<byte>();

	public string gameSettingsJson { get; set; } = string.Empty;

	public int initialCommandCount { get; set; } = -1;

	public string? currentPlayerId { get; set; }
}
