using BananaAuthApi.Common;
using BananaAuthApi.Data;
using BananaAuthApi.DTOs;
using BananaAuthApi.Models;
using BCrypt.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BananaAuthApi.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _dbContext;
    private readonly ITokenService _tokenService;

    public AuthService(AppDbContext dbContext, ITokenService tokenService)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
    }

    public async Task<RegisterResponseDto> RegisterAsync(
        RegisterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var exists = await _dbContext.Users
            .AnyAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (exists)
        {
            throw new ApiException(StatusCodes.Status409Conflict, "E-mail já cadastrado.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RegisterResponseDto
        {
            UserId = user.Id,
            Name = user.Name,
            Email = user.Email
        };
    }

    public async Task<AuthResponseDto> LoginAsync(
        LoginRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(request.Email);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "Credenciais inválidas.");
        }

        // Mantém somente um refresh token ativo por usuário para simplificar a revogação.
        var activeTokens = await _dbContext.RefreshTokens
            .Where(x => x.UserId == user.Id && x.RevokedAtUtc == null && x.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
        }

        var rawRefreshToken = _tokenService.GenerateRefreshToken();
        var hashedRefreshToken = _tokenService.HashRefreshToken(rawRefreshToken);

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = hashedRefreshToken,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = _tokenService.GetRefreshTokenExpiryUtc(),
            RevokedAtUtc = null
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, rawRefreshToken);
    }

    public async Task<AuthResponseDto> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var hashedToken = _tokenService.HashRefreshToken(refreshToken);

        var tokenEntity = await _dbContext.RefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == hashedToken, cancellationToken);

        if (tokenEntity is null ||
            tokenEntity.RevokedAtUtc.HasValue ||
            tokenEntity.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ApiException(StatusCodes.Status401Unauthorized, "Refresh token inválido ou expirado.");
        }

        tokenEntity.RevokedAtUtc = DateTime.UtcNow;

        var newRawRefreshToken = _tokenService.GenerateRefreshToken();

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = tokenEntity.UserId,
            TokenHash = _tokenService.HashRefreshToken(newRawRefreshToken),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = _tokenService.GetRefreshTokenExpiryUtc(),
            RevokedAtUtc = null
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(tokenEntity.User, newRawRefreshToken);
    }

    private AuthResponseDto BuildAuthResponse(User user, string refreshToken)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);

        return new AuthResponseDto
        {
            Token = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = _tokenService.GetAccessTokenExpiresInSeconds()
        };
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }
}
