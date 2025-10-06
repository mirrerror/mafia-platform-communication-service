using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class Announcement
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string LobbyId { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }
}