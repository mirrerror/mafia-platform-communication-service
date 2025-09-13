using System.Collections.Concurrent;

namespace MafiaCommunicationService.Models;

public class Lobby
{
    public string Id { get; set; }
    public ConcurrentDictionary<string, PrivateChannel> PrivateChannels { get; } = new();
}