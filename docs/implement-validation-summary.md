# Implementation Plan: Animated Slide-Down Validation Summary

## Overview

Add a collapsible validation summary panel at the top of each form tab that
slides into view when validation errors exist. The summary uses the Radzen
`AlertStyle.Danger` theme, groups errors in a compact list, and provides a
one-click close button. It animates in with a slide-down effect and disappears
when all errors are resolved.

## Reference

- **Recommendation:** [state-machine-recommendations.md](state-machine-recommendations.md) §7, Style 4
- **Working demo:** `ECTSystem.Web/Pages/ValidatorDemo.razor` — Style 4 section
- **Demo CSS:** `ECTSystem.Web/Pages/ValidatorDemo.razor.css` —
  `.validation-summary-slide`, `.validation-error-list`

## Current State

- No validation summary exists on any tab — validation errors are only surfaced
  via Radzen's default inline messages on the Medical Officer tab (the only tab
  with a validator).
- `LineOfDutyViewModel` implements `IValidatableObject` with 10+ conditional
  rules that produce `ValidationResult` objects with field-specific member names.
- No centralized error collection mechanism exists in `EditCase.razor.cs`.

## Target Behavior

| State | Visual |
|-------|--------|
| Tab has **no errors** | Summary panel is hidden (not rendered) |
| Tab has **1+ errors** | Danger-themed alert slides in from top with error count + grouped list |
| User **fixes all errors** | Panel slides out and is removed from DOM |
| User **clicks close** | Panel hides temporarily (reappears on next validation trigger) |

The summary should display after explicit validation triggers (form submit
attempt, tab change, or manual "Validate" button click) — not on every
keystroke, to avoid a distracting constant presence.

## Implementation Steps

### Step 1 — Move CSS to a Shared Location

**File:** `ECTSystem.Web/wwwroot/css/validation-indicators.css` (append)

```css
/* ── Animated Slide-Down Validation Summary ── */
.validation-summary-slide {
    animation: slide-down 0.35s cubic-bezier(0.4, 0, 0.2, 1);
    transform-origin: top;
}

@keyframes slide-down {
    from { opacity: 0; transform: translateY(-0.5rem); }
    to   { opacity: 1; transform: translateY(0); }
}

.validation-error-list {
    list-style: none;
    padding: 0;
    margin: 0.5rem 0 0;
    display: flex;
    flex-direction: column;
    gap: 0.35rem;
}

.validation-error-list li {
    display: flex;
    align-items: center;
    gap: 0.375rem;
    color: var(--rz-danger-light);
    font-size: 0.85rem;
}
```

### Step 2 — Create a `ValidationSummary` Component

Encapsulate the summary panel in a reusable component that accepts a list of
validation results and renders the alert.

**File:** `ECTSystem.Web/Shared/ValidationSummaryPanel.razor` (new)

```razor
@if (Errors.Count > 0 && !_dismissed)
{
    <RadzenAlert AlertStyle="AlertStyle.Danger" Variant="Variant.Flat"
                 ShowIcon="true" AllowClose="true"
                 Close="@(() => _dismissed = true)"
                 class="validation-summary-slide">
        <RadzenText TextStyle="TextStyle.Subtitle2">
            @Errors.Count issue(s) remaining
        </RadzenText>
        <ul class="validation-error-list">
            @foreach (var error in Errors)
            {
                <li>
                    <RadzenIcon Icon="error_outline"
                                Style="font-size: 0.875rem; vertical-align: middle;" />
                    @error.ErrorMessage
                </li>
            }
        </ul>
    </RadzenAlert>
}
```

**File:** `ECTSystem.Web/Shared/ValidationSummaryPanel.razor.cs` (new)

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Shared;

public partial class ValidationSummaryPanel
{
    private bool _dismissed;

    [Parameter, EditorRequired]
    public IReadOnlyList<ValidationResult> Errors { get; set; } = [];

    protected override void OnParametersSet()
    {
        // Reset dismissed state when errors change (new validation run)
        if (Errors.Count > 0)
            _dismissed = false;
    }
}
```

### Step 3 — Build a Section Validation Method

Add a method to `EditCase.razor.cs` that runs `IValidatableObject.Validate()`
and `DataAnnotations` validation scoped to the current tab's section, then
stores the results for the summary panel.

**In `EditCase.razor.cs`:**

```csharp
private List<ValidationResult> _currentTabErrors = [];

