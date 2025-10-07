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
    IChatService chatService) : ControllerBase
{
    [HttpGet("lobby/{lobbyId}")]
    public async Task<IActionResult> GetLobby(string lobbyId)
    {
        var lobby = await chatService.GetLobbyAsync(lobbyId);
        if (lobby == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        return Ok(new ApiResponse<Lobby>(lobby));
    }

    [HttpPost("lobby/create")]
    public async Task<IActionResult> CreateLobby([FromBody] LobbyCreationDto lobbyCreationDto)
    {
        try
        {
            var lobby = await chatService.CreateNewLobbyAsync(lobbyCreationDto);
            return Ok(new ApiResponse<Lobby>(lobby));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ErrorResponse("LOBBY_EXISTS", ex.Message));
        }
    }
    
    [HttpDelete("lobby/{lobbyId}")]
    public async Task<IActionResult> DeleteLobby(string lobbyId)
    {
        var success = await chatService.DeleteLobbyAsync(lobbyId);
        if (!success)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        return Ok(new ApiResponse<object>(new { message = "Lobby deleted successfully" }));
    }

    [HttpPost("global/{lobbyId}/send-message")]
    public async Task<IActionResult> SendGlobalMessage(string lobbyId, [FromBody] ChatMessage message)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        if (!await chatService.IsGlobalChatEnabledAsync(lobbyId))
            return BadRequest(new ErrorResponse("CHAT_DISABLED", "Global chat is currently disabled for this lobby"));

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

        var response = new ChatResponse { LobbyId = lobbyId, SenderId = entity.SenderId, SenderName = entity.SenderName, Content = entity.Content, Timestamp = entity.Timestamp };
        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("ReceiveGlobalMessage", new ApiResponse<ChatResponse>(response));
        return Ok(new ApiResponse<ChatResponse>(response));
    }

    [HttpGet("global/{lobbyId}/history")]
    public async Task<IActionResult> GetGlobalChatHistory(string lobbyId)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        var history = await chatService.GetMessageHistoryAsync(lobbyId, null);
        return Ok(new ApiResponse<IEnumerable<ChatMessageEntity>>(history));
    }

    [HttpPost("global/{lobbyId}/toggle")]
    public async Task<IActionResult> ToggleGlobalChat(string lobbyId)
    {
        var newStatus = await chatService.ToggleGlobalChatAsync(lobbyId);
        if (newStatus == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        var response = new GlobalChatStatusResponse { LobbyId = lobbyId, IsGlobalChatEnabled = newStatus.Value };
        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("GlobalChatStatusChanged", new ApiResponse<GlobalChatStatusResponse>(response));
        return Ok(new ApiResponse<GlobalChatStatusResponse>(response));
    }

    [HttpGet("global/{lobbyId}/status")]
    public async Task<IActionResult> GetGlobalChatStatus(string lobbyId)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        var isEnabled = await chatService.IsGlobalChatEnabledAsync(lobbyId);
        var response = new GlobalChatStatusResponse { LobbyId = lobbyId, IsGlobalChatEnabled = isEnabled };
        return Ok(new ApiResponse<GlobalChatStatusResponse>(response));
    }

    [HttpPost("private/{lobbyId}/{channelName}/send-message")]
    public async Task<IActionResult> SendPrivateMessage(string lobbyId, string channelName, [FromBody] ChatMessage message)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        if (!await chatService.PrivateChannelExistsAsync(lobbyId, channelName))
            return NotFound(new ErrorResponse("CHANNEL_NOT_FOUND", "Private channel does not exist"));
        if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId))
            return StatusCode(403, new ErrorResponse("ACCESS_DENIED", "You do not have access to this private channel"));

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
        await hubContext.Clients.Group($"private_{channelName}_{lobbyId}").SendAsync("ReceivePrivateMessage", new ApiResponse<PrivateChatResponse>(response));
        return Ok(new ApiResponse<PrivateChatResponse>(response));
    }

    [HttpGet("private/{lobbyId}/{channelName}/history")]
    public async Task<IActionResult> GetPrivateChatHistory(string lobbyId, string channelName, [FromQuery] long userId)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        if (!await chatService.PrivateChannelExistsAsync(lobbyId, channelName))
            return NotFound(new ErrorResponse("CHANNEL_NOT_FOUND", "Channel does not exist"));

        if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, userId))
            return StatusCode(403, new ErrorResponse("ACCESS_DENIED", "You do not have access to this private channel's history"));

        var history = await chatService.GetMessageHistoryAsync(lobbyId, channelName);
        return Ok(new ApiResponse<IEnumerable<ChatMessageEntity>>(history));
    }

    [HttpGet("private/{lobbyId}/channels")]
    public async Task<IActionResult> GetPrivateChannels(string lobbyId)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        var channels = await chatService.GetPrivateChannelsAsync(lobbyId);
        return Ok(new ApiResponse<IEnumerable<string>>(channels));
    }
    
    [HttpPost("announcement/{lobbyId}")]
    public async Task<IActionResult> MakeAnnouncement(string lobbyId, [FromBody] AnnouncementDto announcementDto)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        var announcement = await chatService.CreateAnnouncementAsync(lobbyId, announcementDto);

        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("ReceiveAnnouncement", new ApiResponse<Announcement>(announcement));
        return Ok(new ApiResponse<Announcement>(announcement));
    }

    [HttpGet("announcement/{lobbyId}/history")]
    public async Task<IActionResult> GetAnnouncementHistory(string lobbyId)
    {
        if (await chatService.GetLobbyAsync(lobbyId) == null)
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        var history = await chatService.GetAnnouncementHistoryAsync(lobbyId);
        return Ok(new ApiResponse<IEnumerable<Announcement>>(history));
    }
}