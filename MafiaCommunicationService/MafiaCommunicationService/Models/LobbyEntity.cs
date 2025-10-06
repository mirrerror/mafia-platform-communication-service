using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class LobbyEntity
{
    [Key]
    public required string Id { get; set; }

    public bool IsGlobalChatEnabled { get; set; }

    public virtual ICollection<PrivateChannelEntity> PrivateChannels { get; set; } = new List<PrivateChannelEntity>();
}