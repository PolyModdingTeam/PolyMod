
namespace PolyMod.Multiplayer.ViewModels;
public class SetupGameDataViewModel : IMonoServerResponseData
{
    public string lobbyId { get; set; }

	public byte[] serializedGameState { get; set; }

	public string gameSettingsJson { get; set; }
}