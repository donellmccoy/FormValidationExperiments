# Implementation Plan: Pulse-Glow Required Field Indicator

## Overview

Add an animated border glow to unfilled required fields across all LOD workflow
form tabs. The glow pulses subtly to draw attention without being disruptive, and
stops immediately when the user fills the field.

## Reference

- **Recommendation:** [state-machine-recommendations.md](state-machine-recommendations.md) §7, Style 1
- **Working demo:** `ECTSystem.Web/Pages/ValidatorDemo.razor` — Style 1 section
- **Demo CSS:** `ECTSystem.Web/Pages/ValidatorDemo.razor.css` — `.pulse-required`

## Current State

- Only 1 of 11 `RadzenTemplateForm` instances has any validation
  (`RadzenDataAnnotationValidator` on the Medical Officer tab).
- No visual indicators exist on empty required fields in production forms.
- `LineOfDutyViewModel` already has `[Required]` attributes on 16+ properties
  and `[FormSection]` attributes mapping properties to workflow sections.

## Target Behavior

| State | Visual |
|-------|--------|
| Required field is **empty** and **not focused** | 2-second pulsing red glow (box-shadow) |
| Required field is **empty** and **focused** | Glow stops; normal focus ring appears |
| Required field has a **value** | No glow — normal appearance |
| Optional field (no `[Required]`) | Never glows |

## Implementation Steps

### Step 1 — Move CSS to a Shared Location

The `.pulse-required` class currently lives in `ValidatorDemo.razor.css`
(component-scoped). Move it to a shared stylesheet so all form pages can use it.

**File:** `ECTSystem.Web/wwwroot/css/validation-indicators.css` (new)

```css
/* ── Pulse-Glow Required Field Indicator ── */
.pulse-required {
    animation: pulse-required 2s ease-in-out infinite;
    border-radius: var(--rz-border-radius);
}

@keyframes pulse-required {
    0%, 100% { box-shadow: 0 0 0 0 rgba(255, 82, 82, 0); }
    50%      { box-shadow: 0 0 0 5px rgba(255, 82, 82, 0.18); }
}
```

**File:** `wwwroot/index.html` — add stylesheet reference:

```html
<link rel="stylesheet" href="css/validation-indicators.css" />
```

### Step 2 — Create a Validation Helper Service

Build a reusable service that determines whether a given property requires a
glow, based on `[Required]` and `[FormSection]` attributes on
`LineOfDutyViewModel`.

**File:** `ECTSystem.Web/Helpers/RequiredFieldHelper.cs` (new)

```csharp
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Web.Helpers;

public static class RequiredFieldHelper
{
    private static readonly Dictionary<string, HashSet<string>> SectionRequiredFields = BuildMap();

    /// <summary>
    /// Returns the CSS class to apply to a <c>RadzenFormField</c>.
    /// Returns <c>"pulse-required"</c> when the field is required and empty;
    /// otherwise returns an empty string.
    /// </summary>
    public static string GlowClass(string propertyName, object? value)
    {
        if (!IsRequired(propertyName))
            return string.Empty;

        var isEmpty = value is null
                      || (value is string s && string.IsNullOrWhiteSpace(s));

        return isEmpty ? "pulse-required" : string.Empty;
    }

    /// <summary>
    /// Returns all required property names for a given form section.
    /// </summary>
    public static IReadOnlySet<string> RequiredFieldsForSection(string sectionName)
        => SectionRequiredFields.TryGetValue(sectionName, out var set)
            ? set
            : new HashSet<string>();

    private static bool IsRequired(string propertyName)
        => typeof(LineOfDutyViewModel)
            .GetProperty(propertyName)?
            .GetCustomAttribute<RequiredAttribute>() is not null;

    private static Dictionary<string, HashSet<string>> BuildMap()
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in typeof(LineOfDutyViewModel).GetProperties())
        {
            if (prop.GetCustomAttribute<RequiredAttribute>() is null) continue;
            foreach (var fs in prop.GetCustomAttributes<FormSectionAttribute>())
            {
                if (!map.TryGetValue(fs.SectionName, out var set))
                {
                    set = new HashSet<string>();
                    map[fs.SectionName] = set;
                }
                set.Add(prop.Name);
            }
        }
        return map;
    }
}
```

