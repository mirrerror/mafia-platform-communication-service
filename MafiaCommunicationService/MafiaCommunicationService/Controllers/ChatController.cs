using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace MafiaCommunicationService.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatController(
    IHubContext<ChatHub> hubContext,
    IChatService chatService,
    ILogger<ChatController> logger) : ControllerBase
{
    [HttpGet("lobby/{lobbyId}")]
    public async Task<IActionResult> GetLobby(string lobbyId)
    {
        logger.LogInformation("Attempting to get lobby with ID: {LobbyId}", lobbyId);
        var lobby = await chatService.GetLobbyAsync(lobbyId);
        if (lobby == null)
        {
            logger.LogWarning("Lobby not found for ID: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }
        
        logger.LogInformation("Successfully retrieved lobby with ID: {LobbyId}", lobbyId);
        return Ok(new ApiResponse<Lobby>(lobby));
    }

    [HttpPost("lobby/create")]
    public async Task<IActionResult> CreateLobby([FromBody] LobbyCreationDto lobbyCreationDto)
    {
        logger.LogInformation("Attempting to create lobby with ID: {LobbyId}", lobbyCreationDto.LobbyId);
        try
        {
            var lobby = await chatService.CreateNewLobbyAsync(lobbyCreationDto);
            logger.LogInformation("Successfully created lobby with ID: {LobbyId}", lobby.Id);
            return Ok(new ApiResponse<Lobby>(lobby));
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Failed to create lobby. Lobby already exists with ID: {LobbyId}", lobbyCreationDto.LobbyId);
            return BadRequest(new ErrorResponse("LOBBY_EXISTS", ex.Message));
        }
    }
    
    [HttpDelete("lobby/{lobbyId}")]
    public async Task<IActionResult> DeleteLobby(string lobbyId)
    {
        logger.LogInformation("Attempting to delete lobby with ID: {LobbyId}", lobbyId);
        var success = await chatService.DeleteLobbyAsync(lobbyId);
        if (!success)
        {
            logger.LogWarning("Failed to delete lobby. Lobby not found with ID: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        logger.LogInformation("Successfully deleted lobby with ID: {LobbyId}", lobbyId);
        return Ok(new ApiResponse<object>(new { message = "Lobby deleted successfully" }));
    }

    [HttpPost("global/{lobbyId}/send-message")]
    public async Task<IActionResult> SendGlobalMessage(string lobbyId, [FromBody] ChatMessage message)
    {
        logger.LogDebug("Attempting to send global message in lobby {LobbyId} from Sender {SenderId}", lobbyId, message.SenderId);
        
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("SendGlobalMessage failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        if (!await chatService.IsGlobalChatEnabledAsync(lobbyId))
        {
            logger.LogWarning("SendGlobalMessage failed. Global chat disabled for lobby: {LobbyId}", lobbyId);
            return BadRequest(new ErrorResponse("CHAT_DISABLED", "Global chat is currently disabled for this lobby"));
        }

        var entity = new ChatMessageEntity
        {
            LobbyId = lobbyId,
            ChannelName = null, // Global message
            SenderId = message.SenderId,
            SenderName = message.SenderName,
            Content = message.Content,
            Timestamp = DateTime.UtcNow
        };
        await chatService.SaveMessageAsync(entity);
        logger.LogInformation("Global message saved from {SenderId} in lobby {LobbyId}", message.SenderId, lobbyId);

        var response = new ChatResponse { LobbyId = lobbyId, SenderId = entity.SenderId, SenderName = entity.SenderName, Content = entity.Content, Timestamp = entity.Timestamp };
        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("ReceiveGlobalMessage", new ApiResponse<ChatResponse>(response));
        return Ok(new ApiResponse<ChatResponse>(response));
    }

    [HttpGet("global/{lobbyId}/history")]
    public async Task<IActionResult> GetGlobalChatHistory(string lobbyId)
    {
        logger.LogInformation("Attempting to get global chat history for lobby: {LobbyId}", lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("GetGlobalChatHistory failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        var history = await chatService.GetMessageHistoryAsync(lobbyId, null);
        logger.LogInformation("Successfully retrieved {Count} global messages for lobby: {LobbyId}", history.Count(), lobbyId);
        return Ok(new ApiResponse<IEnumerable<ChatMessageEntity>>(history));
    }

    [HttpPost("global/{lobbyId}/toggle")]
    public async Task<IActionResult> ToggleGlobalChat(string lobbyId)
    {
        logger.LogInformation("Attempting to toggle global chat for lobby: {LobbyId}", lobbyId);
        var newStatus = await chatService.ToggleGlobalChatAsync(lobbyId);
        if (newStatus == null)
        {
            logger.LogWarning("ToggleGlobalChat failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        var response = new GlobalChatStatusResponse { LobbyId = lobbyId, IsGlobalChatEnabled = newStatus.Value };
        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("GlobalChatStatusChanged", new ApiResponse<GlobalChatStatusResponse>(response));
        
        logger.LogInformation("Global chat status for lobby {LobbyId} changed to: {Status}", lobbyId, newStatus.Value);
        return Ok(new ApiResponse<GlobalChatStatusResponse>(response));
    }

    [HttpGet("global/{lobbyId}/status")]
    public async Task<IActionResult> GetGlobalChatStatus(string lobbyId)
    {
        logger.LogDebug("Attempting to get global chat status for lobby: {LobbyId}", lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("GetGlobalChatStatus failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        var isEnabled = await chatService.IsGlobalChatEnabledAsync(lobbyId);
        var response = new GlobalChatStatusResponse { LobbyId = lobbyId, IsGlobalChatEnabled = isEnabled };
        return Ok(new ApiResponse<GlobalChatStatusResponse>(response));
    }

    [HttpPost("private/{lobbyId}/{channelName}/send-message")]
    public async Task<IActionResult> SendPrivateMessage(string lobbyId, string channelName, [FromBody] ChatMessage message)
    {
        logger.LogDebug("Attempting to send private message in lobby {LobbyId}, channel {ChannelName} from Sender {SenderId}", lobbyId, channelName, message.SenderId);

        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("SendPrivateMessage failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        if (!await chatService.PrivateChannelExistsAsync(lobbyId, channelName))
        {
            logger.LogWarning("SendPrivateMessage failed. Channel not found: {ChannelName} in lobby: {LobbyId}", channelName, lobbyId);
            return NotFound(new ErrorResponse("CHANNEL_NOT_FOUND", "Private channel does not exist"));
        }
        if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId))
        {
            logger.LogWarning("SendPrivateMessage failed. Access denied for user {UserId} to channel {ChannelName} in lobby {LobbyId}", message.SenderId, channelName, lobbyId);
            return StatusCode(403, new ErrorResponse("ACCESS_DENIED", "You do not have access to this private channel"));
        }

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
        logger.LogInformation("Private message saved from {SenderId} in lobby {LobbyId}, channel {ChannelName}", message.SenderId, lobbyId, channelName);

        var response = new PrivateChatResponse { LobbyId = lobbyId, ChannelName = channelName, SenderId = entity.SenderId, SenderName = entity.SenderName, Content = entity.Content, Timestamp = entity.Timestamp };
        await hubContext.Clients.Group($"private_{channelName}_{lobbyId}").SendAsync("ReceivePrivateMessage", new ApiResponse<PrivateChatResponse>(response));
        return Ok(new ApiResponse<PrivateChatResponse>(response));
    }

    [HttpGet("private/{lobbyId}/{channelName}/history")]
    public async Task<IActionResult> GetPrivateChatHistory(string lobbyId, string channelName, [FromQuery] long userId)
    {
        logger.LogInformation("Attempting to get private chat history for lobby {LobbyId}, channel {ChannelName} by user {UserId}", lobbyId, channelName, userId);

        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("GetPrivateChatHistory failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        if (!await chatService.PrivateChannelExistsAsync(lobbyId, channelName))
        {
            logger.LogWarning("GetPrivateChatHistory failed. Channel not found: {ChannelName} in lobby: {LobbyId}", channelName, lobbyId);
            return NotFound(new ErrorResponse("CHANNEL_NOT_FOUND", "Channel does not exist"));
        }

        if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, userId))
        {
            logger.LogWarning("GetPrivateChatHistory failed. Access denied for user {UserId} to channel {ChannelName} in lobby {LobbyId}", userId, channelName, lobbyId);
            return StatusCode(403, new ErrorResponse("ACCESS_DENIED", "You do not have access to this private channel's history"));
        }

        var history = await chatService.GetMessageHistoryAsync(lobbyId, channelName);
        logger.LogInformation("Successfully retrieved {Count} private messages for lobby {LobbyId}, channel {ChannelName}", history.Count(), lobbyId, channelName);
        return Ok(new ApiResponse<IEnumerable<ChatMessageEntity>>(history));
    }

    [HttpGet("private/{lobbyId}/channels")]
    public async Task<IActionResult> GetPrivateChannels(string lobbyId)
    {
        logger.LogInformation("Attempting to get private channels for lobby: {LobbyId}", lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("GetPrivateChannels failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        var channels = await chatService.GetPrivateChannelsAsync(lobbyId);
        logger.LogInformation("Successfully retrieved {Count} private channels for lobby: {LobbyId}", channels.Count(), lobbyId);
        return Ok(new ApiResponse<IEnumerable<string>>(channels));
    }
    
    [HttpPost("announcement/{lobbyId}")]
    public async Task<IActionResult> MakeAnnouncement(string lobbyId, [FromBody] AnnouncementDto announcementDto)
    {
        logger.LogInformation("Attempting to make announcement in lobby: {LobbyId}", lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("MakeAnnouncement failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        var announcement = await chatService.CreateAnnouncementAsync(lobbyId, announcementDto);
        logger.LogInformation("Announcement created in lobby {LobbyId}. Content: {Content}", lobbyId, string.Concat(announcementDto.Content.AsSpan(0, Math.Min(announcementDto.Content.Length, 50)), "..."));

        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("ReceiveAnnouncement", new ApiResponse<Announcement>(announcement));
        return Ok(new ApiResponse<Announcement>(announcement));
    }

    [HttpGet("announcement/{lobbyId}/history")]
    public async Task<IActionResult> GetAnnouncementHistory(string lobbyId)
    {
        logger.LogInformation("Attempting to get announcement history for lobby: {LobbyId}", lobbyId);
        if (await chatService.GetLobbyAsync(lobbyId) == null)
        {
            logger.LogWarning("GetAnnouncementHistory failed. Lobby not found: {LobbyId}", lobbyId);
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        }

        var history = await chatService.GetAnnouncementHistoryAsync(lobbyId);
        logger.LogInformation("Successfully retrieved {Count} announcements for lobby: {LobbyId}", history.Count(), lobbyId);
        return Ok(new ApiResponse<IEnumerable<Announcement>>(history));
    }
}