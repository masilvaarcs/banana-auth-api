using BananaAuthApi.Common;
using BananaAuthApi.Data;
using BananaAuthApi.DTOs;
using BananaAuthApi.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BananaAuth.Tests.Services;

/// <summary>
/// Testes unitários do AuthService.
/// Usam banco InMemory (sem SQL Server) e Mock de ITokenService para isolamento total.
/// </summary>
public class AuthServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Cria um AppDbContext com banco InMemory exclusivo (Guid garante isolamento entre testes).
    /// </summary>
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Mock de ITokenService com retornos previsíveis.</summary>
    private static Mock<ITokenService> CreateTokenMock()
    {
        var mock = new Mock<ITokenService>();
        mock.Setup(t => t.GenerateAccessToken(It.IsAny<BananaAuthApi.Models.User>()))
            .Returns("fake-access-token");
        mock.Setup(t => t.GenerateRefreshToken())
            .Returns("fake-raw-refresh-token");
        mock.Setup(t => t.HashRefreshToken(It.IsAny<string>()))
            .Returns<string>(raw => $"hash-of-{raw}");
        mock.Setup(t => t.GetAccessTokenExpiresInSeconds())
            .Returns(3600);
        mock.Setup(t => t.GetRefreshTokenExpiryUtc())
            .Returns(DateTime.UtcNow.AddDays(7));
        return mock;
    }

    private static AuthService CreateService(AppDbContext db, Mock<ITokenService>? tokenMock = null)
        => new(db, (tokenMock ?? CreateTokenMock()).Object);

    private static RegisterRequestDto ValidRegisterRequest(
        string name = "Test User",
        string email = "test@banana.com",
        string password = "Banana@123")
        => new() { Name = name, Email = email, Password = password };

    private static LoginRequestDto ValidLoginRequest(
        string email = "test@banana.com",
        string password = "Banana@123")
        => new() { Email = email, Password = password };

    // ── RegisterAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUserAndReturnsDto()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.RegisterAsync(ValidRegisterRequest());

        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal("Test User", result.Name);
        Assert.Equal("test@banana.com", result.Email);

        // Verifica que foi persistido no banco
        var savedUser = await db.Users.FindAsync(result.UserId);
        Assert.NotNull(savedUser);
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmailToLowercase()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.RegisterAsync(
            ValidRegisterRequest(email: "UPPER@BANANA.COM"));

        Assert.Equal("upper@banana.com", result.Email);
    }

    [Fact]
    public async Task RegisterAsync_HashesPasswordBeforePersisting()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        const string plainPassword = "Banana@123";

        var result = await svc.RegisterAsync(
            ValidRegisterRequest(password: plainPassword));

        var savedUser = await db.Users.FindAsync(result.UserId);
        Assert.NotNull(savedUser);
        // Senha jamais deve ser armazenada em texto puro
        Assert.NotEqual(plainPassword, savedUser.PasswordHash);
        // BCrypt hashes começam com "$2"
        Assert.StartsWith("$2", savedUser.PasswordHash);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsApiException409()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        var request = ValidRegisterRequest(email: "duplicate@banana.com");

        await svc.RegisterAsync(request);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => svc.RegisterAsync(request));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmailIsCaseInsensitive()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        await svc.RegisterAsync(ValidRegisterRequest(email: "case@banana.com"));

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => svc.RegisterAsync(ValidRegisterRequest(email: "CASE@BANANA.COM")));

        Assert.Equal(409, ex.StatusCode);
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAuthResponse()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        await svc.RegisterAsync(ValidRegisterRequest());

        var result = await svc.LoginAsync(ValidLoginRequest());

        Assert.Equal("fake-access-token", result.Token);
        Assert.Equal("fake-raw-refresh-token", result.RefreshToken);
        Assert.Equal(3600, result.ExpiresIn);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsApiException401()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        await svc.RegisterAsync(ValidRegisterRequest());

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => svc.LoginAsync(ValidLoginRequest(password: "SenhaErrada@999")));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsApiException401()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => svc.LoginAsync(ValidLoginRequest(email: "naoexiste@banana.com")));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task LoginAsync_EmailIsCaseInsensitive()
    {
        await using var db = CreateDb();
        var svc = CreateService(db);
        await svc.RegisterAsync(ValidRegisterRequest(email: "login.case@banana.com"));

        // Login com caixa diferente deve funcionar
        var result = await svc.LoginAsync(
            ValidLoginRequest(email: "LOGIN.CASE@BANANA.COM"));

        Assert.Equal("fake-access-token", result.Token);
    }

    [Fact]
    public async Task LoginAsync_RevokesExistingRefreshTokensBeforeIssuingNew()
    {
        await using var db = CreateDb();
        var tokenMock = CreateTokenMock();
        var svc = CreateService(db, tokenMock);
        await svc.RegisterAsync(ValidRegisterRequest());

        // Primeiro login — cria um refresh token ativo
        await svc.LoginAsync(ValidLoginRequest());

        // Segundo login — deve revogar o anterior e criar um novo
        await svc.LoginAsync(ValidLoginRequest());

        // Todos os tokens exceto o último devem estar revogados
        var tokens = db.RefreshTokens.ToList();
        var revoked = tokens.Where(t => t.RevokedAtUtc.HasValue).ToList();
        var active = tokens.Where(t => !t.RevokedAtUtc.HasValue).ToList();

        Assert.Single(active);
        Assert.NotEmpty(revoked);
    }

    // ── RefreshAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshAsync_WithValidToken_ReturnsNewAuthResponse()
    {
        await using var db = CreateDb();

        // HashRefreshToken precisa retornar hash real para o RefreshAsync encontrar no banco.
        var tokenMock = new Mock<ITokenService>();
        tokenMock.Setup(t => t.GenerateAccessToken(It.IsAny<BananaAuthApi.Models.User>()))
            .Returns("new-access-token");
        tokenMock.Setup(t => t.GenerateRefreshToken())
            .Returns("new-raw-refresh");
        // Hash real (SHA256) para que o RefreshAsync encontre o token gravado pelo LoginAsync
        tokenMock.Setup(t => t.HashRefreshToken(It.IsAny<string>()))
            .Returns<string>(raw =>
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return Convert.ToHexString(bytes);
            });
        tokenMock.Setup(t => t.GetAccessTokenExpiresInSeconds()).Returns(3600);
        tokenMock.Setup(t => t.GetRefreshTokenExpiryUtc())
            .Returns(DateTime.UtcNow.AddDays(7));

        var svc = CreateService(db, tokenMock);
        await svc.RegisterAsync(ValidRegisterRequest());
        var loginResult = await svc.LoginAsync(ValidLoginRequest());

        var refreshResult = await svc.RefreshAsync(loginResult.RefreshToken);

        Assert.Equal("new-access-token", refreshResult.Token);
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidToken_ThrowsApiException401()
    {
        await using var db = CreateDb();
        var tokenMock = new Mock<ITokenService>();
        tokenMock.Setup(t => t.HashRefreshToken(It.IsAny<string>()))
            .Returns<string>(raw =>
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(raw));
                return Convert.ToHexString(bytes);
            });
        tokenMock.Setup(t => t.GetRefreshTokenExpiryUtc()).Returns(DateTime.UtcNow.AddDays(7));

        var svc = CreateService(db, tokenMock);

        var ex = await Assert.ThrowsAsync<ApiException>(
            () => svc.RefreshAsync("token-invalido-que-nao-existe"));

        Assert.Equal(401, ex.StatusCode);
    }
}
