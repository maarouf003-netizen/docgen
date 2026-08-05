using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DocGenerator.Application.Common;
using DocGenerator.Application.Common.Interfaces;
using DocGenerator.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace DocGenerator.Infrastructure.Security;

public class TokenService : ITokenService
{
    private readonly JwtOptions _options;

    public TokenService(JwtOptions options) => _options = options;

    public string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Name, user.Username),
            // الأحرف الصغيرة لتطابق قيم الأدوار في التحكم ([Authorize(Roles="manager,admin")] إلخ)
            new(ClaimTypes.Role, user.Role.ToString().ToLowerInvariant()),
        };

        if (user.BranchId.HasValue)
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));

        // نسخة التوكن لإبطال الرموز القديمة عند تغيير كلمة المرور/تعطيل الحساب
        claims.Add(new Claim("token_version", user.TokenVersion.ToString()));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
