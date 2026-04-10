using Microsoft.Playwright;
using Xunit;
using Xunit.Abstractions;

namespace ECTSystem.Tests.E2E;

/// <summary>
/// End-to-end Playwright tests covering the LOD case workflow:
/// register, login, dashboard, create case, upload file, documents tab, delete file.
/// <para>
/// Prerequisites:
/// <list type="bullet">
///   <item>API running at https://localhost:7173 (<c>dotnet watch run --project ECTSystem.Api</c>)</item>
///   <item>Web running at https://localhost:7240 (<c>dotnet watch run --project ECTSystem.Web</c>)</item>
///   <item>Playwright browsers installed: <c>pwsh bin\Debug\net10.0\playwright.ps1 install</c></item>
/// </list>
/// Run: <c>dotnet test --filter "Category=E2E"</c>
/// </para>
/// </summary>
[Collection("Playwright")]
[Trait("Category", "E2E")]
public class LodCaseWorkflowTests : IAsyncLifetime
{
    private readonly PlaywrightFixture _fixture;
    private readonly ITestOutputHelper _output;
    private IPage _page;

    // Shared state across ordered tests (static survives across test instances).
    // Because we share a single browser context, auth/localStorage persists.
    private static string _testEmail;
    private static string _testPassword;
    private static bool _registered;
    private static bool _loggedIn;

    public LodCaseWorkflowTests(PlaywrightFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;

        // Unique email per test run to avoid registration conflicts
        _testEmail ??= $"e2e-{DateTime.UtcNow:yyyyMMddHHmmss}@ect.mil";
        _testPassword ??= "Test123!";
    }

