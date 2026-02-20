namespace ECTSystem.Web.Services;

/// <summary>
/// Shared scoped service that tracks the current user's bookmark count and
/// notifies subscribers (e.g. MainLayout badge) whenever the count changes.
/// </summary>
public class BookmarkCountService
{
    private readonly IDataService _dataService;

    public int Count { get; private set; }

    public event Action OnCountChanged;

    public BookmarkCountService(IDataService dataService)
    {
        _dataService = dataService;
    }

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
