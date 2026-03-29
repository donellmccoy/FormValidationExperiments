using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Mapping;

/// <summary>
/// Unit tests for <see cref="AuthorityDtoMapper"/>, which maps
/// <see cref="CreateAuthorityDto"/> to <see cref="Shared.Models.LineOfDutyAuthority"/>.
/// </summary>
public class AuthorityDtoMapperTests
{
    /// <summary>
    /// Verifies that all scalar properties are correctly mapped from the DTO to the entity.
    /// </summary>
    [Fact]
    public void ToEntity_AllPropertiesSet_MapsAllProperties()
    {
        var actionDate = new DateTime(2024, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var dto = new CreateAuthorityDto
        {
            LineOfDutyCaseId = 42,
            Role = "Immediate Commander",
            Name = "Smith, John A.",
            Rank = "Col",
            Title = "Wing CC",
            ActionDate = actionDate,
            Recommendation = "Line of Duty",
            Comments = ["Reviewed medical records", "Concur with findings"]
        };

        var entity = AuthorityDtoMapper.ToEntity(dto);

        Assert.Equal(42, entity.LineOfDutyCaseId);
        Assert.Equal("Immediate Commander", entity.Role);
        Assert.Equal("Smith, John A.", entity.Name);
        Assert.Equal("Col", entity.Rank);
        Assert.Equal("Wing CC", entity.Title);
        Assert.Equal(actionDate, entity.ActionDate);
        Assert.Equal("Line of Duty", entity.Recommendation);
        Assert.Equal(2, entity.Comments.Count);
        Assert.Equal("Reviewed medical records", entity.Comments[0]);
        Assert.Equal("Concur with findings", entity.Comments[1]);
    }

    /// <summary>
    /// Verifies that default/empty DTO values produce an entity with default values.
    /// </summary>
    [Fact]
    public void ToEntity_DefaultDto_MapsDefaults()
    {
        var dto = new CreateAuthorityDto();

        var entity = AuthorityDtoMapper.ToEntity(dto);

        Assert.Equal(0, entity.LineOfDutyCaseId);
        Assert.Equal(string.Empty, entity.Role);
        Assert.Equal(string.Empty, entity.Name);
        Assert.Equal(string.Empty, entity.Rank);
        Assert.Equal(string.Empty, entity.Title);
        Assert.Null(entity.ActionDate);
        Assert.Equal(string.Empty, entity.Recommendation);
        Assert.Empty(entity.Comments);
    }

    /// <summary>
    /// Verifies that null ActionDate is preserved in the mapped entity.
    /// </summary>
    [Fact]
    public void ToEntity_NullActionDate_MapsNull()
    {
        var dto = new CreateAuthorityDto
        {
            LineOfDutyCaseId = 1,
            Role = "SJA",
            Name = "Doe, Jane",
            ActionDate = null
        };

        var entity = AuthorityDtoMapper.ToEntity(dto);

        Assert.Null(entity.ActionDate);
    }
}
