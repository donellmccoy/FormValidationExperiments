using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ECTSystem.Persistence.Data;

public class EctIdentityDbContextFactory : IDesignTimeDbContextFactory<EctIdentityDbContext>
{
    public EctIdentityDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "ECTSystem.Api");
        var appSettingsPath = Path.Combine(basePath, "appsettings.json");

        using var stream = File.OpenRead(appSettingsPath);
        using var doc = JsonDocument.Parse(stream);

        var connectionString = doc.RootElement
            .GetProperty("ConnectionStrings")
            .GetProperty("EctDatabase")
            .GetString();

        var optionsBuilder = new DbContextOptionsBuilder<EctIdentityDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new EctIdentityDbContext(optionsBuilder.Options);
    }
}
