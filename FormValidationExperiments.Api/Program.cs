using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Api.Data;
using FormValidationExperiments.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Entity Framework Core — SQL Server
builder.Services.AddDbContextFactory<EctDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("EctDatabase")));

// Application services
builder.Services.AddScoped<ILineOfDutyCaseService, LineOfDutyCaseService>();

// Controllers
builder.Services.AddControllers()
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

app.Run();
