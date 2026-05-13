using BananaAuthApi.DTOs;

namespace BananaAuthApi.Services;

public interface IAuthService
{
    Task<RegisterResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResponseDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
