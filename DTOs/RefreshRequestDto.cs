using System.ComponentModel.DataAnnotations;

namespace BananaAuthApi.DTOs;

public class RefreshRequestDto
{
    [Required]
    [MinLength(24)]
    [MaxLength(512)]
    public string RefreshToken { get; set; } = string.Empty;
}
