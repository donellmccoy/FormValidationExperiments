# Implementation Plan: Step-Level Validation Badges (Workflow Sidebar)

## Overview

Add a compact `RadzenBadge` to each step in the `WorkflowSidebar` that displays
the count of outstanding validation errors for the corresponding form section.
The badge provides a birds-eye view of which workflow steps still need attention
without the user having to click into each tab.

## Reference

- **Recommendation:** [state-machine-recommendations.md](state-machine-recommendations.md) §7, Style 5
- **Working demo:** `ECTSystem.Web/Pages/ValidatorDemo.razor` — Style 5 section
- **Demo CSS:** `ECTSystem.Web/Pages/ValidatorDemo.razor.css` —
  `.step-validation-badge`

## Current State

- `WorkflowSidebar.razor` renders 12 steps with status indicators
  (`Completed` / `InProgress` / `Pending`) and date metadata — no validation
  feedback.
- `WorkflowStep` class has workflow-tracking properties (`Status`, `StartDate`,
  `CompletedDate`, `DaysInProcess`, etc.) but no validation-related properties.
- `WorkflowSidebar` receives a `LineOfDutyCase` parameter; it does **not**
  receive the `LineOfDutyViewModel` or any validation state.
- `LineOfDutyViewModel` has `[FormSection]` attributes mapping properties to
  section names, and `[Required]` / `IValidatableObject` rules.
- No mapping exists between `WorkflowState` enum values and `[FormSection]`
  section names.

## Target Behavior

| Badge State | Appearance | Condition |
|------------|------------|-----------|
| No badge | Hidden | Step has zero validation errors |
| Error badge | Red pill badge with count (e.g., `3`) | Step has 1+ validation errors |
| Optional: all-clear | Green check badge | Step had errors, now all resolved |

Badges update whenever the view model changes (field edits, tab switches,
explicit validation triggers). They are visible on all steps — not just the
current step — so the user sees the full picture.

## Implementation Steps

### Step 1 — Add a Validation Error Count to `WorkflowStep`

Extend the existing `WorkflowStep` class with a property to hold the count:

**File:** `ECTSystem.Web/Shared/WorkflowStep.cs`

```csharp
// Add these properties to the existing WorkflowStep class:

/// <summary>
/// Number of outstanding validation errors for this step's form section.
/// A value of 0 hides the badge; a positive value shows it.
/// </summary>
public int ValidationErrorCount { get; set; }
```

### Step 2 — Create a WorkflowState ↔ Section Name Mapping

A lookup is needed to map each `WorkflowState` to the `[FormSection]` section
name(s) used in `LineOfDutyViewModel`.

**File:** `ECTSystem.Web/Helpers/WorkflowSectionMapping.cs` (new)

```csharp
using ECTSystem.Shared.Enums;

namespace ECTSystem.Web.Helpers;

/// <summary>
/// Maps WorkflowState values to the FormSection names used on
/// LineOfDutyViewModel. A step may map to multiple sections
/// (e.g., a board review step that covers several field groups).
/// </summary>
public static class WorkflowSectionMapping
{
    private static readonly Dictionary<WorkflowState, string[]> Map = new()
    {
        [WorkflowState.MemberInformationEntry]        = ["MemberInfo"],
        [WorkflowState.MedicalTechnicianReview]       = ["MedicalAssessment"],
        [WorkflowState.MedicalOfficerReview]          = ["MedicalAssessment"],
        [WorkflowState.UnitCommanderReview]           = ["UnitCommander"],
        [WorkflowState.WingJudgeAdvocateReview]       = ["WingJudgeAdvocate"],
        [WorkflowState.AppointingAuthorityReview]     = ["AppointingAuthority"],
        [WorkflowState.WingCommanderReview]           = ["WingCommander"],
        [WorkflowState.BoardMedicalTechnicianReview]  = ["BoardReview"],
        [WorkflowState.BoardMedicalOfficerReview]     = ["BoardReview"],
        [WorkflowState.BoardLegalReview]              = ["BoardReview"],
        [WorkflowState.BoardAdministratorReview]      = ["BoardReview"],
        [WorkflowState.Completed]                     = [],
    };

    /// <summary>
    /// Returns the FormSection names associated with a WorkflowState.
    /// Returns an empty array for states with no validation-relevant section.
    /// </summary>
    public static string[] GetSections(WorkflowState state)
        => Map.TryGetValue(state, out var sections) ? sections : [];
}
```

### Step 3 — Create a Step-Level Validation Service

Build a service that validates all required fields per section and returns error
counts keyed by `WorkflowState`.

**File:** `ECTSystem.Web/Helpers/StepValidationHelper.cs` (new)

