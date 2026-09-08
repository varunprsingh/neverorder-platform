using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NeverOrder.Application.Abstractions;
using NeverOrder.Application.Auth;

namespace NeverOrder.Infrastructure.Identity;

public sealed class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _tokens;
    private readonly IClock _clock;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator tokens,
        IClock clock,
        ILogger<AuthService> logger)
    {
        _userManager = userManager;
        _tokens = tokens;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthResultDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            DisplayName = request.Name.Trim(),
            CreatedAt = _clock.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            _logger.LogInformation("Registration rejected for {Email}", email);
            throw new RegistrationFailedException(result.Errors.Select(e => e.Description).ToList());
        }

        await _userManager.AddToRoleAsync(user, Roles.User);

        _logger.LogInformation("User {UserId} registered", user.Id);

        return await BuildResultAsync(user);
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email);

        // Same failure for unknown email and wrong password, so accounts cannot be enumerated.
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            _logger.LogWarning("Failed login attempt for {Email}", email);
            throw new AuthenticationFailedException();
        }

        _logger.LogInformation("User {UserId} signed in", user.Id);

        return await BuildResultAsync(user);
    }

    private async Task<AuthResultDto> BuildResultAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokens.Create(user.Id, user.Email!, user.DisplayName, roles);

        return new AuthResultDto(
            token,
            expiresAt,
            new AuthenticatedUserDto(user.Id, user.DisplayName, user.Email!, roles.ToList()));
    }
}
