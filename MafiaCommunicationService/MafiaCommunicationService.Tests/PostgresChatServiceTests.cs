using System.Reflection;
using MafiaCommunicationService.Data;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Tests;

public class PostgresChatServiceTests : IDisposable
{
    private readonly DbContextOptions<ChatDbContext> _dbContextOptions = new DbContextOptionsBuilder<ChatDbContext>()
        .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
        .Options;

    public void Dispose()
    {
        var lobbiesField = typeof(PostgresChatService).GetField("Lobbies", BindingFlags.NonPublic | BindingFlags.Static);
        if (lobbiesField != null)
        {
            var lobbies = lobbiesField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, Lobby>;
            lobbies?.Clear();
        }

        var lobbyChatStatusField = typeof(PostgresChatService).GetField("LobbyChatStatus", BindingFlags.NonPublic | BindingFlags.Static);
        if (lobbyChatStatusField == null) return;
        var lobbyChatStatuses = lobbyChatStatusField.GetValue(null) as System.Collections.Concurrent.ConcurrentDictionary<string, bool>;
        lobbyChatStatuses?.Clear();
    }

    private ChatDbContext CreateContext() => new(_dbContextOptions);

    [Fact]
    public void CreateNewLobby_ShouldCreateLobbyWithDefaultChannels()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels =
            [
                new PrivateChannelDto { ChannelName = "mafia", MemberIds = [] },
                new PrivateChannelDto { ChannelName = "detectives", MemberIds = [] }
            ]
        };

        var lobby = chatService.CreateNewLobby(lobbyDto);

        Assert.NotNull(lobby);
        Assert.Equal(lobbyDto.LobbyId, lobby.Id);
        Assert.True(lobby.PrivateChannels.ContainsKey("mafia"));
        Assert.True(lobby.PrivateChannels.ContainsKey("detectives"));
    }

    [Fact]
    public void CreateNewLobby_WithExistingId_ShouldThrowException()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = []
        };
        chatService.CreateNewLobby(lobbyDto);

        Assert.Throws<InvalidOperationException>(() => chatService.CreateNewLobby(lobbyDto));
    }

    [Fact]
    public void GetLobby_ShouldReturnLobby_WhenExists()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = []
        };
        chatService.CreateNewLobby(lobbyDto);

        var lobby = chatService.GetLobby(lobbyDto.LobbyId);

        Assert.NotNull(lobby);
        Assert.Equal(lobbyDto.LobbyId, lobby.Id);
    }

    [Fact]
    public void GetLobby_ShouldReturnNull_WhenNotExists()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);

        var lobby = chatService.GetLobby("nonExistentLobby");

        Assert.Null(lobby);
    }

    [Fact]
    public async Task PrivateChannelExistsAsync_ShouldReturnTrue_WhenExists()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = [new PrivateChannelDto { ChannelName = "mafia", MemberIds = [] }]
        };
        chatService.CreateNewLobby(lobbyDto);

        var exists = await chatService.PrivateChannelExistsAsync(lobbyDto.LobbyId, "mafia");

        Assert.True(exists);
    }

    [Fact]
    public async Task PrivateChannelExistsAsync_ShouldReturnFalse_WhenNotExists()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = []
        };
        chatService.CreateNewLobby(lobbyDto);

        var exists = await chatService.PrivateChannelExistsAsync(lobbyDto.LobbyId, "nonExistentChannel");

        Assert.False(exists);
    }

    [Fact]
    public async Task UserHasAccessToChannelAsync_ShouldReturnTrue_WhenUserIsInChannel()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = [new PrivateChannelDto { ChannelName = "mafia", MemberIds = [1] }]
        };
        chatService.CreateNewLobby(lobbyDto);

        var hasAccess = await chatService.UserHasAccessToChannelAsync(lobbyDto.LobbyId, "mafia", 1);

        Assert.True(hasAccess);
    }

    [Fact]
    public async Task UserHasAccessToChannelAsync_ShouldReturnFalse_WhenUserIsNotInChannel()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = [new PrivateChannelDto { ChannelName = "mafia", MemberIds = [] }]
        };
        chatService.CreateNewLobby(lobbyDto);

        var hasAccess = await chatService.UserHasAccessToChannelAsync(lobbyDto.LobbyId, "mafia", 1);

        Assert.False(hasAccess);
    }

    [Fact]
    public async Task UserHasAccessToChannelAsync_ShouldReturnFalse_WhenChannelDoesNotExist()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = []
        };
        chatService.CreateNewLobby(lobbyDto);

        var hasAccess = await chatService.UserHasAccessToChannelAsync(lobbyDto.LobbyId, "nonExistentChannel", 1);

        Assert.False(hasAccess);
    }

    [Fact]
    public void IsGlobalChatEnabled_And_ToggleGlobalChat_ShouldWorkCorrectly()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels = []
        };
        chatService.CreateNewLobby(lobbyDto);

        Assert.False(chatService.IsGlobalChatEnabled(lobbyDto.LobbyId));

        var newStatus = chatService.ToggleGlobalChat(lobbyDto.LobbyId);
        Assert.True(newStatus);
        Assert.True(chatService.IsGlobalChatEnabled(lobbyDto.LobbyId));

        newStatus = chatService.ToggleGlobalChat(lobbyDto.LobbyId);
        Assert.False(newStatus);
        Assert.False(chatService.IsGlobalChatEnabled(lobbyDto.LobbyId));
    }

    [Fact]
    public async Task GetPrivateChannelsAsync_ShouldReturnChannelNames()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "testLobby",
            PrivateChannels =
            [
                new PrivateChannelDto { ChannelName = "mafia", MemberIds = [] },
                new PrivateChannelDto { ChannelName = "detectives", MemberIds = [] }
            ]
        };
        chatService.CreateNewLobby(lobbyDto);

        var channels = (await chatService.GetPrivateChannelsAsync(lobbyDto.LobbyId)).ToList();

        Assert.Equal(2, channels.Count);
        Assert.Contains("mafia", channels);
        Assert.Contains("detectives", channels);
    }

    [Fact]
    public async Task GetPrivateChannelsAsync_ShouldReturnEmpty_WhenLobbyDoesNotExist()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        const string lobbyId = "nonExistentLobby";

        var channels = (await chatService.GetPrivateChannelsAsync(lobbyId)).ToList();

        Assert.Empty(channels);
    }

    [Fact]
    public async Task SaveMessageAsync_And_GetMessageHistoryAsync_ShouldWorkCorrectly()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        const string lobbyId = "testLobby";
        var message = new ChatMessageEntity
        {
            LobbyId = lobbyId,
            Content = "Hello, world!",
            SenderId = 1,
            SenderName = "TestUser",
            Timestamp = DateTime.UtcNow
        };

        await chatService.SaveMessageAsync(message);
        var history = (await chatService.GetMessageHistoryAsync(lobbyId, null)).ToList();

        Assert.Single(history);
        Assert.Equal("Hello, world!", history[0].Content);
    }

    [Fact]
    public async Task GetMessageHistoryAsync_ShouldReturnEmpty_WhenNoMessages()
    {
        var dbContext = CreateContext();
        var chatService = new PostgresChatService(dbContext);
        const string lobbyId = "testLobby";

        var history = (await chatService.GetMessageHistoryAsync(lobbyId, null)).ToList();

        Assert.Empty(history);
    }
}