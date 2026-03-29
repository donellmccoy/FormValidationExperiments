using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Mapping;

/// <summary>
/// Unit tests for <see cref="BookmarkDtoMapper"/>, which maps
/// <see cref="CreateBookmarkDto"/> to <see cref="Shared.Models.CaseBookmark"/>.
/// </summary>
public class BookmarkDtoMapperTests
{
    /// <summary>
    /// Verifies that the LineOfDutyCaseId is correctly mapped from the DTO to the entity.
    /// </summary>
    [Fact]
    public void ToEntity_ValidCaseId_MapsLineOfDutyCaseId()
    {
        var dto = new CreateBookmarkDto { LineOfDutyCaseId = 99 };

        var entity = BookmarkDtoMapper.ToEntity(dto);

        Assert.Equal(99, entity.LineOfDutyCaseId);
    }

    /// <summary>
    /// Verifies that a default DTO (CaseId = 0) produces an entity with CaseId = 0.
    /// </summary>
    [Fact]
    public void ToEntity_DefaultDto_MapsZeroCaseId()
    {
        var dto = new CreateBookmarkDto();

        var entity = BookmarkDtoMapper.ToEntity(dto);

        Assert.Equal(0, entity.LineOfDutyCaseId);
    }

    /// <summary>
    /// Verifies that the entity's non-mapped properties (Id, UserId, BookmarkedDate)
    /// retain their defaults, since only LineOfDutyCaseId is mapped from the DTO.
    /// </summary>
    [Fact]
    public void ToEntity_OnlyMapsLineOfDutyCaseId_OtherPropertiesDefault()
    {
        var dto = new CreateBookmarkDto { LineOfDutyCaseId = 42 };

        var entity = BookmarkDtoMapper.ToEntity(dto);

        Assert.Equal(0, entity.Id);
        Assert.Equal(string.Empty, entity.UserId);
        Assert.Equal(default, entity.BookmarkedDate);
    }
}
