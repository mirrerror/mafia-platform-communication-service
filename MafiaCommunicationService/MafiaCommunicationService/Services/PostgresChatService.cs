using MafiaCommunicationService.Data;
using MafiaCommunicationService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Services;

public class PostgresChatService(ChatDbContext dbContext, ILogger<PostgresChatService> logger) : IChatService
{
    public async Task<Lobby> CreateNewLobbyAsync(LobbyCreationDto lobbyCreationDto)
    {
        logger.LogInformation("Service: Creating new lobby with ID: {LobbyId}", lobbyCreationDto.LobbyId);
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
            logger.LogDebug("Service: Adding channel {ChannelName} with {MemberCount} members to lobby {LobbyId}", channelEntity.Name, channelEntity.Members.Count, lobbyEntity.Id);
        }

        dbContext.Lobbies.Add(lobbyEntity);

        try
        {
            await dbContext.SaveChangesAsync();
            logger.LogInformation("Service: Successfully saved new lobby with ID: {LobbyId}", lobbyCreationDto.LobbyId);
        }
        catch (Exception ex) when (ex is DbUpdateException or ArgumentException)
        {
            logger.LogWarning(ex, "Service: Failed to create lobby. Lobby with this ID already exists: {LobbyId}", lobbyCreationDto.LobbyId);
            throw new InvalidOperationException("Lobby with this ID already exists.", ex);
        }

        return MapLobbyEntityToModel(lobbyEntity);
    }
    
    public async Task<Lobby?> GetLobbyAsync(string lobbyId)
    {
        logger.LogDebug("Service: Getting lobby by ID: {LobbyId}", lobbyId);
        var lobbyEntity = await dbContext.Lobbies
            .Include(l => l.PrivateChannels)
            .ThenInclude(pc => pc.Members)
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == lobbyId);

        if (lobbyEntity == null)
        {
            logger.LogDebug("Service: Lobby not found: {LobbyId}", lobbyId);
            return null;
        }
        
        logger.LogDebug("Service: Found lobby: {LobbyId}", lobbyId);
        return MapLobbyEntityToModel(lobbyEntity);
    }

    public async Task<bool> DeleteLobbyAsync(string lobbyId)
    {
        logger.LogInformation("Service: Deleting lobby with ID: {LobbyId}", lobbyId);
        var lobby = await dbContext.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId);
        if (lobby == null)
        {
            logger.LogWarning("Service: Cannot delete. Lobby not found: {LobbyId}", lobbyId);
            return false;
        }

        dbContext.Lobbies.Remove(lobby);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Service: Successfully deleted lobby: {LobbyId}", lobbyId);
        return true;
    }
    
    public async Task<bool> PrivateChannelExistsAsync(string lobbyId, string channelName) => 
        await dbContext.PrivateChannels.AnyAsync(pc => pc.LobbyId == lobbyId && pc.Name == channelName);

    public async Task<bool> UserHasAccessToChannelAsync(string lobbyId, string channelName, long userId) =>
        await dbContext.PrivateChannelMembers.AnyAsync(pcm => pcm.MemberId == userId &&
                                pcm.PrivateChannel.LobbyId == lobbyId &&
                                pcm.PrivateChannel.Name == channelName);

    public async Task<IEnumerable<string>> GetPrivateChannelsAsync(string lobbyId)
    {
        logger.LogDebug("Service: Getting private channels for lobby: {LobbyId}", lobbyId);
        return await dbContext.PrivateChannels.Where(pc => pc.LobbyId == lobbyId).Select(pc => pc.Name).ToListAsync();
    }
        
    public async Task<bool> IsGlobalChatEnabledAsync(string lobbyId)
    {
        logger.LogDebug("Service: Checking global chat status for lobby: {LobbyId}", lobbyId);
        var status = await dbContext.Lobbies
            .Where(l => l.Id == lobbyId)
            .Select(l => (bool?)l.IsGlobalChatEnabled)
            .FirstOrDefaultAsync();
        return status ?? false;
    }

    public async Task<bool?> ToggleGlobalChatAsync(string lobbyId)
    {
        logger.LogInformation("Service: Toggling global chat status for lobby: {LobbyId}", lobbyId);
        var lobby = await dbContext.Lobbies.FirstOrDefaultAsync(l => l.Id == lobbyId);
        if (lobby == null)
        {
            logger.LogWarning("Service: Cannot toggle chat. Lobby not found: {LobbyId}", lobbyId);
            return null;
        }

        lobby.IsGlobalChatEnabled = !lobby.IsGlobalChatEnabled;
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Service: Global chat status for lobby {LobbyId} set to {Status}", lobbyId, lobby.IsGlobalChatEnabled);
        return lobby.IsGlobalChatEnabled;
    }

    public async Task SaveMessageAsync(ChatMessageEntity message)
    {
        message.Id = Guid.NewGuid();
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
        logger.LogDebug("Service: Saved message from {SenderId} in lobby {LobbyId}, channel {ChannelName}", message.SenderId, message.LobbyId, message.ChannelName ?? "global");
    }

    public async Task<IEnumerable<ChatMessageEntity>> GetMessageHistoryAsync(string lobbyId, string? channelName, int limit = 50)
    {
        logger.LogDebug("Service: Getting message history for lobby {LobbyId}, channel {ChannelName}", lobbyId, channelName ?? "global");
        return await dbContext.Messages
            .Where(m => m.LobbyId == lobbyId && m.ChannelName == channelName)
            .OrderByDescending(m => m.Timestamp).Take(limit).OrderBy(m => m.Timestamp)
            .AsNoTracking().ToListAsync();
    }
    
    public async Task<Announcement> CreateAnnouncementAsync(string lobbyId, AnnouncementDto announcementDto)
    {
        logger.LogInformation("Service: Creating announcement for lobby: {LobbyId}", lobbyId);
        var announcement = new Announcement
        {
            LobbyId = lobbyId,
            Content = announcementDto.Content,
            Timestamp = DateTime.UtcNow
        };

        dbContext.Announcements.Add(announcement);
        await dbContext.SaveChangesAsync();
        logger.LogInformation("Service: Saved new announcement for lobby: {LobbyId}", lobbyId);

        return announcement;
    }
    
    public async Task<IEnumerable<Announcement>> GetAnnouncementHistoryAsync(string lobbyId, int limit = 50)
    {
        logger.LogDebug("Service: Getting announcement history for lobby: {LobbyId}", lobbyId);
        return await dbContext.Announcements
            .Where(a => a.LobbyId == lobbyId)
            .OrderByDescending(a => a.Timestamp)
            .Take(limit)
            .OrderBy(a => a.Timestamp)
            .AsNoTracking()
            .ToListAsync();
    }

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