using Microsoft.AspNetCore.Mvc;

namespace ECTSystem.Api.Extensions;

public static class IDisposableExtensions
{
    public static TDisposable TrackDisposable<TDisposable>(this TDisposable disposable, ControllerContext context) where TDisposable : IDisposable
    {
        context.HttpContext.Response.RegisterForDispose(disposable);

        return disposable;
    }
}
