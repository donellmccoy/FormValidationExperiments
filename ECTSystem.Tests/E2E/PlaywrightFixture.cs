using Microsoft.Playwright;
using Xunit;

namespace ECTSystem.Tests.E2E;

/// <summary>
/// Shared xUnit fixture that manages Playwright browser lifecycle.
/// A single browser instance is reused across all tests in the collection.
/// Each test gets a fresh <see cref="IBrowserContext"/> for isolation.
/// </summary>
public class PlaywrightFixture : IAsyncLifetime
{
    public IPlaywright Playwright { get; private set; }
    public IBrowser Browser { get; private set; }

    public const string BaseUrl = "https://localhost:7240";
    public const string ApiUrl = "https://localhost:7173";

    public async Task InitializeAsync()
    {
        Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
            await Browser.DisposeAsync();

        Playwright?.Dispose();
    }

    /// <summary>
    /// Creates an isolated browser context with HTTPS errors ignored
    /// (required for localhost self-signed certs).
    /// </summary>
    public async Task<IBrowserContext> CreateContextAsync()
    {
        return await Browser.NewContextAsync(new BrowserNewContextOptions
        {
            IgnoreHTTPSErrors = true
        });
    }
}

[CollectionDefinition("Playwright")]
public class PlaywrightCollection : ICollectionFixture<PlaywrightFixture>
{
}
