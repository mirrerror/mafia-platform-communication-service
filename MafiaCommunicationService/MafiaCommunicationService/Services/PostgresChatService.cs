using System.Collections.Concurrent;
using MafiaCommunicationService.Data;
using MafiaCommunicationService.Models;
using Microsoft.EntityFrameworkCore;

namespace MafiaCommunicationService.Services;

public class PostgresChatService : IChatService
{
    private readonly ChatDbContext _dbContext;

    private static readonly ConcurrentDictionary<string, Lobby> Lobbies = new();
    private static readonly ConcurrentDictionary<string, bool> LobbyChatStatus = new();

    static PostgresChatService()
    {
        var lobby1 = new Lobby { Id = "mafia_night_123" };
        var mafiaChannel = new PrivateChannel { Name = "mafia" };
        mafiaChannel.Members.TryAdd(101, true);
        mafiaChannel.Members.TryAdd(102, true);
        var detectivesChannel = new PrivateChannel { Name = "detectives" };
        detectivesChannel.Members.TryAdd(201, true);
        lobby1.PrivateChannels.TryAdd(mafiaChannel.Name, mafiaChannel);
        lobby1.PrivateChannels.TryAdd(detectivesChannel.Name, detectivesChannel);
        Lobbies.TryAdd(lobby1.Id, lobby1);
        LobbyChatStatus.TryAdd(lobby1.Id, false);
    }
    
    public PostgresChatService(ChatDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> LobbyExistsAsync(string lobbyId) => Task.FromResult(Lobbies.ContainsKey(lobbyId));
    public Task<bool> PrivateChannelExistsAsync(string lobbyId, string channelName) => Task.FromResult(Lobbies.TryGetValue(lobbyId, out var lobby) && lobby.PrivateChannels.ContainsKey(channelName));
    public Task<bool> UserHasAccessToChannelAsync(string lobbyId, string channelName, long userId)
    {
        if (Lobbies.TryGetValue(lobbyId, out var lobby) && lobby.PrivateChannels.TryGetValue(channelName, out var channel))
            return Task.FromResult(channel.Members.ContainsKey(userId));
        return Task.FromResult(false);
    }
    public bool IsGlobalChatEnabled(string lobbyId) => LobbyChatStatus.GetValueOrDefault(lobbyId, false);
    public bool ToggleGlobalChat(string lobbyId) => LobbyChatStatus.AddOrUpdate(lobbyId, true, (_, v) => !v);

    public async Task SaveMessageAsync(ChatMessageEntity message)
    {
        message.Id = Guid.NewGuid();
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<ChatMessageEntity>> GetMessageHistoryAsync(string lobbyId, string? channelName, int limit = 50)
    {
        return await _dbContext.Messages
            .Where(m => m.LobbyId == lobbyId && m.ChannelName == channelName)
            .OrderByDescending(m => m.Timestamp).Take(limit).OrderBy(m => m.Timestamp)
            .AsNoTracking().ToListAsync();
    }
}