using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.ModelBuilder;
using ECTSystem.Persistence.Data;
using ECTSystem.Persistence.Models;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;

// OData is the primary API surface — convention routing, Delta<T>.Patch(), and bound actions
// are used throughout. The client excludes navigation/collection/FK properties from PATCH
// bodies via a JsonTypeInfoResolver modifier, so no separate DTO is needed.

var builder = WebApplication.CreateBuilder(args);

// Entity Framework Core — SQL Server
var connectionString = builder.Configuration.GetConnectionString("EctDatabase");

builder.Services.AddDbContextFactory<EctDbContext>(options =>
    options.UseSqlServer(connectionString));

// Dedicated Identity DbContext — shares the same database but keeps Identity
// concerns separate from the application domain model.
builder.Services.AddDbContext<EctIdentityDbContext>(options =>
    options.UseSqlServer(connectionString));

// ASP.NET Core Identity with Bearer token authentication
builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireDigit = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
    .AddEntityFrameworkStores<EctIdentityDbContext>();

builder.Services.AddAuthorization();

// Logging
builder.Services.AddSingleton<IApiLogService, ApiLogService>();

// Application services
builder.Services.AddScoped<DataService>();
builder.Services.AddScoped<IDataService>(sp => sp.GetRequiredService<DataService>());
builder.Services.AddScoped<ILineOfDutyDocumentService>(sp => sp.GetRequiredService<DataService>());
builder.Services.AddScoped<ILineOfDutyAppealService>(sp => sp.GetRequiredService<DataService>());
builder.Services.AddScoped<ILineOfDutyAuthorityService>(sp => sp.GetRequiredService<DataService>());
builder.Services.AddScoped<ILineOfDutyTimelineService>(sp => sp.GetRequiredService<DataService>());
builder.Services.AddScoped<ILineOfDutyNotificationService>(sp => sp.GetRequiredService<DataService>());
builder.Services.AddScoped<ICaseBookmarkService>(sp => sp.GetRequiredService<DataService>());

// OData Entity Data Model
var odataBuilder = new ODataConventionModelBuilder();
var casesEntitySet = odataBuilder.EntitySet<LineOfDutyCase>("Cases");
casesEntitySet.EntityType.Collection.Function("Bookmarked").ReturnsCollectionFromEntitySet<LineOfDutyCase>("Cases");
odataBuilder.EntitySet<Member>("Members");
odataBuilder.EntitySet<Notification>("Notifications");
odataBuilder.EntitySet<LineOfDutyAuthority>("Authorities");
odataBuilder.EntitySet<LineOfDutyDocument>("Documents");
odataBuilder.EntitySet<TimelineStep>("TimelineSteps");
odataBuilder.EntitySet<LineOfDutyAppeal>("Appeals");
odataBuilder.EntitySet<MEDCONDetail>("MEDCONDetails");
odataBuilder.EntitySet<INCAPDetails>("INCAPDetails");
odataBuilder.EntitySet<CaseBookmark>("CaseBookmarks");
var edmModel = odataBuilder.GetEdmModel();
builder.Services.AddSingleton<Microsoft.OData.Edm.IEdmModel>(edmModel);

// Delta<LineOfDutyCase> uses the entity type directly — no ComplexType registration needed.

// Controllers + OData
builder.Services.AddControllers()
    .AddOData(options =>
    {
        options.AddRouteComponents("odata", edmModel)
               .Select()
               .Filter()
               .Expand()
               .OrderBy()
               .SetMaxTop(100)
               .Count();


    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// CORS — allow the Blazor WASM client
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
    {
        policy.WithOrigins("https://localhost:7240", "http://localhost:5101")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddOpenApi();

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

app.UseHttpsRedirection();
app.UseCors("BlazorClient");

app.UseAuthentication();
app.UseAuthorization();

// Identity API endpoints: /register, /login, /refresh, /confirmEmail, etc.
app.MapIdentityApi<ApplicationUser>();

// Lightweight user-info endpoint for the Blazor WASM client
app.MapGet("/me", (System.Security.Claims.ClaimsPrincipal user) =>
    Results.Ok(new { user.Identity!.Name }))
    .RequireAuthorization();

app.MapControllers();

await app.RunAsync();
