using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace ECTSystem.Tests.Integration;

/// <summary>
/// Base class for integration tests providing an authenticated <see cref="HttpClient"/>
/// configured against the <see cref="EctSystemWebApplicationFactory"/>.
/// </summary>
public abstract class IntegrationTestBase : IClassFixture<EctSystemWebApplicationFactory>, IDisposable
{
    protected readonly EctSystemWebApplicationFactory Factory;
    protected readonly HttpClient Client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected IntegrationTestBase(EctSystemWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateClient();
    }

    /// <summary>
    /// Authenticates the shared <see cref="Client"/> by logging in with the seeded test user
    /// via the ASP.NET Core Identity API endpoints.
    /// </summary>
    protected async Task AuthenticateAsync()
    {
        var loginPayload = new { email = "test@ect.mil", password = "Pass123" };
        var response = await Client.PostAsJsonAsync("/login", loginPayload);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>
    /// Deserializes an OData single-entity or collection response value.
    /// </summary>
    protected static async Task<T> ReadODataValueAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.Deserialize<T>(JsonOptions);
    }

    public void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }
}
