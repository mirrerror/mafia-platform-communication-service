using Microsoft.AspNetCore.SignalR;
using Moq;
using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.Extensions.Logging;

namespace MafiaCommunicationService.Tests;

public class ChatHubTests
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<ILogger<ChatHub>> _loggerMock;
    private readonly ChatHub _chatHub;
    private readonly Mock<HubCallerContext> _hubCallerContextMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly Mock<ISingleClientProxy> _singleClientProxyMock;

    public ChatHubTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _loggerMock = new Mock<ILogger<ChatHub>>();
        _hubCallerContextMock = new Mock<HubCallerContext>();
        _groupManagerMock = new Mock<IGroupManager>();
        _clientsMock = new Mock<IHubCallerClients>();
        _clientProxyMock = new Mock<IClientProxy>();
        _singleClientProxyMock = new Mock<ISingleClientProxy>();

        _chatHub = new ChatHub(_chatServiceMock.Object, _loggerMock.Object)
        {
            Context = _hubCallerContextMock.Object,
            Groups = _groupManagerMock.Object,
            Clients = _clientsMock.Object
        };

        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _clientsMock.Setup(c => c.Client(It.IsAny<string>())).Returns(_singleClientProxyMock.Object);
        _clientsMock.Setup(c => c.All).Returns(_clientProxyMock.Object);
    }

    [Fact]
    public async Task SendGlobalMessage_ValidMessage_SendsMessageToGroup()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabledAsync(lobbyId)).ReturnsAsync(true);

        await _chatHub.SendGlobalMessage(lobbyId, message);

        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "ReceiveGlobalMessage",
                It.Is<object[]>(o => o.Length == 1 && o[0] is ApiResponse<ChatResponse>),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task SendGlobalMessage_ChatDisabled_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabledAsync(lobbyId)).ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendGlobalMessage(lobbyId, message));
        Assert.Contains("Global chat is currently disabled", exception.Message);
    }

    [Fact]
    public async Task SendGlobalMessage_InvalidMessage_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        // Message with empty content, which is invalid
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabledAsync(lobbyId)).ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendGlobalMessage(lobbyId, message));

        Assert.Contains("Content", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendPrivateMessage_ValidMessage_SendsMessageToGroup()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(true);

        await _chatHub.SendPrivateMessage(channelName, lobbyId, message);

        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "ReceivePrivateMessage",
                It.Is<object[]>(o => o.Length == 1 && o[0] is ApiResponse<PrivateChatResponse>),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_InvalidMessage_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        // Message with no SenderName
        var message = new ChatMessage { SenderId = 1, Content = "Secret", SenderName = null! };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(true);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendPrivateMessage(channelName, lobbyId, message));

        Assert.Contains("Sender", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JoinGlobalChat_LobbyExists_AddsToGroup()
    {
        const string lobbyId = "test-lobby";
        const string connectionId = "test-connection";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        await _chatHub.JoinGlobalChat(lobbyId, 1);

        _groupManagerMock.Verify(g => g.AddToGroupAsync(connectionId, $"global_{lobbyId}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LeaveGlobalChat_LobbyExists_RemovesFromGroup()
    {
        const string lobbyId = "test-lobby";
        const string connectionId = "test-connection";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        await _chatHub.LeaveGlobalChat(lobbyId, 1);

        _groupManagerMock.Verify(g => g.RemoveFromGroupAsync(connectionId, $"global_{lobbyId}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LeaveGlobalChat_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        await Assert.ThrowsAsync<HubException>(() => _chatHub.LeaveGlobalChat(lobbyId, 1));
    }

    [Fact]
    public async Task JoinPrivateChannel_Valid_AddsToGroup()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const string connectionId = "test-connection";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(true);
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        await _chatHub.JoinPrivateChannel(lobbyId, channelName, userId);

        _groupManagerMock.Verify(g => g.AddToGroupAsync(connectionId, $"private_{channelName}_{lobbyId}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task JoinPrivateChannel_ChannelNotFound_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "non-existent-channel";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(false);

        await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinPrivateChannel(lobbyId, channelName, 1));
    }

    [Fact]
    public async Task JoinPrivateChannel_AccessDenied_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 99;
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(false);

        await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinPrivateChannel(lobbyId, channelName, userId));
    }

    [Fact]
    public async Task LeavePrivateChannel_LobbyExists_RemovesFromGroup()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const string connectionId = "test-connection";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        await _chatHub.LeavePrivateChannel(lobbyId, channelName, 1);

        _groupManagerMock.Verify(g => g.RemoveFromGroupAsync(connectionId, $"private_{channelName}_{lobbyId}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LeavePrivateChannel_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        const string channelName = "mafia";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        await Assert.ThrowsAsync<HubException>(() => _chatHub.LeavePrivateChannel(lobbyId, channelName, 1));
    }

    [Fact]
    public async Task SendAnnouncement_LobbyExists_SendsAnnouncementToGroup()
    {
        const string lobbyId = "test-lobby";
        var announcementDto = new AnnouncementDto { Content = "Test Announcement" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.CreateAnnouncementAsync(lobbyId, announcementDto))
            .ReturnsAsync(new Announcement { LobbyId = lobbyId, Content = announcementDto.Content });

        await _chatHub.SendAnnouncement(lobbyId, announcementDto);

        _clientProxyMock.Verify(
            c => c.SendCoreAsync(
                "ReceiveAnnouncement",
                It.Is<object[]>(o => o.Length == 1 && o[0] is ApiResponse<Announcement>),
                CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task SendAnnouncement_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        var announcementDto = new AnnouncementDto { Content = "Test Announcement" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        await Assert.ThrowsAsync<HubException>(() => _chatHub.SendAnnouncement(lobbyId, announcementDto));
    }

    [Fact]
    public async Task SendGlobalMessage_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendGlobalMessage(lobbyId, message));
        Assert.Contains("LOBBY_NOT_FOUND", exception.Message);
    }

    [Fact]
    public async Task SendPrivateMessage_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendPrivateMessage(channelName, lobbyId, message));
        Assert.Contains("LOBBY_NOT_FOUND", exception.Message);
    }

    [Fact]
    public async Task SendPrivateMessage_AccessDenied_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 99, SenderName = "BadUser", Content = "Secret" };
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendPrivateMessage(channelName, lobbyId, message));
        Assert.Contains("ACCESS_DENIED", exception.Message);
    }

    [Fact]
    public async Task JoinGlobalChat_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinGlobalChat(lobbyId, 1));
        Assert.Contains("LOBBY_NOT_FOUND", exception.Message);
    }

    [Fact]
    public async Task JoinPrivateChannel_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        const string channelName = "mafia";
        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync((Lobby)null!);

        var exception = await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinPrivateChannel(lobbyId, channelName, 1));
        Assert.Contains("LOBBY_NOT_FOUND", exception.Message);
    }

    [Fact]
    public async Task SendGlobalMessage_LogsError_WhenSaveFailsUnexpectedly()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        var testException = new Exception("Database connection failed");

        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabledAsync(lobbyId)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.SaveMessageAsync(It.IsAny<ChatMessageEntity>())).ThrowsAsync(testException);

        var thrownException = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendGlobalMessage(lobbyId, message));
        Assert.Equal(testException, thrownException.InnerException);

        _loggerMock.Verify(
            log => log.Log(
                LogLevel.Error, 
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in SendGlobalMessage")),
                testException, 
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_LogsError_WhenSaveFailsUnexpectedly()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret" };
        var testException = new Exception("Database write error");

        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.SaveMessageAsync(It.IsAny<ChatMessageEntity>())).ThrowsAsync(testException);

        var thrownException = await Assert.ThrowsAsync<HubException>(() => _chatHub.SendPrivateMessage(channelName, lobbyId, message));
        Assert.Equal(testException, thrownException.InnerException);

        _loggerMock.Verify(
            log => log.Log(
                LogLevel.Error, 
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in SendPrivateMessage")),
                testException, 
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task JoinPrivateChannel_LogsError_WhenUnexpectedExceptionOccurs()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        var testException = new Exception("Unexpected error during group add");

        _chatServiceMock.Setup(s => s.GetLobbyAsync(lobbyId)).ReturnsAsync(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(true);
        _groupManagerMock.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), $"private_{channelName}_{lobbyId}", CancellationToken.None))
                         .ThrowsAsync(testException);

        var thrownException = await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinPrivateChannel(lobbyId, channelName, userId));
        Assert.Equal(testException, thrownException.InnerException);

        _loggerMock.Verify(
            log => log.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in JoinPrivateChannel")),
                testException, 
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
    
    [Fact]
    public async Task SendAnnouncement_ThrowsHubException_On_Generic_Exception()
    {
        const string testLobbyId = "test-lobby";
        var testAnnouncementDto = new AnnouncementDto { Content = "Test" };
        var genericException = new Exception("Generic database error");

        _chatServiceMock.Setup(s => s.GetLobbyAsync(testLobbyId))
            .ReturnsAsync(new Lobby { Id = testLobbyId });
    
        _chatServiceMock.Setup(s => s.CreateAnnouncementAsync(testLobbyId, testAnnouncementDto))
            .ThrowsAsync(genericException);

        var hubException = await Assert.ThrowsAsync<HubException>(() => 
            _chatHub.SendAnnouncement(testLobbyId, testAnnouncementDto)
        );

        Assert.Equal("An unexpected error occurred while sending the announcement.", hubException.Message);
        Assert.Same(genericException, hubException.InnerException);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error in SendAnnouncement")),
                genericException, 
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}