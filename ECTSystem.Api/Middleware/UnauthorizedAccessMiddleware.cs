namespace ECTSystem.Api.Middleware;

/// <summary>
/// Catches <see cref="UnauthorizedAccessException"/> thrown when required identity claims
/// are missing and returns HTTP 401 instead of letting the exception propagate as 500.
/// </summary>
public class UnauthorizedAccessMiddleware
{
    private readonly RequestDelegate _next;

    public UnauthorizedAccessMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (UnauthorizedAccessException)
        {
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new
                {
                    type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                    title = "Unauthorized",
                    detail = "Authentication credentials are missing or invalid.",
                    status = 401
                });
            }
        }
    }
}
