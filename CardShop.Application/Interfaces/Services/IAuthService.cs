using CardShop.Application.DTOs.Auth;

namespace CardShop.Application.Interfaces.Services
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
        Task<AuthResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    }
}
