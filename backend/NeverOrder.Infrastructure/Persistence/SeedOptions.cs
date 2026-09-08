namespace NeverOrder.Infrastructure.Persistence;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>Development convenience only; leave off outside local runs.</summary>
    public bool Enabled { get; set; }

    public bool ApplyMigrations { get; set; } = true;

    public string AdminEmail { get; set; } = "admin@neverorder.local";

    /// <summary>Must be supplied by configuration (user-secrets / env). No default password is baked in.</summary>
    public string AdminPassword { get; set; } = string.Empty;
}
