using MafiaCommunicationService.Hubs;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace MafiaCommunicationService.Tests;

public class ChatHubTests
{
    private readonly Mock<IChatService> _mockChatService;
    private readonly ChatHub _hub;
    private readonly Mock<IHubCallerClients> _mockClients;
    private readonly Mock<IClientProxy> _mockClientProxy;
    private readonly Mock<IGroupManager> _mockGroups;
    private readonly Mock<HubCallerContext> _mockContext;

    public ChatHubTests()
    {
        _mockChatService = new Mock<IChatService>();
        _mockClients = new Mock<IHubCallerClients>();
        _mockClientProxy = new Mock<IClientProxy>();
        _mockGroups = new Mock<IGroupManager>();
        _mockContext = new Mock<HubCallerContext>();
        
        _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
        _mockContext.Setup(c => c.ConnectionId).Returns(Guid.NewGuid().ToString());

        _hub = new ChatHub(_mockChatService.Object)
        {
            Clients = _mockClients.Object,
            Groups = _mockGroups.Object,
            Context = _mockContext.Object
        };
    }

    [Fact]
    public async Task SendPrivateMessage_WhenAuthorized_SendsMessage()
    {
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>())).ReturnsAsync(true);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Secret" };

        await _hub.SendPrivateMessage("channel1", "lobby1", message);

        _mockClientProxy.Verify(
            x => x.SendCoreAsync("ReceivePrivateMessage", It.IsAny<object[]>(), default),
            Times.Once);
    }
    
    [Fact]
    public async Task SendPrivateMessage_WhenUnauthorized_ThrowsHubException()
    {
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>())).ReturnsAsync(false);
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "Secret" };

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SendPrivateMessage("channel1", "lobby1", message));
        Assert.Contains("ACCESS_DENIED", ex.Message);
    }

    [Fact]
    public async Task SendPrivateMessage_WithInvalidMessage_ThrowsHubException()
    {
        var message = new ChatMessage { SenderId = 1, SenderName = "Test", Content = "" }; // Invalid content

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.SendPrivateMessage("channel1", "lobby1", message));
        Assert.Contains("Invalid message received", ex.Message);
    }
    
    [Fact]
    public async Task JoinPrivateChannel_WhenAuthorized_AddsToGroup()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync("lobby1", "channel1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync("lobby1", "channel1", 1)).ReturnsAsync(true);

        await _hub.JoinPrivateChannel("lobby1", "channel1", 1);
        
        _mockGroups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), $"private_channel1_lobby1", default), Times.Once);
    }
    
    [Fact]
    public async Task JoinPrivateChannel_WhenAccessDenied_ThrowsHubException()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync("lobby1", "channel1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.UserHasAccessToChannelAsync("lobby1", "channel1", 1)).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.JoinPrivateChannel("lobby1", "channel1", 1));
        Assert.Contains("ACCESS_DENIED", ex.Message);
    }

    [Fact]
    public async Task JoinPrivateChannel_WhenChannelNotFound_ThrowsHubException()
    {
        _mockChatService.Setup(s => s.LobbyExistsAsync("lobby1")).ReturnsAsync(true);
        _mockChatService.Setup(s => s.PrivateChannelExistsAsync("lobby1", "channel1")).ReturnsAsync(false);

        var ex = await Assert.ThrowsAsync<HubException>(() => _hub.JoinPrivateChannel("lobby1", "channel1", 1));
        Assert.Contains("CHANNEL_NOT_FOUND", ex.Message);
    }
}