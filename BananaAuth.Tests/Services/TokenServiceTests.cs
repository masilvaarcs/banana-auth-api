using System.IdentityModel.Tokens.Jwt;
using BananaAuthApi.Configuration;
using BananaAuthApi.Models;
using BananaAuthApi.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace BananaAuth.Tests.Services;

/// <summary>
/// Testes unitários do TokenService.
/// Verificam geração de JWT, refresh token e hashing — sem banco de dados.
/// </summary>
public class TokenServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static TokenService CreateService(
        int expirationMinutes = 60,
        int refreshExpirationDays = 7)
    {
        var settings = new JwtSettings
        {
            Secret = "banana_test_secret_chave_supersecreta_xunit_32c",
            Issuer = "banana-auth-service",
            Audience = "banana-app",
            ExpirationMinutes = expirationMinutes,
            RefreshExpirationDays = refreshExpirationDays,
        };
        return new TokenService(Options.Create(settings));
    }

    private static User CreateUser(
        string name = "Test User",
        string email = "test@banana.com")
        => new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            PasswordHash = "hash-placeholder",
            CreatedAtUtc = DateTime.UtcNow,
        };

    // ── GenerateAccessToken ───────────────────────────────────────────────────

    [Fact]
    public void GenerateAccessToken_ReturnsNonEmptyString()
    {
        var svc = CreateService();
        var user = CreateUser();

        var token = svc.GenerateAccessToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateAccessToken_TokenContainsUserClaims()
    {
        var svc = CreateService();
        var user = CreateUser(name: "Marcos Banana", email: "marcos@banana.com");

        var tokenString = svc.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Name && c.Value == user.Name);
    }

    [Fact]
    public void GenerateAccessToken_TokenHasCorrectIssuerAndAudience()
    {
        var svc = CreateService();
        var user = CreateUser();

        var tokenString = svc.GenerateAccessToken(user);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        Assert.Equal("banana-auth-service", jwt.Issuer);
        Assert.Contains("banana-app", jwt.Audiences);
    }

    [Fact]
    public void GenerateAccessToken_ExpiresInConfiguredMinutes()
    {
        const int expectedMinutes = 30;
        var svc = CreateService(expirationMinutes: expectedMinutes);
        var user = CreateUser();

        var before = DateTime.UtcNow;
        var tokenString = svc.GenerateAccessToken(user);
        var after = DateTime.UtcNow;

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(tokenString);

        // Margem de 5 segundos para compensar o tempo de geração
        Assert.True(jwt.ValidTo >= before.AddMinutes(expectedMinutes).AddSeconds(-5));
        Assert.True(jwt.ValidTo <= after.AddMinutes(expectedMinutes).AddSeconds(5));
    }

    // ── GenerateRefreshToken ──────────────────────────────────────────────────

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyString()
    {
        var svc = CreateService();

        var token = svc.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateRefreshToken_EachCallReturnsUniqueValue()
    {
        var svc = CreateService();

        var t1 = svc.GenerateRefreshToken();
        var t2 = svc.GenerateRefreshToken();

        Assert.NotEqual(t1, t2);
    }

    [Fact]
    public void GenerateRefreshToken_IsValidBase64()
    {
        var svc = CreateService();

        var token = svc.GenerateRefreshToken();
        var bytes = Convert.TryFromBase64String(token, new byte[512], out _);

        Assert.True(bytes, "Refresh token deve ser Base64 válido.");
    }

    // ── HashRefreshToken ──────────────────────────────────────────────────────

    [Fact]
    public void HashRefreshToken_SameInputProducesSameHash()
    {
        var svc = CreateService();
        const string raw = "meu-refresh-token-de-teste";

        var h1 = svc.HashRefreshToken(raw);
        var h2 = svc.HashRefreshToken(raw);

        Assert.Equal(h1, h2);
    }

    [Fact]
    public void HashRefreshToken_DifferentInputsProduceDifferentHashes()
    {
        var svc = CreateService();

        var h1 = svc.HashRefreshToken("token-um");
        var h2 = svc.HashRefreshToken("token-dois");

        Assert.NotEqual(h1, h2);
    }

    [Fact]
    public void HashRefreshToken_ReturnsNonEmptyString()
    {
        var svc = CreateService();

        var hash = svc.HashRefreshToken("qualquer-token");

        Assert.False(string.IsNullOrWhiteSpace(hash));
    }

    // ── GetAccessTokenExpiresInSeconds ────────────────────────────────────────

    [Theory]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(120)]
    public void GetAccessTokenExpiresInSeconds_MatchesConfiguredMinutes(int minutes)
    {
        var svc = CreateService(expirationMinutes: minutes);

        var seconds = svc.GetAccessTokenExpiresInSeconds();

        Assert.Equal(minutes * 60, seconds);
    }

    // ── GetRefreshTokenExpiryUtc ──────────────────────────────────────────────

    [Fact]
    public void GetRefreshTokenExpiryUtc_IsInFuture()
    {
        var svc = CreateService();

        var expiry = svc.GetRefreshTokenExpiryUtc();

        Assert.True(expiry > DateTime.UtcNow);
    }

    [Fact]
    public void GetRefreshTokenExpiryUtc_MatchesConfiguredDays()
    {
        const int days = 14;
        var svc = CreateService(refreshExpirationDays: days);

        var before = DateTime.UtcNow;
        var expiry = svc.GetRefreshTokenExpiryUtc();

        Assert.True(expiry >= before.AddDays(days).AddSeconds(-5));
        Assert.True(expiry <= before.AddDays(days).AddSeconds(5));
    }
}
