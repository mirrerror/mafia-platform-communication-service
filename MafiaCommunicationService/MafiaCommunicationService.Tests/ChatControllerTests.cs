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

        var mockClients = new Mock<IHubClients>();
        var mockGroup = new Mock<IClientProxy>();
        mockClients.Setup(clients => clients.Group(It.IsAny<string>())).Returns(mockGroup.Object);
        _hubContextMock.Setup(x => x.Clients).Returns(mockClients.Object);
    }

    [Fact]
    public async Task GetLobby_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });

        var result = await _controller.GetLobby(lobbyId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<Lobby>>(okResult.Value);
    }

    [Fact]
    public async Task GetLobby_LobbyDoesNotExist_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        var result = await _controller.GetLobby(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task CreateLobby_LobbyDoesNotExist_ReturnsOk()
    {
        var lobbyDto = new LobbyCreationDto { LobbyId = "new-lobby", PrivateChannels = [] };
        _chatServiceMock.Setup(s => s.CreateNewLobbyAsync(lobbyDto)).ReturnsAsync(new Lobby { Id = lobbyDto.LobbyId });

        var result = await _controller.CreateLobby(lobbyDto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<Lobby>>(okResult.Value);
    }

    [Fact]
    public async Task CreateLobby_LobbyAlreadyExists_ReturnsBadRequest()
    {
        var lobbyDto = new LobbyCreationDto { LobbyId = "existing-lobby", PrivateChannels = [] };
        _chatServiceMock.Setup(s => s.CreateNewLobbyAsync(lobbyDto)).ThrowsAsync(new InvalidOperationException());

        var result = await _controller.CreateLobby(lobbyDto);

        Assert.IsType<BadRequestObjectResult>(result);
    }
    
    [Fact]
    public async Task DeleteLobby_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.DeleteLobbyAsync(lobbyId)).ReturnsAsync(true);

        var result = await _controller.DeleteLobby(lobbyId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<object>>(okResult.Value);
    }

    [Fact]
    public async Task DeleteLobby_LobbyDoesNotExist_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.DeleteLobbyAsync(lobbyId)).ReturnsAsync(false);

        var result = await _controller.DeleteLobby(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SendPrivateMessage_ChannelNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "non-existent-channel";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret message" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(false);

        var result = await _controller.SendPrivateMessage(lobbyId, channelName, message);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task SendPrivateMessage_AccessDenied_ReturnsForbidden()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 99, SenderName = "Outsider", Content = "Secret message" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(false);

        var result = await _controller.SendPrivateMessage(lobbyId, channelName, message);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task GetPrivateChatHistory_ChannelNotFound_ReturnsNotFound()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "non-existent-channel";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(false);

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetPrivateChatHistory_AccessDenied_ReturnsForbidden()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 99; // User who does not have access
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(false);

        var result = await _controller.GetPrivateChatHistory(lobbyId, channelName, userId);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task MakeAnnouncement_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        var announcementDto = new AnnouncementDto { Content = "Test Announcement" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.CreateAnnouncementAsync(lobbyId, announcementDto))
            .ReturnsAsync(new Announcement { LobbyId = lobbyId, Content = announcementDto.Content });

        var result = await _controller.MakeAnnouncement(lobbyId, announcementDto);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<Announcement>>(okResult.Value);
        Assert.Equal(announcementDto.Content, apiResponse.Data.Content);
    }

    [Fact]
    public async Task MakeAnnouncement_LobbyDoesNotExist_ReturnsNotFound()
    {
        const string lobbyId = "non-existent-lobby";
        var announcementDto = new AnnouncementDto { Content = "Test Announcement" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        var result = await _controller.MakeAnnouncement(lobbyId, announcementDto);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAnnouncementHistory_LobbyExists_ReturnsOk()
    {
        const string lobbyId = "test-lobby";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.GetAnnouncementHistoryAsync(lobbyId, 50)).ReturnsAsync(new List<Announcement>());

        var result = await _controller.GetAnnouncementHistory(lobbyId);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<ApiResponse<IEnumerable<Announcement>>>(okResult.Value);
    }

    [Fact]
    public async Task GetAnnouncementHistory_LobbyDoesNotExist_ReturnsNotFound()
    {
        const string lobbyId = "non-existent-lobby";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        var result = await _controller.GetAnnouncementHistory(lobbyId);

        Assert.IsType<NotFoundObjectResult>(result);
    }
}