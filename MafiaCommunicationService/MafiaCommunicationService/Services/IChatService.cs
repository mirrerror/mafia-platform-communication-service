using MafiaCommunicationService.Models;

namespace MafiaCommunicationService.Services;

public interface IChatService
{
    Lobby CreateNewLobby(LobbyCreationDto lobbyCreationDto);
    Lobby? GetLobby(string lobbyId);
    void DeleteLobby(string lobbyId);
    
    Task<bool> PrivateChannelExistsAsync(string lobbyId, string channelName);
    Task<bool> UserHasAccessToChannelAsync(string lobbyId, string channelName, long userId);
    Task<IEnumerable<string>> GetPrivateChannelsAsync(string lobbyId);
    
    bool IsGlobalChatEnabled(string lobbyId);
    bool ToggleGlobalChat(string lobbyId);
    
    Task SaveMessageAsync(ChatMessageEntity message);
    Task<IEnumerable<ChatMessageEntity>> GetMessageHistoryAsync(string lobbyId, string? channelName, int limit = 50);
}