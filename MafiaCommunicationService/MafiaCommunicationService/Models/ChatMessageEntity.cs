using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class ChatMessageEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string LobbyId { get; set; }

    // Null for global messages
    public string? ChannelName { get; set; }

    [Required]
    public long SenderId { get; set; }
    
    [Required]
    [StringLength(50)]
    public string SenderName { get; set; }

    [Required]
    [StringLength(200)]
    public string Content { get; set; }

    [Required]
    public DateTime Timestamp { get; set; }
}