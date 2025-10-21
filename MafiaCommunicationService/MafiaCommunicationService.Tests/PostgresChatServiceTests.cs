using MafiaCommunicationService.Data;
using MafiaCommunicationService.Models;
using MafiaCommunicationService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace MafiaCommunicationService.Tests;

public class PostgresChatServiceTests
{
    private readonly ILogger<PostgresChatService> _logger = new Mock<ILogger<PostgresChatService>>().Object;

    private static DbContextOptions<ChatDbContext> CreateNewContextOptions()
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
            var service = new PostgresChatService(context, _logger);
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
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateNewLobbyAsync(lobbyDto));
        }
    }

    [Fact]
    public async Task GetLobbyAsync_ShouldReturnCorrectlyMappedModel_WhenExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
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
            var service = new PostgresChatService(context, _logger);
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
        var service = new PostgresChatService(context, _logger);

        var lobby = await service.GetLobbyAsync("non-existent-lobby");

        Assert.Null(lobby);
    }

    [Fact]
    public async Task DeleteLobbyAsync_ShouldReturnFalse_WhenLobbyNotExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using var context = new ChatDbContext(dbContextOptions);
        var service = new PostgresChatService(context, _logger);

        var result = await service.DeleteLobbyAsync("non-existent-lobby");

        Assert.False(result);
    }

    [Fact]
    public async Task ToggleGlobalChatAsync_ShouldReturnNull_WhenLobbyNotExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using var context = new ChatDbContext(dbContextOptions);
        var service = new PostgresChatService(context, _logger);

        var result = await service.ToggleGlobalChatAsync("non-existent-lobby");

        Assert.Null(result);
    }

    [Fact]
    public async Task IsGlobalChatEnabledAsync_ShouldReturnFalse_WhenLobbyNotExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using var context = new ChatDbContext(dbContextOptions);
        var service = new PostgresChatService(context, _logger);

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
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(new LobbyCreationDto {
                LobbyId = "test-lobby",
                PrivateChannels = [new PrivateChannelDto { ChannelName = "mafia", MemberIds = [1, 2] }]
            });
        }

        bool hasAccess;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
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
            var service = new PostgresChatService(context, _logger);
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
            var service = new PostgresChatService(context, _logger);
            history = await service.GetAnnouncementHistoryAsync(lobbyId);
        }

        Assert.Equal(2, history.Count());
        Assert.All(history, a => Assert.Equal(lobbyId, a.LobbyId));
    }

    [Fact]
    public async Task DeleteLobbyAsync_ShouldReturnTrue_WhenLobbyExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto { LobbyId = "test-lobby-delete", PrivateChannels = [] };

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        bool deleteResult;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            deleteResult = await service.DeleteLobbyAsync("test-lobby-delete");
        }

        Assert.True(deleteResult);
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var lobby = await context.Lobbies.FirstOrDefaultAsync(l => l.Id == "test-lobby-delete");
            Assert.Null(lobby);
        }
    }

    [Fact]
    public async Task ToggleGlobalChatAsync_ShouldToggleStatus_WhenLobbyExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto { LobbyId = "test-lobby-toggle", PrivateChannels = [] };

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        bool? newStatus;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            newStatus = await service.ToggleGlobalChatAsync("test-lobby-toggle");
        }

        Assert.True(newStatus);

        bool? finalStatus;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            finalStatus = await service.IsGlobalChatEnabledAsync("test-lobby-toggle");
        }

        Assert.True(finalStatus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task IsGlobalChatEnabledAsync_ShouldReturnCorrectStatus_WhenLobbyExists(bool isEnabled)
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyEntity = new LobbyEntity { Id = "test-lobby-status", IsGlobalChatEnabled = isEnabled };

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            context.Lobbies.Add(lobbyEntity);
            await context.SaveChangesAsync();
        }

        bool result;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            result = await service.IsGlobalChatEnabledAsync("test-lobby-status");
        }

        Assert.Equal(isEnabled, result);
    }

    [Fact]
    public async Task GetPrivateChannelsAsync_ShouldReturnChannelNames_WhenExists()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto {
            LobbyId = "test-lobby-channels",
            PrivateChannels = [
                new PrivateChannelDto { ChannelName = "mafia", MemberIds = [1] },
                new PrivateChannelDto { ChannelName = "town", MemberIds = [2] }
            ]
        };
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        IEnumerable<string> channels;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            channels = await service.GetPrivateChannelsAsync("test-lobby-channels");
        }

        var channelList = channels.ToList();
        Assert.Equal(2, channelList.Count);
        Assert.Contains("mafia", channelList);
        Assert.Contains("town", channelList);
    }

    [Fact]
    public async Task PrivateChannelExistsAsync_ShouldReturnCorrectValue()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto {
            LobbyId = "test-lobby-chan-exists",
            PrivateChannels = [ new PrivateChannelDto { ChannelName = "mafia", MemberIds = [1] } ]
        };
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            Assert.True(await service.PrivateChannelExistsAsync("test-lobby-chan-exists", "mafia"));
            Assert.False(await service.PrivateChannelExistsAsync("test-lobby-chan-exists", "town"));
            Assert.False(await service.PrivateChannelExistsAsync("fake-lobby", "mafia"));
        }
    }

    [Fact]
    public async Task SaveMessageAsync_ShouldPersistMessage()
    {
        var dbContextOptions = CreateNewContextOptions();
        var message = new ChatMessageEntity
        {
            LobbyId = "test-lobby-msg",
            ChannelName = "global",
            SenderId = 1,
            SenderName = "User1",
            Content = "Hello",
            Timestamp = DateTime.UtcNow
        };

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await service.SaveMessageAsync(message);
        }

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var savedMessage = await context.Messages.FirstOrDefaultAsync(m => m.LobbyId == "test-lobby-msg");
            Assert.NotNull(savedMessage);
            Assert.Equal("Hello", savedMessage.Content);
        }
    }

    [Fact]
    public async Task GetMessageHistoryAsync_ShouldReturnMessagesForChannel()
    {
        var dbContextOptions = CreateNewContextOptions();
        var now = DateTime.UtcNow;

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            context.Messages.Add(new ChatMessageEntity {
                LobbyId = "hist-lobby", ChannelName = "global", Content = "Msg1",
                SenderId = 1, SenderName = "User1", Timestamp = now
            });
            context.Messages.Add(new ChatMessageEntity {
                LobbyId = "hist-lobby", ChannelName = "global", Content = "Msg2",
                SenderId = 2, SenderName = "User2", Timestamp = now.AddSeconds(1)
            });
            context.Messages.Add(new ChatMessageEntity {
                LobbyId = "hist-lobby", ChannelName = "mafia", Content = "Msg3",
                SenderId = 1, SenderName = "User1", Timestamp = now.AddSeconds(2)
            });
            await context.SaveChangesAsync();
        }

        IEnumerable<ChatMessageEntity> history;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            history = await service.GetMessageHistoryAsync("hist-lobby", "global");
        }

        Assert.Equal(2, history.Count());
    }

    [Fact]
    public async Task GetLobbyAsync_WithChannelWithNoMembers_ShouldMapCorrectly()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto {
            LobbyId = "test-lobby-no-members",
            PrivateChannels = [ new PrivateChannelDto { ChannelName = "mafia", MemberIds = [] } ]
        };
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        Lobby? lobbyModel;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            lobbyModel = await service.GetLobbyAsync("test-lobby-no-members");
        }

        Assert.NotNull(lobbyModel);
        Assert.True(lobbyModel.PrivateChannels.TryGetValue("mafia", out var mafiaChannel));
        Assert.Empty(mafiaChannel.Members);
    }

    [Fact]
    public async Task CreateNewLobbyAsync_WithNoPrivateChannels_ShouldCreateLobby()
    {
        var dbContextOptions = CreateNewContextOptions();
        var lobbyDto = new LobbyCreationDto
        {
            LobbyId = "test-lobby-no-channels",
            PrivateChannels = []
        };

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            await service.CreateNewLobbyAsync(lobbyDto);
        }

        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var lobby = await context.Lobbies
                .Include(l => l.PrivateChannels)
                .FirstOrDefaultAsync(l => l.Id == "test-lobby-no-channels");

            Assert.NotNull(lobby);
            Assert.Empty(lobby.PrivateChannels);
        }
    }

    [Fact]
    public async Task GetMessageHistoryAsync_WithZeroLimit_ShouldReturnEmpty()
    {
        var dbContextOptions = CreateNewContextOptions();
        var now = DateTime.UtcNow;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            context.Messages.Add(new ChatMessageEntity {
                LobbyId = "hist-lobby-zero", ChannelName = "global", Content = "Msg1",
                SenderId = 1, SenderName = "User1", Timestamp = now
            });
            await context.SaveChangesAsync();
        }

        IEnumerable<ChatMessageEntity> history;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            history = await service.GetMessageHistoryAsync("hist-lobby-zero", "global", limit: 0);
        }

        Assert.Empty(history);
    }

    [Fact]
    public async Task GetAnnouncementHistoryAsync_WithZeroLimit_ShouldReturnEmpty()
    {
        var dbContextOptions = CreateNewContextOptions();
        await using (var context = new ChatDbContext(dbContextOptions))
        {
             context.Announcements.Add(new Announcement { LobbyId = "ann-lobby-zero", Content = "Announcement 1" });
            await context.SaveChangesAsync();
        }

        IEnumerable<Announcement> history;
        await using (var context = new ChatDbContext(dbContextOptions))
        {
            var service = new PostgresChatService(context, _logger);
            history = await service.GetAnnouncementHistoryAsync("ann-lobby-zero", limit: 0);
        }

        Assert.Empty(history);
    }
    
    private static ChatDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ChatDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new ChatDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
    
    [Fact]
    public async Task IsGlobalChatEnabledAsync_LobbyNotFound_ReturnsFalse()
    {
        await using var dbContext = GetInMemoryDbContext();
        var mockLogger = new Mock<ILogger<PostgresChatService>>();
        var service = new PostgresChatService(dbContext, mockLogger.Object);

        var isEnabled = await service.IsGlobalChatEnabledAsync("non-existent-lobby");

        Assert.False(isEnabled);
    }
}