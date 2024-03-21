using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
namespace MauiApp1
{
    public class JwtService
    {
        private readonly string _secretKey= "2F5A9T841C3LD06GFBC8D45A8AR6W7PC";
        private readonly string _issuer="Mingle";

        public JwtService(string secretKey, string issuer)
        {
            _secretKey = secretKey;
            _issuer = issuer;
        }
        public JwtService()
        {
            
        }

        public string GenerateToken(string username)
        {
            var tokenHandler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _issuer,
                Audience = _issuer
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
