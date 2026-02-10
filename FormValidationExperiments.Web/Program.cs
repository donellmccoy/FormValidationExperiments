using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Web;
using FormValidationExperiments.Web.Data;
using FormValidationExperiments.Web.Services;
using Radzen;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddRadzenComponents();

// Entity Framework Core — In-Memory database
builder.Services.AddDbContextFactory<LineOfDutyDbContext>(options =>
    options.UseInMemoryDatabase("LODDatabase"));

// Database service
builder.Services.AddScoped<ILineOfDutyCaseService, LineOfDutyCaseService>();

var host = builder.Build();

// Seed sample data
var contextFactory = host.Services.GetRequiredService<IDbContextFactory<LineOfDutyDbContext>>();
await EctDbSeeder.SeedAsync(contextFactory);

await host.RunAsync();
