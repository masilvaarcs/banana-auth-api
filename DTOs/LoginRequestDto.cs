using System.ComponentModel.DataAnnotations;

namespace BananaAuthApi.DTOs;

public class LoginRequestDto
{
    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
