using System;

namespace FarmKart.Application.Abstractions.Authentication;

public interface IJwtTokenService
{
    string GenerateToken(Guid userId, string email, string role);
}
