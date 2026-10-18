using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BillSplitter.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace BillSplitter.Services;

public interface ITokenService
{
    Task<AuthTokenResult> CreateAsync(AppUser user, CancellationToken cancellationToken);
    Task<AuthTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}

public sealed record AuthTokenResult(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

public sealed class TokenService(IConfiguration configuration, AppDbContext db) : ITokenService
{
    public async Task<AuthTokenResult> CreateAsync(AppUser user, CancellationToken cancellationToken)
    {
    // Keep the access token short-lived so a leaked browser token has a small window of usefulness.
        var expires = DateTime.UtcNow.AddMinutes(configuration.GetValue("Jwt:AccessMinutes", 15));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty), new Claim("name", user.DisplayName) };
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        // Only a SHA-256 hash is persisted; the original refresh token is returned once to the client.
        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        db.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = Hash(refresh), ExpiresAtUtc = DateTime.UtcNow.AddDays(configuration.GetValue("Jwt:RefreshDays", 30)) });
        await db.SaveChangesAsync(cancellationToken);
        return new(new JwtSecurityTokenHandler().WriteToken(token), refresh, expires);
    }

    public async Task<AuthTokenResult?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        // Rotation makes replaying an already-used refresh token fail safely.
        var stored = await db.RefreshTokens.Include(x => x.User).SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken);
        if (stored is null || !stored.IsActive) return null;
        stored.RevokedAtUtc = DateTime.UtcNow;
        return await CreateAsync(stored.User, cancellationToken);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

public sealed class CurrencyService(HttpClient http, IConfiguration configuration)
{
    public async Task<decimal> GetRateAsync(string from, string to, CancellationToken cancellationToken)
    {
    // Same-currency conversions avoid a network call and preserve the exact amount.
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 1m;
        var url = configuration["Currency:BaseUrl"] ?? "https://api.frankfurter.app/latest";
        // Escape the ISO codes even though they normally contain only three letters.
        using var response = await http.GetAsync($"{url}?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        return document.RootElement.GetProperty("rates").GetProperty(to.ToUpperInvariant()).GetDecimal();
    }
}
