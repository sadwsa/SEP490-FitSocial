using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Conversations;

public class SendMessageRequestDto
{
    [Required(ErrorMessage = "Message content cannot be empty.")]
    [MaxLength(300, ErrorMessage = "Message cannot exceed 300 characters.")]
    public string Content { get; set; } = string.Empty;
}
