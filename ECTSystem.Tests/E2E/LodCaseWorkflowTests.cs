using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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
[TestCaseOrderer("ECTSystem.Tests.E2E.AlphabeticalOrderer", "ECTSystem.Tests")]
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
    private static bool _caseSaved;
    private static string _savedCaseId;

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
        await EnsureCaseSavedAsync();

        // Note the current document badge count
        var badgeBefore = await GetDocumentBadgeCountAsync();
        _output.WriteLine($"Document badge count before upload: {badgeBefore}");

        // Use RunAndWaitForFileChooserAsync to click the toolbar "Attach file" button —
        // this triggers JS triggerFileInput() which clicks the hidden InputFile, opening
        // the native file dialog that Playwright intercepts. This ensures Blazor's
        // InputFile OnChange handler fires correctly.
        var uploadResponse = _page.WaitForResponseAsync(
            resp => resp.Url.Contains("/odata/Cases") && resp.Url.Contains("/Documents") && resp.Request.Method == "POST",
            new PageWaitForResponseOptions { Timeout = 30_000 });

        var fileChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
        {
            await _page.Locator("[title='Attach file']").ClickAsync();
        });
        await fileChooser.SetFilesAsync(new FilePayload
        {
            Name = "e2e-test-document.pdf",
            MimeType = "application/pdf",
            Buffer = GenerateTestPdfBytes()
        });

        // Wait for the upload POST to complete
        var response = await uploadResponse;
        _output.WriteLine($"Upload response status: {response.Status}");

        // Wait for badge to update after successful upload
        await _page.WaitForTimeoutAsync(1_000);

        var badgeAfter = await GetDocumentBadgeCountAsync();
        _output.WriteLine($"Document badge count after upload: {badgeAfter}");

        Assert.True(badgeAfter > badgeBefore,
            $"Expected badge count to increase from {badgeBefore}, but got {badgeAfter}");
    }

    // ─────────────────────────────────────────────────────────
    // 5b. Upload via RadzenUpload shows progress indicator
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T05b_UploadFile_ShowsProgressIndicator()
    {
        await EnsureCaseSavedAsync();

        // Switch to Documents tab where RadzenUpload lives
        await ClickTabAsync("Documents");

        // Throttle upload speed via Chrome DevTools Protocol so the ~2 MB
        // test file takes several seconds to send. This ensures the browser
        // fires xhr.upload.onprogress events — route interception suppresses
        // them because the browser never actually sends data over the wire.
        var cdpSession = await _page.Context.NewCDPSessionAsync(_page);
        await cdpSession.SendAsync("Network.enable");
        await cdpSession.SendAsync("Network.emulateNetworkConditions", new Dictionary<string, object>
        {
            ["offline"] = false,
            ["latency"] = 0,
            ["downloadThroughput"] = -1,
            ["uploadThroughput"] = 300 * 1024, // 300 KB/s → ~7 s for 2 MB
        });

        try
        {
            // Use RunAndWaitForFileChooserAsync to click RadzenUpload's "Choose Files"
            // button — this triggers the native file dialog which Playwright intercepts,
            // ensuring Radzen's JS upload handler fires correctly.
            var fileChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
            {
                await _page.GetByRole(AriaRole.Button, new() { Name = "Choose Files" }).ClickAsync();
            });
            // Use a large (~2 MB) file so the browser fires xhr.upload.onprogress
            // at least once. Small files send in a single TCP segment and the
            // progress event never fires, leaving IsUploading = false.
            await fileChooser.SetFilesAsync(new FilePayload
            {
                Name = "progress-test.pdf",
                MimeType = "application/pdf",
                Buffer = GenerateLargeTestPdfBytes()
            });

            // The upload-progress-bar container should become visible
            var progressBar = _page.Locator(".upload-progress-bar");
            await Assertions.Expect(progressBar).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 10_000 });

            // Verify "Uploading" text appears
            await Assertions.Expect(progressBar).ToContainTextAsync("Uploading",
                new LocatorAssertionsToContainTextOptions { Timeout = 5_000 });

            // Verify percentage text appears (e.g. "0%" or "50%" or "100%")
            await Assertions.Expect(progressBar).ToContainTextAsync("%",
                new LocatorAssertionsToContainTextOptions { Timeout = 5_000 });

            // Verify the RadzenProgressBar element exists inside the container
            var progressElement = progressBar.Locator(".rz-progressbar");
            await Assertions.Expect(progressElement).ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

            _output.WriteLine("Upload progress indicator appeared during file upload");

            // Restore full speed so upload finishes quickly
            await cdpSession.SendAsync("Network.emulateNetworkConditions", new Dictionary<string, object>
            {
                ["offline"] = false,
                ["latency"] = 0,
                ["downloadThroughput"] = -1,
                ["uploadThroughput"] = -1,
            });

            // Wait for upload to complete — progress bar should disappear
            await Assertions.Expect(progressBar).Not.ToBeVisibleAsync(
                new LocatorAssertionsToBeVisibleOptions { Timeout = 30_000 });

            _output.WriteLine("Upload progress indicator disappeared after upload completed");
        }
        finally
        {
            // Ensure network throttling is removed even on failure
            await cdpSession.SendAsync("Network.emulateNetworkConditions", new Dictionary<string, object>
            {
                ["offline"] = false,
                ["latency"] = 0,
                ["downloadThroughput"] = -1,
                ["uploadThroughput"] = -1,
            });
        }
    }

    // ─────────────────────────────────────────────────────────
    // 6. Navigate to Documents tab and verify file appears
    // ─────────────────────────────────────────────────────────

    [Fact]
    public async Task T06_DocumentsTab_ShowsUploadedFile()
    {
        await EnsureCaseSavedAsync();

        // Force grid reload by switching away then back — clicking the same
        // tab does NOT trigger OnTabIndexChanged, so the grid won't refresh.
        await ClickTabAsync("Member Information");
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
        await EnsureCaseSavedAsync();

        // Force grid reload by switching away then back
        await ClickTabAsync("Member Information");
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
        await EnsureCaseSavedAsync();

        // Badge tracks _documentsCount which is accurate after T05's += 1
        var badgeBefore = await GetDocumentBadgeCountAsync();

        // Force grid to sync with server by switching tabs. The toolbar
        // upload in T05 increments the badge but does NOT call
        // _documentsGrid.Reload(), so the grid may show stale data.
        // Switching away then back triggers OnTabIndexChanged which reloads.
        await ClickTabAsync("Member Information");
        await ClickTabAsync("Documents");

        // Wait for grid to fully reload and show all server documents
        await Assertions.Expect(_page.Locator(".doc-file-link")).ToHaveCountAsync(
            badgeBefore, new LocatorAssertionsToHaveCountOptions { Timeout = 15_000 });

        var countBefore = badgeBefore;

        _output.WriteLine($"Documents before delete: {countBefore}, badge: {badgeBefore}");

        // Click the last delete button (most recent file)
        var deleteButton = _page.Locator(".doc-actions button").Last;
        await deleteButton.ClickAsync();

        // Confirm deletion in the dialog
        var dialog = _page.Locator(".rz-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync(new LocatorAssertionsToBeVisibleOptions { Timeout = 5_000 });

        // Wait for the delete API response after clicking Delete
        var deleteResponse = _page.WaitForResponseAsync(
            resp => resp.Url.Contains("/odata/") && resp.Url.Contains("Documents") && resp.Request.Method == "DELETE",
            new PageWaitForResponseOptions { Timeout = 15_000 });

        await dialog.Locator("button:has-text('Delete')").ClickAsync();

        // Wait for delete API call to complete
        var response = await deleteResponse;
        _output.WriteLine($"Delete response status: {response.Status}");

        // Wait for grid to refresh — the count should decrease
        await Assertions.Expect(_page.Locator(".doc-file-link")).ToHaveCountAsync(
            countBefore - 1, new LocatorAssertionsToHaveCountOptions { Timeout = 10_000 });

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
        await EnsureCaseSavedAsync();

        // Navigate to Medical Technician tab (index 1) — may be disabled for new cases
        // Instead, test non-disabled tabs: Member Information, Case Dialogue, Documents
        await ClickTabAsync("Member Information");
        // After saving, member search box is hidden; verify member form fields are visible instead
        var memberContent = _page.Locator("input[placeholder='___-__-____']");
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

        // If we have a saved case and we're not on its edit page, navigate there
        if (_savedCaseId is not null && !_page.Url.Contains($"/case/{_savedCaseId}"))
        {
            await _page.GotoAsync($"{PlaywrightFixture.BaseUrl}/case/{_savedCaseId}");
            await _page.WaitForSelectorAsync(".rz-tabview", new PageWaitForSelectorOptions { Timeout = 30_000 });
            return;
        }

        // Fallback: create a new case via dashboard
        if (!_page.Url.Contains("/case/"))
        {
            await _page.GotoAsync(PlaywrightFixture.BaseUrl);
            await _page.WaitForSelectorAsync(".dashboard-card", new PageWaitForSelectorOptions { Timeout = 30_000 });
            await _page.ClickAsync("button:has-text('Add New')");
            await _page.WaitForURLAsync("**/case/**", new PageWaitForURLOptions { Timeout = 30_000 });
            await _page.WaitForSelectorAsync(".rz-tabview", new PageWaitForSelectorOptions { Timeout = 30_000 });
        }
    }

    /// <summary>
    /// Ensures a saved case exists so upload controls are enabled (Id &gt; 0).
    /// Creates the case via OData API POST and navigates to its edit page.
    /// </summary>
    private async Task EnsureCaseSavedAsync()
    {
        await EnsureLoggedInAsync();

        if (_caseSaved)
        {
            await EnsureOnEditCasePageAsync();
            return;
        }

        // Create a case via the OData API directly — bypasses Radzen popup timing issues
        using var httpClient = new HttpClient(new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        });

        // Authenticate with the API to get a bearer token
        var loginPayload = new { email = _testEmail, password = _testPassword };
        var loginResponse = await httpClient.PostAsJsonAsync(
            $"{PlaywrightFixture.ApiUrl}/login", loginPayload);
        loginResponse.EnsureSuccessStatusCode();
        var loginBody = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = loginBody.GetProperty("accessToken").GetString();
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        _output.WriteLine("Authenticated with API");

        // First, create a Member so the FK is satisfied
        var memberPayload = new
        {
            FirstName = "Jane",
            MiddleInitial = "A",
            LastName = "Doe",
            Rank = "SSgt",
            ServiceNumber = "999-99-9999",
            Unit = "99 TW/CC",
            Component = "AirForceReserve",
            DateOfBirth = new DateTime(1990, 1, 15, 0, 0, 0, DateTimeKind.Utc)
        };

        var memberResponse = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post,
            $"{PlaywrightFixture.ApiUrl}/odata/Members")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(memberPayload),
                Encoding.UTF8,
                "application/json")
        });

        if (!memberResponse.IsSuccessStatusCode)
        {
            var memberError = await memberResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"Member POST failed ({memberResponse.StatusCode}): {memberError}");
        }

        memberResponse.EnsureSuccessStatusCode();
        var memberBody = await memberResponse.Content.ReadFromJsonAsync<JsonElement>();
        var memberId = memberBody.GetProperty("Id").GetInt32();
        _output.WriteLine($"Created member via API: Id={memberId}");

        // Now create the case with the valid MemberId
        var payload = new
        {
            MemberName = "Doe, Jane A",
            MemberRank = "SSgt",
            MemberId = memberId,
            ProcessType = "Informal",
            Component = "AirForceReserve",
            IncidentType = "Injury",
            IncidentDutyStatus = "Title10ActiveDuty",
            IncidentDate = DateTime.UtcNow.AddDays(-5),
            InitiationDate = DateTime.UtcNow,
            IncidentDescription = "Training injury during PT — E2E test"
        };

        var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Post,
            $"{PlaywrightFixture.ApiUrl}/odata/Cases")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json")
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _output.WriteLine($"API POST failed ({response.StatusCode}): {errorBody}");
        }

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var caseId = body.GetProperty("CaseId").GetString();
        _output.WriteLine($"Created case via API: CaseId={caseId}, Id={body.GetProperty("Id").GetInt32()}");

        // Navigate to the saved case's edit page
        await _page.GotoAsync($"{PlaywrightFixture.BaseUrl}/case/{caseId}");

        // Wait for the edit page to finish loading (busy overlay disappears, tabs render)
        await _page.WaitForSelectorAsync(".rz-tabview", new PageWaitForSelectorOptions { Timeout = 30_000 });

        // Verify the toolbar attach button is enabled (case has Id > 0)
        await _page.Locator("[title='Attach file']").WaitForAsync(
            new LocatorWaitForOptions { Timeout = 10_000 });

        _caseSaved = true;
        _savedCaseId = caseId;
        _output.WriteLine($"Navigated to case edit page — uploads enabled");
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

    /// <summary>
    /// Generates a large (~2 MB) PDF so the browser fires at least one
    /// <c>xhr.upload.onprogress</c> event during the upload.
    /// </summary>
    private static byte[] GenerateLargeTestPdfBytes(int sizeInBytes = 2 * 1024 * 1024)
    {
        var header = GenerateTestPdfBytes();
        var result = new byte[sizeInBytes];
        Array.Copy(header, result, header.Length);
        // Fill remaining bytes with PDF-safe whitespace
        Array.Fill(result, (byte)' ', header.Length, sizeInBytes - header.Length);
        return result;
    }
}
