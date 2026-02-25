# Plan: Consolidate View Models into LineOfDutyViewModel

## TL;DR
Replace 14 separate per-step form model classes (+ `CaseInfoModel` + `CaseViewModelsDto`) with a single `LineOfDutyViewModel` that extends `TrackableModel`. This simplifies the code-behind from 14 field declarations to 1, eliminates `CaseViewModelsDto`, and collapses the mapper from 24 methods (12 To + 12 Apply) to 2 methods (`ToViewModel` + `ApplyToCase`). The `IValidatableObject` logic from `MedicalAssessmentFormModel` moves into the consolidated class. Per-tab dirty tracking is preserved via a `[FormSection]` attribute (Option A).

## Scope
- **In scope:** Consolidate all 14 `TrackableModel` subclasses + `CaseInfoModel` into `LineOfDutyViewModel`; update mapper, EditCase code-behind, EditCase Razor bindings, and `MedicalAssessmentMapper` (Mapperly). Add `[FormSection]` attribute and section-aware dirty tracking to `TrackableModel`.
- **Out of scope:** No API changes (view models are client-side only). No changes to `LineOfDutyCase` domain model, services, or controllers.
- **Delete afterward:** 15 source files (14 form models + `CaseViewModelsDto`); `MedicalAssessmentMapper.cs` will be inlined into the consolidated mapper and deleted.

---

## Phase 1: Create `FormSectionAttribute` and Update `TrackableModel` (ECTSystem.Shared)

1. Create `ECTSystem.Shared/ViewModels/FormSectionAttribute.cs`:
   - A custom `[AttributeUsage(AttributeTargets.Property)]` attribute with a `string SectionName` property.
   - Usage: `[FormSection("MemberInfo")]` on each property to identify which tab/section it belongs to.

2. Update `TrackableModel`:
   - Add `IsDirtySection(string sectionName)` method:
     - Deserializes the snapshot.
     - Compares only properties tagged with `[FormSection(sectionName)]` to the current values.
     - Returns `true` if any property in that section has changed.
   - The existing `IsDirty` property continues to work as before (compares entire object).

3. Create `ECTSystem.Shared/ViewModels/LineOfDutyViewModel.cs`:
   - Extends `TrackableModel`, implements `IValidatableObject`
   - Contains ALL properties from:
     - `CaseInfoModel` (12 props — case header, read-only display) — tagged `[FormSection("CaseInfo")]`
     - `MemberInfoFormModel` (18 props including 2 computed: `MemberFullName`, `ShowArcSection`) — tagged `[FormSection("MemberInfo")]`
     - `MedicalAssessmentFormModel` (30 props including 8 computed visibility + `Validate()` with 12 rules) — tagged `[FormSection("MedicalAssessment")]`
     - `UnitCommanderFormModel` (19 props including 2 computed) — tagged `[FormSection("UnitCommander")]`
     - `WingCommanderFormModel` (9 props including 1 computed) — tagged `[FormSection("WingCommander")]`
     - Stub classes (all 0 properties currently):
       - `MedicalTechnicianFormModel` — `[FormSection("MedicalTechnician")]`
       - `WingJudgeAdvocateFormModel` — `[FormSection("WingJudgeAdvocate")]`
       - `AppointingAuthorityFormModel` — `[FormSection("AppointingAuthority")]`
       - `BoardTechnicianFormModel` — `[FormSection("BoardTechnician")]`
       - `BoardMedicalFormModel` — `[FormSection("BoardMedical")]`
       - `BoardLegalFormModel` — `[FormSection("BoardLegal")]`
       - `BoardAdminFormModel` — `[FormSection("BoardAdmin")]`
       - `CaseDialogueFormModel` — `[FormSection("CaseDialogue")]`
       - `CaseNotificationsFormModel` — `[FormSection("CaseNotifications")]`
       - `CaseDocumentsFormModel` — `[FormSection("CaseDocuments")]`
   - Totals: ~71 data properties + ~12 computed properties + validation logic
   - Keep all `[Required]`, `[StringLength]` DataAnnotation attributes from `MedicalAssessmentFormModel`
   - Move the `IValidatableObject.Validate()` method from `MedicalAssessmentFormModel`
   - Property names stay identical (no renames needed — no collisions exist between models)

