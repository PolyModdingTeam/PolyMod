namespace PolyMod.Multiplayer.ViewModels;

/// <summary>
/// Payload of the UpdateGameStateModded hub method.
/// The locally executed command batch plus everything the server would normally compute itself (new state, summary, turn metadata).
/// </summary>
public class ModdedGameStateViewModel
{
	public string gameId { get; set; } = string.Empty;

	public List<ModdedCommandViewModel> commands { get; set; } = new();

	public byte[] serializedGameState { get; set; } = Array.Empty<byte>();

	public byte[] serializedGameSummary { get; set; } = Array.Empty<byte>();

	public int newCommandCount { get; set; } = -1;

	public string? currentPlayerId { get; set; }

	public bool isEndTurn { get; set; }

	public bool isGameEnded { get; set; }

	public string? resignedPlayerId { get; set; }
}

public class ModdedCommandViewModel
{
	public byte[] serializedData { get; set; } = Array.Empty<byte>();

	public int commandIndex { get; set; } = -1;
}
