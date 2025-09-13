using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class ChatMessage
{
    [Range(0, long.MaxValue, ErrorMessage = "Sender ID must be a non-negative number.")]
    public long SenderId { get; set; } = -1;

    [Required(ErrorMessage = "Sender name cannot be empty.")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Sender name must be between 2 and 50 characters.")]
    public string SenderName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Message content cannot be empty.")]
    [StringLength(200, ErrorMessage = "Message content cannot exceed 200 characters.")]
    public string Content { get; set; } = string.Empty;
}