4. Verify no property name collisions across all models before merging. Known analysis: no collisions exist — each model uses unique, section-prefixed names (e.g., `CommanderName`, `SJAName`, `InvestigationType`).

## Phase 2: Update Mapper (ECTSystem.Shared)

5. **Rewrite `LineOfDutyCaseMapper.cs`**:
   - Replace 12 `To*FormModel()` methods with single `ToLineOfDutyViewModel(LineOfDutyCase) → LineOfDutyViewModel`
   - Replace 12 `Apply*()` methods with single `ApplyToCase(LineOfDutyViewModel, LineOfDutyCase)`
   - Remove `ToCaseViewModelsDto()` and `ApplyAll()` — no longer needed
   - Keep all helper methods (`ParseMemberName`, `MaskSsn`, `FindAuthority`, `FindOrCreateAuthority`, `ParseMilitaryRank`, `FormatRankToFullName`, `FormatRankToPayGrade`, etc.)
   - Inline the property mappings from each old `To*` / `Apply*` method into the two new methods

6. **Delete `MedicalAssessmentMapper.cs`** (Mapperly source-generated):
   - Inline the ~20 direct property copies into the manual mapper
   - Remove the file entirely — the Mapperly mapper would need excessive `[MapperIgnore]` attributes for the ~60 other properties on the consolidated model

## Phase 3: Update EditCase Code-Behind (ECTSystem.Web)

7. **Simplify `EditCase.razor.cs` field declarations** — replace 14 separate fields:
   ```
   _memberFormModel, _medicalFormModel, _commanderFormModel, _wingJAFormModel,
   _wingCommanderFormModel, _medTechFormModel, _appointingAuthorityFormModel,
   _boardTechFormModel, _boardMedFormModel, _boardLegalFormModel, _boardAdminFormModel,
   _caseDialogueFormModel, _caseNotificationsFormModel, _caseDocumentsFormModel
   ```
   Plus `_caseInfo` → all become one field: `private LineOfDutyViewModel _viewModel = new();`

8. **Update `LoadCaseAsync()`**: Replace `ToCaseViewModelsDto()` + 12 field assignments with single `_viewModel = LineOfDutyCaseMapper.ToLineOfDutyViewModel(_lodCase);`

9. **Update `AllFormModels`** property: Returns `[_viewModel]` instead of 14 items. Consider whether `AllFormModels` is still needed or can be replaced with direct `_viewModel.IsDirty` checks.

10. **Update `HasAnyChanges`**: Simplify to `_viewModel.IsDirty`.

11. **Update `GetTabOperations()`**: All entries now reference `_viewModel` as the model. The Apply action calls the single `ApplyToCase`. Since there's only one model, the per-tab `Model` return is always `_viewModel`.

12. **Update `SaveCurrentTabAsync()`**: Remove the `CaseViewModelsDto` construction in the else branch — just call `LineOfDutyCaseMapper.ApplyToCase(_viewModel, _lodCase)`. Re-snapshot is just `_viewModel.TakeSnapshot(JsonOptions)`.

13. **Update `TakeSnapshots()`**: Single call to `_viewModel.TakeSnapshot(JsonOptions)`.

14. **Update `OnRevertChanges()`**: Single call to `_viewModel.Revert()`.

15. **Update form submit handlers**: `OnMemberFormSubmit`, `OnFormSubmit`, `OnCommanderFormSubmit`, etc. — parameter types change to `LineOfDutyViewModel`.

16. **Update conditional visibility handlers**: `OnIsMilitaryFacilityChanged`, `OnWasUnderInfluenceChanged`, etc. — change `_medicalFormModel.*` to `_viewModel.*`.

17. **Update `_medicalForm` ref**: Change type from `RadzenTemplateForm<MedicalAssessmentFormModel>` to `RadzenTemplateForm<LineOfDutyViewModel>`.

18. **Update dirty indicators per tab**: Replace `_memberFormModel.IsDirty` with `_viewModel.IsDirtySection("MemberInfo")`, `_medicalFormModel.IsDirty` with `_viewModel.IsDirtySection("MedicalAssessment")`, etc.

## Phase 4: Update EditCase Razor Markup (ECTSystem.Web)

