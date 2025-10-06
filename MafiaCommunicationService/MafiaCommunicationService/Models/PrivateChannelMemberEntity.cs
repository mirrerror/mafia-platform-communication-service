using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MafiaCommunicationService.Models;

public class PrivateChannelMemberEntity
{
    [Key]
    public Guid Id { get; set; }

    public long MemberId { get; set; }

    [ForeignKey(nameof(PrivateChannelEntity))]
    public Guid PrivateChannelId { get; set; }

    public virtual PrivateChannelEntity PrivateChannel { get; set; } = null!;
}