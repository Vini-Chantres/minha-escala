using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinhaEscala.Infrastructure;

public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("DATABASE_URL");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("Defina DATABASE_URL para executar migrations.");
        var development = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(AppSettings.ParseConnectionString(connection, development)).Options);
    }
}