/// <summary>
/// Validates all properties in the specified section and
/// returns the filtered errors.
/// </summary>
private List<ValidationResult> ValidateSection(string sectionName)
{
    var context = new ValidationContext(_viewModel);
    var allResults = new List<ValidationResult>();

    // 1. DataAnnotation validation
    Validator.TryValidateObject(_viewModel, context, allResults,
        validateAllProperties: true);

    // 2. IValidatableObject custom rules
    allResults.AddRange(_viewModel.Validate(context));

    // 3. Filter to section-specific properties
    var sectionProps = typeof(LineOfDutyViewModel)
        .GetProperties()
        .Where(p => p.GetCustomAttributes<FormSectionAttribute>()
                      .Any(a => a.SectionName == sectionName))
        .Select(p => p.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return allResults
        .Where(r => r.MemberNames.Any(m => sectionProps.Contains(m)))
        .ToList();
}
```

### Step 4 — Trigger Validation on Form Submit

Update each tab's submit handler to populate the error list before saving:

```csharp
private async Task SaveTabFormDataAsync(string tabName)
{
    _currentTabErrors = ValidateSection(GetSectionNameForTab(tabName));

    if (_currentTabErrors.Count > 0)
    {
        // Don't save — show the summary panel
        StateHasChanged();
        return;
    }

    // Proceed with save...
    await SaveAsync(tabName);
}
```

### Step 5 — Place the Summary Panel in `EditCase.razor`

Add the `<ValidationSummaryPanel>` component at the top of each tab's content
area, just inside the `RadzenTemplateForm`:

```razor
<RadzenTemplateForm TItem="LineOfDutyViewModel" Data="@_viewModel"
                    Submit="@(() => SaveTabFormDataAsync(TabNames.MemberInformation))">

    <ValidationSummaryPanel Errors="@_currentTabErrors" />

    @* ... existing form fields ... *@
</RadzenTemplateForm>
```

### Step 6 — Clear Errors on Tab Change

When the user navigates to a different tab, clear the error list so the summary
doesn't persist from a previous tab:

```csharp
private void OnTabIndexChanged(int newIndex)
{
    _selectedTabIndex = newIndex;
    _currentTabErrors = [];  // Clear previous tab's errors
    // ... existing tab change logic ...
}
```

### Step 7 — Add a "Validate" Button (Optional Enhancement)

Add a secondary button next to the Save button that runs validation without
attempting to save, allowing users to check their progress:

```razor
<RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem"
             JustifyContent="JustifyContent.End" Style="margin-top: 1rem;">
    <RadzenButton Text="Validate" ButtonStyle="ButtonStyle.Info"
                  Icon="checklist" Variant="Variant.Outlined"
                  Click="@(() => ValidateCurrentTab())" />
    <RadzenButton Text="Save" ButtonStyle="ButtonStyle.Primary"
                  Icon="save" ButtonType="ButtonType.Submit" />
</RadzenStack>
```

### Step 8 — Group Errors by Fieldset (Optional Enhancement)

For tabs with multiple fieldsets, group errors by their `[FormSection]`
sub-section to make the summary easier to scan:

```razor
@foreach (var group in Errors.GroupBy(e => GetSectionLabel(e)))
{
    <RadzenText TextStyle="TextStyle.Caption"
                Style="color: var(--rz-text-secondary-color); margin-top: 0.5rem;">
        @group.Key
    </RadzenText>
    <ul class="validation-error-list">
        @foreach (var error in group)
        {
            <li>
                <RadzenIcon Icon="error_outline"
                            Style="font-size: 0.875rem;" />
                @error.ErrorMessage
            </li>
        }
    </ul>
}
```

## Testing Checklist

- [ ] Summary panel appears with slide animation when form submit has errors.
- [ ] Summary panel shows correct error count and individual error messages.
- [ ] Closing the panel via the X button hides it until next validation run.
- [ ] Fixing all errors and re-submitting causes the panel to disappear.
- [ ] Switching tabs clears the previous tab's error panel.
- [ ] `IValidatableObject` cross-field rules appear in the summary (e.g.,
      "Misconduct explanation required when misconduct is indicated").
- [ ] Panel renders correctly in Material 3 Dark theme with danger styling.
- [ ] Optional "Validate" button runs validation without saving.
- [ ] Panel does NOT appear on every keystroke — only on explicit triggers.

## Files Changed

| File | Change |
|------|--------|
| `wwwroot/css/validation-indicators.css` | Add slide-down + error-list styles |
| `ECTSystem.Web/Shared/ValidationSummaryPanel.razor` | **New** — summary component |
| `ECTSystem.Web/Shared/ValidationSummaryPanel.razor.cs` | **New** — summary code-behind |
| `ECTSystem.Web/Pages/EditCase.razor` | Add `<ValidationSummaryPanel>` to each tab |
| `ECTSystem.Web/Pages/EditCase.razor.cs` | Add `ValidateSection()`, `_currentTabErrors`, update submit handlers |
| `ECTSystem.Web/Pages/ValidatorDemo.razor.css` | Remove `.validation-summary-slide` etc. (moved to shared) |

## Dependencies

- Requires `[FormSection]` attributes on `LineOfDutyViewModel` properties to
  filter errors by section (partially done; needs extension for non-Medical
  sections).
- Requires `IValidatableObject.Validate()` to cover all cross-field rules
  (partially done; needs extension per Recommendation 7).
- Works independently of other indicator styles but complements **Step-Level
  Badges** (Style 5), which show error counts at the sidebar level.
