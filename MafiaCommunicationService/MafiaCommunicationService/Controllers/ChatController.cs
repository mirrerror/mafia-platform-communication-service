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

    [HttpPost("global/{lobbyId}/send-message")]
    public async Task<IActionResult> SendGlobalMessage(string lobbyId, [FromBody] ChatMessage message)
    {
        if (!chatService.IsGlobalChatEnabled(lobbyId))
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
        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("ReceiveGlobalMessage", response);
        return Ok(response);
    }

    [HttpGet("global/{lobbyId}/history")]
    public async Task<IActionResult> GetGlobalChatHistory(string lobbyId)
    {
        if (!await chatService.LobbyExistsAsync(lobbyId))
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));

        var history = await chatService.GetMessageHistoryAsync(lobbyId, null);
        return Ok(history);
    }

    [HttpPost("global/{lobbyId}/toggle")]
    public async Task<IActionResult> ToggleGlobalChat(string lobbyId)
    {
        if (!await chatService.LobbyExistsAsync(lobbyId))
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        
        var newStatus = chatService.ToggleGlobalChat(lobbyId);
        var response = new GlobalChatStatusResponse { LobbyId = lobbyId, IsGlobalChatEnabled = newStatus };
        await hubContext.Clients.Group($"global_{lobbyId}").SendAsync("ToggleGlobalChat");
        return Ok(response);
    }
    
    [HttpGet("global/{lobbyId}/status")]
    public async Task<IActionResult> GetGlobalChatStatus(string lobbyId)
    {
        if (!await chatService.LobbyExistsAsync(lobbyId))
            return NotFound(new ErrorResponse("LOBBY_NOT_FOUND", "Lobby does not exist"));
        
        var response = new GlobalChatStatusResponse { LobbyId = lobbyId, IsGlobalChatEnabled = chatService.IsGlobalChatEnabled(lobbyId) };
        return Ok(response);
    }

    [HttpPost("private/{lobbyId}/{channelName}/send-message")]
    public async Task<IActionResult> SendPrivateMessage(string lobbyId, string channelName, [FromBody] ChatMessage message)
    {
        if (!await chatService.LobbyExistsAsync(lobbyId))
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
        await hubContext.Clients.Group($"private_{channelName}_{lobbyId}").SendAsync("ReceivePrivateMessage", response);
        return Ok(response);
    }
    
    [HttpGet("private/{lobbyId}/{channelName}/history")]
    public async Task<IActionResult> GetPrivateChatHistory(string lobbyId, string channelName, [FromQuery] long userId)
    {
        if (!await chatService.LobbyExistsAsync(lobbyId) || !await chatService.PrivateChannelExistsAsync(lobbyId, channelName))
            return NotFound(new ErrorResponse("NOT_FOUND", "Lobby or channel does not exist"));
        
        if (!await chatService.UserHasAccessToChannelAsync(lobbyId, channelName, userId))
        {
            return StatusCode(403, new ErrorResponse("ACCESS_DENIED", "You do not have access to this private channel's history"));
        }
        
        var history = await chatService.GetMessageHistoryAsync(lobbyId, channelName);
        return Ok(history);
    }
}