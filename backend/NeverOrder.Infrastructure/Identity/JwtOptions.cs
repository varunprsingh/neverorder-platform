namespace NeverOrder.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Claim type names are kept short and explicit; inbound claim mapping is disabled.</summary>
    public const string RoleClaimType = "role";

    public const string NameClaimType = "name";

    public string Issuer { get; set; } = "NeverOrder";

    public string Audience { get; set; } = "NeverOrderClients";

    /// <summary>Supplied via user-secrets or environment variables. Never committed.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 60;
}