    public Task InitializeAsync()
    {
        // Reuse the shared page — WASM only downloads once, auth persists
        _page = _fixture.SharedPage;
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        // Don't close the shared page — it persists across tests
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────
    // 1. Register a new account
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T01_Register_CreatesAccount()
    {
        await _page.GotoAsync($"{PlaywrightFixture.BaseUrl}/register");
        // First navigation downloads entire Blazor WASM runtime — needs generous timeout
        await _page.WaitForSelectorAsync(".register-card", new PageWaitForSelectorOptions { Timeout = 60_000 });

        await _page.FillAsync("input[name='Email']", _testEmail);
        await _page.FillAsync("input[name='Password']", _testPassword);
        await _page.FillAsync("input[name='ConfirmPassword']", _testPassword);

        await _page.ClickAsync("button:has-text('Create Account')");

        // Registration triggers: Register API → auto-Login API → NavigateTo("/", forceLoad: true)
        // forceLoad causes full browser navigation and WASM reload — wait for URL to leave /register first
        await _page.WaitForURLAsync(
            url => !url.Contains("/register"),
            new PageWaitForURLOptions { Timeout = 60_000 });

        // Then wait for dashboard content to render after WASM reload
        await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 60_000 });
        _output.WriteLine($"Registration succeeded for {_testEmail}, redirected to dashboard");
        _registered = true;
        _loggedIn = true;
    }

    // ─────────────────────────────────────────────────────────
    // 2. Login
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T02_Login_AuthenticatesAndRedirectsToDashboard()
    {
        if (!_registered)
            await RegisterAsync();

        await _page.GotoAsync($"{PlaywrightFixture.BaseUrl}/login");
        await _page.WaitForSelectorAsync(".login-card", new PageWaitForSelectorOptions { Timeout = 60_000 });

        await _page.FillAsync("input[name='Username']", _testEmail);
        await _page.FillAsync("input[name='Password']", _testPassword);

        await _page.ClickAsync("button:has-text('Login')");

        // Dashboard should load after login (forceLoad: true triggers full page reload)
        await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 60_000 });

        _output.WriteLine("Login succeeded, dashboard visible");
        _loggedIn = true;
    }

    // ─────────────────────────────────────────────────────────
    // 3. Dashboard loads with stats cards
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T03_Dashboard_DisplaysStatsCards()
    {
        await EnsureLoggedInAsync();

        await _page.GotoAsync(PlaywrightFixture.BaseUrl);
        await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 10_000 });

        var cards = await _page.QuerySelectorAllAsync(".dashboard-card");
        Assert.True(cards.Count >= 4, $"Expected at least 4 dashboard cards, found {cards.Count}");

        // Verify Add New button exists
        var addNewButton = _page.GetByText("Add New", new PageGetByTextOptions { Exact = false });
        await Assertions.Expect(addNewButton).ToBeVisibleAsync();

        _output.WriteLine($"Dashboard loaded with {cards.Count} stat cards");
    }

    // ─────────────────────────────────────────────────────────
    // 4. Create a new LOD case
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T04_CreateCase_NavigatesToEditCasePage()
    {
        await EnsureLoggedInAsync();

        await _page.GotoAsync(PlaywrightFixture.BaseUrl);
        await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 10_000 });

        // Click "Add New" button on dashboard
        await _page.ClickAsync("button:has-text('Add New')");

        // Should navigate to /case/new or /case/{id} in edit mode
        await _page.WaitForURLAsync("**/case/**", new PageWaitForURLOptions { Timeout = 30_000 });

        // Wait for the tab structure to render
        await _page.WaitForSelectorAsync(".rz-tabview", new PageWaitForSelectorOptions { Timeout = 30_000 });

        // The first tab "Member Information" should be visible — scope to tab nav to avoid
        // strict mode violation (text also appears in panel headings and sidebar)
        var memberTab = _page.Locator(".rz-tabview-nav").GetByText("Member Information");
        await Assertions.Expect(memberTab).ToBeVisibleAsync();

        _output.WriteLine($"Case created, navigated to: {_page.Url}");
    }

    // ─────────────────────────────────────────────────────────
    // 5. Upload a file via the toolbar attach button
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T05_UploadFile_BadgeCountIncrements()
    {
        await EnsureOnEditCasePageAsync();

        // Note the current document badge count
        var badgeBefore = await GetDocumentBadgeCountAsync();
        _output.WriteLine($"Document badge count before upload: {badgeBefore}");

        // The hidden InputFile is triggered by the attach button via JS
        // Use Playwright's SetInputFiles on the hidden input
        var fileInput = _page.Locator("#toolbar-file-input");
        await fileInput.SetInputFilesAsync(new FilePayload
        {
            Name = "e2e-test-document.pdf",
            MimeType = "application/pdf",
            Buffer = GenerateTestPdfBytes()
        });

        // Wait for upload to complete — OnAttachFilesSelected increments _documentsCount,
        // which causes the badge to render in the Documents tab header
        await _page.Locator(".rz-tabview-nav .rz-badge").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 30_000 });

        var badgeAfter = await GetDocumentBadgeCountAsync();
        _output.WriteLine($"Document badge count after upload: {badgeAfter}");

        Assert.True(badgeAfter > badgeBefore,
            $"Expected badge count to increase from {badgeBefore}, but got {badgeAfter}");
    }

    // ─────────────────────────────────────────────────────────
    // 6. Navigate to Documents tab and verify file appears
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T06_DocumentsTab_ShowsUploadedFile()
    {
        await EnsureOnEditCasePageAsync();

        // Click the Documents tab
        await ClickTabAsync("Documents");

        // Wait for the documents grid to load data and render file links
        // (skip generic .rz-datatable wait — it matches multiple grids in strict mode)
        var fileLink = _page.Locator(".doc-file-link");
        await fileLink.First.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        var count = await fileLink.CountAsync();

        _output.WriteLine($"Documents in grid: {count}");
        Assert.True(count >= 1, "Expected at least one document in the grid");
    }

    // ─────────────────────────────────────────────────────────
    // 7. Delete a file and verify confirmation dialog
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T07_DeleteFile_ShowsConfirmationDialog()
    {
        await EnsureOnEditCasePageAsync();
        await ClickTabAsync("Documents");

        // Wait for grid to have at least one row (use Locator to avoid strict mode)
        await _page.Locator(".doc-file-link").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        // Click the last delete button in the documents grid (most recent file)
        var deleteButtons = _page.Locator(".doc-actions button").Last;
        await deleteButtons.ClickAsync();

        // Radzen Confirm dialog should appear
        var dialog = _page.Locator(".rz-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        // Verify dialog title
        var dialogTitle = dialog.Locator(".rz-dialog-title");
        await Assertions.Expect(dialogTitle).ToContainTextAsync("Delete Document");

        // Click Cancel to dismiss without deleting
        await dialog.Locator("button:has-text('Cancel')").ClickAsync();

        // Dialog should close
        await Assertions.Expect(dialog).Not.ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        _output.WriteLine("Delete confirmation dialog appeared and was cancelled");
    }

    // ─────────────────────────────────────────────────────────
    // 8. Delete a file and confirm — verify badge and grid update
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T08_DeleteFile_ConfirmRemovesFileFromGrid()
    {
        await EnsureOnEditCasePageAsync();
        await ClickTabAsync("Documents");

        // Wait for at least one document (use Locator to avoid strict mode)
        await _page.Locator(".doc-file-link").First.WaitForAsync(
            new LocatorWaitForOptions { Timeout = 15_000 });

        var countBefore = await _page.Locator(".doc-file-link").CountAsync();
        var badgeBefore = await GetDocumentBadgeCountAsync();

        _output.WriteLine($"Documents before delete: {countBefore}, badge: {badgeBefore}");

        // Click the last delete button (most recent file)
        var deleteButton = _page.Locator(".doc-actions button").Last;
        await deleteButton.ClickAsync();

        // Confirm deletion in the dialog
        var dialog = _page.Locator(".rz-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });
        await dialog.Locator("button:has-text('Delete')").ClickAsync();

        // Wait for grid to refresh
        await _page.WaitForTimeoutAsync(3_000);

        var countAfter = await _page.Locator(".doc-file-link").CountAsync();
        var badgeAfter = await GetDocumentBadgeCountAsync();

        _output.WriteLine($"Documents after delete: {countAfter}, badge: {badgeAfter}");

        Assert.True(countAfter < countBefore,
            $"Expected document count to decrease from {countBefore}, got {countAfter}");
        Assert.True(badgeAfter < badgeBefore,
            $"Expected badge to decrease from {badgeBefore}, got {badgeAfter}");
    }

    // ─────────────────────────────────────────────────────────
    // 9. Navigate between tabs
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T09_TabNavigation_SwitchesBetweenFormSections()
    {
        await EnsureOnEditCasePageAsync();

        // Navigate to Medical Technician tab (index 1) — may be disabled for new cases
        // Instead, test non-disabled tabs: Member Information, Case Dialogue, Documents
        await ClickTabAsync("Member Information");
        // Member Information tab has the member search box with an aria-label
        var memberContent = _page.Locator("[aria-label='Search members']");
        await Assertions.Expect(memberContent).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        await ClickTabAsync("Documents");
        // Documents tab has a search box for filtering documents
        var docContent = _page.Locator("[placeholder='Search documents...']");
        await Assertions.Expect(docContent).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        _output.WriteLine("Tab navigation works between Member Information and Documents");
    }

    // ─────────────────────────────────────────────────────────
    // 10. Case list page loads
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T10_CaseList_LoadsAndDisplaysGrid()
    {
        await EnsureLoggedInAsync();

        await _page.GotoAsync($"{PlaywrightFixture.BaseUrl}/cases");

        // Wait for the search box or grid to appear
        await _page.WaitForSelectorAsync(
            "input[placeholder='Search cases...'], .rz-datatable",
            new PageWaitForSelectorOptions { Timeout = 10_000 });

        // The "Create Case" button should be present
        var createButton = _page.GetByText("Create Case", new PageGetByTextOptions { Exact = false });
        await Assertions.Expect(createButton).ToBeVisibleAsync();

        _output.WriteLine("Case list page loaded successfully");
    }

    // ═══════════════════════════════════════════════════════════
    //  Helper methods
    // ═══════════════════════════════════════════════════════════

    private async Task RegisterAsync()
    {
        await _page.GotoAsync($"{PlaywrightFixture.BaseUrl}/register");
        await _page.WaitForSelectorAsync(".register-card", new PageWaitForSelectorOptions { Timeout = 60_000 });
        await _page.FillAsync("input[name='Email']", _testEmail);
        await _page.FillAsync("input[name='Password']", _testPassword);
        await _page.FillAsync("input[name='ConfirmPassword']", _testPassword);
        await _page.ClickAsync("button:has-text('Create Account')");
        // forceLoad: true causes full browser navigation and WASM reload
        await _page.WaitForURLAsync(
            url => !url.Contains("/register"),
            new PageWaitForURLOptions { Timeout = 60_000 });
        await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 60_000 });
        _registered = true;
        _loggedIn = true;
    }

    private async Task LoginAsync()
    {
        if (!_registered)
        {
            await RegisterAsync(); // RegisterAsync auto-logs in
            return;
        }

        await _page.GotoAsync($"{PlaywrightFixture.BaseUrl}/login");
        await _page.WaitForSelectorAsync(".login-card", new PageWaitForSelectorOptions { Timeout = 60_000 });
        await _page.FillAsync("input[name='Username']", _testEmail);
        await _page.FillAsync("input[name='Password']", _testPassword);
        await _page.ClickAsync("button:has-text('Login')");
        // forceLoad: true triggers full browser navigation and WASM reload
        await _page.WaitForURLAsync(
            url => !url.Contains("/login"),
            new PageWaitForURLOptions { Timeout = 60_000 });
        await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 60_000 });
        _loggedIn = true;
    }

    private async Task EnsureLoggedInAsync()
    {
        if (!_loggedIn)
            await LoginAsync();
    }

    private async Task EnsureOnEditCasePageAsync()
    {
        await EnsureLoggedInAsync();

        // If not already on an edit case page, create a new case
        if (!_page.Url.Contains("/case/"))
        {
            await _page.GotoAsync(PlaywrightFixture.BaseUrl);
            await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 30_000 });
            await _page.ClickAsync("button:has-text('Add New')");
            await _page.WaitForURLAsync("**/case/**", new PageWaitForURLOptions { Timeout = 30_000 });
            await _page.WaitForSelectorAsync(".rz-tabview", new PageWaitForSelectorOptions { Timeout = 30_000 });
        }
    }

    private async Task ClickTabAsync(string tabText)
    {
        var tab = _page.Locator($".rz-tabview-nav >> text='{tabText}'");
        await tab.ClickAsync();
        await _page.WaitForTimeoutAsync(500); // brief settle time for tab content
    }

    private async Task<int> GetDocumentBadgeCountAsync()
    {
        // The document badge is inside the Documents tab label
        var badge = _page.Locator(".rz-tabview-nav >> text='Documents' >> .. >> .rz-badge");
        var exists = await badge.CountAsync() > 0;

        if (!exists)
            return 0;

        var text = await badge.TextContentAsync();
        return int.TryParse(text?.Trim(), out var count) ? count : 0;
    }

    /// <summary>
    /// Generates minimal valid PDF bytes for upload testing.
    /// </summary>
    private static byte[] GenerateTestPdfBytes()
    {
        var pdfContent = "%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n" +
                         "2 0 obj<</Type/Pages/Count 1/Kids[3 0 R]>>endobj\n" +
                         "3 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 612 792]>>endobj\n" +
                         "xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n" +
                         "0000000058 00000 n \n0000000110 00000 n \n" +
                         "trailer<</Size 4/Root 1 0 R>>\nstartxref\n174\n%%EOF";
        return System.Text.Encoding.ASCII.GetBytes(pdfContent);
    }
}
