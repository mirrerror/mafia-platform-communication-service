namespace MafiaCommunicationService.Models;

public class ChatResponse
{
    public required string LobbyId { get; set; }
    public required long SenderId { get; set; }
    public required string SenderName { get; set; }
    public required string Content { get; set; }
    public required DateTime Timestamp { get; set; }
}