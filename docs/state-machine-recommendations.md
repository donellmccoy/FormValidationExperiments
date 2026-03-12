# LineOfDutyStateMachine Recommendations

## 1. Implement Guard Methods

**Severity: Critical** | **Status: Open**

All 14 guard methods unconditionally return `true`. This means any transition is
always allowed regardless of data completeness.

**Recommendation:** Implement real business rules. Examples:

- `CanForwardToMedicalTechnicianAsync` — Validate Items 1–8 populated (name,
  rank, SSN, unit, incident type, date)
- `CanForwardToUnitCommanderReviewAsync` — Validate medical assessment fields
  complete (EPTS determination, substance involvement)
- `CanCompleteAsync` — Validate all board reviewers have signed off
- `CanCancelAsync` — Check user has authority (role-based guard)
- `CanReturnAsync` — Validate the destination state is a valid return target
  from the current state

Consider making guards `async` and accepting a `LineOfDutyCase` parameter so
they can query the data service for completeness checking.

---

## 2. Add Transactional Atomicity to `SaveAndNotifyAsync`

**Severity: High** | **Status: Partially Addressed**

The N+1 API call issue has been resolved — `SaveAndNotifyAsync` now uses a batch
`AddHistoryEntriesAsync` call instead of individual `AddHistoryEntryAsync` calls
in a loop. However, `SaveAndNotifyAsync` still performs **two sequential API
calls** — one `SaveCaseAsync` followed by one `AddHistoryEntriesAsync`. If
`SaveCaseAsync` succeeds but `AddHistoryEntriesAsync` fails, the database is
left in an inconsistent state: the case's `WorkflowState` is updated but the
corresponding history entries are missing.

```csharp
// Current: 2 API calls with no transactional guarantee
saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

foreach (var entry in entriesToSave)
{
    entry.LineOfDutyCaseId = saved.Id;
}

// ⚠️ If this fails, case state is saved but history entries are lost
savedEntries = await _dataService.AddHistoryEntriesAsync(entriesToSave);
```

The try/catch only reverts the in-memory state — it cannot undo the
already-committed case write.

**Recommendation:** Wrap the case save + history creation in a single API
endpoint (e.g., `TransitionCaseAsync`) that performs both writes atomically on
the server within a single database transaction.

---

## 3. Eliminate Repetitive Entry Handlers

**Severity: Medium** | **Status: Partially Addressed**

The shared `SaveAndNotifyAsync` helper now encapsulates all entry handler logic
— state update, history recording, persistence with try/catch rollback, and
result capture via `_lastTransitionResult`. Each forward entry handler is now a
trivial 1-line wrapper:

```csharp
private async Task OnXxxEntryAsync(LineOfDutyCase lineOfDutyCase)
{
    await SaveAndNotifyAsync(lineOfDutyCase, WorkflowState.Xxx);
}
```

A shared `OnReturnEntryAsync(WorkflowState)` handler is registered via
`.OnEntryFromAsync(_returnTrigger, OnReturnEntryAsync)` for all states (2–11).

**Remaining opportunity:** The 11 forward entry handler methods are still
individually defined even though they are trivial 1-line wrappers. Further
consolidation (e.g., a single generic handler via a
`Dictionary<LineOfDutyTrigger, WorkflowState>` mapping) could eliminate them
entirely, but the impact is smaller now (~50 lines vs. the original ~300 lines).

---

## 4. Consolidate Exit Handlers

**Severity: Low** | **Status: Open**

All 12 exit handlers return `Task.CompletedTask` with no logic. They exist
solely as placeholders.

**Recommendation:** Remove them from the state configuration until actually
needed. Stateless doesn't require exit handlers. When a specific step needs exit
logic, add only that handler. This removes ~120 lines of dead code.

---

## 5. Remove `Async` Suffix from Synchronous Guards

**Severity: Low** | **Status: Open**

Methods like `CanStartLodAsync()`, `CanCancelAsync()`, `CanReturnAsync()` are
synchronous (`bool` return) but named with `Async`. The XML doc even
acknowledges this: _"Despite the Async suffix (retained for naming
consistency)..."_.