```csharp
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Web.Helpers;

/// <summary>
/// Computes validation error counts per WorkflowState using
/// FormSection mappings and DataAnnotation / IValidatableObject rules.
/// </summary>
public static class StepValidationHelper
{
    // Cache: section name → list of PropertyInfo with [Required]
    private static readonly Dictionary<string, List<PropertyInfo>> SectionProps = BuildSectionMap();

    /// <summary>
    /// Returns validation error counts keyed by WorkflowState.
    /// Only states with at least one error are included.
    /// </summary>
    public static Dictionary<WorkflowState, int> GetErrorCounts(
        LineOfDutyViewModel viewModel)
    {
        var result = new Dictionary<WorkflowState, int>();

        // 1. Collect all DataAnnotation + IValidatableObject errors
        var context = new ValidationContext(viewModel);
        var allErrors = new List<ValidationResult>();
        Validator.TryValidateObject(viewModel, context, allErrors,
            validateAllProperties: true);
        allErrors.AddRange(viewModel.Validate(context));

        // 2. Build a member-name → error count lookup
        var errorsByMember = new Dictionary<string, int>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var error in allErrors)
        {
            foreach (var member in error.MemberNames)
            {
                errorsByMember[member] =
                    errorsByMember.GetValueOrDefault(member) + 1;
            }
        }

        // 3. Also count empty required fields (they may not produce a
        //    ValidationResult if TryValidateObject was not called)
        foreach (var (section, props) in SectionProps)
        {
            foreach (var prop in props)
            {
                var value = prop.GetValue(viewModel);
                if (value is null || (value is string s &&
                    string.IsNullOrWhiteSpace(s)))
                {
                    // Ensure this is counted even if TryValidateObject
                    // didn't flag it
                    errorsByMember.TryAdd(prop.Name, 1);
                }
            }
        }

        // 4. Map errors to WorkflowStates
        foreach (WorkflowState state in Enum.GetValues<WorkflowState>())
        {
            var sections = WorkflowSectionMapping.GetSections(state);
            if (sections.Length == 0) continue;

            var count = 0;
            foreach (var section in sections)
            {
                if (!SectionProps.TryGetValue(section, out var props))
                    continue;
                foreach (var prop in props)
                {
                    if (errorsByMember.ContainsKey(prop.Name))
                        count++;
                }
            }

            if (count > 0)
                result[state] = count;
        }

        return result;
    }

    private static Dictionary<string, List<PropertyInfo>> BuildSectionMap()
    {
        var map = new Dictionary<string, List<PropertyInfo>>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var prop in typeof(LineOfDutyViewModel).GetProperties())
        {
            if (prop.GetCustomAttribute<RequiredAttribute>() is null)
                continue;
            foreach (var fs in prop.GetCustomAttributes<FormSectionAttribute>())
            {
                if (!map.TryGetValue(fs.SectionName, out var list))
                {
                    list = [];
                    map[fs.SectionName] = list;
                }
                list.Add(prop);
            }
        }
        return map;
    }
}
```

### Step 4 — Pass Validation Data into `WorkflowSidebar`

Add a new parameter to `WorkflowSidebar` that receives the error counts from the
parent `EditCase` page.

**File:** `ECTSystem.Web/Shared/WorkflowSidebar.razor.cs`

```csharp
// Add this parameter to WorkflowSidebar:

/// <summary>
/// Validation error counts keyed by WorkflowState.
/// Populated by the parent page via StepValidationHelper.
/// An empty dictionary hides all badges.
/// </summary>
[Parameter]
public Dictionary<WorkflowState, int> ValidationErrors { get; set; } = [];
```

Update `OnParametersSet` or `ApplyWorkflowState` to apply the counts:

```csharp
// At the end of ApplyWorkflowState():
foreach (var step in Steps)
{
    step.ValidationErrorCount = ValidationErrors
        .GetValueOrDefault(step.WorkflowState);
}
```

### Step 5 — Render the Badge in `WorkflowSidebar.razor`

Add a `RadzenBadge` next to the step name when the error count is > 0.

**In `WorkflowSidebar.razor`, inside the `.step-name-row` div:**

```razor
<div class="step-name-row">
    <span class="step-name">@step.Name</span>

    @if (step.ValidationErrorCount > 0)
    {
        <RadzenBadge Text="@step.ValidationErrorCount.ToString()"
                     BadgeStyle="BadgeStyle.Danger"
                     IsPill="true"
                     class="step-validation-badge" />
    }

    @if (step.Status == WorkflowStepStatus.InProgress)
    {
        <span class="material-symbols-outlined step-action-icon active">edit</span>
    }
    else if (step.Status == WorkflowStepStatus.Completed)
    {
        <span class="material-symbols-outlined step-action-icon">info</span>
    }
</div>
```

### Step 6 — Add Badge CSS

**File:** `ECTSystem.Web/wwwroot/css/validation-indicators.css` (append)

```css
/* ── Step-Level Validation Badges ── */
.step-validation-badge {
    font-size: 0.65rem;
    min-width: 1.25rem;
    height: 1.25rem;
    padding: 0 0.375rem;
    margin-left: auto;
    flex-shrink: 0;
    animation: badge-pop 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
}

@keyframes badge-pop {
    from { transform: scale(0); }
    to   { transform: scale(1); }
}
```

### Step 7 — Wire Up in `EditCase.razor.cs`

