# Implementation Plan: Inline Status Chip Indicators

## Overview

Replace or supplement default validation error text with compact, color-coded
status chips displayed next to field labels. Each chip shows the field's current
validation state — **Required**, **Valid**, **Invalid**, or **Optional** — with a
corresponding icon and color from the Material 3 Dark theme.

## Reference

- **Recommendation:** [state-machine-recommendations.md](state-machine-recommendations.md) §7, Style 2
- **Working demo:** `ECTSystem.Web/Pages/ValidatorDemo.razor` — Style 2 section
- **Demo CSS:** `ECTSystem.Web/Pages/ValidatorDemo.razor.css` — `.field-status*`

## Current State

- Field labels are rendered inside `RadzenFormField Text="..."` with no
  supplementary status indicators.
- No inline chips exist anywhere in the production forms.
- `LineOfDutyViewModel` has `[Required]`, `[StringLength]`, and
  `IValidatableObject` rules that can drive chip state.

## Target Behavior

| Chip Style | Color | Icon | Condition |
|-----------|-------|------|-----------|
| `field-status--required` | Warning (amber) | `priority_high` | Required field is empty |
| `field-status--valid` | Success (green) | `check_circle` | Field has a valid value |
| `field-status--invalid` | Danger (red) | `error_outline` | Field has a value but fails validation |
| `field-status--optional` | Base/muted (gray) | `do_not_disturb_on` | Field has no `[Required]` attribute |

Chips update in real time as the user types or changes selections.

## Implementation Steps

### Step 1 — Move CSS to a Shared Location

**File:** `ECTSystem.Web/wwwroot/css/validation-indicators.css` (append to existing or create)

```css
/* ── Inline Status Chip Indicators ── */
.field-status {
    display: inline-flex;
    align-items: center;
    gap: 0.25rem;
    padding: 0.125rem 0.5rem;
    border-radius: 1rem;
    font-size: 0.7rem;
    font-weight: 600;
    letter-spacing: 0.03em;
    text-transform: uppercase;
    transition: all 0.25s ease;
    white-space: nowrap;
}

.field-status--valid {
    background: color-mix(in srgb, var(--rz-success) 15%, transparent);
    color: var(--rz-success);
}

.field-status--invalid {
    background: color-mix(in srgb, var(--rz-danger) 15%, transparent);
    color: var(--rz-danger);
}

.field-status--required {
    background: color-mix(in srgb, var(--rz-warning) 15%, transparent);
    color: var(--rz-warning);
}

.field-status--optional {
    background: color-mix(in srgb, var(--rz-base-500) 15%, transparent);
    color: var(--rz-base-400);
}
```

### Step 2 — Create a Reusable `FieldStatusChip` Component

Encapsulate the chip markup in a small component so it can be dropped next to
any label without repeating the logic at every call site.

**File:** `ECTSystem.Web/Shared/FieldStatusChip.razor` (new)

```razor
@if (Status == FieldValidationStatus.Required)
{
    <span class="field-status field-status--required">
        <RadzenIcon Icon="priority_high" Style="font-size: 0.75rem;" /> Required
    </span>
}
else if (Status == FieldValidationStatus.Valid)
{
    <span class="field-status field-status--valid">
        <RadzenIcon Icon="check_circle" Style="font-size: 0.75rem;" /> Valid
    </span>
}
else if (Status == FieldValidationStatus.Invalid)
{
    <span class="field-status field-status--invalid">
        <RadzenIcon Icon="error_outline" Style="font-size: 0.75rem;" /> @Message
    </span>
}
else
{
    <span class="field-status field-status--optional">
        <RadzenIcon Icon="do_not_disturb_on" Style="font-size: 0.75rem;" /> Optional
    </span>
}
```

**File:** `ECTSystem.Web/Shared/FieldStatusChip.razor.cs` (new)

```csharp
using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Shared;

public partial class FieldStatusChip
{
    [Parameter, EditorRequired]
    public FieldValidationStatus Status { get; set; }

    [Parameter]
    public string Message { get; set; } = "Invalid";
}

public enum FieldValidationStatus
{
    Optional,
    Required,
    Valid,
    Invalid
}
```

### Step 3 — Create a Field-Status Evaluation Helper

Build a helper that evaluates a field's status by checking `[Required]`,
property value, and `IValidatableObject` errors.

**File:** `ECTSystem.Web/Helpers/FieldStatusHelper.cs` (new)

```csharp
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ECTSystem.Shared.ViewModels;
using ECTSystem.Web.Shared;

namespace ECTSystem.Web.Helpers;

public static class FieldStatusHelper
{
    /// <summary>
    /// Evaluates the validation status of a single property.
    /// </summary>
    public static (FieldValidationStatus Status, string? Message) Evaluate(
        LineOfDutyViewModel viewModel,
        string propertyName)
    {
        var prop = typeof(LineOfDutyViewModel).GetProperty(propertyName);
        if (prop is null) return (FieldValidationStatus.Optional, null);

        var isRequired = prop.GetCustomAttribute<RequiredAttribute>() is not null;
        var value = prop.GetValue(viewModel);
        var isEmpty = value is null || (value is string s && string.IsNullOrWhiteSpace(s));

        if (isEmpty)
        {
            return isRequired
                ? (FieldValidationStatus.Required, null)
                : (FieldValidationStatus.Optional, null);
        }

        // Validate the single property
        var context = new ValidationContext(viewModel) { MemberName = propertyName };
        var results = new List<ValidationResult>();
        Validator.TryValidateProperty(value, context, results);

        if (results.Count > 0)
            return (FieldValidationStatus.Invalid, results[0].ErrorMessage);

        return (FieldValidationStatus.Valid, null);
    }
}
```

