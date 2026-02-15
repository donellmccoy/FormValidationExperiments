namespace ECTSystem.Shared.ViewModels;

/// <summary>
/// Generic paged result DTO for server-side paging.
/// </summary>
/// <typeparam name="T">The type of items in the result set.</typeparam>
public class PagedResult<T>
{
    /// <summary>
    /// The items for the current page.
    /// </summary>
    public List<T> Items { get; set; } = new();

    /// <summary>
    /// Total count of items (before paging).
    /// </summary>
    public int TotalCount { get; set; }
}
