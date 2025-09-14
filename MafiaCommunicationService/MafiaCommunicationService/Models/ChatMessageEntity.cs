using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class ChatMessageEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(50)]
    public required string LobbyId { get; set; }

    // Null for global messages
    [StringLength(50)]
    public string? ChannelName { get; set; }

    [Required]
    public required long SenderId { get; set; }
    
    [Required]
    [StringLength(50)]
    public required string SenderName { get; set; }

    [Required]
    [StringLength(200)]
    public required string Content { get; set; }

    [Required]
    public required DateTime Timestamp { get; set; }
}