Compute error counts and pass them to the sidebar.

**In `EditCase.razor.cs`:**

```csharp
private Dictionary<WorkflowState, int> _stepErrors = new();

private void RefreshStepValidation()
{
    _stepErrors = StepValidationHelper.GetErrorCounts(_viewModel);
}
```

Call `RefreshStepValidation()`:
- In `OnParametersSet` / after loading a case.
- After `SaveTabFormDataAsync` completes.
- After any field change (debounced or on blur).

**In `EditCase.razor`:**

```razor
<WorkflowSidebar LineOfDutyCase="@_lodCase"
                 ValidationErrors="@_stepErrors"
                 OnStepClicked="@HandleStepClicked" />
```

### Step 8 — Handle Edge Cases

1. **Pending steps:** Show the badge even for future steps so the user knows
   what's coming. This is especially useful for steps that share sections (e.g.,
   Medical Technician and Medical Officer both map to `MedicalAssessment`).

2. **Completed steps:** Optionally suppress badges on steps whose
   `WorkflowStepStatus` is `Completed`, since they've already been signed off.
   This is a UX decision — discuss with stakeholders.

3. **Shared sections:** Medical Technician and Medical Officer both map to
   `MedicalAssessment`. Decide whether both get the same count or each gets a
   sub-section count. The simplest approach: both show the same aggregate count.

4. **Completed step → 0 badge transition:** When errors drop to zero, animate
   the badge out. The existing `badge-pop` animation handles appearance; for
   disappearance, rely on Blazor removing the element (no explicit exit
   animation needed unless desired).

### Step 9 — Add Tooltip with Error Summary (Optional Enhancement)

Show a tooltip on hover that lists the specific errors:

```razor
@if (step.ValidationErrorCount > 0)
{
    <RadzenTooltip>
        <ChildContent>
            <RadzenBadge Text="@step.ValidationErrorCount.ToString()"
                         BadgeStyle="BadgeStyle.Danger" IsPill="true"
                         class="step-validation-badge" />
        </ChildContent>
        <TooltipTemplate>
            <RadzenText TextStyle="TextStyle.Caption">
                @step.ValidationErrorCount required field(s) need attention
            </RadzenText>
        </TooltipTemplate>
    </RadzenTooltip>
}
```

## Testing Checklist

- [ ] Badge appears with danger style and correct count on steps with errors.
- [ ] Badge is hidden when a step has zero errors.
- [ ] Badge animates in with the `badge-pop` keyframe.
- [ ] Badge count updates in real time as fields are filled/cleared.
- [ ] Shared-section steps (Medical Technician / Medical Officer) show
      consistent counts.
- [ ] Completed steps either suppress the badge or retain it per the chosen UX
      policy.
- [ ] Badge renders correctly in Material 3 Dark theme on the 325px sidebar.
- [ ] Progress bar at top of sidebar is not affected by badge additions.
- [ ] All 12 steps render correctly with and without badges.
- [ ] Sidebar step click navigation still works with badges present.
- [ ] `StepValidationHelper.GetErrorCounts()` performance is acceptable
      (~1ms per call for 16–30 properties).

## Files Changed

| File | Change |
|------|--------|
| `ECTSystem.Web/Shared/WorkflowStep.cs` | Add `ValidationErrorCount` property |
| `ECTSystem.Web/Helpers/WorkflowSectionMapping.cs` | **New** — `WorkflowState` → section name map |
| `ECTSystem.Web/Helpers/StepValidationHelper.cs` | **New** — per-step error count calculator |
| `ECTSystem.Web/Shared/WorkflowSidebar.razor.cs` | Add `ValidationErrors` parameter, apply in `ApplyWorkflowState` |
| `ECTSystem.Web/Shared/WorkflowSidebar.razor` | Add `<RadzenBadge>` inside `.step-name-row` |
| `wwwroot/css/validation-indicators.css` | Add `.step-validation-badge` + animation |
| `ECTSystem.Web/Pages/EditCase.razor.cs` | Add `_stepErrors`, `RefreshStepValidation()` |
| `ECTSystem.Web/Pages/EditCase.razor` | Pass `ValidationErrors` parameter to sidebar |
| `ECTSystem.Web/Pages/ValidatorDemo.razor.css` | Remove `.step-validation-badge` (moved to shared) |

## Dependencies

- Requires `[FormSection]` and `[Required]` attributes on all relevant
  `LineOfDutyViewModel` properties (partially done; extend per
  Recommendation 7 Option A).
- `WorkflowSectionMapping.cs` must be kept in sync with `WorkflowState` enum
  values and `[FormSection]` attribute values if either changes.
- **Prerequisite:** If implementing alongside **Progress Ring** (Style 3), the
  `SectionCompletenessHelper` can share the same cached property map as
  `StepValidationHelper` — consider extracting a shared
  `SectionPropertyCache` to avoid duplication.
- Pairs naturally with **Validation Summary** (Style 4) — the badge count draws
  the user to the problem step; the validation summary inside the tab provides
  the detail.
