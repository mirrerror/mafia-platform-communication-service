using System.Collections.Concurrent;

namespace MafiaCommunicationService.Models;

public class PrivateChannel
{
    public required string Name { get; set; }
    public ConcurrentDictionary<long, bool> Members { get; } = new();
}