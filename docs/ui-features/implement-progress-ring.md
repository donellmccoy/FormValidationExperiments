# Implementation Plan: Section Completeness Progress Ring

## Overview

Add a circular progress indicator and percentage badge to each `RadzenFieldset`
header in the LOD workflow forms. The ring shows at-a-glance what percentage of
required fields in that section are filled, giving users a visual sense of
completeness as they work through each form section.

## Reference

- **Recommendation:** [state-machine-recommendations.md](state-machine-recommendations.md) §7, Style 3
- **Working demo:** `ECTSystem.Web/Pages/ValidatorDemo.razor` — Style 3 section
- **Demo CSS:** `ECTSystem.Web/Pages/ValidatorDemo.razor.css` — `.section-card`

## Current State

- Fieldset headers use plain `<RadzenText>` labels with no completeness
  indicators.
- `LineOfDutyViewModel` has `[FormSection]` attributes that group properties by
  section — this mapping can be leveraged to compute completeness.
- The demo page shows the pattern working with a manually computed percentage.

## Target Behavior

| Completeness | Ring Color | Badge Color | Badge Text |
|-------------|-----------|-------------|------------|
| 0% | Primary (blue) | Warning (amber) | `0%` |
| 1–99% | Primary (blue) | Warning (amber) | `XX%` |
| 100% | Success (green) | Success (green) | `100%` |

The ring and badge update in real time as users fill or clear fields.

## Implementation Steps

### Step 1 — Create a Section Completeness Calculator

Build a service that uses `[FormSection]` and `[Required]` attributes to compute
the percentage of required fields that are filled for a given section.

**File:** `ECTSystem.Web/Helpers/SectionCompletenessHelper.cs` (new)

```csharp
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Web.Helpers;

public static class SectionCompletenessHelper
{
    // Cache: section name → list of (PropertyInfo, RequiredAttribute?)
    private static readonly Dictionary<string, List<PropertyInfo>> SectionRequiredProps = BuildMap();

    /// <summary>
    /// Returns the completeness percentage (0–100) for a given form section.
    /// Only counts properties with [Required] that belong to the section.
    /// </summary>
    public static double GetCompleteness(LineOfDutyViewModel viewModel, string sectionName)
    {
        if (!SectionRequiredProps.TryGetValue(sectionName, out var props) || props.Count == 0)
            return 100; // No required fields = fully complete

        var filled = 0;
        foreach (var prop in props)
        {
            var value = prop.GetValue(viewModel);
            if (!IsEmpty(value))
                filled++;
        }

        return (double)filled / props.Count * 100;
    }

    /// <summary>
    /// Returns the count of unfilled required fields for a section.
    /// </summary>
    public static int GetMissingCount(LineOfDutyViewModel viewModel, string sectionName)
    {
        if (!SectionRequiredProps.TryGetValue(sectionName, out var props))
            return 0;

        return props.Count(p => IsEmpty(p.GetValue(viewModel)));
    }

    /// <summary>
    /// Returns the total number of required fields for a section.
    /// </summary>
    public static int GetTotalRequired(string sectionName)
        => SectionRequiredProps.TryGetValue(sectionName, out var props) ? props.Count : 0;

    private static bool IsEmpty(object? value)
        => value is null || (value is string s && string.IsNullOrWhiteSpace(s));

    private static Dictionary<string, List<PropertyInfo>> BuildMap()
    {
        var map = new Dictionary<string, List<PropertyInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in typeof(LineOfDutyViewModel).GetProperties())
        {
            if (prop.GetCustomAttribute<RequiredAttribute>() is null) continue;
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

### Step 2 — Add Shared CSS for Section Cards

**File:** `ECTSystem.Web/wwwroot/css/validation-indicators.css` (append)

```css
/* ── Section Completeness Progress Ring ── */
.section-card {
    border: 1px solid var(--rz-base-700);
    border-radius: var(--rz-border-radius);
}
```

### Step 3 — Refactor Fieldset Headers in `EditCase.razor`

Replace plain `HeaderTemplate` content with the progress ring + badge pattern.

**Before (typical fieldset header in EditCase.razor):**

```razor
<RadzenFieldset>
    <HeaderTemplate>
        <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.Span">
            Item 9 — Type of Investigation
        </RadzenText>
    </HeaderTemplate>
    ...
</RadzenFieldset>
```

**After:**

```razor
<RadzenFieldset class="section-card">
    <HeaderTemplate>
        <RadzenStack Orientation="Orientation.Horizontal"
                     AlignItems="AlignItems.Center" Gap="0.75rem">
            <RadzenProgressBarCircular
                Value="@SectionCompletenessHelper.GetCompleteness(_viewModel, "MedicalAssessment")"
                Size="ProgressBarCircularSize.Small"
                ShowValue="false"
                ProgressBarStyle="@(SectionCompletenessHelper.GetCompleteness(_viewModel, "MedicalAssessment") == 100
                    ? ProgressBarStyle.Success : ProgressBarStyle.Primary)" />
            <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.Span">
                Item 9 — Type of Investigation
            </RadzenText>
            <RadzenBadge
                Text="@($"{SectionCompletenessHelper.GetCompleteness(_viewModel, "MedicalAssessment"):0}%")"
                BadgeStyle="@(SectionCompletenessHelper.GetCompleteness(_viewModel, "MedicalAssessment") == 100
                    ? BadgeStyle.Success : BadgeStyle.Warning)"
                IsPill="true" />
        </RadzenStack>
    </HeaderTemplate>
    ...
