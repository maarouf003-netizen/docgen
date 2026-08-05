using DocGenerator.Domain.Entities;

namespace DocGenerator.Application.Common.Interfaces;

public interface ITokenService
{
    string CreateToken(User user);
}
