using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MafiaCommunicationService.Models;

public class PrivateChannelEntity
{
    [Key]
    public Guid Id { get; set; }

    public required string Name { get; set; }

    [ForeignKey(nameof(LobbyEntity))]
    public required string LobbyId { get; set; }

    public virtual LobbyEntity Lobby { get; set; } = null!;

    public virtual ICollection<PrivateChannelMemberEntity> Members { get; set; } = new List<PrivateChannelMemberEntity>();
}