using BillSplitter.Contracts;
using BillSplitter.Domain;
using BillSplitter.Services;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BillSplitter.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(UserManager<AppUser> users, ITokenService tokens, IValidator<RegisterRequest> registerValidator, IValidator<LoginRequest> loginValidator) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
    // Identity owns password hashing and returns safe validation errors for rejected accounts.
    var validation = await registerValidator.ValidateAsync(request, cancellationToken);
    if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var user = new AppUser { UserName = request.Username, Email = request.Email, DisplayName = request.Username };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(result.Errors.Select(x => x.Description));
        var auth = await tokens.CreateAsync(user, cancellationToken);
        return Ok(new AuthResponse(auth.AccessToken, auth.RefreshToken, auth.ExpiresAtUtc));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return BadRequest(new ValidationProblemDetails(validation.ToDictionary()));
        var user = await users.FindByNameAsync(request.Username) ?? await users.FindByEmailAsync(request.Username);
        if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return Unauthorized("Invalid credentials.");
        var auth = await tokens.CreateAsync(user, cancellationToken);
        return Ok(new AuthResponse(auth.AccessToken, auth.RefreshToken, auth.ExpiresAtUtc));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken cancellationToken)
    {
        // Refreshing is explicit so clients never need to send credentials again after login.
        var auth = await tokens.RefreshAsync(request.RefreshToken, cancellationToken);
        return auth is null ? Unauthorized("Invalid or expired refresh token.") : Ok(new AuthResponse(auth.AccessToken, auth.RefreshToken, auth.ExpiresAtUtc));
    }
}
