using System.ComponentModel.DataAnnotations;

namespace MafiaCommunicationService.Models;

public class AnnouncementDto
{
    [Required(ErrorMessage = "Message content cannot be empty.")]
    [StringLength(200, ErrorMessage = "Message content cannot exceed 200 characters.")]
    public string Content { get; set; } = string.Empty;
}