### Step 4 — Change Label Rendering in `EditCase.razor`

The current pattern uses `RadzenFormField Text="..."` for labels. To add a chip,
extract the label into a separate row above the form field so the chip sits
inline with the label text.

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
<div>
    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                 Gap="0.5rem" Style="margin-bottom: 0.25rem;">
        <RadzenText TextStyle="TextStyle.Body1" Style="font-weight: 500;">
            Clinical Diagnosis
        </RadzenText>
        <FieldStatusChip Status="@FieldStatusHelper.Evaluate(_viewModel,
            nameof(_viewModel.ClinicalDiagnosis)).Status"
            Message="@(FieldStatusHelper.Evaluate(_viewModel,
            nameof(_viewModel.ClinicalDiagnosis)).Message ?? "Invalid")" />
    </RadzenStack>
    <RadzenFormField AllowFloatingLabel="false" Variant="Variant.Outlined"
                     class="rz-w-100">
        <ChildContent>
            <RadzenTextBox @bind-Value="_viewModel.ClinicalDiagnosis" />
        </ChildContent>
    </RadzenFormField>
</div>
```

### Step 5 — Optimize for Performance

The reflection-based helper is called on every render cycle. To avoid repeated
reflection overhead:

1. **Cache** `PropertyInfo` and `RequiredAttribute` lookups in a static
   dictionary (built once at startup).
2. **Debounce** evaluation — chips already re-render on `@bind-Value` changes;
   no additional wiring is needed.
3. Consider a **pre-computed tuple** approach in the code-behind that evaluates
   all field statuses once per render cycle and stores them in a dictionary.

```csharp
// In EditCase.razor.cs
private Dictionary<string, (FieldValidationStatus Status, string? Msg)> _fieldStatuses = new();

private void RefreshFieldStatuses()
{
    foreach (var propName in _currentTabRequiredFields)
    {
        _fieldStatuses[propName] = FieldStatusHelper.Evaluate(_viewModel, propName);
    }
}
```

### Step 6 — Apply Across All Tabs

Apply chips to every field on every tab following the same pattern. Priority
order (by form complexity):

| Priority | Tab | Approx. Fields |
|----------|-----|----------------|
| 1 | Medical Officer | 12+ fields (most validation rules) |
| 2 | Member Information | 8 fields |
| 3 | Unit Commander | 8 fields |
| 4 | Wing Commander | 4 fields |
| 5 | Medical Technician | 4 fields |
| 6 | Wing JA Review | 3 fields |
| 7 | Appointing Authority | 3 fields |
| 8–10 | Board tabs (3) | 2–4 fields each |

### Step 7 — Accessibility

- Each chip includes an icon **and** text, satisfying WCAG color-alone
  requirements.
- Add `role="status"` and `aria-live="polite"` to the chip `<span>` so screen
  readers announce status changes:

```razor
<span class="field-status field-status--required"
      role="status" aria-live="polite">
    ...
</span>
```

## Testing Checklist

- [ ] Empty required field shows amber "Required" chip.
- [ ] Filled valid field shows green "Valid" chip.
- [ ] Field with invalid value (e.g., future date, over-length) shows red chip
      with specific error message.
- [ ] Optional field shows gray "Optional" chip.
- [ ] Chip transitions animate smoothly (CSS `transition: all 0.25s ease`).
- [ ] Chip renders correctly in Material 3 Dark theme on all tabs.
- [ ] No duplicate status indicators on the Medical Officer tab (which already
      has `RadzenDataAnnotationValidator`).
- [ ] Screen reader announces chip state changes via `aria-live="polite"`.

## Files Changed

| File | Change |
|------|--------|
| `wwwroot/css/validation-indicators.css` | Add `.field-status*` classes |
| `ECTSystem.Web/Shared/FieldStatusChip.razor` | **New** — reusable chip component |
| `ECTSystem.Web/Shared/FieldStatusChip.razor.cs` | **New** — chip code-behind + enum |
| `ECTSystem.Web/Helpers/FieldStatusHelper.cs` | **New** — field status evaluation |
| `ECTSystem.Web/Pages/EditCase.razor` | Refactor labels to include `<FieldStatusChip>` |
| `ECTSystem.Web/Pages/ValidatorDemo.razor.css` | Remove `.field-status*` (moved to shared) |
| `_Imports.razor` | Add `@using ECTSystem.Web.Helpers` if not already present |

## Dependencies

- Requires `[Required]` and `[FormSection]` attributes on `LineOfDutyViewModel`
  properties (already present for Medical Assessment; needs extension for other
  sections per Recommendation 7 Option A).
- Pairs well with **Pulse-Glow** (Style 1) — the glow draws initial attention,
  the chip provides specific status information.
