using MafiaCommunicationService.Models;

namespace MafiaCommunicationService.Services;

public interface IChatService
{
    Task<bool> LobbyExistsAsync(string lobbyId);
    Task<bool> PrivateChannelExistsAsync(string lobbyId, string channelName);
    Task<bool> UserHasAccessToChannelAsync(string lobbyId, string channelName, long userId);
    
    bool IsGlobalChatEnabled(string lobbyId);
    bool ToggleGlobalChat(string lobbyId);
    
    Task SaveMessageAsync(ChatMessageEntity message);
    Task<IEnumerable<ChatMessageEntity>> GetMessageHistoryAsync(string lobbyId, string? channelName, int limit = 50);
}