namespace ECTSystem.Api.Middleware;

/// <summary>
/// Catches <see cref="OperationCanceledException"/> raised when a client disconnects mid-request
/// and returns HTTP 499 (client closed request) instead of letting the exception propagate as 500.
/// Avoids noise in error logs for normal client-disconnect events.
/// </summary>
public class OperationCancelledMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<OperationCancelledMiddleware> _logger;

    public OperationCancelledMiddleware(RequestDelegate next, ILogger<OperationCancelledMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request cancelled by client: {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 499;
        }
    }
}
