using BananaAuthApi.Models;

namespace BananaAuthApi.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);

    string GenerateRefreshToken();

    string HashRefreshToken(string rawToken);

    int GetAccessTokenExpiresInSeconds();

    DateTime GetRefreshTokenExpiryUtc();
}
