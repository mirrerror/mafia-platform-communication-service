using System.Collections.Concurrent;
using MafiaCommunicationService.Data;
using MafiaCommunicationService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Services;

public class PostgresChatService(ChatDbContext dbContext) : IChatService
{
    private static readonly ConcurrentDictionary<string, Lobby> Lobbies = new();
    private static readonly ConcurrentDictionary<string, bool> LobbyChatStatus = new();

    public Lobby CreateNewLobby(LobbyCreationDto lobbyCreationDto)
    {
        var lobby = new Lobby { Id = lobbyCreationDto.LobbyId };

        foreach (var channelDto in lobbyCreationDto.PrivateChannels)
        {
            var privateChannel = new PrivateChannel { Name = channelDto.ChannelName };
            foreach (var memberId in channelDto.MemberIds)
            {
                privateChannel.Members.TryAdd(memberId, true);
            }
            lobby.PrivateChannels.TryAdd(privateChannel.Name, privateChannel);
        }

        var wasAdded = Lobbies.TryAdd(lobby.Id, lobby);
        if (!wasAdded)
        {
            throw new InvalidOperationException("Lobby with this ID already exists.");
        }
        LobbyChatStatus.TryAdd(lobby.Id, false);
        return lobby;
    }

    public Lobby? GetLobby(string lobbyId)
    {
        return Lobbies.GetValueOrDefault(lobbyId);
    }

    public Task<bool> PrivateChannelExistsAsync(string lobbyId, string channelName) => Task.FromResult(Lobbies.TryGetValue(lobbyId, out var lobby) && lobby.PrivateChannels.ContainsKey(channelName));
    public Task<bool> UserHasAccessToChannelAsync(string lobbyId, string channelName, long userId)
    {
        if (Lobbies.TryGetValue(lobbyId, out var lobby) && lobby.PrivateChannels.TryGetValue(channelName, out var channel))
            return Task.FromResult(channel.Members.ContainsKey(userId));
        return Task.FromResult(false);
    }
    public bool IsGlobalChatEnabled(string lobbyId) => LobbyChatStatus.GetValueOrDefault(lobbyId, false);
    public bool ToggleGlobalChat(string lobbyId) => LobbyChatStatus.AddOrUpdate(lobbyId, true, (_, v) => !v);

    public Task<IEnumerable<string>> GetPrivateChannelsAsync(string lobbyId)
    {
        return Lobbies.TryGetValue(lobbyId, out var lobby) ? Task.FromResult<IEnumerable<string>>(lobby.PrivateChannels.Keys.ToList()) : Task.FromResult(Enumerable.Empty<string>());
    }

    public async Task SaveMessageAsync(ChatMessageEntity message)
    {
        message.Id = Guid.NewGuid();
        dbContext.Messages.Add(message);
        await dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<ChatMessageEntity>> GetMessageHistoryAsync(string lobbyId, string? channelName, int limit = 50)
    {
        return await dbContext.Messages
            .Where(m => m.LobbyId == lobbyId && m.ChannelName == channelName)
            .OrderByDescending(m => m.Timestamp).Take(limit).OrderBy(m => m.Timestamp)
            .AsNoTracking().ToListAsync();
    }
}