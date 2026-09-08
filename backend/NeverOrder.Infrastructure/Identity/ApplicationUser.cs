using Microsoft.AspNetCore.Identity;

namespace NeverOrder.Infrastructure.Identity;

/// <summary>
/// Lives in Infrastructure, not Domain, so the domain model stays free of ASP.NET Identity.
/// Domain aggregates reference the user only by <see cref="Guid"/>.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }
}

public static class Roles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = new[] { User, Admin };
}
