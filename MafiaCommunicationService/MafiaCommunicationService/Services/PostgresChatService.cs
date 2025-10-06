using MafiaCommunicationService.Data;
using MafiaCommunicationService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Services;

public class PostgresChatService(ChatDbContext dbContext) : IChatService
{
    public async Task<Lobby> CreateNewLobbyAsync(LobbyCreationDto lobbyCreationDto)
    {
        var lobbyEntity = new LobbyEntity
        {
            Id = lobbyCreationDto.LobbyId,
            IsGlobalChatEnabled = false,
            PrivateChannels = new List<PrivateChannelEntity>()
        };

        foreach (var channelEntity in lobbyCreationDto.PrivateChannels.Select(channelDto => new PrivateChannelEntity
                 {
                     Name = channelDto.ChannelName,
                     LobbyId = lobbyEntity.Id,
                     Lobby = lobbyEntity,
                     Members = channelDto.MemberIds.Select(memberId => new PrivateChannelMemberEntity { MemberId = memberId }).ToList()
                 }))
        {
            lobbyEntity.PrivateChannels.Add(channelEntity);
        }

        dbContext.Lobbies.Add(lobbyEntity);

        try
        {
            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex) when (ex is DbUpdateException or ArgumentException)
        {
            throw new InvalidOperationException("Lobby with this ID already exists.", ex);
        }

        return MapLobbyEntityToModel(lobbyEntity);
    }
    
    public async Task<Lobby?> GetLobbyAsync(string lobbyId)
    {
        var lobbyEntity = await dbContext.Lobbies
            .Include(l => l.PrivateChannels)
            .ThenInclude(pc => pc.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lobbyId);

        return lobbyEntity == null ? null : MapLobbyEntityToModel(lobbyEntity);
    }

    public async Task<bool> DeleteLobbyAsync(string lobbyId)
    {
        var lobby = await dbContext.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId);
        if (lobby == null) return false;

        dbContext.Lobbies.Remove(lobby);
        await dbContext.SaveChangesAsync();
        return true;
    }
    
    public async Task<bool> PrivateChannelExistsAsync(string lobbyId, string channelName) => 
        await dbContext.PrivateChannels.AnyAsync(pc => pc.LobbyId == lobbyId && pc.Name == channelName);

    public async Task<bool> UserHasAccessToChannelAsync(string lobbyId, string channelName, long userId) =>
        await dbContext.PrivateChannelMembers.AnyAsync(pcm => pcm.MemberId == userId &&
                                pcm.PrivateChannel.LobbyId == lobbyId &&
                                pcm.PrivateChannel.Name == channelName);

    public async Task<IEnumerable<string>> GetPrivateChannelsAsync(string lobbyId) =>
        await dbContext.PrivateChannels.Where(pc => pc.LobbyId == lobbyId).Select(pc => pc.Name).ToListAsync();
        
    public async Task<bool> IsGlobalChatEnabledAsync(string lobbyId)
    {
        var status = await dbContext.Lobbies
            .Where(l => l.Id == lobbyId)
            .Select(l => (bool?)l.IsGlobalChatEnabled)
            .FirstOrDefaultAsync();
        return status ?? false;
    }

    public async Task<bool?> ToggleGlobalChatAsync(string lobbyId)
    {
        var lobby = await dbContext.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId);
        if (lobby == null) return null;

        lobby.IsGlobalChatEnabled = !lobby.IsGlobalChatEnabled;
        await dbContext.SaveChangesAsync();
        return lobby.IsGlobalChatEnabled;
    }

    public async Task SaveMessageAsync(ChatMessageEntity message)
    {
        message.Id = Guid.NewGuid();
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<ChatMessageEntity>> GetMessageHistoryAsync(string lobbyId, string? channelName, int limit = 50) =>
        await dbContext.Messages
            .Where(m => m.LobbyId == lobbyId && m.ChannelName == channelName)
            .OrderByDescending(m => m.Timestamp).Take(limit).OrderBy(m => m.Timestamp)
            .AsNoTracking().ToListAsync();
    
    public async Task<Announcement> CreateAnnouncementAsync(string lobbyId, AnnouncementDto announcementDto)
    {
        var announcement = new Announcement
        {
            LobbyId = lobbyId,
            Content = announcementDto.Content,
            Timestamp = DateTime.UtcNow
        };

        dbContext.Announcements.Add(announcement);
        await dbContext.SaveChangesAsync();

        return announcement;
    }
    
    public async Task<IEnumerable<Announcement>> GetAnnouncementHistoryAsync(string lobbyId, int limit = 50) =>
        await dbContext.Announcements
            .Where(a => a.LobbyId == lobbyId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .OrderBy(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync();

    private static Lobby MapLobbyEntityToModel(LobbyEntity entity)
    {
        var lobby = new Lobby { Id = entity.Id };
        foreach (var channelEntity in entity.PrivateChannels)
        {
            var privateChannel = new PrivateChannel { Name = channelEntity.Name };
            
            foreach (var memberEntity in channelEntity.Members)
            {
                privateChannel.Members.TryAdd(memberEntity.MemberId, true);
            }
            
            lobby.PrivateChannels.TryAdd(privateChannel.Name, privateChannel);
        }
        return lobby;
    }
}