**Recommendation:** Drop the `Async` suffix now while the codebase is still
maturing. If guards become truly async later, rename them then. This avoids
confusing callers about concurrency expectations.

---

## 6. Clean Up Unused Triggers and Orphaned State

**Severity: Low** | **Status: Open**

The `LineOfDutyTrigger` enum has 4 values not used anywhere in the state machine
configuration:

- `ForwardToMemberInformationEntry` — never wired
- `ForwardToApprovingAuthorityReview` — appears to be a duplicate of
  `ForwardToAppointingAuthorityReview`
- `Close` — no transition defined
- `Reopen` — no transition defined

`WorkflowState.Closed = 13` is also defined but never configured.

**Recommendation:** Either implement `Close`/`Reopen` transitions (for
post-completion case management) or remove the unused values to keep the enum
aligned with reality.

---

## 7. Add Comprehensive Model Validation with Modern Validator Indicators

**Severity: High** | **Status: Open**

### Current State

Of 9 `RadzenTemplateForm` instances in `EditCase.razor`, only 1 (Medical
Officer) includes a `RadzenDataAnnotationValidator`. The remaining 8 tabs —
Member Information, Medical Technician, Unit Commander, Wing JA, Appointing
Authority, Wing Commander, and 3 Board Review tabs — have **no validation
whatsoever**. Users can submit blank or invalid forms with no feedback.

`LineOfDutyViewModel` already defines `[Required]` and `[StringLength]`
attributes for the MedicalAssessment section and implements `IValidatableObject`
with 10+ conditional rules — but these annotations are silently ignored because
most forms lack a validator component.

### Recommendation

#### Option A — DataAnnotation Validator per Tab (Minimal Effort)

Add `<RadzenDataAnnotationValidator />` inside every `RadzenTemplateForm` to
activate the existing `[Required]`/`[StringLength]` attributes and
`IValidatableObject` rules.

Extend `LineOfDutyViewModel` with annotations for sections that currently have
none:

```csharp
// ── MemberInfo section ──
[FormSection("MemberInfo")]
[Required(ErrorMessage = "Last name is required.")]
[StringLength(100)]
public string LastName { get; set; } = string.Empty;

[FormSection("MemberInfo")]
[Required(ErrorMessage = "First name is required.")]
[StringLength(100)]
public string FirstName { get; set; } = string.Empty;

[FormSection("MemberInfo")]
[Required(ErrorMessage = "Date of birth is required.")]
public DateTime? DateOfBirth { get; set; }

// ── UnitCommander section ──
[FormSection("UnitCommander")]
[Required(ErrorMessage = "Commander's recommendation is required.")]
public CommanderRecommendation? Recommendation { get; set; }

[FormSection("UnitCommander")]
[Required(ErrorMessage = "Narrative of circumstances is required.")]
[StringLength(4000)]
public string NarrativeOfCircumstances { get; set; } = string.Empty;

// ── WingCommander section ──
[FormSection("WingCommander")]
[Required(ErrorMessage = "Legal sufficiency determination is required.")]
public bool? IsLegallySufficient { get; set; }
```

Add corresponding `IValidatableObject` cross-field rules:

```csharp
// UnitCommander conditional
if (ResultOfMisconduct == true && string.IsNullOrWhiteSpace(MisconductExplanation))
    yield return new ValidationResult(
        "Misconduct explanation is required when misconduct is indicated.",
        [nameof(MisconductExplanation)]);

// WingCommander conditional
if (ConcurWithRecommendation == false && string.IsNullOrWhiteSpace(NonConcurrenceReason))
    yield return new ValidationResult(
        "Non-concurrence reason is required when SJA does not concur.",
        [nameof(NonConcurrenceReason)]);
```

#### Option B — Inline Radzen Validators with Field-Level Indicators (Recommended)

Use Radzen's component-based validators inside `<RadzenFormField>` `<Helper>`
slots for immediate, context-aware feedback with built-in animated indicators.
This is the same pattern used on the Register page:

