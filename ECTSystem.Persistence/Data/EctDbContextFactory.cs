using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ECTSystem.Persistence.Data;

public class EctDbContextFactory : IDesignTimeDbContextFactory<EctDbContext>
{
    public EctDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "ECTSystem.Api");
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString("EctDatabase");

        var optionsBuilder = new DbContextOptionsBuilder<EctDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new EctDbContext(optionsBuilder.Options);
    }
}
