using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateBookmarkDto"/> to <see cref="Bookmark"/>.
/// </summary>
public static class BookmarkDtoMapper
{
    public static Bookmark ToEntity(CreateBookmarkDto dto)
    {
        return new Bookmark
        {
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
        };
    }
}
