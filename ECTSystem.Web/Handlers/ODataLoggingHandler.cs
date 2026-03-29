namespace ECTSystem.Web.Handlers;

/// <summary>
/// Logs the full request URL and response body when the server returns a non-success status code.
/// Useful for diagnosing OData 400/500 errors whose details are swallowed by the OData client library.
/// </summary>
public class ODataLoggingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[OData] >>> {request.Method} {request.RequestUri}");

        if (request.Content is not null)
        {
            var requestBody = await request.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[OData] Request Content-Type: {request.Content.Headers.ContentType}");
            Console.WriteLine($"[OData] Request body: {requestBody}");
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[OData] <<< {(int)response.StatusCode} {response.StatusCode}");
            Console.WriteLine($"[OData] Response body: {body}");
        }
        else
        {
            Console.WriteLine($"[OData] <<< {(int)response.StatusCode} {response.StatusCode}");
        }

        return response;
    }
}
