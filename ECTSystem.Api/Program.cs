using Microsoft.AspNetCore.OData;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.ModelBuilder;
using ECTSystem.Persistence.Data;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;

// OData is the primary API surface — convention routing, Delta<T>.Patch(), and bound actions
// are used throughout. The client excludes navigation/collection/FK properties from PATCH
// bodies via a JsonTypeInfoResolver modifier, so no separate DTO is needed.

var builder = WebApplication.CreateBuilder(args);

// Entity Framework Core — SQL Server
builder.Services.AddDbContextFactory<EctDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EctDatabase")));

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

// OData Entity Data Model
var odataBuilder = new ODataConventionModelBuilder();
odataBuilder.EntitySet<LineOfDutyCase>("Cases");
odataBuilder.EntitySet<Member>("Members");
odataBuilder.EntitySet<Notification>("Notifications");
odataBuilder.EntitySet<LineOfDutyAuthority>("Authorities");
odataBuilder.EntitySet<LineOfDutyDocument>("Documents");
odataBuilder.EntitySet<TimelineStep>("TimelineSteps");
odataBuilder.EntitySet<LineOfDutyAppeal>("Appeals");
odataBuilder.EntitySet<MEDCONDetail>("MEDCONDetails");
odataBuilder.EntitySet<INCAPDetails>("INCAPDetails");

// Delta<LineOfDutyCase> uses the entity type directly — no ComplexType registration needed.

// Controllers + OData
builder.Services.AddControllers()
    .AddOData(options =>
    {
        options.AddRouteComponents("odata", odataBuilder.GetEdmModel())
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
              .AllowAnyMethod();
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
    await EctDbSeeder.SeedAsync(contextFactory);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("BlazorClient");
app.MapControllers();

await app.RunAsync();
