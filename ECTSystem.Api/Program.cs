using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;
using ECTSystem.Persistence.Models;
using ECTSystem.Api.Extensions;
using ECTSystem.Api.Middleware;
using System.Security.Claims;

namespace ECTSystem.Api;

// OData is the primary API surface — convention routing, Delta<T>.Patch(), and bound actions
// are used throughout. The client excludes navigation/collection/FK properties from PATCH
// bodies via a JsonTypeInfoResolver modifier, so no separate DTO is needed.

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddApplicationServices(builder.Configuration);

        var app = builder.Build();

        // Apply migrations and seed data
        using (var scope = app.Services.CreateScope())
        {
            var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
            await using var context = await contextFactory.CreateDbContextAsync();
            await context.Database.MigrateAsync();

            var identityContext = scope.ServiceProvider.GetRequiredService<EctIdentityDbContext>();
            await identityContext.Database.MigrateAsync();

            await EctDbSeeder.SeedAsync(contextFactory);

            // Seed a default dev user so the app works immediately after a database reset
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByEmailAsync("admin@ect.mil") is null)
            {
                var devUser = new ApplicationUser { UserName = "admin@ect.mil", Email = "admin@ect.mil", EmailConfirmed = true };
                await userManager.CreateAsync(devUser, "Pass123");
            }
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseHttpsRedirection();
        app.UseCors("BlazorClient");

        app.UseAuthentication();
        app.UseAuthorization();

        // Identity API endpoints: /register, /login, /refresh, /confirmEmail, etc.
        app.MapIdentityApi<ApplicationUser>();

        // Lightweight user-info endpoint for the Blazor WASM client
        app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new { user.Identity!.Name }))
           .RequireAuthorization();

        app.MapControllers();

        await app.RunAsync();
    }
}
