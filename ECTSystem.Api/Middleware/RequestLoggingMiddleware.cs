using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ECTSystem.Api.Middleware;

public partial class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly bool _isDevelopment;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _isDevelopment = environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = context.Request.Path.Value;
        var query = context.Request.QueryString.Value;

        var stopwatch = Stopwatch.StartNew();

        // Only buffer and log bodies in Development at Debug level
        if (_isDevelopment && _logger.IsEnabled(LogLevel.Debug))
        {
            context.Request.EnableBuffering();
            var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (!string.IsNullOrEmpty(requestBody))
            {
                LogRequestBody(ScrubPii(requestBody));
            }

            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;

            await _next(context);

            stopwatch.Stop();

            context.Response.Body.Position = 0;
            var responseBodyText = await new StreamReader(context.Response.Body).ReadToEndAsync();
            context.Response.Body.Position = 0;

            LogRequestCompleted(method, path, query, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);

            if (!string.IsNullOrEmpty(responseBodyText))
            {
                LogResponseBody(ScrubPii(responseBodyText));
            }

            await responseBody.CopyToAsync(originalBodyStream);
        }
        else
        {
            await _next(context);
            stopwatch.Stop();

            LogRequestCompleted(method, path, query, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }

    private static string ScrubPii(string text)
    {
        // Scrub SSN patterns (XXX-XX-XXXX or XXXXXXXXX)
        text = SsnDashPattern().Replace(text, "***-**-****");
        text = SsnPlainPattern().Replace(text, "\"SSN\":\"*********\"");

        return text;
    }

    [GeneratedRegex(@"\b\d{3}-\d{2}-\d{4}\b")]
    private static partial Regex SsnDashPattern();

    [GeneratedRegex(@"""SSN""\s*:\s*""\d{9}""", RegexOptions.IgnoreCase)]
    private static partial Regex SsnPlainPattern();

    [LoggerMessage(Level = LogLevel.Information, Message = "HTTP {Method} {Path}{Query} responded {StatusCode} in {ElapsedMs}ms")]
    private partial void LogRequestCompleted(string method, string path, string query, int statusCode, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Request body: {Body}")]
    private partial void LogRequestBody(string body);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Response body: {Body}")]
    private partial void LogResponseBody(string body);
}
