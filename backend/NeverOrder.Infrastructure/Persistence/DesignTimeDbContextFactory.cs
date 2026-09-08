using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace NeverOrder.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` build the model without booting the API host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NeverOrderDbContext>
{
    public NeverOrderDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? "Host=localhost;Port=5432;Database=neverorder;Username=neverorder;Password=neverorder_dev_pw";

        var options = new DbContextOptionsBuilder<NeverOrderDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new NeverOrderDbContext(options);
    }
}
