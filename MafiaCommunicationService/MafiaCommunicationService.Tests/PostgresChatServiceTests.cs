using MafiaCommunicationService.Data;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Tests;

public class PostgresChatServiceTests
{
    private DbContextOptions<ChatDbContext> CreateNewContextOptions()
    {
        return new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task CreateNewLobbyAsync_ShouldCreateAndPersistLobby()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "test-lobby",
            PrivateChannels =
            [
                new PrivateChannelDto { ChannelName = "mafia", MemberIds = [1, 2] }
            ]
        };

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var lobby = await context.Lobbies
                .Include(l => l.PrivateChannels)
                .ThenInclude(pc => pc.Members)
                .FirstOrDefaultAsync(l => l.Id == "test-lobby");

            Assert.NotNull(lobby);
            Assert.False(lobby.IsGlobalChatEnabled);
            var mafiaChannel = Assert.Single(lobby.PrivateChannels);
            Assert.Equal("mafia", mafiaChannel.Name);
            Assert.Equal(2, mafiaChannel.Members.Count);
        }
    }

    [Fact]
    public async Task CreateNewLobbyAsync_WithExistingId_ShouldThrowException()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto { LobbyId = "test-lobby", PrivateChannels = [] };
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateNewLobbyAsync(lobbyDto));
        }
    }

    [Fact]
    public async Task GetLobbyAsync_ShouldReturnCorrectlyMappedModel_WhenExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            await service.CreateNewLobbyAsync(new LobbyCreationDto
            {
                LobbyId = "test-lobby",
                PrivateChannels =
                [
                    new PrivateChannelDto { ChannelName = "mafia", MemberIds = [1, 2] },
                    new PrivateChannelDto { ChannelName = "town", MemberIds = [3, 4] }
                ]
            });
        }
    
        Lobby? lobbyModel;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            lobbyModel = await service.GetLobbyAsync("test-lobby");
        }
    
        Assert.NotNull(lobbyModel);
        Assert.True(lobbyModel.PrivateChannels.TryGetValue("mafia", out var mafiaChannel));
        Assert.Equal(2, mafiaChannel.Members.Count);
        Assert.True(mafiaChannel.Members.ContainsKey(1));
        Assert.True(mafiaChannel.Members.ContainsKey(2));
    
        Assert.True(lobbyModel.PrivateChannels.TryGetValue("town", out var townChannel));
        Assert.Equal(2, townChannel.Members.Count);
        Assert.True(townChannel.Members.ContainsKey(3));
        Assert.True(townChannel.Members.ContainsKey(4));
        Assert.False(townChannel.Members.ContainsKey(1));
    }

    [Fact]
    public async Task GetLobbyAsync_ShouldReturnNull_WhenNotExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using var context = new ChatDbContext(dbContextOptions);
        var service = new PostgresChatService(context);

        var lobby = await service.GetLobbyAsync("non-existent-lobby");

        Assert.Null(lobby);
    }
    
    [Fact]
    public async Task DeleteLobbyAsync_ShouldReturnFalse_WhenLobbyNotExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using var context = new ChatDbContext(dbContextOptions);
        var service = new PostgresChatService(context);
        
        var result = await service.DeleteLobbyAsync("non-existent-lobby");
        
        Assert.False(result);
    }
    
    [Fact]
    public async Task ToggleGlobalChatAsync_ShouldReturnNull_WhenLobbyNotExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using var context = new ChatDbContext(dbContextOptions);
        var service = new PostgresChatService(context);
        
        var result = await service.ToggleGlobalChatAsync("non-existent-lobby");
        
        Assert.Null(result);
    }

    [Fact]
    public async Task IsGlobalChatEnabledAsync_ShouldReturnFalse_WhenLobbyNotExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using var context = new ChatDbContext(dbContextOptions);
        var service = new PostgresChatService(context);

        var result = await service.IsGlobalChatEnabledAsync("non-existent-lobby");

        Assert.False(result);
    }

    [Theory]
    [InlineData("test-lobby", "mafia", 1, true)]  // User has access
    [InlineData("test-lobby", "mafia", 3, false)]  // User does not have access
    [InlineData("test-lobby", "town", 1, false)]   // User has access to a different channel
    [InlineData("test-lobby", "ghosts", 1, false)] // Channel does not exist
    [InlineData("fake-lobby", "mafia", 1, false)]  // Lobby does not exist
    public async Task UserHasAccessToChannelAsync_ShouldReturnCorrectValue(string lobbyId, string channelName, long userId, bool expected)
    {
        var dbContextOptions = CreateNewContextOptions();
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            await service.CreateNewLobbyAsync(new LobbyCreationDto {
                LobbyId = "test-lobby",
                PrivateChannels = [new PrivateChannelDto { ChannelName = "mafia", MemberIds = [1, 2] }]
            });
        }
        
        bool hasAccess;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            hasAccess = await service.UserHasAccessToChannelAsync(lobbyId, channelName, userId);
        }
        
        Assert.Equal(expected, hasAccess);
    }

    [Fact]
    public async Task CreateAnnouncementAsync_ShouldCreateAndPersistAnnouncement()
    {
        var dbContextOptions = CreateNewContextOptions();
        const string lobbyId = "test-lobby";
        var announcementDto = new AnnouncementDto { Content = "Test Announcement" };

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            await service.CreateAnnouncementAsync(lobbyId, announcementDto);
        }

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var announcement = await context.Announcements.FirstOrDefaultAsync(a => a.LobbyId == lobbyId);
            Assert.NotNull(announcement);
            Assert.Equal(announcementDto.Content, announcement.Content);
        }
    }

    [Fact]
    public async Task GetAnnouncementHistoryAsync_ShouldReturnAnnouncementsForLobby()
    {
        var dbContextOptions = CreateNewContextOptions();
        const string lobbyId = "test-lobby";

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            context.Announcements.Add(new Announcement { LobbyId = lobbyId, Content = "Announcement 1" });
            context.Announcements.Add(new Announcement { LobbyId = lobbyId, Content = "Announcement 2" });
            context.Announcements.Add(new Announcement { LobbyId = "other-lobby", Content = "Other Announcement" });
            await context.SaveChangesAsync();
        }

        IEnumerable<Announcement> history;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context);
            history = await service.GetAnnouncementHistoryAsync(lobbyId);
        }

        Assert.Equal(2, history.Count());
        Assert.All(history, a => Assert.Equal(lobbyId, a.LobbyId));
    }
}