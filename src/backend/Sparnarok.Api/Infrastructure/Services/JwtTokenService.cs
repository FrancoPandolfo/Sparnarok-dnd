using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Sparnarok.Api.Application.Interfaces;
using Sparnarok.Api.Domain.Entities;

namespace Sparnarok.Api.Infrastructure.Services;

/// <summary>
/// Implementación de <see cref="IJwtTokenService"/> para la emisión de tokens JWT simétricos.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _keyBytes;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryDays;

    /// <summary>
    /// Clave predeterminada de desarrollo seguro para entornos locales sin archivo de configuración.
    /// </summary>
    public const string DefaultDevKey = "Sparnarok_Super_Secret_Key_For_Jwt_Token_Signing_2026_Minimum_256_Bits!";
    public const string DefaultIssuer = "SparnarokApi";
    public const string DefaultAudience = "SparnarokWeb";

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="JwtTokenService"/>.
    /// </summary>
    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;

        var keyString = _configuration["Authentication:Jwt:Key"] ?? DefaultDevKey;
        _keyBytes = Encoding.UTF8.GetBytes(keyString);
        _issuer = _configuration["Authentication:Jwt:Issuer"] ?? DefaultIssuer;
        _audience = _configuration["Authentication:Jwt:Audience"] ?? DefaultAudience;

        if (!int.TryParse(_configuration["Authentication:Jwt:ExpiryDays"], out _expiryDays))
        {
            _expiryDays = 7;
        }
    }

    /// <inheritdoc />
    public string GenerateToken(ApplicationUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(_keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
        {
            claims.Add(new Claim("picture", user.AvatarUrl));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(_expiryDays),
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = credentials
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}
