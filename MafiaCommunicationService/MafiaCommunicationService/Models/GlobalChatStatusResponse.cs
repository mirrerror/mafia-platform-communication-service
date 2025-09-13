namespace MafiaCommunicationService.Models;

public class GlobalChatStatusResponse
{
    public required string LobbyId { get; set; }
    public required bool IsGlobalChatEnabled { get; set; }
}