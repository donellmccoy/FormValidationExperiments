namespace ECTSystem.Web.Services;

/// <summary>
/// Shared scoped service that tracks the current user's bookmark count and
/// notifies subscribers (e.g., MainLayout badge) whenever the count changes.
/// Registered as a scoped service so the count is shared across all components
/// within the same Blazor circuit/tab. Components subscribe to <see cref="OnCountChanged"/>
/// to reactively update badge or indicator UI when bookmarks are added or removed.
/// </summary>
public class BookmarkCountService
{
    /// <summary>
    /// The bookmark service used to query the server for the current user's bookmark count.
    /// </summary>
    private readonly IBookmarkService _dataService;

    /// <summary>
    /// Gets the current number of cases bookmarked by the authenticated user.
    /// Updated by calling <see cref="RefreshAsync"/>. Defaults to <c>0</c> until the first refresh completes.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Raised whenever <see cref="Count"/> is updated after a successful refresh.
    /// Subscribers (e.g., layout components displaying a bookmark badge) should call
    /// <c>StateHasChanged()</c> in their handler to re-render with the new count.
    /// </summary>
    public event Action OnCountChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarkCountService"/> class.
    /// </summary>
    /// <param name="dataService">The bookmark service for querying bookmark counts from the API.</param>
    public BookmarkCountService(IBookmarkService dataService)
    {
        _dataService = dataService;
    }

    /// <summary>
    /// Increments the bookmark count by one and notifies subscribers.
    /// </summary>
    public void Increment()
    {
        Count++;
        OnCountChanged?.Invoke();
    }

    /// <summary>
    /// Decrements the bookmark count by one and notifies subscribers.
    /// </summary>
    public void Decrement()
    {
        if (Count > 0)
        {
            Count--;
            OnCountChanged?.Invoke();
        }
    }

    /// <summary>
    /// Fetches the current bookmark count from the server and raises <see cref="OnCountChanged"/>
    /// if the request succeeds. Uses a <c>$top=0&amp;$count=true</c> OData query to retrieve only
    /// the total count without loading any case data. Silently swallows failures to avoid
    /// disrupting the UI for a non-critical metric.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _dataService.GetBookmarkedCasesAsync(
                count: true, top: 0, cancellationToken: cancellationToken);

            Count = result.Count;
            OnCountChanged?.Invoke();
        }
        catch
        {
            // Non-critical — keep stale count on failure
        }
    }
}