```razor
<RadzenFormField Text="Last Name" Variant="Variant.Outlined">
    <ChildContent>
        <RadzenTextBox Name="LastName" @bind-Value="_viewModel.LastName" />
    </ChildContent>
    <Helper>
        <RadzenRequiredValidator Component="LastName"
                                 Text="Last name is required" />
        <RadzenLengthValidator Component="LastName" Max="100"
                               Text="Maximum 100 characters" />
    </Helper>
</RadzenFormField>
```

Available Radzen validators and their use cases:

| Validator | Use Case | Example Fields |
| --- | --- | --- |
| `RadzenRequiredValidator` | Mandatory fields | Name, SSN, Rank, Diagnosis |
| `RadzenLengthValidator` | Min/max character limits | Narrative (min 50), Remarks (max 4000) |
| `RadzenNumericRangeValidator` | Bounded numeric values | Days of incapacitation |
| `RadzenRegexValidator` | Pattern enforcement | SSN (`^\d{3}-\d{2}-\d{4}$`), Grade |
| `RadzenCompareValidator` | Cross-field equality | Confirm password, date ranges |
| `RadzenEmailValidator` | Email format | Notification email addresses |
| `RadzenCustomValidator` | Complex business rules | Conditional fields, cross-section checks |

#### Option C — Hybrid: DataAnnotations + Custom Validator Component (Most Robust)

Combine `RadzenDataAnnotationValidator` for simple rules with a custom
`RadzenCustomValidator` for business logic that spans multiple fields or depends
on workflow state:

```razor
<RadzenTemplateForm TItem="LineOfDutyViewModel" Data="@_viewModel"
                    Submit="@(() => SaveTabFormDataAsync(TabNames.UnitCommander))">
    <RadzenDataAnnotationValidator />

    @* ── Cross-field rule: misconduct explanation ── *@
    <RadzenCustomValidator Component="MisconductExplanation"
                           Validator="@(() => ValidateMisconductExplanation())"
                           Text="Explanation required when misconduct is indicated" />

    @* ── Workflow-aware rule: commander must sign before forwarding ── *@
    <RadzenCustomValidator Component="CommanderSignatureDate"
                           Validator="@(() => ValidateCommanderSignature())"
                           Text="Commander signature is required before forwarding" />
</RadzenTemplateForm>
```

### Modern Validator Indicator Styles

#### 1. Pulse-Glow Required Field Indicator

A subtle animated glow on unfilled required fields that draws attention without
being disruptive:

```css
/* Pulsing border glow for empty required fields */
.rz-form-field:has(input:invalid:not(:focus)) {
    animation: pulse-required 2s ease-in-out infinite;
}

@keyframes pulse-required {
    0%, 100% { box-shadow: 0 0 0 0 rgba(var(--rz-danger-rgb), 0); }
    50%      { box-shadow: 0 0 0 4px rgba(var(--rz-danger-rgb), 0.15); }
}
```

#### 2. Inline Status Chip Indicators

Replace default error text with compact status chips next to field labels:

```css
/* Validation status chip */
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
```

```razor
<RadzenLabel>
    Last Name
    @if (string.IsNullOrWhiteSpace(_viewModel.LastName))
    {
        <span class="field-status field-status--required">
            <RadzenIcon Icon="priority_high" Style="font-size: 0.75rem;" /> Required
        </span>
    }
    else
    {
        <span class="field-status field-status--valid">
            <RadzenIcon Icon="check_circle" Style="font-size: 0.75rem;" /> Valid
        </span>
    }
</RadzenLabel>
```

#### 3. Section Completeness Progress Ring

Show per-fieldset validation completeness as a circular progress indicator in
each fieldset header:

```razor
<RadzenFieldset>
    <HeaderTemplate>
        <div style="display: flex; align-items: center; gap: 0.75rem;">
            <RadzenProgressBarCircular Value="@GetSectionCompleteness("MemberInfo")"
                                       Size="28" ShowValue="false"
                                       ProgressBarStyle="ProgressBarStyle.Primary" />
            <span>Member Information</span>
            <RadzenBadge Text="@($"{GetSectionCompleteness("MemberInfo"):0}%")"
                         BadgeStyle="@(GetSectionCompleteness("MemberInfo") == 100
                             ? BadgeStyle.Success : BadgeStyle.Warning)"
                         IsPill="true" />
        </div>
    </HeaderTemplate>
</RadzenFieldset>
```

