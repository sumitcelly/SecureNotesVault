using Microsoft.AspNetCore.Mvc;
using SecureNotesVault.Core.Services;

namespace SecureNotesVault.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    // POST: api/auth/register
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] UserAuthRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Username and password cannot be empty." });
        }

        var user = await _authService.RegisterAsync(request.Username, request.Password);
        
        if (user == null)
        {
            return BadRequest(new { error = "Username is already taken." });
        }

        // Return a 201 Created status code on successful user account registration
        return StatusCode(StatusCodes.Status201Created, new { 
            message = "User registered successfully.", 
            userId = user.Id 
        });
    }

    // POST: api/auth/login
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] UserAuthRequest request)
    {
        var token = await _authService.LoginAsync(request.Username, request.Password);
        
        if (token == null)
        {
            return Unauthorized(new { error = "Invalid username or password." });
        }

        return Ok(new { token });
    }
}

// Data Transfer Object (DTO) for standard binding validation
public class UserAuthRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
