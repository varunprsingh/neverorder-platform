using NeverOrder.Domain.Common;

namespace NeverOrder.Application.Auth;

public sealed record RegisterRequest(string Name, string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record AuthenticatedUserDto(Guid Id, string Name, string Email, IReadOnlyList<string> Roles);

public sealed record AuthResultDto(string AccessToken, DateTimeOffset ExpiresAt, AuthenticatedUserDto User);

public sealed class AuthenticationFailedException : DomainException
{
    public AuthenticationFailedException() : base("Invalid email or password.")
    {
    }
}

public sealed class RegistrationFailedException : DomainException
{
    public RegistrationFailedException(IReadOnlyList<string> errors)
        : base("Registration failed.") => Errors = errors;

    public IReadOnlyList<string> Errors { get; }
}

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResultDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
