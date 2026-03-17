using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using ECTSystem.Persistence.Data;
using ECTSystem.Persistence.Models;

namespace ECTSystem.Tests.Integration;

/// <summary>
/// Rewrites SQL Server–specific DDL and DQL to SQLite-compatible syntax.
/// Handles column types (<c>nvarchar(max)</c> → <c>TEXT</c>), functions
/// (<c>GETUTCDATE()</c> → <c>datetime('now')</c>), and query-level rewrites
/// for <c>GenerateCaseIdAsync</c>.
/// </summary>
internal sealed partial class SqlServerToSqliteInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        RewriteSqlServerSyntax(command);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        RewriteSqlServerSyntax(command);
        return base.NonQueryExecutingAsync(command, eventData, result, ct);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
    {
        RewriteSqlServerSyntax(command);
        return base.ReaderExecutingAsync(command, eventData, result, ct);
    }

    private static void RewriteSqlServerSyntax(DbCommand command)
    {
        var sql = command.CommandText;

        // SQL Server column types → SQLite equivalents (DDL)
        sql = NvarcharMaxRegex().Replace(sql, "TEXT");
        sql = VarcharMaxRegex().Replace(sql, "TEXT");
        sql = VarbinaryMaxRegex().Replace(sql, "BLOB");

        // SQL Server functions → SQLite equivalents
        sql = sql.Replace("GETUTCDATE()", "datetime('now')", StringComparison.OrdinalIgnoreCase);

        // Query-level rewrites (GenerateCaseIdAsync raw SQL)
        sql = sql.Replace("SUBSTRING(", "SUBSTR(", StringComparison.OrdinalIgnoreCase);
        sql = sql.Replace("LEN(", "LENGTH(", StringComparison.OrdinalIgnoreCase);
        sql = sql.Replace(" WITH (UPDLOCK, HOLDLOCK)", "", StringComparison.OrdinalIgnoreCase);
        sql = sql.Replace("AS [Value]", "AS \"Value\"", StringComparison.OrdinalIgnoreCase);
        sql = sql.Replace("@p0 + '%'", "@p0 || '%'", StringComparison.Ordinal);

        command.CommandText = sql;
    }

    [GeneratedRegex(@"\bnvarchar\s*\(\s*max\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex NvarcharMaxRegex();

    [GeneratedRegex(@"\bvarchar\s*\(\s*max\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex VarcharMaxRegex();

    [GeneratedRegex(@"\bvarbinary\s*\(\s*max\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex VarbinaryMaxRegex();
}

/// <summary>
/// Custom <see cref="WebApplicationFactory{TEntryPoint}"/> that replaces the SQL Server
/// databases with in-memory SQLite for fast, isolated integration tests.
/// </summary>
public class EctSystemWebApplicationFactory : WebApplicationFactory<ECTSystem.Api.Program>
{
    private SqliteConnection _ectConnection;
    private SqliteConnection _identityConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations (options, factories, pools,
            // pool policies, context types, etc.).  AddPooledDbContextFactory registers
            // many internal types whose names all contain "DbContext".
            var descriptorsToRemove = services
                .Where(d => d.ServiceType.FullName?.Contains("DbContext") == true)
                .ToList();

            foreach (var d in descriptorsToRemove)
            {
                services.Remove(d);
            }

            // Create persistent in-memory SQLite connections (must stay open)
            _ectConnection = new SqliteConnection("DataSource=:memory:");
            _ectConnection.Open();

            _identityConnection = new SqliteConnection("DataSource=:memory:");
            _identityConnection.Open();

            services.AddDbContextFactory<EctDbContext>(options =>
            {
                options.UseSqlite(_ectConnection);
                options.AddInterceptors(new SqlServerToSqliteInterceptor());
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.AmbientTransactionWarning));
            });

            services.AddDbContextFactory<EctIdentityDbContext>(options =>
            {
                options.UseSqlite(_identityConnection);
            });

            // Also register the contexts directly for Identity's AddEntityFrameworkStores
            services.AddDbContext<EctIdentityDbContext>(options =>
            {
                options.UseSqlite(_identityConnection);
            });

            // Build the service provider and create the schemas
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();

            var ectContext = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>()
                .CreateDbContext();
            ectContext.Database.EnsureCreated();

            var identityContext = scope.ServiceProvider.GetRequiredService<EctIdentityDbContext>();
            identityContext.Database.EnsureCreated();

            // Seed a test user for authentication
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
            if (userManager.FindByEmailAsync("test@ect.mil").GetAwaiter().GetResult() is null)
            {
                var testUser = new ApplicationUser
                {
                    UserName = "test@ect.mil",
                    Email = "test@ect.mil",
                    EmailConfirmed = true
                };
                userManager.CreateAsync(testUser, "Pass123").GetAwaiter().GetResult();
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _ectConnection?.Dispose();
            _identityConnection?.Dispose();
        }
    }
}
