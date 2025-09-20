using System.ComponentModel.DataAnnotations;
using System.Text;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.AspNetCore.SignalR;

namespace MafiaCommunicationService.Hubs;

public class ChatHub(IChatService chatService) : Hub
{
    public async Task SendGlobalMessage(string lobbyId, ChatMessage message)
    {
        var lobby = chatService.GetLobby(lobbyId);
        if (lobby == null)
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        
        ValidateMessage(message);
        if (!chatService.IsGlobalChatEnabled(lobbyId))
            throw new HubException("Global chat is currently disabled for this lobby.");
        
        var entity = new ChatMessageEntity
        {
            LobbyId = lobbyId,
            ChannelName = null,
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            Content = message.Content,
            Timestamp = DateTime.UtcNow
        };
        await chatService.SaveMessageAsync(entity);

        var response = new ChatResponse { LobbyId = lobbyId, SenderId = entity.SenderId, SenderName = entity.SenderName, Content = entity.Content, Timestamp = entity.Timestamp };
        await Clients.Group($"global_{lobbyId}").SendAsync("ReceiveGlobalMessage", response);
    }

    public async Task SendPrivateMessage(string channelName, string lobbyId, ChatMessage message)
    {
        var lobby = chatService.GetLobby(lobbyId);
        if (lobby == null)
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        
        ValidateMessage(message);
        if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId))
            throw new HubException("ACCESS_DENIED: You do not have access to this private channel.");
        
        var entity = new ChatMessageEntity
        {
            LobbyId = lobbyId,
            ChannelName = channelName,
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            Content = message.Content,
            Timestamp = DateTime.UtcNow
        };
        await chatService.SaveMessageAsync(entity);
        
        var response = new PrivateChatResponse { LobbyId = lobbyId, ChannelName = channelName, SenderId = entity.SenderId, SenderName = entity.SenderName, Content = entity.Content, Timestamp = entity.Timestamp };
        await Clients.Group($"private_{channelName}_{lobbyId}").SendAsync("ReceivePrivateMessage", response);
    }

    public async Task JoinGlobalChat(string lobbyId, long userId)
    {
        var lobby = chatService.GetLobby(lobbyId);
        if (lobby == null)
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        
        await Groups.AddToGroupAsync(Context.ConnectionId, $"global_{lobbyId}");
    }

    public Task LeaveGlobalChat(string lobbyId, long userId)
    {
        var lobby = chatService.GetLobby(lobbyId);
        if (lobby == null)
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"global_{lobbyId}");
    }

    public async Task JoinPrivateChannel(string lobbyId, string channelName, long userId)
    {
        var lobby = chatService.GetLobby(lobbyId);
        if (lobby == null)
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        
        if (!await chatService.PrivateChannelExistsAsync(lobbyId, channelName))
            throw new HubException("CHANNEL_NOT_FOUND: Private channel does not exist.");
        if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, userId))
            throw new HubException("ACCESS_DENIED: You do not have access to this private channel.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"private_{channelName}_{lobbyId}");
    }

    public Task LeavePrivateChannel(string lobbyId, string channelName, long userId)
    {
        var lobby = chatService.GetLobby(lobbyId);
        if (lobby == null)
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"private_{channelName}_{lobbyId}");
    }
    
    private static void ValidateMessage(ChatMessage message)
    {
        var validationContext = new ValidationContext(message);
        var validationResults = new List<ValidationResult>();

        if (Validator.TryValidateObject(message, validationContext, validationResults, true)) return;
        
        var errorBuilder = new StringBuilder("Invalid message received:");
        foreach (var validationResult in validationResults)
        {
            errorBuilder.Append($" - {validationResult.ErrorMessage}");
        }

        throw new HubException(errorBuilder.ToString());
    }
}