#### 4. Animated Slide-Down Validation Summary

A collapsible validation summary panel that slides in when errors exist, styled
with the danger theme and grouped by section:

```razor
@if (_validationErrors.Any())
{
    <RadzenAlert AlertStyle="AlertStyle.Danger" Variant="Variant.Flat"
                 ShowIcon="true" AllowClose="true"
                 class="validation-summary-slide">
        <RadzenText TextStyle="TextStyle.Subtitle2">
            @_validationErrors.Count issue(s) remaining
        </RadzenText>
        <ul class="validation-error-list">
            @foreach (var error in _validationErrors)
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

```css
.validation-summary-slide {
    animation: slide-down 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    transform-origin: top;
}

@keyframes slide-down {
    from { opacity: 0; transform: translateY(-0.5rem); max-height: 0; }
    to   { opacity: 1; transform: translateY(0);       max-height: 500px; }
}

.validation-error-list {
    list-style: none;
    padding: 0;
    margin: 0.5rem 0 0;
    display: flex;
    flex-direction: column;
    gap: 0.25rem;
}

.validation-error-list li {
    display: flex;
    align-items: center;
    gap: 0.375rem;
    color: var(--rz-danger-light);
    font-size: 0.85rem;
}
```

#### 5. Step-Level Validation Badge on Workflow Sidebar

Surface validation status on the `WorkflowSidebar` steps so users can see at a
glance which sections need attention — before attempting a workflow transition:

```razor
@* Inside WorkflowSidebar step rendering *@
<RadzenBadge Visible="@(step.ValidationErrorCount > 0)"
             Text="@step.ValidationErrorCount.ToString()"
             BadgeStyle="BadgeStyle.Danger"
             IsPill="true"
             class="step-validation-badge" />
```

```css
.step-validation-badge {
    position: absolute;
    top: -4px;
    right: -8px;
    min-width: 18px;
    height: 18px;
    font-size: 0.65rem;
    animation: badge-pop 0.3s cubic-bezier(0.175, 0.885, 0.32, 1.275);
}

@keyframes badge-pop {
    from { transform: scale(0); }
    to   { transform: scale(1); }
}
```

### Integration with State Machine Guards

Model validation directly enables guard implementation (Recommendation 1). The
`[FormSection]` attribute already tags each property with its workflow section.
A section-aware validation helper ties the two together:

```csharp
public bool ValidateSection(string sectionName)
{
    var context = new ValidationContext(_viewModel);
    var results = new List<ValidationResult>();

    // DataAnnotation validation
    Validator.TryValidateObject(_viewModel, context, results, validateAllProperties: true);

    // Filter to section-specific properties via [FormSection] attribute
    var sectionProperties = typeof(LineOfDutyViewModel)
        .GetProperties()
        .Where(p => p.GetCustomAttributes<FormSectionAttribute>()
                      .Any(a => a.SectionName == sectionName))
        .Select(p => p.Name)
        .ToHashSet();

    return !results.Any(r => r.MemberNames.Any(m => sectionProperties.Contains(m)));
}
```

Guards can then delegate to this helper:

```csharp
private bool CanForwardToMedicalTechnicianAsync()
    => ValidateSection("MemberInfo");

private bool CanForwardToUnitCommanderReviewAsync()
    => ValidateSection("MedicalAssessment");
```

---

## Priority Summary

| Priority | Recommendation | Impact | Status |
| --- | --- | --- | --- |
| **P0** | 1. Implement guards | No validation = invalid transitions | Open |
| **P0** | 7. Add model validation + indicators | 8 of 9 forms have zero validation | Open |
| **P1** | 2. Add transactional atomicity | Partial writes (2 calls) | Partial |
| **P2** | 3. Consolidate entry handlers | ~50 lines of wrappers | Partial |
| **P2** | 4. Remove placeholder exits | ~120 lines of dead code | Open |
| **P3** | 5. Remove `Async` suffix from sync guards | Naming accuracy | Open |
| **P3** | 6. Clean up unused triggers/states | Enum hygiene | Open |
