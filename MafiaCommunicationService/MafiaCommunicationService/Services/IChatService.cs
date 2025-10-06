using MafiaCommunicationService.Models;

namespace MafiaCommunicationService.Services;

public interface IChatService
{
    Task<Lobby> CreateNewLobbyAsync(LobbyCreationDto lobbyCreationDto);
    Task<Lobby?> GetLobbyAsync(string lobbyId);
    Task<bool> DeleteLobbyAsync(string lobbyId);
    
    Task<bool> PrivateChannelExistsAsync(string lobbyId, string channelName);
    Task<bool> UserHasAccessToChannelAsync(string lobbyId, string channelName, long userId);
    Task<IEnumerable<string>> GetPrivateChannelsAsync(string lobbyId);
    
    Task<bool> IsGlobalChatEnabledAsync(string lobbyId);
    Task<bool?> ToggleGlobalChatAsync(string lobbyId);
    
    Task SaveMessageAsync(ChatMessageEntity message);
    Task<IEnumerable<ChatMessageEntity>> GetMessageHistoryAsync(string lobbyId, string? channelName, int limit = 50);
}