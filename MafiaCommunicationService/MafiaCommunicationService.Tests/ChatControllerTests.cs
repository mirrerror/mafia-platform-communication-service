using MafiaCommunicationService.Controllers;
using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace MafiaCommunicationService.Tests;

public class ChatControllerTests
{
    private readonly Mock<IChatService> _mockChatService;
    private readonly Mock<IHubContext<ChatHub>> _mockHubContext;
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _mockChatService = new Mock<IChatService>();
        _mockHubContext = new Mock<IHubContext<ChatHub>>();
        
        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(clients => clients.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHubContext.Setup(x => x.Clients).Returns(mockClients.Object);
        
        _controller = new ChatController(_mockHubContext.Object, _mockChatService.Object);
    }
    
    [Fact]
    public async Task GetGlobalChatHistory_WhenLobbyExists_ReturnsOkWithHistory()
    {
        const string lobbyId = "existing-lobby";
        var history = new List<ChatMessageEntity> { new() { LobbyId = lobbyId, SenderId = 101, SenderName = "test-user", Content = "test", Timestamp = DateTime.UtcNow } };
        _mockChatService.Setup(s => s.LobbyExistsAsync(lobbyId)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.GetMessageHistoryAsync(lobbyId, null, 50)).ReturnsAsync(history);

        var result = await _controller.GetGlobalChatHistory(lobbyId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var returnedHistory = Assert.IsAssignableFrom<IEnumerable<ChatMessageEntity>>(okResult.Value);
        Assert.Single(returnedHistory);
    }

    [Fact]
    public async Task GetGlobalChatHistory_WhenLobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "non-existent-lobby";
        _mockChatService.Setup(s => s.LobbyExistsAsync(lobbyId)).ReturnsAsync(false);

        var result = await _controller.GetGlobalChatHistory(lobbyId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("LOBBY_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetPrivateChatHistory_WhenAuthorized_ReturnsOkWithHistory()
    {
        const string lobbyId = "lobby1", channelName = "channel1";
        const long userId = 1;
        _mockChatService.Setup(s => s.LobbyExistsAsync(lobbyId)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.GetMessageHistoryAsync(lobbyId, channelName, 50)).ReturnsAsync(new List<ChatMessageEntity>());

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        Assert.IsType<OkObjectResult>(result);
    }
    
    [Fact]
    public async Task GetPrivateChatHistory_WhenAccessDenied_ReturnsForbidden()
    {
        const string lobbyId = "lobby1", channelName = "channel1";
        const long userId = 2; // Unauthorized user
        _mockChatService.Setup(s => s.LobbyExistsAsync(lobbyId)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(false);

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        var forbiddenResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbiddenResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(forbiddenResult.Value);
        Assert.Equal("ACCESS_DENIED", error.Error.Code);
    }
    
    [Fact]
    public async Task GetPrivateChatHistory_WhenChannelNotFound_ReturnsNotFound()
    {
        const string lobbyId = "lobby1", channelName = "non-existent-channel";
        _mockChatService.Setup(s => s.LobbyExistsAsync(lobbyId)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(false);
        
        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, 1);
        
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task GetGlobalChatStatus_WhenLobbyExists_ReturnsOkWithStatus()
    {
        const string lobbyId = "existing-lobby";
        _mockChatService.Setup(s => s.LobbyExistsAsync(lobbyId)).ReturnsAsync(true);
        _mockChatService.Setup(s => s.IsGlobalChatEnabled(lobbyId)).Returns(true);

        var result = await _controller.GetGlobalChatStatus(lobbyId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GlobalChatStatusResponse>(okResult.Value);
        Assert.Equal(lobbyId, response.LobbyId);
        Assert.True(response.IsGlobalChatEnabled);
    }

    [Fact]
    public async Task GetGlobalChatStatus_WhenLobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "non-existent-lobby";
        _mockChatService.Setup(s => s.LobbyExistsAsync(lobbyId)).ReturnsAsync(false);

        var result = await _controller.GetGlobalChatStatus(lobbyId);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("LOBBY_NOT_FOUND", errorResponse.Error.Code);
    }

    [Fact]
    public async Task SendGlobalMessage_WhenChatIsEnabled_ReturnsOk()
    {
        _mockChatService.Setup(s => s.IsGlobalChatEnabled(It.IsAny<string>())).Returns(true);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Hello" };

        var result = await _controller.SendGlobalMessage("lobby1", message);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SendGlobalMessage_WhenChatIsDisabled_ReturnsBadRequest()
    {
        _mockChatService.Setup(s => s.IsGlobalChatEnabled("lobby1")).Returns(false);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Hello" };

        var result = await _controller.SendGlobalMessage("lobby1", message);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errorResponse = Assert.IsType<ErrorResponse>(badRequestResult.Value);
        Assert.Equal("CHAT_DISABLED", errorResponse.Error.Code);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenAuthorized_ReturnsOk()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync("lobby1", "channel1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync("lobby1", "channel1", 1)).ReturnsAsync(true);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Secret" };

        var result = await _controller.SendPrivateMessage("lobby1", "channel1", message);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenLobbyNotFound_ReturnsNotFound()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(false);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Secret" };

        var result = await _controller.SendPrivateMessage("lobby1", "channel1", message);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("LOBBY_NOT_FOUND", error.Error.Code);
    }
    
    [Fact]
    public async Task SendPrivateMessage_WhenChannelNotFound_ReturnsNotFound()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync("lobby1", "channel1")).ReturnsAsync(false);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Secret" };
        
        var result = await _controller.SendPrivateMessage("lobby1", "channel1", message);
        
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("CHANNEL_NOT_FOUND", error.Error.Code);
    }

    [Fact]
    public async Task SendPrivateMessage_WhenAccessDenied_ReturnsForbidden()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync("lobby1", "channel1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync("lobby1", "channel1", 1)).ReturnsAsync(false);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Secret" };

        var result = await _controller.SendPrivateMessage("lobby1", "channel1", message);

        var forbiddenResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbiddenResult.StatusCode);
        var error = Assert.IsType<ErrorResponse>(forbiddenResult.Value);
        Assert.Equal("ACCESS_DENIED", error.Error.Code);
    }
    
    [Fact]
    public async Task ToggleGlobalChat_WhenLobbyExists_ReturnsOk()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.ToggleGlobalChat("lobby1")).Returns(false);

        var result = await _controller.ToggleGlobalChat("lobby1");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GlobalChatStatusResponse>(okResult.Value);
        Assert.False(response.IsGlobalChatEnabled);
    }
    
    [Fact]
    public async Task ToggleGlobalChat_WhenLobbyNotFound_ReturnsNotFound()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(false);

        var result = await _controller.ToggleGlobalChat("lobby1");

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ErrorResponse>(notFoundResult.Value);
        Assert.Equal("LOBBY_NOT_FOUND", error.Error.Code);
    }
}