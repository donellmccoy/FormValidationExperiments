namespace ECTSystem.Web.Handlers;

/// <summary>
/// Buffers the entire HTTP response body into memory before returning it to the OData client.
/// </summary>
/// <remarks>
/// Microsoft.OData.Client (8.x) reads response streams synchronously
/// (<c>Stream.Read</c> / <c>Stream.EndRead</c> on the OData query result pipeline).
/// In Blazor WebAssembly, the underlying <c>BrowserHttpReadStream</c> only supports
/// asynchronous reads and throws <c>NotSupportedException: net_http_synchronous_reads_not_supported</c>.
/// Calling <see cref="HttpContent.LoadIntoBufferAsync(System.Threading.CancellationToken)"/> here forces the
/// response body into an in-memory <c>MemoryStream</c>, which the OData client can read synchronously.
/// </remarks>
public class ODataResponseBufferingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        if (response.Content is not null)
        {
            await response.Content.LoadIntoBufferAsync(cancellationToken);
        }

        return response;
    }
}
