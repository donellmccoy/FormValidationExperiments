using Microsoft.Playwright;
using Xunit;

namespace ECTSystem.Tests.E2E;

[Trait("Category", "Diagnostic")]
public class DiagnosticTest
{
    private readonly ITestOutputHelper _output;

    public DiagnosticTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task Diagnose_BlazorWasmLoad()
    {
        using var pw = await Playwright.CreateAsync();
        await using var browser = await pw.Chromium.LaunchAsync(new() { Headless = true });
        var ctx = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true });
        var page = await ctx.NewPageAsync();

        page.Console += (_, msg) => _output.WriteLine($"[CONSOLE {msg.Type}] {msg.Text}");
        page.PageError += (_, err) => _output.WriteLine($"[PAGE ERROR] {err}");

        _output.WriteLine("Navigating to login...");
        var resp = await page.GotoAsync("https://localhost:7240/login", new() { Timeout = 30_000 });
        _output.WriteLine($"HTTP Status: {resp?.Status}");

        // Wait for network to settle
        _output.WriteLine("Waiting for network idle...");
        try
        {
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 60_000 });
            _output.WriteLine("Network idle reached");
        }
        catch (TimeoutException)
        {
            _output.WriteLine("Network idle timeout after 60s");
        }

        // Dump the app div content
        var appHtml = await page.InnerHTMLAsync("#app");
        _output.WriteLine($"#app content ({appHtml.Length} chars):");
        _output.WriteLine(appHtml.Length > 3000 ? appHtml[..3000] : appHtml);

        // Check for error ui
        var errorVisible = await page.Locator("#blazor-error-ui").IsVisibleAsync();
        _output.WriteLine($"Blazor error UI visible: {errorVisible}");

        // Check page title
        var title = await page.TitleAsync();
        _output.WriteLine($"Page title: {title}");
    }
}
