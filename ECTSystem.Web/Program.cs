using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using ECTSystem.Web;
using ECTSystem.Web.Extensions;

namespace ECTSystem.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

        builder.Services.AddApplicationServices(builder.Configuration);

        var host = builder.Build();
        await host.RunAsync();
    }
}
