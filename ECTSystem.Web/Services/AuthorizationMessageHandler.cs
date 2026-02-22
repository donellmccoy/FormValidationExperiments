using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;

namespace ECTSystem.Web.Services;

public class AuthorizationMessageHandler : DelegatingHandler
{
    private readonly ILocalStorageService _localStorage;
    private readonly Uri _baseAddress;
    private static readonly SemaphoreSlim RefreshLock = new(1, 1);

    public AuthorizationMessageHandler(ILocalStorageService localStorage, Uri baseAddress)
    {
        _localStorage = localStorage;
        _baseAddress = baseAddress;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await _localStorage.GetItemAsStringAsync("accessToken");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Attempt to refresh the token
        if (!await TryRefreshTokenAsync(cancellationToken))
        {
            return response;
        }

        // Retry the original request with the new token
        var newToken = await _localStorage.GetItemAsStringAsync("accessToken");
        var retryRequest = await CloneRequestAsync(request);
        retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);

        response.Dispose();
        return await base.SendAsync(retryRequest, cancellationToken);
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        // Serialize refresh attempts so concurrent 401s don't all try to refresh at once
        await RefreshLock.WaitAsync(cancellationToken);
        try
        {
            var refreshToken = await _localStorage.GetItemAsStringAsync("refreshToken");
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseAddress, "refresh"))
            {
                Content = JsonContent.Create(new { refreshToken })
            };

            var refreshResponse = await base.SendAsync(refreshRequest, cancellationToken);
            if (!refreshResponse.IsSuccessStatusCode)
            {
                // Refresh failed — clear tokens so the UI redirects to login
                await _localStorage.RemoveItemAsync("accessToken");
                await _localStorage.RemoveItemAsync("refreshToken");
                return false;
            }

            var auth = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: cancellationToken);
            if (auth is null || string.IsNullOrWhiteSpace(auth.AccessToken))
            {
                return false;
            }

            await _localStorage.SetItemAsStringAsync("accessToken", auth.AccessToken);
            await _localStorage.SetItemAsStringAsync("refreshToken", auth.RefreshToken);
            return true;
        }
        finally
        {
            RefreshLock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri);

        if (original.Content is not null)
        {
            var content = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(content);

            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