19. **Update all `RadzenTemplateForm` instances** — 4 exist currently:
    - `<RadzenTemplateForm TItem="MemberInfoFormModel" Data="@_memberFormModel" ...>` → `TItem="LineOfDutyViewModel" Data="@_viewModel"`
    - `<RadzenTemplateForm TItem="MedicalAssessmentFormModel" Data="@_medicalFormModel" ...>` → same
    - `<RadzenTemplateForm TItem="UnitCommanderFormModel" Data="@_commanderFormModel" ...>` → same
    - `<RadzenTemplateForm TItem="WingCommanderFormModel" Data="@_wingCommanderFormModel" ...>` → same

20. **Update all data bindings** — replace field prefixes throughout:
    - `_memberFormModel.` → `_viewModel.` (~15 occurrences)
    - `_medicalFormModel.` → `_viewModel.` (~30 occurrences)
    - `_commanderFormModel.` → `_viewModel.` (~30 occurrences)
    - `_wingCommanderFormModel.` → `_viewModel.` (~10 occurrences)
    - `_caseInfo.` → `_viewModel.` (~8 occurrences)

21. **Update dirty indicators on tabs**: Use section-aware dirty tracking:
    - `_memberFormModel.IsDirty` → `_viewModel.IsDirtySection("MemberInfo")`
    - `_medicalFormModel.IsDirty` → `_viewModel.IsDirtySection("MedicalAssessment")`
    - `_commanderFormModel.IsDirty` → `_viewModel.IsDirtySection("UnitCommander")`
    - `_wingCommanderFormModel.IsDirty` → `_viewModel.IsDirtySection("WingCommander")`

## Phase 5: Delete Old Files

22. Delete the following files from `ECTSystem.Shared/ViewModels/`:
    - `MemberInfoFormModel.cs`
    - `MedicalAssessmentFormModel.cs`
    - `MedicalTechnicianFormModel.cs`
    - `UnitCommanderFormModel.cs`
    - `WingJudgeAdvocateFormModel.cs`
    - `WingCommanderFormModel.cs`
    - `AppointingAuthorityFormModel.cs`
    - `LineOfDutyBoardFormModel.cs` (contains 4 board classes)
    - `CaseDialogueFormModel.cs`
    - `CaseNotificationsFormModel.cs`
    - `CaseDocumentsFormModel.cs`
    - `CaseInfoModel.cs`
    - `CaseViewModelsDto.cs`
    - `MedicalAssessmentMapper.cs` (Mapperly, inlined into manual mapper)

## Phase 6: Verify

23. Build: `dotnet build ECTSystem.slnx` — must produce 0 errors
24. Run the app and navigate to EditCase page — verify form loads, bindings work, dirty tracking works

---

## Option A: Per-Tab Dirty Tracking via `[FormSection]` Attribute

### Design

Each property on `LineOfDutyViewModel` is decorated with a `[FormSection("SectionName")]` attribute identifying which tab/section it belongs to. The `TrackableModel` base class gains a new `IsDirtySection(string sectionName)` method that compares only properties in the specified section against their snapshot values.

### Implementation

**`FormSectionAttribute.cs`:**
```csharp
namespace ECTSystem.Shared.ViewModels;

[AttributeUsage(AttributeTargets.Property)]
public sealed class FormSectionAttribute : Attribute
{
    public string SectionName { get; }
    public FormSectionAttribute(string sectionName) => SectionName = sectionName;
}
```

**`TrackableModel` additions:**
```csharp
public bool IsDirtySection(string sectionName)
{
    if (_snapshot is null) return false;

    var snapshotObj = JsonSerializer.Deserialize(
        _snapshot, GetType(), _jsonOptions);
    if (snapshotObj is null) return false;

    var sectionProps = GetType()
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.GetCustomAttribute<FormSectionAttribute>()?.SectionName == sectionName)
        .ToList();

    foreach (var prop in sectionProps)
    {
        var currentVal = prop.GetValue(this);
        var snapshotVal = prop.GetValue(snapshotObj);

        if (!Equals(currentVal, snapshotVal))
            return true;
    }

    return false;
}
```

**Usage in `LineOfDutyViewModel`:**
```csharp
[FormSection("MemberInfo")]
public string LastName { get; set; }

[FormSection("MemberInfo")]
public string FirstName { get; set; }

[FormSection("MedicalAssessment")]
[Required(ErrorMessage = "Investigation type is required")]
public IncidentType? InvestigationType { get; set; }
```

