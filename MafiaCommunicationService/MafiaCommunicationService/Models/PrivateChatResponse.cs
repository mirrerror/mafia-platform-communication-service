namespace MafiaCommunicationService.Models;

public class PrivateChatResponse : ChatResponse
{
    public required string ChannelName { get; set; }
}