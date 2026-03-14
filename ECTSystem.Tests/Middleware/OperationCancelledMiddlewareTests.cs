using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using ECTSystem.Api.Middleware;
using Xunit;

namespace ECTSystem.Tests.Middleware;

/// <summary>
/// Unit tests for <see cref="OperationCancelledMiddleware"/>, which intercepts
/// <see cref="OperationCanceledException"/> thrown when a client disconnects
/// mid-request and returns HTTP 499 instead of allowing a 500 error.
/// </summary>
public class OperationCancelledMiddlewareTests
{
    private readonly Mock<ILogger<OperationCancelledMiddleware>> _mockLogger;

    public OperationCancelledMiddlewareTests()
    {
        _mockLogger = new Mock<ILogger<OperationCancelledMiddleware>>();
    }

    /// <summary>
    /// Verifies that when the next delegate completes normally, the middleware
    /// passes through without modifying the response status code.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_NoException_PassesThrough()
    {
        var context = new DefaultHttpContext();
        var middleware = new OperationCancelledMiddleware(
            next: _ => Task.CompletedTask,
            logger: _mockLogger.Object);

        await middleware.InvokeAsync(context);

        Assert.Equal(200, context.Response.StatusCode);
    }

    /// <summary>
    /// Verifies that when the next delegate throws <see cref="OperationCanceledException"/>
    /// and the request's cancellation token has been cancelled (client disconnect),
    /// the middleware sets the response status code to 499.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ClientDisconnect_Returns499()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var context = new DefaultHttpContext();
        context.RequestAborted = cts.Token;

        var middleware = new OperationCancelledMiddleware(
            next: _ => throw new OperationCanceledException(),
            logger: _mockLogger.Object);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
    }

    /// <summary>
    /// Verifies that when <see cref="OperationCanceledException"/> is thrown but
    /// the request's cancellation token has NOT been cancelled (server-side cancellation),
    /// the exception propagates rather than being swallowed as a client disconnect.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_ServerSideCancellation_Propagates()
    {
        var context = new DefaultHttpContext();
        // RequestAborted is NOT cancelled — this is a server-side cancellation

        var middleware = new OperationCancelledMiddleware(
            next: _ => throw new OperationCanceledException(),
            logger: _mockLogger.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => middleware.InvokeAsync(context));
    }

    /// <summary>
    /// Verifies that non-cancellation exceptions propagate through the middleware
    /// without being caught.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OtherException_Propagates()
    {
        var context = new DefaultHttpContext();

        var middleware = new OperationCancelledMiddleware(
            next: _ => throw new InvalidOperationException("Something broke"),
            logger: _mockLogger.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));
    }
}