**Usage in `EditCase.razor`:**
```razor
@* Tab header dirty indicator *@
<span>Member Info @(_viewModel.IsDirtySection("MemberInfo") ? " •" : "")</span>
```

### Section Names

| Section Name | Source Model | Tab |
|---|---|---|
| `CaseInfo` | `CaseInfoModel` | Case header (read-only) |
| `MemberInfo` | `MemberInfoFormModel` | Member Info tab |
| `MedicalAssessment` | `MedicalAssessmentFormModel` | Medical Assessment tab |
| `MedicalTechnician` | `MedicalTechnicianFormModel` | Medical Tech tab |
| `UnitCommander` | `UnitCommanderFormModel` | Commander tab |
| `WingJudgeAdvocate` | `WingJudgeAdvocateFormModel` | Wing JA tab |
| `WingCommander` | `WingCommanderFormModel` | Wing CC tab |
| `AppointingAuthority` | `AppointingAuthorityFormModel` | Appointing Authority tab |
| `BoardTechnician` | `BoardTechnicianFormModel` | Board Tech tab |
| `BoardMedical` | `BoardMedicalFormModel` | Board Med tab |
| `BoardLegal` | `BoardLegalFormModel` | Board Legal tab |
| `BoardAdmin` | `BoardAdminFormModel` | Board Admin tab |
| `CaseDialogue` | `CaseDialogueFormModel` | Case Dialogue tab |
| `CaseNotifications` | `CaseNotificationsFormModel` | Notifications tab |
| `CaseDocuments` | `CaseDocumentsFormModel` | Documents tab |

### Performance Note

`IsDirtySection()` performs reflection on each call but caches are not needed for form-sized objects. If performance becomes a concern, the section property lists can be cached in a static `ConcurrentDictionary<(Type, string), PropertyInfo[]>`.

---

## Relevant Files

- `ECTSystem.Shared/ViewModels/*.cs` — all 16 files to consolidate/delete; create `LineOfDutyViewModel.cs` and `FormSectionAttribute.cs`
- `ECTSystem.Shared/ViewModels/TrackableModel.cs` — add `IsDirtySection()` method
- `ECTSystem.Shared/Mapping/LineOfDutyCaseMapper.cs` — rewrite To*/Apply* methods → `ToLineOfDutyViewModel` / `ApplyToCase`
- `ECTSystem.Shared/Mapping/MedicalAssessmentMapper.cs` — inline and delete
- `ECTSystem.Web/Pages/EditCase.razor.cs` — replace 15 field declarations with 1, update all method references
- `ECTSystem.Web/Pages/EditCase.razor` — update ~90+ data binding expressions and 4 `RadzenTemplateForm` TItem types

---

## Decisions

1. **`CaseInfoModel` merges into `LineOfDutyViewModel`** — it currently doesn't extend `TrackableModel` and is read-only, but folding it in is simpler than maintaining a separate class. Its properties (e.g., `CaseNumber`, `Status`) become part of the unified model. Tagged with `[FormSection("CaseInfo")]`.

2. **Per-tab dirty tracking is preserved** — via `[FormSection]` attribute on each property and `IsDirtySection(string)` method on `TrackableModel`. Each tab can check `_viewModel.IsDirtySection("SectionName")` independently.

3. **`CaseViewModelsDto` is eliminated** — the API doesn't use it (view models are client-only, the API works with `LineOfDutyCase` directly). It was only a local DTO inside EditCase.

4. **`MedicalAssessmentMapper` (Mapperly) is inlined and deleted** — the Mapperly mapper would need excessive `[MapperIgnore]` attributes for the ~60 other properties on the consolidated model, making it more complex than manual mapping. The ~20 direct property copies are inlined into the manual mapper.

## Further Considerations

1. **Reflection caching for `IsDirtySection()`:** If per-tab dirty checks are called frequently (e.g., on every render), consider caching the `PropertyInfo[]` per section name in a static dictionary. For now, reflection cost is negligible for ~20 properties per section.

2. **Snapshot performance:** `TrackableModel.IsDirty` serializes the entire object for comparison. With ~83 properties (vs. the previous max of ~30 per model), each check is slightly more expensive. Still negligible for a form-sized object. No action needed.

3. **Constants for section names:** Consider defining section name constants (e.g., `FormSections.MemberInfo = "MemberInfo"`) to avoid magic strings and enable compile-time checking of typos.
