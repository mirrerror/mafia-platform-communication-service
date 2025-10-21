using System.ComponentModel.DataAnnotations;
using System.Text;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.AspNetCore.SignalR;

namespace MafiaCommunicationService.Hubs;

public class ChatHub(IChatService chatService, ILogger<ChatHub> logger) : Hub
{
    public async Task SendGlobalMessage(string lobbyId, ChatMessage message)
    {
        logger.LogDebug("Hub: Attempting SendGlobalMessage in lobby {LobbyId} from {SenderId}", lobbyId, message.SenderId);
        try
        {
            if (await chatService.GetLobbyAsync(lobbyId) == null)
                throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");

            ValidateMessage(message);
            if (!await chatService.IsGlobalChatEnabledAsync(lobbyId))
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
            logger.LogInformation("Hub: Global message saved from {SenderId} in lobby {LobbyId}", message.SenderId, lobbyId);

            var response = new ChatResponse { LobbyId = lobbyId, SenderId = entity.SenderId, SenderName = entity.SenderName, Content = entity.Content, Timestamp = entity.Timestamp };
            await Clients.Group($"global_{lobbyId}").SendAsync("ReceiveGlobalMessage", new ApiResponse<ChatResponse>(response));
        }
        catch (HubException ex)
        {
            logger.LogWarning(ex, "HubException in SendGlobalMessage for lobby {LobbyId}, sender {SenderId}: {Message}", lobbyId, message.SenderId, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in SendGlobalMessage for lobby {LobbyId}, sender {SenderId}", lobbyId, message.SenderId);
            throw new HubException("An unexpected error occurred while sending the global message.", ex);
        }
    }

    public async Task SendPrivateMessage(string channelName, string lobbyId, ChatMessage message)
    {
        logger.LogDebug("Hub: Attempting SendPrivateMessage in lobby {LobbyId}, channel {ChannelName} from {SenderId}", lobbyId, channelName, message.SenderId);
        try
        {
            if (await chatService.GetLobbyAsync(lobbyId) == null)
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
            logger.LogInformation("Hub: Private message saved from {SenderId} in lobby {LobbyId}, channel {ChannelName}", message.SenderId, lobbyId, channelName);

            var response = new PrivateChatResponse { LobbyId = lobbyId, ChannelName = channelName, SenderId = entity.SenderId, SenderName = entity.SenderName, Content = entity.Content, Timestamp = entity.Timestamp };
            await Clients.Group($"private_{channelName}_{lobbyId}").SendAsync("ReceivePrivateMessage", new ApiResponse<PrivateChatResponse>(response));
        }
        catch (HubException ex)
        {
            logger.LogWarning(ex, "HubException in SendPrivateMessage for lobby {LobbyId}, channel {ChannelName}, sender {SenderId}: {Message}", lobbyId, channelName, message.SenderId, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error in SendPrivateMessage for lobby {LobbyId}, channel {ChannelName}, sender {SenderId}", lobbyId, channelName, message.SenderId);
            throw new HubException("An unexpected error occurred while sending the private message.", ex);
        }
    }

    public async Task JoinGlobalChat(string lobbyId, long userId)
    {
        logger.LogDebug("Hub: User {UserId} attempting to join global chat for lobby {LobbyId}", userId, lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("Hub: JoinGlobalChat failed. Lobby not found: {LobbyId}", lobbyId);
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"global_{lobbyId}");
        logger.LogInformation("Hub: User {UserId} (Connection {ConnectionId}) joined global chat for lobby {LobbyId}", userId, Context.ConnectionId, lobbyId);
    }

    public async Task LeaveGlobalChat(string lobbyId, long userId)
    {
        logger.LogDebug("Hub: User {UserId} attempting to leave global chat for lobby {LobbyId}", userId, lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("Hub: LeaveGlobalChat failed. Lobby not found: {LobbyId}", lobbyId);
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"global_{lobbyId}");
        logger.LogInformation("Hub: User {UserId} (Connection {ConnectionId}) left global chat for lobby {LobbyId}", userId, Context.ConnectionId, lobbyId);
    }

    public async Task JoinPrivateChannel(string lobbyId, string channelName, long userId)
    {
        logger.LogDebug("Hub: User {UserId} attempting to join private channel {ChannelName} for lobby {LobbyId}", userId, channelName, lobbyId);
        try
        {
            if (await chatService.GetLobbyAsync(lobbyId) == null)
                throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");

            if (!await chatService.PrivateChannelExistsAsync(lobbyId, channelName))
                throw new HubException("CHANNEL_NOT_FOUND: Private channel does not exist.");
            if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, userId))
                throw new HubException("ACCESS_DENIED: You do not have access to this private channel.");

            await Groups.AddToGroupAsync(Context.ConnectionId, $"private_{channelName}_{lobbyId}");
            logger.LogInformation("Hub: User {UserId} (Connection {ConnectionId}) joined private channel {ChannelName} for lobby {LobbyId}", userId, Context.ConnectionId, channelName, lobbyId);
        }
        catch (HubException ex)
        {
            logger.LogWarning(ex, "HubException in JoinPrivateChannel for lobby {LobbyId}, channel {ChannelName}, user {UserId}: {Message}", lobbyId, channelName, userId, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
             logger.LogError(ex, "Unexpected error in JoinPrivateChannel for lobby {LobbyId}, channel {ChannelName}, user {UserId}", lobbyId, channelName, userId);
             throw new HubException("An unexpected error occurred while joining the private channel.", ex);
        }
    }

    public async Task LeavePrivateChannel(string lobbyId, string channelName, long userId)
    {
        logger.LogDebug("Hub: User {UserId} attempting to leave private channel {ChannelName} for lobby {LobbyId}", userId, channelName, lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("Hub: LeavePrivateChannel failed. Lobby not found: {LobbyId}", lobbyId);
            throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"private_{channelName}_{lobbyId}");
        logger.LogInformation("Hub: User {UserId} (Connection {ConnectionId}) left private channel {ChannelName} for lobby {LobbyId}", userId, Context.ConnectionId, channelName, lobbyId);
    }

    public async Task SendAnnouncement(string lobbyId, AnnouncementDto announcementDto)
    {
        logger.LogInformation("Hub: Attempting SendAnnouncement in lobby: {LobbyId}", lobbyId);
        try
        {
            if (await chatService.GetLobbyAsync(lobbyId) == null)
                throw new HubException("LOBBY_NOT_FOUND: Lobby does not exist.");

            var announcement = await chatService.CreateAnnouncementAsync(lobbyId, announcementDto);
            logger.LogInformation("Hub: Announcement created in lobby {LobbyId}. Content: {Content}", lobbyId, string.Concat(announcementDto.Content.AsSpan(0, Math.Min(announcementDto.Content.Length, 50)), "..."));

            await Clients.Group($"global_{lobbyId}").SendAsync("ReceiveAnnouncement", new ApiResponse<Announcement>(announcement));
        }
        catch (HubException ex)
        {
            logger.LogWarning(ex, "HubException in SendAnnouncement for lobby {LobbyId}: {Message}", lobbyId, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
             logger.LogError(ex, "Unexpected error in SendAnnouncement for lobby {LobbyId}", lobbyId);
             throw new HubException("An unexpected error occurred while sending the announcement.", ex);
        }
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