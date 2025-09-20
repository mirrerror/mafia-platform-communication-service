using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class LobbyCreationDto
{
    [Required]
    public string LobbyId { get; set; } = null!;

    [Required]
    public List<PrivateChannelDto> PrivateChannels { get; set; } = null!;
}