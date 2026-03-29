using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Xunit;

namespace ECTSystem.Tests.Mapping;

/// <summary>
/// Unit tests for <see cref="MemberDtoMapper"/>, which maps
/// <see cref="CreateMemberDto"/> to <see cref="Member"/> and applies
/// <see cref="UpdateMemberDto"/> updates to an existing <see cref="Member"/> entity.
/// </summary>
public class MemberDtoMapperTests
{
    /// <summary>
    /// Verifies that all scalar properties are correctly mapped from <see cref="CreateMemberDto"/> to <see cref="Member"/>.
    /// </summary>
    [Fact]
    public void ToEntity_AllPropertiesSet_MapsAllProperties()
    {
        var dob = new DateTime(1990, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var dto = new CreateMemberDto
        {
            FirstName = "John",
            MiddleInitial = "A",
            LastName = "Smith",
            Rank = "TSgt",
            ServiceNumber = "123-45-6789",
            Unit = "55th Wing",
            Component = ServiceComponent.RegularAirForce,
            DateOfBirth = dob
        };

        var entity = MemberDtoMapper.ToEntity(dto);

        Assert.Equal("John", entity.FirstName);
        Assert.Equal("A", entity.MiddleInitial);
        Assert.Equal("Smith", entity.LastName);
        Assert.Equal("TSgt", entity.Rank);
        Assert.Equal("123-45-6789", entity.ServiceNumber);
        Assert.Equal("55th Wing", entity.Unit);
        Assert.Equal(ServiceComponent.RegularAirForce, entity.Component);
        Assert.Equal(dob, entity.DateOfBirth);
    }

    /// <summary>
    /// Verifies that default DTO values produce an entity with default values,
    /// including the default enum value for <see cref="ServiceComponent"/>.
    /// </summary>
    [Fact]
    public void ToEntity_DefaultDto_MapsDefaults()
    {
        var dto = new CreateMemberDto();

        var entity = MemberDtoMapper.ToEntity(dto);

        Assert.Equal(string.Empty, entity.FirstName);
        Assert.Equal(string.Empty, entity.MiddleInitial);
        Assert.Equal(string.Empty, entity.LastName);
        Assert.Equal(string.Empty, entity.Rank);
        Assert.Equal(string.Empty, entity.ServiceNumber);
        Assert.Equal(string.Empty, entity.Unit);
        Assert.Equal(default, entity.Component);
        Assert.Null(entity.DateOfBirth);
    }

    /// <summary>
    /// Verifies that <see cref="MemberDtoMapper.ApplyUpdate"/> overwrites all properties
    /// on the target entity, including RowVersion for concurrency control.
    /// </summary>
    [Fact]
    public void ApplyUpdate_AllPropertiesSet_UpdatesAllProperties()
    {
        var entity = new Member
        {
            FirstName = "OldFirst",
            MiddleInitial = "O",
            LastName = "OldLast",
            Rank = "Amn",
            ServiceNumber = "000-00-0000",
            Unit = "Old Unit",
            Component = ServiceComponent.AirNationalGuard,
            DateOfBirth = new DateTime(1985, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            RowVersion = [0x01]
        };

        var newDob = new DateTime(1992, 7, 4, 0, 0, 0, DateTimeKind.Utc);
        var newRowVersion = new byte[] { 0x02, 0x03 };
        var dto = new UpdateMemberDto
        {
            FirstName = "Jane",
            MiddleInitial = "B",
            LastName = "Doe",
            Rank = "MSgt",
            ServiceNumber = "987-65-4321",
            Unit = "72nd ABW",
            Component = ServiceComponent.AirForceReserve,
            DateOfBirth = newDob,
            RowVersion = newRowVersion
        };

        MemberDtoMapper.ApplyUpdate(dto, entity);

        Assert.Equal("Jane", entity.FirstName);
        Assert.Equal("B", entity.MiddleInitial);
        Assert.Equal("Doe", entity.LastName);
        Assert.Equal("MSgt", entity.Rank);
        Assert.Equal("987-65-4321", entity.ServiceNumber);
        Assert.Equal("72nd ABW", entity.Unit);
        Assert.Equal(ServiceComponent.AirForceReserve, entity.Component);
        Assert.Equal(newDob, entity.DateOfBirth);
        Assert.Same(newRowVersion, entity.RowVersion);
    }

    /// <summary>
    /// Verifies that <see cref="MemberDtoMapper.ApplyUpdate"/> correctly handles null DateOfBirth,
    /// overwriting a non-null value.
    /// </summary>
    [Fact]
    public void ApplyUpdate_NullDateOfBirth_SetsNull()
    {
        var entity = new Member
        {
            DateOfBirth = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        var dto = new UpdateMemberDto { DateOfBirth = null, RowVersion = [0x01] };

        MemberDtoMapper.ApplyUpdate(dto, entity);

        Assert.Null(entity.DateOfBirth);
    }
}
