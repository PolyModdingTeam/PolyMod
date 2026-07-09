
namespace PolyMod.Multiplayer.ViewModels;
public class SetupGameDataViewModel : IMonoServerResponseData
{
    public string lobbyId { get; set; } = string.Empty;

	public byte[] serializedGameState { get; set; } = Array.Empty<byte>();

	public string gameSettingsJson { get; set; } = string.Empty;
}