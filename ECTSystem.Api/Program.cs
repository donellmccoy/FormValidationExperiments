using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
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
        //using (var scope = app.Services.CreateScope())
        //{
        //    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<EctDbContext>>();
        //    //await using var context = await contextFactory.CreateDbContextAsync();
        //    //await context.Database.MigrateAsync();

        //    //var identityContext = scope.ServiceProvider.GetRequiredService<EctIdentityDbContext>();
        //    //await identityContext.Database.MigrateAsync();

        //    //await EctDbSeeder.SeedAsync(contextFactory);

        //    // Seed a default dev user so the app works immediately after a database reset
        //    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        //    if (await userManager.FindByEmailAsync("admin@ect.mil") is null)
        //    {
        //        var devUser = new ApplicationUser { UserName = "admin@ect.mil", Email = "admin@ect.mil", EmailConfirmed = true };
        //        await userManager.CreateAsync(devUser, "Pass123");
        //    }
        //}

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler(errorApp =>
            {
                errorApp.Run(async context =>
                {
                    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                    var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();

                    if (exceptionFeature?.Error is not null)
                    {
                        logger.LogError(exceptionFeature.Error, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
                    }

                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    context.Response.ContentType = "application/problem+json";

                    await context.Response.WriteAsJsonAsync(new
                    {
                        type = "https://tools.ietf.org/html/rfc7807",
                        title = "An unexpected error occurred",
                        status = 500
                    });
                });
            });
        }

        // Security response headers
        app.Use(async (context, next) =>
        {
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["X-Frame-Options"] = "DENY";
            context.Response.Headers["X-XSS-Protection"] = "0";
            context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-eval'; style-src 'self' 'unsafe-inline'";
            await next();
        });

        app.UseODataBatching();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<OperationCancelledMiddleware>();
        app.UseMiddleware<UnauthorizedAccessMiddleware>();
        app.UseHttpsRedirection();
        app.UseCors("BlazorClient");

        app.UseAuthentication();
        app.UseAuthorization();
        //app.UseRateLimiter();

        // Identity API endpoints: /register, /login, /refresh, /confirmEmail, etc.
        app.MapIdentityApi<ApplicationUser>();

        // Lightweight user-info endpoint for the Blazor WASM client
        app.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new { user.Identity!.Name }))
           .RequireAuthorization();

        app.MapControllers();

        await app.RunAsync();
    }
}
