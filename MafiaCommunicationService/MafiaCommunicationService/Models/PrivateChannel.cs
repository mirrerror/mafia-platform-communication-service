using System.Collections.Concurrent;

namespace MafiaCommunicationService.Models;

public class PrivateChannel
{
    public string Name { get; set; }
    public ConcurrentDictionary<long, bool> Members { get; } = new();
}