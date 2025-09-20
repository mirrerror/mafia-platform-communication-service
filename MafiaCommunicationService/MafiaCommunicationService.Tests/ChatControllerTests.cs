using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Moq;
using MafiaCommunicationService.Controllers;
using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;

namespace MafiaCommunicationService.Tests;

public class ChatControllerTests
{
    private readonly Mock<IHubContext<ChatHub>> _hubContextMock;
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly ChatController _controller;

    public ChatControllerTests()
    {
        _hubContextMock = new Mock<IHubContext<ChatHub>>();
        _chatServiceMock = new Mock<IChatService>();
        _controller = new ChatController(_hubContextMock.Object, _chatServiceMock.Object);
    }

    [Fact]
    public void GetLobby_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });

        var result = _controller.GetLobby(lobbyId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void GetLobby_LobbyDoesNotExist_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = _controller.GetLobby(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void CreateLobby_LobbyDoesNotExist_ReturnsOk()
    {
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "new-lobby",
            PrivateChannels = []
        };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyDto.LobbyId)).Returns((Lobby)null!);
        _chatServiceMock.Setup(s => s.CreateNewLobby(lobbyDto)).Returns(new Lobby { Id = lobbyDto.LobbyId });

        var result = _controller.CreateLobby(lobbyDto);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public void CreateLobby_LobbyAlreadyExists_ReturnsBadRequest()
    {
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "existing-lobby",
            PrivateChannels = []
        };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyDto.LobbyId)).Returns(new Lobby { Id = lobbyDto.LobbyId });

        var result = _controller.CreateLobby(lobbyDto);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task SendGlobalMessage_ValidRequest_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabled(lobbyId)).Returns(true);
        var mockClients = new Mock<IHubClients>();
        var mockGroup = new Mock<IClientProxy>();
        mockClients.Setup(clients => clients.Group(It.IsAny<string>())).Returns(mockGroup.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(mockClients.Object);

        var result = await _controller.SendGlobalMessage(lobbyId, message);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SendGlobalMessage_LobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = await _controller.SendGlobalMessage(lobbyId, message);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SendGlobalMessage_ChatDisabled_ReturnsBadRequest()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabled(lobbyId)).Returns(false);

        var result = await _controller.SendGlobalMessage(lobbyId, message);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetGlobalChatHistory_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.GetMessageHistoryAsync(lobbyId, null, 50))
            .ReturnsAsync(new List<ChatMessageEntity>());

        var result = await _controller.GetGlobalChatHistory(lobbyId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetGlobalChatHistory_LobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = await _controller.GetGlobalChatHistory(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ToggleGlobalChat_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.ToggleGlobalChat(lobbyId)).Returns(true);
        var mockClients = new Mock<IHubClients>();
        var mockGroup = new Mock<IClientProxy>();
        mockClients.Setup(clients => clients.Group(It.IsAny<string>())).Returns(mockGroup.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(mockClients.Object);

        var result = await _controller.ToggleGlobalChat(lobbyId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task ToggleGlobalChat_LobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = await _controller.ToggleGlobalChat(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetGlobalChatStatus_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabled(lobbyId)).Returns(true);

        var result = _controller.GetGlobalChatStatus(lobbyId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GlobalChatStatusResponse>(okResult.Value);
        Assert.True(response.IsGlobalChatEnabled);
    }

    [Fact]
    public void GetGlobalChatStatus_LobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = _controller.GetGlobalChatStatus(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SendPrivateMessage_ValidRequest_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret message" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(true);
        var mockClients = new Mock<IHubClients>();
        var mockGroup = new Mock<IClientProxy>();
        mockClients.Setup(clients => clients.Group(It.IsAny<string>())).Returns(mockGroup.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(mockClients.Object);

        var result = await _controller.SendPrivateMessage(lobbyId, channelName, message);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task SendPrivateMessage_LobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret message" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = await _controller.SendPrivateMessage(lobbyId, channelName, message);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SendPrivateMessage_ChannelNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret message" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(false);

        var result = await _controller.SendPrivateMessage(lobbyId, channelName, message);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SendPrivateMessage_AccessDenied_ReturnsForbidden()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret message" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(false);

        var result = await _controller.SendPrivateMessage(lobbyId, channelName, message);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetPrivateChatHistory_ValidRequest_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.GetMessageHistoryAsync(lobbyId, channelName, 50))
            .ReturnsAsync(new List<ChatMessageEntity>());

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetPrivateChatHistory_LobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetPrivateChatHistory_ChannelNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(false);

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetPrivateChatHistory_AccessDenied_ReturnsForbidden()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(false);

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetPrivateChannels_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.GetPrivateChannelsAsync(lobbyId)).ReturnsAsync(new List<string> { "mafia" });

        var result = await _controller.GetPrivateChannels(lobbyId);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task GetPrivateChannels_LobbyNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);

        var result = await _controller.GetPrivateChannels(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}