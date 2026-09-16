using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BankFlow.ServiceDefaults;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BankFlow.Identity.Api.Auth;

public interface ITokenService
{
    public TokenResponse Issue(DemoUser user);
}

public sealed class TokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TokenResponse Issue(DemoUser user)
    {
        var issuedAt = timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddMinutes(_options.ExpirationMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(issuedAt.UtcDateTime).ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64),
            new("name", user.Login)
        };
        claims.AddRange(user.Roles.Select(role => new Claim("role", role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new TokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            "Bearer",
            (int)TimeSpan.FromMinutes(_options.ExpirationMinutes).TotalSeconds,
            expiresAt);
    }
}

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, DateTimeOffset ExpiresAtUtc);

public sealed record LoginRequest(string Login, string Password);
