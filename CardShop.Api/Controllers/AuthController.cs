using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CardShop.Application.Interfaces.Services;
using CardShop.Application.DTOs.Auth;
using CardShop.Application.Common.Exceptions;

namespace CardShop.Api.Controllers
{
    [ApiController]
    [Route("auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }
        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await _authService.RegisterAsync(request, cancellationToken);
                return Ok(response);
            }
            catch (RegistrationException exception)
            {
                return BadRequest(new { Errors = exception.Errors });
            }
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
        {
            var response = await _authService.LoginAsync(request, cancellationToken);
            if (response == null)
            {
                return Unauthorized(new { Error = "Invalid email or password." });
            }
            return Ok(response);
        }
    }
}