### Step 3 — Apply to `RadzenFormField` Components in `EditCase.razor`

For each required field, conditionally apply the `pulse-required` CSS class on
the `RadzenFormField` wrapper. Example for the Medical Assessment tab:

**Before:**

```razor
<RadzenFormField Text="Clinical Diagnosis" AllowFloatingLabel="false"
                 Variant="Variant.Outlined" class="rz-w-100">
    <ChildContent>
        <RadzenTextBox @bind-Value="_viewModel.ClinicalDiagnosis" />
    </ChildContent>
</RadzenFormField>
```

**After:**

```razor
<RadzenFormField Text="Clinical Diagnosis" AllowFloatingLabel="false"
                 Variant="Variant.Outlined"
                 class="@($"rz-w-100 {RequiredFieldHelper.GlowClass(
                     nameof(_viewModel.ClinicalDiagnosis), _viewModel.ClinicalDiagnosis)}")">
    <ChildContent>
        <RadzenTextBox @bind-Value="_viewModel.ClinicalDiagnosis" />
    </ChildContent>
</RadzenFormField>
```

Repeat for every field whose property has `[Required]` across all 11 tabs.

### Step 4 — Wire Up Real-Time Updates

The glow class re-evaluates on every render. Ensure fields using `@bind-Value`
trigger re-render automatically (they do by default in Blazor). For dropdowns
and date pickers that use `Change` callbacks, call `StateHasChanged()` if not
already triggered.

### Step 5 — Tab-Scoped Application Order

Apply the glow to each tab's required fields in this order (matching the
existing `[FormSection]` groupings):

| Tab | Section Name | Required Fields |
|-----|-------------|-----------------|
| Member Information | `MemberInfo` | LastName, FirstName, DateOfBirth, Rank, etc. |
| Medical Technician | `MedicalAssessment` | TreatmentDateTime, ClinicalDiagnosis |
| Medical Officer | `MedicalAssessment` | InvestigationType, IsMilitaryFacility, WasUnderInfluence, etc. |
| Unit Commander | `UnitCommander` | Recommendation, NarrativeOfCircumstances |
| Wing JA Review | (to be annotated) | Legal sufficiency fields |
| Wing Commander | `WingCommander` | IsLegallySufficient, ConcurWithRecommendation |
| Board tabs | (to be annotated) | Board-specific required fields |

### Step 6 — Accessibility

- The glow is purely decorative (box-shadow). Screen readers ignore it.
- Ensure every required field also has `aria-required="true"` on the input
  element (Radzen sets this automatically when a `RadzenRequiredValidator` is
  present — add validators in tandem).
- The glow does **not** replace error messages — it supplements them.

## Testing Checklist

- [ ] Empty required field on each tab shows the pulsing glow.
- [ ] Filling the field stops the glow immediately (no page reload needed).
- [ ] Focused empty required field does NOT glow (`:focus` suppression).
- [ ] Optional fields never glow regardless of value.
- [ ] Glow renders correctly in Material 3 Dark theme.
- [ ] Glow does not interfere with Radzen's built-in focus/error border styles.
- [ ] `RequiredFieldHelper.GlowClass` returns `""` for non-existent property names.

## Files Changed

| File | Change |
|------|--------|
| `wwwroot/css/validation-indicators.css` | **New** — shared validation CSS |
| `wwwroot/index.html` | Add stylesheet link |
| `ECTSystem.Web/Helpers/RequiredFieldHelper.cs` | **New** — reflection-based helper |
| `ECTSystem.Web/Pages/EditCase.razor` | Add `pulse-required` class to required `RadzenFormField`s |
| `ECTSystem.Web/Pages/ValidatorDemo.razor.css` | Remove `.pulse-required` (moved to shared) |

## Dependencies

- None. This indicator can be implemented independently of other styles.
- Pairs well with **Inline Status Chips** (Style 2) for complementary feedback.
