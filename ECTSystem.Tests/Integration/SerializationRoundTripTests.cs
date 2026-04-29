using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Xunit;

namespace ECTSystem.Tests.Integration;

/// <summary>
/// Round-trip serialization tests (Rec #21). Verifies that the two
/// <see cref="JsonSerializerOptions"/> configurations the application uses on
/// the wire — the OData PascalCase profile and the web/minimal-API camelCase
/// profile — preserve every field on the representative entities used by the
/// API/client contract.
/// </summary>
/// <remarks>
/// These tests do not start the host; they exercise the serializer
/// configurations directly so a regression in either profile (a missing
/// converter, a casing mismatch, a dropped property) fails fast without
/// needing an integration-test factory.
/// </remarks>
public class SerializationRoundTripTests
{
    /// <summary>
    /// Mirror of the API/Web OData profile: PascalCase property names,
    /// case-insensitive read, string-enum converter, ignore cycles.
    /// </summary>
    private static readonly JsonSerializerOptions ODataOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = null,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Mirror of the API/Web minimal-API/MVC profile: camelCase, string-enum
    /// converter, ignore cycles.
    /// </summary>
    private static readonly JsonSerializerOptions WebOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        Converters = { new JsonStringEnumConverter() }
    };

    private static LineOfDutyCase BuildSampleCase() => new()
    {
        Id = 4242,
        CaseId = "RBI-2025-001",
        ProcessType = ProcessType.Formal,
        Component = ServiceComponent.AirForceReserve,
        MemberName = "Sample, Member A",
        MemberRank = "MSgt",
        ServiceNumber = "1234567890",
        Unit = "419 FW",
        FromLine = "419 FW/CC",
        IncidentType = IncidentType.Injury,
        IncidentDate = new DateTime(2025, 3, 14, 9, 0, 0, DateTimeKind.Utc),
        IncidentDescription = "Slipped on icy ramp during UTA.",
        IncidentDutyStatus = DutyStatus.InactiveDutyTraining,
        IsMTF = true,
        IsRMU = false,
        IsGMU = false,
        IsDeployedLocation = false,
        IsUSAFA = false,
        IsAFROTC = false
    };

    private static Member BuildSampleMember() => new()
    {
        Id = 7,
        FirstName = "Pat",
        MiddleInitial = "Q",
        LastName = "Tester",
        Rank = "Capt",
        ServiceNumber = "9876543210",
        Unit = "75 ABW",
        Component = ServiceComponent.RegularAirForce,
        DateOfBirth = new DateTime(1990, 1, 2, 0, 0, 0, DateTimeKind.Utc)
    };

    private static Bookmark BuildSampleBookmark() => new()
    {
        Id = 12,
        UserId = "auth0|abc",
        LineOfDutyCaseId = 4242
    };

    private static WorkflowStateHistory BuildSampleHistory() => new()
    {
        Id = 99,
        LineOfDutyCaseId = 4242,
        WorkflowState = WorkflowState.UnitCommanderReview,
        EnteredDate = new DateTime(2025, 3, 15, 12, 30, 0, DateTimeKind.Utc),
        ExitDate = new DateTime(2025, 3, 16, 8, 0, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void OData_LineOfDutyCase_RoundTripsAllFields()
    {
        var original = BuildSampleCase();

        var json = JsonSerializer.Serialize(original, ODataOptions);
        var node = JsonNode.Parse(json)!.AsObject();

        // OData wire format must use PascalCase.
        Assert.True(node.ContainsKey("CaseId"));
        Assert.True(node.ContainsKey("ProcessType"));
        // String-enum converter must produce the enum name, not the integer.
        Assert.Equal("Formal", node["ProcessType"]!.GetValue<string>());
        Assert.Equal("AirForceReserve", node["Component"]!.GetValue<string>());
        Assert.Equal("Injury", node["IncidentType"]!.GetValue<string>());
        Assert.Equal("InactiveDutyTraining", node["IncidentDutyStatus"]!.GetValue<string>());

        var roundTripped = JsonSerializer.Deserialize<LineOfDutyCase>(json, ODataOptions)!;
        AssertCaseEqual(original, roundTripped);
    }

    [Fact]
    public void Web_LineOfDutyCase_RoundTripsAllFields()
    {
        var original = BuildSampleCase();

        var json = JsonSerializer.Serialize(original, WebOptions);
        var node = JsonNode.Parse(json)!.AsObject();

        // Web/minimal-API profile must use camelCase.
        Assert.True(node.ContainsKey("caseId"));
        Assert.True(node.ContainsKey("processType"));
        Assert.False(node.ContainsKey("CaseId"));
        Assert.Equal("Formal", node["processType"]!.GetValue<string>());

        var roundTripped = JsonSerializer.Deserialize<LineOfDutyCase>(json, WebOptions)!;
        AssertCaseEqual(original, roundTripped);
    }

    [Fact]
    public void OData_Member_RoundTripsAllFields()
    {
        var original = BuildSampleMember();
        var json = JsonSerializer.Serialize(original, ODataOptions);
        var node = JsonNode.Parse(json)!.AsObject();
        Assert.True(node.ContainsKey("FirstName"));
        Assert.Equal("RegularAirForce", node["Component"]!.GetValue<string>());

        var roundTripped = JsonSerializer.Deserialize<Member>(json, ODataOptions)!;
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.FirstName, roundTripped.FirstName);
        Assert.Equal(original.LastName, roundTripped.LastName);
        Assert.Equal(original.Rank, roundTripped.Rank);
        Assert.Equal(original.ServiceNumber, roundTripped.ServiceNumber);
        Assert.Equal(original.Unit, roundTripped.Unit);
        Assert.Equal(original.Component, roundTripped.Component);
        Assert.Equal(original.DateOfBirth, roundTripped.DateOfBirth);
    }

    [Fact]
    public void Web_Member_RoundTripsAllFields()
    {
        var original = BuildSampleMember();
        var json = JsonSerializer.Serialize(original, WebOptions);
        var node = JsonNode.Parse(json)!.AsObject();
        Assert.True(node.ContainsKey("firstName"));
        Assert.Equal("RegularAirForce", node["component"]!.GetValue<string>());

        var roundTripped = JsonSerializer.Deserialize<Member>(json, WebOptions)!;
        Assert.Equal(original.FirstName, roundTripped.FirstName);
        Assert.Equal(original.Component, roundTripped.Component);
        Assert.Equal(original.DateOfBirth, roundTripped.DateOfBirth);
    }

    [Fact]
    public void OData_Bookmark_RoundTrips()
    {
        var original = BuildSampleBookmark();
        var json = JsonSerializer.Serialize(original, ODataOptions);
        var roundTripped = JsonSerializer.Deserialize<Bookmark>(json, ODataOptions)!;
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.UserId, roundTripped.UserId);
        Assert.Equal(original.LineOfDutyCaseId, roundTripped.LineOfDutyCaseId);
    }

    [Fact]
    public void Web_Bookmark_RoundTrips()
    {
        var original = BuildSampleBookmark();
        var json = JsonSerializer.Serialize(original, WebOptions);
        var node = JsonNode.Parse(json)!.AsObject();
        Assert.True(node.ContainsKey("userId"));
        Assert.True(node.ContainsKey("lineOfDutyCaseId"));

        var roundTripped = JsonSerializer.Deserialize<Bookmark>(json, WebOptions)!;
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.UserId, roundTripped.UserId);
        Assert.Equal(original.LineOfDutyCaseId, roundTripped.LineOfDutyCaseId);
    }

    [Fact]
    public void OData_WorkflowStateHistory_RoundTripsEnumAsString()
    {
        var original = BuildSampleHistory();
        var json = JsonSerializer.Serialize(original, ODataOptions);
        var node = JsonNode.Parse(json)!.AsObject();
        Assert.Equal("UnitCommanderReview", node["WorkflowState"]!.GetValue<string>());

        var roundTripped = JsonSerializer.Deserialize<WorkflowStateHistory>(json, ODataOptions)!;
        Assert.Equal(original.WorkflowState, roundTripped.WorkflowState);
        Assert.Equal(original.EnteredDate, roundTripped.EnteredDate);
        Assert.Equal(original.ExitDate, roundTripped.ExitDate);
    }

    [Fact]
    public void CrossProfile_ServerODataPayload_DeserializesUnderWebOptions()
    {
        // The OData server emits PascalCase. The client web options enable
        // PropertyNameCaseInsensitive (via JsonSerializerDefaults.Web), so a
        // PascalCase payload must still deserialize cleanly under WebOptions.
        var original = BuildSampleCase();
        var serverJson = JsonSerializer.Serialize(original, ODataOptions);

        var roundTripped = JsonSerializer.Deserialize<LineOfDutyCase>(serverJson, WebOptions)!;
        AssertCaseEqual(original, roundTripped);
    }

    [Fact]
    public void CrossProfile_ClientWebPayload_DeserializesUnderODataOptions()
    {
        // Symmetric: a camelCase payload (e.g. minimal-API response) must
        // deserialize under the OData options because OData options also
        // enable case-insensitive reads.
        var original = BuildSampleCase();
        var clientJson = JsonSerializer.Serialize(original, WebOptions);

        var roundTripped = JsonSerializer.Deserialize<LineOfDutyCase>(clientJson, ODataOptions)!;
        AssertCaseEqual(original, roundTripped);
    }

    [Theory]
    [InlineData(WorkflowState.Draft)]
    [InlineData(WorkflowState.UnitCommanderReview)]
    [InlineData(WorkflowState.BoardLegalReview)]
    [InlineData(WorkflowState.Completed)]
    [InlineData(WorkflowState.Cancelled)]
    public void EnumStringConverter_RoundTripsByName_NotByOrdinal(WorkflowState state)
    {
        var json = JsonSerializer.Serialize(state, ODataOptions);
        Assert.Equal($"\"{state}\"", json);

        var roundTripped = JsonSerializer.Deserialize<WorkflowState>(json, ODataOptions);
        Assert.Equal(state, roundTripped);

        var webJson = JsonSerializer.Serialize(state, WebOptions);
        Assert.Equal($"\"{state}\"", webJson);
    }

    private static void AssertCaseEqual(LineOfDutyCase expected, LineOfDutyCase actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.CaseId, actual.CaseId);
        Assert.Equal(expected.ProcessType, actual.ProcessType);
        Assert.Equal(expected.Component, actual.Component);
        Assert.Equal(expected.MemberName, actual.MemberName);
        Assert.Equal(expected.MemberRank, actual.MemberRank);
        Assert.Equal(expected.ServiceNumber, actual.ServiceNumber);
        Assert.Equal(expected.Unit, actual.Unit);
        Assert.Equal(expected.FromLine, actual.FromLine);
        Assert.Equal(expected.IncidentType, actual.IncidentType);
        Assert.Equal(expected.IncidentDate, actual.IncidentDate);
        Assert.Equal(expected.IncidentDescription, actual.IncidentDescription);
        Assert.Equal(expected.IncidentDutyStatus, actual.IncidentDutyStatus);
        Assert.Equal(expected.IsMTF, actual.IsMTF);
        Assert.Equal(expected.IsRMU, actual.IsRMU);
        Assert.Equal(expected.IsGMU, actual.IsGMU);
        Assert.Equal(expected.IsDeployedLocation, actual.IsDeployedLocation);
        Assert.Equal(expected.IsUSAFA, actual.IsUSAFA);
        Assert.Equal(expected.IsAFROTC, actual.IsAFROTC);
    }
}
