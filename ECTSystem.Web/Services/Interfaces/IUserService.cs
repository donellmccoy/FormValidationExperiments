namespace ECTSystem.Web.Services;

public interface IUserService
{
    Task<Dictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default);
    Task<string> GetDisplayNameAsync(string userId, CancellationToken cancellationToken = default);
}