</RadzenFieldset>
```

### Step 4 — Avoid Repeated Calculation in Render

Calling `GetCompleteness()` three times for the same section in one header is
wasteful. Cache the value in the code-behind:

**In `EditCase.razor.cs`:**

```csharp
private readonly Dictionary<string, double> _sectionCompleteness = new();

private void RefreshCompleteness()
{
    string[] sections = ["MemberInfo", "MedicalAssessment", "UnitCommander",
                         "WingCommander", "WingJudgeAdvocate"];
    foreach (var section in sections)
    {
        _sectionCompleteness[section] =
            SectionCompletenessHelper.GetCompleteness(_viewModel, section);
    }
}
```

Call `RefreshCompleteness()` in `OnParametersSet` and after each field change.
Then use `_sectionCompleteness["MedicalAssessment"]` in the template.

### Step 5 — Create a Reusable `SectionHeader` Component (Optional)

To avoid repeating the ring+badge markup for every fieldset, extract a component:

**File:** `ECTSystem.Web/Shared/SectionHeader.razor` (new)

```razor
<RadzenStack Orientation="Orientation.Horizontal"
             AlignItems="AlignItems.Center" Gap="0.75rem">
    <RadzenProgressBarCircular Value="@Completeness"
                               Size="ProgressBarCircularSize.Small"
                               ShowValue="false"
                               ProgressBarStyle="@(Completeness == 100
                                   ? ProgressBarStyle.Success
                                   : ProgressBarStyle.Primary)" />
    <RadzenText TextStyle="TextStyle.Subtitle1" TagName="TagName.Span">
        @Title
    </RadzenText>
    <RadzenBadge Text="@($"{Completeness:0}%")"
                 BadgeStyle="@(Completeness == 100
                     ? BadgeStyle.Success : BadgeStyle.Warning)"
                 IsPill="true" />
</RadzenStack>
```

**File:** `ECTSystem.Web/Shared/SectionHeader.razor.cs` (new)

```csharp
using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Shared;

public partial class SectionHeader
{
    [Parameter, EditorRequired]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public double Completeness { get; set; }
}
```

**Usage:**

```razor
<RadzenFieldset class="section-card">
    <HeaderTemplate>
        <SectionHeader Title="Item 9 — Type of Investigation"
                       Completeness="@_sectionCompleteness["MedicalAssessment"]" />
    </HeaderTemplate>
    ...
</RadzenFieldset>
```

### Step 6 — Map All Fieldsets to Sections

Each tab contains multiple `RadzenFieldset` groups. Map each to the appropriate
`[FormSection]` name:

| Tab | Fieldset Title | Section Name |
|-----|---------------|-------------|
| Member Information | Items 1–4, Items 5–8 | `MemberInfo` |
| Medical Technician | Treatment Data | `MedicalAssessment` |
| Medical Officer | Items 9–15 | `MedicalAssessment` |
| Unit Commander | Items 16–23 | `UnitCommander` |
| Wing JA Review | Legal Review | `WingJudgeAdvocate` |
| Wing Commander | Items 24–25 | `WingCommander` |
| Appointing Authority | Authority Review | `AppointingAuthority` |
| Board Tabs | Board Review Sections | `BoardReview` |

**Note:** Some sections may need new `[FormSection]` attributes added to
`LineOfDutyViewModel` properties (e.g., `WingJudgeAdvocate`,
`AppointingAuthority`, `BoardReview`).

### Step 7 — Handle Conditional Fields

Some required fields are conditionally visible (e.g., `SubstanceType` is only
required when `WasUnderInfluence == true`). The completeness calculator should
account for conditional requirements:

```csharp
// In SectionCompletenessHelper — override for conditional fields
public static double GetCompleteness(
    LineOfDutyViewModel viewModel, string sectionName)
{
    // ... existing logic ...

    // Exclude conditionally-hidden fields from totals
    // e.g., SubstanceType is only required when ShowSubstanceType is true
    if (sectionName == "MedicalAssessment" && !viewModel.ShowSubstanceType)
        excludedProps.Add(nameof(viewModel.SubstanceType));
    // ... similar for other conditional fields ...
}
```

This can be implemented via a callback or a configuration dictionary mapping
property names to visibility conditions.

## Testing Checklist

- [ ] Progress ring shows 0% when all required fields in a section are empty.
- [ ] Progress ring shows 100% (green) when all required fields are filled.
- [ ] Partial completion shows proportional fill and amber badge.
- [ ] Ring updates in real time as fields are filled/cleared.
- [ ] Conditional fields are correctly excluded when hidden.
- [ ] `SectionHeader` component renders correctly in all fieldset headers.
- [ ] Progress ring renders correctly at small size in Material 3 Dark theme.
- [ ] Sections with no required fields show 100% by default.

## Files Changed

| File | Change |
|------|--------|
| `ECTSystem.Web/Helpers/SectionCompletenessHelper.cs` | **New** — completeness calculator |
| `ECTSystem.Web/Shared/SectionHeader.razor` | **New** — reusable header component |
| `ECTSystem.Web/Shared/SectionHeader.razor.cs` | **New** — header code-behind |
| `wwwroot/css/validation-indicators.css` | Add `.section-card` styles |
| `ECTSystem.Web/Pages/EditCase.razor` | Update all fieldset `HeaderTemplate`s |
| `ECTSystem.Web/Pages/EditCase.razor.cs` | Add `_sectionCompleteness` caching |
| `ECTSystem.Shared/ViewModels/LineOfDutyViewModel.cs` | Add missing `[FormSection]` attributes |

## Dependencies

- Requires `[FormSection]` and `[Required]` attributes on all view model
  properties (partially done; needs extension per Recommendation 7 Option A).
- Pairs well with **Step-Level Badges** (Style 5) — section completeness feeds
  into the overall step validation count on the sidebar.
