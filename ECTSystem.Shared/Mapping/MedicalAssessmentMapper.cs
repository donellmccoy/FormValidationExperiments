using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Riok.Mapperly.Abstractions;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Mapperly source-generated mapper for <see cref="MedicalAssessmentFormModel"/> ↔ <see cref="LineOfDutyCase"/>
/// medical assessment properties. Handles the ~20 direct property copies; callers handle the
/// remaining fields that require custom logic (toxicology, EPTS/NSA, incident type null-coalescing).
/// </summary>
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.None)]
public static partial class MedicalAssessmentMapper
{
    // ──────────────────── Entity → Form Model ────────────────────

    [MapProperty(nameof(LineOfDutyCase.IncidentType), nameof(MedicalAssessmentFormModel.InvestigationType))]
    [MapperIgnoreTarget(nameof(MedicalAssessmentFormModel.IsEptsNsa))]
    [MapperIgnoreTarget(nameof(MedicalAssessmentFormModel.ToxicologyTestDone))]
    [MapperIgnoreTarget(nameof(MedicalAssessmentFormModel.ToxicologyTestResults))]
    public static partial MedicalAssessmentFormModel ToFormModel(LineOfDutyCase source);

    // ──────────────────── Form Model → Entity (update) ────────────────────

    [MapperIgnoreSource(nameof(MedicalAssessmentFormModel.InvestigationType))]
    [MapperIgnoreSource(nameof(MedicalAssessmentFormModel.IsEptsNsa))]
    [MapperIgnoreSource(nameof(MedicalAssessmentFormModel.ToxicologyTestDone))]
    [MapperIgnoreSource(nameof(MedicalAssessmentFormModel.ToxicologyTestResults))]
    public static partial void ApplyToEntity(MedicalAssessmentFormModel source, LineOfDutyCase target);
}
