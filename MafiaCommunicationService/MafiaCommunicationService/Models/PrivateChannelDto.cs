using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class PrivateChannelDto
{
    [Required]
    public string ChannelName { get; set; } = null!;

    [Required]
    public List<long> MemberIds { get; set; } = null!;
}