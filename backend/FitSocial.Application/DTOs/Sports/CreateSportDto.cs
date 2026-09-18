using System.ComponentModel.DataAnnotations;

namespace FitSocial.Application.DTOs.Sports;

public class CreateSportDto
{
    [Required(ErrorMessage = "Tên môn thể thao không được để trống.")]
    [StringLength(100, ErrorMessage = "Tên môn thể thao không được vượt quá 100 ký tự.")]
    public string SportName { get; set; } = string.Empty;
}
