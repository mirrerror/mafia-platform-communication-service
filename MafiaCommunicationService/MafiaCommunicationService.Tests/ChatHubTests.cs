using Microsoft.AspNetCore.SignalR;
using Moq;
using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;

namespace MafiaCommunicationService.Tests;

public class ChatHubTests
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly ChatHub _chatHub;
    private readonly Mock<HubCallerContext> _hubCallerContextMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly Mock<IHubCallerClients> _clientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;

    public ChatHubTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _hubCallerContextMock = new Mock<HubCallerContext>();
        _groupManagerMock = new Mock<IGroupManager>();
        _clientsMock = new Mock<IHubCallerClients>();
        _clientProxyMock = new Mock<IClientProxy>();

        _chatHub = new ChatHub(_chatServiceMock.Object)
        {
            Context = _hubCallerContextMock.Object,
            Groups = _groupManagerMock.Object,
            Clients = _clientsMock.Object
        };
    }

    [Fact]
    public async Task SendGlobalMessage_ValidMessage_SendsMessageToGroup()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabled(lobbyId)).Returns(true);
        _clientsMock.Setup(c => c.Group($"global_{lobbyId}")).Returns(_clientProxyMock.Object);
        
        await _chatHub.SendGlobalMessage(lobbyId, message);
        
        _clientProxyMock.Verify(
            c => c.SendCoreAsync("ReceiveGlobalMessage", It.IsAny<object[]>(), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task SendGlobalMessage_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "non-existent-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.SendGlobalMessage(lobbyId, message));
    }

    [Fact]
    public async Task SendGlobalMessage_ChatDisabled_ThrowsHubException()
    {
        
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Hello" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabled(lobbyId)).Returns(false);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.SendGlobalMessage(lobbyId, message));
    }

    [Fact]
    public async Task SendGlobalMessage_InvalidMessage_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "" }; 
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.IsGlobalChatEnabled(lobbyId)).Returns(true);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.SendGlobalMessage(lobbyId, message));
    }

    [Fact]
    public async Task SendPrivateMessage_ValidMessage_SendsMessageToGroup()
    {
        
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(true);
        _clientsMock.Setup(c => c.Group($"private_{channelName}_{lobbyId}")).Returns(_clientProxyMock.Object);
        
        await _chatHub.SendPrivateMessage(channelName, lobbyId, message);
        
        _clientProxyMock.Verify(
            c => c.SendCoreAsync("ReceivePrivateMessage", It.IsAny<object[]>(), CancellationToken.None),
            Times.Once);
    }

    [Fact]
    public async Task SendPrivateMessage_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.SendPrivateMessage(channelName, lobbyId, message));
    }

    [Fact]
    public async Task SendPrivateMessage_AccessDenied_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        var message = new ChatMessage { SenderId = 1, SenderName = "User1", Content = "Secret" };
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, message.SenderId)).ReturnsAsync(false);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.SendPrivateMessage(channelName, lobbyId, message));
    }

    [Fact]
    public async Task JoinGlobalChat_LobbyExists_AddsToGroup()
    {
        const string lobbyId = "test-lobby";
        const long userId = 1;
        const string connectionId = "test-connection";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        
        await _chatHub.JoinGlobalChat(lobbyId, userId);
        
        _groupManagerMock.Verify(g => g.AddToGroupAsync(connectionId, $"global_{lobbyId}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task JoinGlobalChat_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinGlobalChat(lobbyId, userId));
    }

    [Fact]
    public async Task LeaveGlobalChat_LobbyExists_RemovesFromGroup()
    {
        const string lobbyId = "test-lobby";
        const long userId = 1;
        const string connectionId = "test-connection";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        
        await _chatHub.LeaveGlobalChat(lobbyId, userId);
        
        _groupManagerMock.Verify(g => g.RemoveFromGroupAsync(connectionId, $"global_{lobbyId}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LeaveGlobalChat_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.LeaveGlobalChat(lobbyId, userId));
    }

    [Fact]
    public async Task JoinPrivateChannel_ValidRequest_AddsToGroup()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        const string connectionId = "test-connection";
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
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
        const string channelName = "mafia";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(false);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinPrivateChannel(lobbyId, channelName, userId));
    }

    [Fact]
    public async Task JoinPrivateChannel_AccessDenied_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns(new Lobby { Id = lobbyId });
        _chatServiceMock.Setup(s => s.PrivateChannelExistsAsync(lobbyId, channelName)).ReturnsAsync(true);
        _chatServiceMock.Setup(s => s.UserHasAccessToChannelAsync(lobbyId, channelName, userId)).ReturnsAsync(false);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.JoinPrivateChannel(lobbyId, channelName, userId));
    }

    [Fact]
    public async Task LeavePrivateChannel_LobbyExists_RemovesFromGroup()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        const string connectionId = "test-connection";
        _chatServiceMock.Setup(s => s.GetLobby( lobbyId)).Returns(new Lobby { Id = lobbyId });
        _hubCallerContextMock.Setup(c => c.ConnectionId).Returns(connectionId);
        
        await _chatHub.LeavePrivateChannel(lobbyId, channelName, userId);
        
        _groupManagerMock.Verify(g => g.RemoveFromGroupAsync(connectionId, $"private_{channelName}_{lobbyId}", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task LeavePrivateChannel_LobbyNotFound_ThrowsHubException()
    {
        const string lobbyId = "test-lobby";
        const string channelName = "mafia";
        const long userId = 1;
        _chatServiceMock.Setup(s => s.GetLobby(lobbyId)).Returns((Lobby)null!);
        
        await Assert.ThrowsAsync<HubException>(() => _chatHub.LeavePrivateChannel(lobbyId, channelName, userId));
    }
}