using Microsoft.Playwright;
using Xunit;

namespace ECTSystem.Tests.E2E;

/// <summary>
/// Shared xUnit fixture that manages Playwright browser lifecycle.
/// A single browser, context, and page are shared across all tests so the
/// Blazor WASM runtime only downloads once and auth state is preserved.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; }
    public IBrowser Browser { get; private set; }
    public IBrowserContext SharedContext { get; private set; }
    public IPage SharedPage { get; private set; }

    public const string BaseUrl = "https://localhost:7240";
    public const string ApiUrl = "https://localhost:7173";

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
        SharedContext = await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
        SharedPage = await SharedContext.NewPageAsync();
    }

    public async Task DisposeAsync()
    {
        if (SharedPage is not null)
            await SharedPage.CloseAsync();

        if (SharedContext is not null)
            await SharedContext.DisposeAsync();

        if (Browser is not null)
            await Browser.DisposeAsync();

        Playwright?.Dispose();
    }
}

[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
}
