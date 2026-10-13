using CardShop.Application.DTOs.Auth;

namespace CardShop.Application.Interfaces.Services;

public interface ITokenService
{
    AuthResponse GenerateToken(
        string userId,
        string email,
        IList<string> roles);
}