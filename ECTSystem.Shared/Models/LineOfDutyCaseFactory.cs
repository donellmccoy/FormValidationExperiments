using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

public static class LineOfDutyCaseFactory
{
    public static LineOfDutyCase Create(int memberId)
    {
        var now = DateTime.UtcNow;
        var timelineSteps = TimelineStep.CreateDefaultSteps();
        timelineSteps[0].StartDate = now;

        return new LineOfDutyCase
        {
            CaseId = GenerateCaseId(now),
            MemberId = memberId,
            InitiationDate = now,
            IncidentDate = now,
            WorkflowState = WorkflowState.MemberInformationEntry,
            CreatedDate = now,
            ModifiedDate = now,
            TimelineSteps = timelineSteps
        };
    }

    public static string GenerateCaseId(DateTime timestamp) =>
        $"{timestamp:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
}
