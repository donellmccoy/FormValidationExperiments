# Copilot Instructions

## Project Overview

This is a **Blazor WebAssembly** application targeting **.NET 10** that implements an **Air Force Line of Duty (LOD) Determination** workflow system. The app uses **Radzen Blazor** components for its UI and manages multi-step form workflows for processing LOD cases (injury/illness determinations for military members). The application models the AF Form 348 (Line of Duty Determination) with form fields mapped to numbered items on the official form.

## Technology Stack

- **Framework:** .NET 10, Blazor WebAssembly (standalone client-side)
- **UI Components:** Radzen Blazor (`Radzen.Blazor` v9.0.0)
- **Language:** C# (nullable disabled, implicit usings enabled)
- **CSS:** Component-scoped CSS (`.razor.css` files)
- **Hosting:** Static WebAssembly — no server-side project; entry point is `wwwroot/index.html`

## Project Structure

The solution contains five projects: `ECTSystem.Web` (Blazor WASM client), `ECTSystem.Api` (ASP.NET Core OData API), `ECTSystem.Shared` (shared models/enums/mappings), `ECTSystem.Persistence` (EF Core data layer), and `ECTSystem.Tests` (unit/integration tests).

```
ECTSystem.Shared/
├── Models/                 # Domain model classes (namespace: ECTSystem.Shared.Models)
│   ├── LineOfDutyCase.cs         # Root aggregate — full LOD case record
│   ├── LineOfDutyDocument.cs     # Uploaded/attached documents
│   ├── LineOfDutyAppeal.cs       # Appeal records
│   ├── LineOfDutyAuthority.cs    # Reviewing authority entries
│   ├── MEDCONDetails.cs          # Medical Continuation benefit tracking
│   ├── INCAPDetails.cs           # Incapacitation Pay benefit tracking
│   ├── Member.cs                 # Service member entity
│   ├── Notification.cs           # Notification entity
│   ├── Bookmark.cs               # Case bookmark entity
│   ├── WorkflowStateHistory.cs   # Workflow state transition history
│   ├── WorkflowStateHistoryFactory.cs
│   ├── WorkflowStateLookup.cs
│   ├── WorkflowModule.cs
│   ├── WorkflowType.cs
│   ├── CaseTransitionRequest.cs  # State transition request DTO
│   ├── CaseTransitionResponse.cs # State transition response DTO
│   ├── AuditableEntity.cs        # Base class with audit fields
│   ├── AuditComment.cs
│   ├── TimelineStep.cs
│   └── WitnessStatement.cs
├── Enums/                  # Domain enums (namespace: ECTSystem.Shared.Enums)
│   ├── CommanderRecommendation.cs  # Item 21: commander's LOD recommendation
│   ├── DutyStatus.cs              # Duty status at time of incident
│   ├── FindingType.cs             # ILOD, NILOD, EPTS findings
│   ├── IncidentType.cs            # Injury, Illness, Disease, Death, etc.
│   ├── MemberStatus.cs            # Item 8: AFR, ANG member status
│   ├── MilitaryRank.cs            # Enlisted, Officer, Cadet ranks
│   ├── ProcessType.cs             # Informal vs. Formal process
│   ├── ServiceComponent.cs        # RegAF, USSF, AFR, ANG
│   ├── SubstanceType.cs           # Item 13a: Alcohol, Drugs, Both
│   ├── WorkflowState.cs           # Workflow states (Draft → Completed/Cancelled)
│   ├── WorkflowStepStatus.cs      # Completed, InProgress, Pending
│   ├── WorkflowTransitionAction.cs # Enter, Leave transition actions
│   └── WorkflowTrigger.cs         # State machine triggers (Forward*, Return, Cancel, etc.)
├── ViewModels/             # Shared view models and DTOs (namespace: ECTSystem.Shared.ViewModels)
│   ├── LineOfDutyViewModel.cs     # Main case view model
│   ├── DocumentItem.cs            # Supporting document metadata
│   ├── PagedResult.cs             # Paged query result wrapper
│   ├── TrackableModel.cs          # Change-tracking base
│   ├── FormSectionAttribute.cs    # Form section metadata attribute
│   ├── Create*Dto.cs              # Create DTOs (Authority, Bookmark, Member, WorkflowStateHistory)
│   └── UpdateMemberDto.cs         # Update member DTO
├── Mapping/                # Model-to-DTO mappers (namespace: ECTSystem.Shared.Mapping)
│   ├── LineOfDutyCaseMapper.cs
│   ├── AuthorityDtoMapper.cs
│   ├── BookmarkDtoMapper.cs
│   ├── MemberDtoMapper.cs
│   └── WorkflowStateHistoryDtoMapper.cs
├── Extensions/             # Extension methods (namespace: ECTSystem.Shared.Extensions)
│   ├── EnumExtensions.cs          # Enum display formatting
│   └── LineOfDutyExtensions.cs    # LOD case helper extensions
└── Factories/              # Factory classes

ECTSystem.Web/
├── Pages/                  # Razor pages with code-behind (.razor.cs) and scoped styles (.razor.css)
│   ├── EditCase.razor/.cs/.css   # Multi-step wizard workflow (primary page)
│   ├── EditCase.Documents.razor.cs # Document management partial
│   ├── EditCase.Form348.razor.cs   # Form 348 PDF generation partial
│   ├── EditCase.MemberSearch.razor.cs # Member search partial
│   ├── EditCase.State.razor.cs     # State machine operations partial
│   ├── CaseList.razor/.cs/.css     # Case search/list grid page
│   ├── Dashboard.razor/.cs/.css    # Dashboard with stats and charts
│   ├── MyBookmarks.razor/.cs/.css  # Bookmarked cases page
│   ├── Login.razor/.cs/.css        # Login page
│   ├── Register.razor/.cs/.css     # Registration page
│   ├── ValidatorDemo.razor/.cs/.css # Validation style demos
│   └── NotFound.razor              # 404 fallback page
├── Services/               # OData client services (namespace: ECTSystem.Web.Services)
│   ├── Interfaces/               # Service interfaces (ICaseService, IBookmarkService, etc.)
│   ├── ODataServiceBase.cs       # Base class for OData services
│   ├── CaseService.cs            # Case CRUD and workflow operations
│   ├── BookmarkService.cs        # Bookmark management
│   ├── AuthorityService.cs       # Authority/reviewer data
│   ├── DocumentService.cs        # Document upload/download
│   ├── MemberService.cs          # Member data
│   ├── WorkflowHistoryService.cs # Workflow state history
│   ├── AuthService.cs            # Authentication
│   ├── BookmarkCountService.cs   # Client-side bookmark count tracking
│   └── EctODataContext.cs        # OData client context
├── StateMachines/          # Client-side state machine (namespace: ECTSystem.Web.StateMachines)
│   └── LineOfDutyStateMachine.cs # Stateless-based workflow state machine
├── Factories/              # Factory classes
│   └── LineOfDutyStateMachineFactory.cs
├── Handlers/               # HTTP message handlers
│   ├── AuthorizationMessageHandler.cs
│   └── ODataLoggingHandler.cs
├── Helpers/
│   └── WorkflowTabHelper.cs
├── Providers/
│   └── JwtAuthStateProvider.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs  # DI registration + OData EdmModel builder
├── Models/                 # Client-only models (namespace: ECTSystem.Web.Models)
│   ├── AuthResponse.cs
│   ├── LoginRequest.cs
│   └── RegisterRequest.cs
├── Shared/                 # Reusable components
│   ├── WorkflowSidebar.razor/.cs/.css  # Vertical step-progress sidebar
│   ├── WorkflowStep.cs                 # Step model
│   ├── WorkflowStepStatus.cs           # Step status enum
│   ├── BookmarkBadge.razor             # Sidebar bookmark count badge
│   ├── CheckOutCaseDialog.razor/.cs    # Case checkout confirmation dialog
│   ├── LoginDisplay.razor/.css         # Auth status display
│   └── RedirectToLogin.razor           # Auth redirect component
├── Layout/                 # App layout
│   ├── MainLayout.razor/.cs/.css
│   └── AuthLayout.razor/.css
├── Program.cs              # WebAssembly host builder and service registration
├── App.razor               # Root component with Router
├── _Imports.razor           # Global usings for Radzen, domain, and view-model namespaces
├── AF348_06012015_Template.md/.pdf  # Reference AF Form 348 template
└── wwwroot/                # Static assets (index.html, CSS, sample data)
```

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `ECTSystem.Shared.Models` | Domain model classes (LineOfDutyCase, Member, etc.) |
| `ECTSystem.Shared.Enums` | Domain enums (WorkflowState, FindingType, WorkflowTrigger, etc.) |
| `ECTSystem.Shared.ViewModels` | Shared view models and DTOs |
| `ECTSystem.Shared.Mapping` | Model-to-DTO mappers |
| `ECTSystem.Shared.Extensions` | Enum and domain extension methods |
| `ECTSystem.Web.Pages` | Page components (EditCase, CaseList, Dashboard, MyBookmarks, Login, Register) |
| `ECTSystem.Web.Services` | OData client services and interfaces |
| `ECTSystem.Web.StateMachines` | Client-side Stateless workflow state machine |
| `ECTSystem.Web.Models` | Client-only models (auth request/response) |
| `ECTSystem.Web.Shared` | Shared components (WorkflowSidebar, BookmarkBadge, CheckOutCaseDialog, etc.) |
| `ECTSystem.Web.Layout` | Layout components (MainLayout, AuthLayout) |
| `ECTSystem.Web.Handlers` | HTTP message handlers (auth, OData logging) |
| `ECTSystem.Web.Providers` | Authentication state provider |
| `ECTSystem.Web.Factories` | State machine factory |
| `ECTSystem.Web.Helpers` | Workflow tab helper |

## Coding Conventions

- Use **partial classes** with code-behind files (`.razor.cs`) to separate logic from markup.
- Domain models, enums, view models, and mappers live in `ECTSystem.Shared` under their respective namespaces (`ECTSystem.Shared.Models`, `ECTSystem.Shared.Enums`, `ECTSystem.Shared.ViewModels`, `ECTSystem.Shared.Mapping`).
- Client-only models (auth) live in `ECTSystem.Web.Models`.
- Namespaces follow the folder structure (e.g., `ECTSystem.Shared.Models`, `ECTSystem.Shared.Enums`).
- Format enum display names by inserting spaces before uppercase letters using `Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1")`.
- Use component-scoped CSS (`.razor.css`) rather than global styles where possible.
- Follow the existing pattern of one form model per workflow step (e.g., `MemberInfoFormModel`, `MedicalAssessmentFormModel`, `UnitCommanderFormModel`, `WingCommanderFormModel`).
- Workflow steps are tracked via a `List<WorkflowStep>` with the `WorkflowStepStatus` enum (`Completed`, `InProgress`, `Pending`), defined in `Shared/WorkflowSidebar.razor.cs`.
- Use conditional visibility properties (e.g., `ShowSubstanceType`, `ShowToxicologyResults`) to toggle dependent form sections.
- Dropdown data sources use helper classes like `DropdownItem<T>` populated from `Enum.GetValues<T>()`.

## Component Patterns

- Use Radzen components (`RadzenTextBox`, `RadzenDropDown`, `RadzenButton`, `RadzenTabs`, etc.) for all form controls and UI elements.
- Radzen services are registered via `builder.Services.AddRadzenComponents()` in `Program.cs`.
- Global Radzen imports (`@using Radzen`, `@using Radzen.Blazor`) are in `_Imports.razor`.
- The `EditCase` page implements a **multi-step wizard** pattern — the `WorkflowSidebar` drives step navigation, and `selectedTabIndex` / `currentStepIndex` control which form sections are visible.
- The `EditCase` page uses a `FormatEnum<T>()` helper for display-friendly enum names.

## Domain Context

This application models the U.S. Air Force's Line of Duty determination process per AFI 36-2910 (now DAFI 36-2910). Key domain concepts:

- **AF Form 348** — The official form being modeled; view models map to numbered form items (Items 1–25+).
- **LOD Case** (`LineOfDutyCase.cs`) — Root aggregate tracking an injury/illness determination through multiple review stages, including documents, appeals, authorities, MEDCON/INCAP details, timeline, and audit comments.
- **Workflow Steps** — Start → Member Reports → LOD Initiation → Medical Assessment → Commander Review → Legal/SJA Review → Wing CC Review → Board Review (8 steps in the current implementation).
- **Service Components** — RegAF (Regular Air Force), AFR (Air Force Reserve), ANG (Air National Guard). ARC-specific fields appear conditionally for Reserve/Guard members.
- **Findings** — Line of Duty, Not in Line of Duty (NILOD), with proximate cause analysis.
- **MEDCON/INCAP** — Medical Continuation and Incapacitation Pay benefit tracking via dedicated detail classes.
- **EPTS/NSA** — Existed Prior to Service / Not Service Aggravated; tracked in the medical assessment with service-aggravation sub-fields.

## Guidelines

- When adding new workflow steps or forms, follow the existing multi-step wizard pattern in `EditCase.razor` / `EditCase.razor.cs`.
- When adding new tabbed form sections, follow the existing tabbed pattern in `EditCase.razor` / `EditCase.razor.cs`.
- Keep form validation logic and conditional visibility in code-behind files.
- Use `EditForm` with model binding for form handling; each workflow step should have its own `EditForm` with a dedicated submit handler.
- Prefer strongly-typed enums for status values and dropdown options; populate dropdowns from `Enum.GetValues<T>()`.
- When generating test data, use realistic military/LOD terminology consistent with AF Form 348 item numbering.
- The `WorkflowStep` and `WorkflowStepStatus` types are defined in `Shared/WorkflowSidebar.razor.cs` — import via `@using ECTSystem.Web.Shared` (already in `_Imports.razor`).
- Use the `CaseInfoModel` for read-only case summary/header displays; use the specific form models for editable form sections.
- **Always use the Simple Browser** when launching the app — use `open_simple_browser` tool with URL `https://localhost:7240` for the Web app.

## UI Component Library

This project uses [Radzen Blazor Components](https://blazor.radzen.com/?theme=material3-dark) with the **Material 3 Dark** theme. All UI should be built using Radzen components and the CSS utility classes documented below.

### Components

Use Radzen Blazor components for all UI elements. The full component catalog is organized into these categories:

| Category | Components |
|----------|-----------|
| **DataGrid** | `RadzenDataGrid`, `RadzenPivotDataGrid` |
| **Data** | `RadzenDropDown`, `RadzenAutoComplete`, `RadzenListBox`, `RadzenTree`, `RadzenScheduler`, `RadzenDataList`, `RadzenPager` |
| **Images** | `RadzenImage`, `RadzenGravatar`, `RadzenIcon` |
| **Layout** | `RadzenStack`, `RadzenRow`, `RadzenColumn`, `RadzenPanel`, `RadzenCard`, `RadzenFieldset`, `RadzenSplitter`, `RadzenAccordion`, `RadzenTabs` |
| **Navigation** | `RadzenMenu`, `RadzenPanelMenu`, `RadzenBreadCrumb`, `RadzenContextMenu`, `RadzenLink`, `RadzenSteps`, `RadzenProfileMenu` |
| **Forms** | `RadzenTextBox`, `RadzenTextArea`, `RadzenPassword`, `RadzenNumeric`, `RadzenDatePicker`, `RadzenTimePicker`, `RadzenColorPicker`, `RadzenCheckBox`, `RadzenRadioButtonList`, `RadzenSwitch`, `RadzenSelectBar`, `RadzenSlider`, `RadzenRating`, `RadzenUpload`, `RadzenTemplateForm`, `RadzenButton`, `RadzenSplitButton`, `RadzenToggleButton` |
| **Data Visualization** | `RadzenChart`, `RadzenGauge`, `RadzenProgressBar`, `RadzenProgressBarCircular` |
| **Feedback** | `RadzenAlert`, `RadzenBadge`, `RadzenNotification`, `RadzenDialog`, `RadzenTooltip` |
| **Validators** | `RadzenRequiredValidator`, `RadzenLengthValidator`, `RadzenEmailValidator`, `RadzenRegexValidator`, `RadzenNumericRangeValidator`, `RadzenCompareValidator`, `RadzenCustomValidator` |

Reference: https://blazor.radzen.com/?theme=material3-dark

### Colors

Use the **Material 3 Dark** theme color system. Reference: https://blazor.radzen.com/colors?theme=material3-dark

#### Theme Color CSS Variables

| Variable | Purpose |
|----------|---------|
| `--rz-primary` / `--rz-primary-lighter` / `--rz-primary-light` / `--rz-primary-dark` / `--rz-primary-darker` | Primary brand color shades |
| `--rz-secondary` / `-lighter` / `-light` / `-dark` / `-darker` | Secondary color shades |
| `--rz-info` / `-lighter` / `-light` / `-dark` / `-darker` | Informational color shades |
| `--rz-success` / `-lighter` / `-light` / `-dark` / `-darker` | Success color shades |
| `--rz-warning` / `-lighter` / `-light` / `-dark` / `-darker` | Warning color shades |
| `--rz-danger` / `-lighter` / `-light` / `-dark` / `-darker` | Danger/error color shades |
| `--rz-base` / `-lighter` / `-light` / `-dark` / `-darker` | Base/neutral color shades |
| `--rz-white` / `--rz-black` | Absolute white and black |
| `--rz-base-50` through `--rz-base-900` | Base grayscale scale |
| `--rz-series-1` through `--rz-series-8` | Chart/data visualization series colors |

#### Color Utility CSS Classes

- **Background:** `rz-background-color-{color}` — e.g. `rz-background-color-primary`, `rz-background-color-success-light`, `rz-background-color-base-700`
- **Text color:** `rz-color-{color}` — e.g. `rz-color-primary`, `rz-color-danger-dark`, `rz-color-base-300`
- **Border color:** `rz-border-color-{color}` — e.g. `rz-border-color-info`, `rz-border-color-warning-lighter`

Where `{color}` can be: `white`, `black`, `base-50` through `base-900`, `primary`, `secondary`, `info`, `success`, `warning`, `danger` (each with `-lighter`, `-light`, `-dark`, `-darker` variants).

### Typography

Use the `<RadzenText>` component for all text formatting. Reference: https://blazor.radzen.com/typography?theme=material3-dark

#### Text Styles

Set via the `TextStyle` property on `<RadzenText>`:

| TextStyle | Usage |
|-----------|-------|
| `TextStyle.H1` – `TextStyle.H6` | Headings |
| `TextStyle.Subtitle1`, `TextStyle.Subtitle2` | Subtitles |
| `TextStyle.Body1`, `TextStyle.Body2` | Body text |
| `TextStyle.Caption` | Small captions |
| `TextStyle.Overline` | Overline text (uppercase) |
| `TextStyle.DisplayH1` – `TextStyle.DisplayH6` | Display headings for emphasis |

- Use `TagName` together with `TextStyle` for semantic HTML while applying a different visual style (e.g. a `<p>` styled as H3).
- Use `TextAlign` property: `TextAlign.Left`, `TextAlign.Center`, `TextAlign.Right`, `TextAlign.Start`, `TextAlign.End`, `TextAlign.Justify`.

#### Text Color CSS Variables

| Variable | Purpose |
|----------|---------|
| `--rz-text-color` | Default text color |
| `--rz-text-secondary-color` | Secondary/muted text |
| `--rz-text-tertiary-color` | Tertiary/subtle text |
| `--rz-text-disabled-color` | Disabled text |
| `--rz-text-contrast-color` | High-contrast text |

Use inline: `style="color: var(--rz-text-secondary-color);"`

#### Text Transform CSS Classes

- `rz-text-uppercase` — ALL CAPS
- `rz-text-lowercase` — all lowercase
- `rz-text-capitalize` — Capitalize Each Word

#### Text Wrap CSS Classes

- `rz-text-wrap` — Normal text wrapping
- `rz-text-nowrap` — Prevent wrapping
- `rz-text-truncate` — Truncate with ellipsis

### Icons

Use the `<RadzenIcon>` component. Reference: https://blazor.radzen.com/icon?theme=material3-dark

- **Default icon font:** Material Symbols Outlined (`MaterialSymbolsOutlined.woff2`) — 2,500+ glyphs, embedded in Radzen Blazor.
- **Full icon list:** https://fonts.google.com/icons?icon.set=Material+Symbols
- **Usage:** `<RadzenIcon Icon="dashboard" />` — use the ligature name as the `Icon` property value.
- **Icon color:** Use the `IconColor` property for custom foreground colors.
- **Icon style:** Use the `IconStyle` property for theme-defined styles (`IconStyle.Primary`, `IconStyle.Secondary`, `IconStyle.Info`, `IconStyle.Success`, `IconStyle.Warning`, `IconStyle.Danger`, `IconStyle.Light`, `IconStyle.Dark`).
- **Filled icons:** Apply CSS `font-variation-settings: 'FILL' 1;` for filled icon variants.
- **Alternative icon fonts:** Set `--rz-icon-font-family` CSS variable to use Material Symbols Rounded, FontAwesome, or other ligature-based icon fonts via `@font-face`.

### Borders

Use border utility CSS classes. Reference: https://blazor.radzen.com/borders?theme=material3-dark

#### Border Radius Classes

- `rz-border-radius` — Theme default radius
- `rz-border-radius-0` through `rz-border-radius-10` — Specific radius values

#### Add/Remove Borders

- **Add:** `rz-border`, `rz-border-left`, `rz-border-right`, `rz-border-top`, `rz-border-bottom`, `rz-border-start`, `rz-border-end`
- **Remove:** `rz-border-0`, `rz-border-left-0`, `rz-border-right-0`, `rz-border-top-0`, `rz-border-bottom-0`, `rz-border-start-0`, `rz-border-end-0`

#### Border Color Classes

- Combined border + color: `rz-border-primary`, `rz-border-success`, `rz-border-danger`, etc. (shorthand for `rz-border rz-border-color-primary`)
- Separate color: `rz-border-color-{color}` — e.g. `rz-border-color-success`, `rz-border-color-base-500`

#### Border Width

Use the `--rz-border-width` CSS variable to customize border thickness:
- Single element: `style="--rz-border-width: 3px;"`
- Group: Set on a parent element to apply to all children using border classes.

#### Borders with CSS Variables

Use theme color variables inline: `style="border: 2px solid var(--rz-success);"`

### Breakpoints

Responsive breakpoints for adapting layouts at different screen sizes. Reference: https://blazor.radzen.com/breakpoints?theme=material3-dark

| Abbreviation | Name | Min Width |
|-------------|------|-----------|
| `xs` | Extra Small | ≥ 576px |
| `sm` | Small | ≥ 768px |
| `md` | Medium | ≥ 1024px |
| `lg` | Large | ≥ 1280px |
| `xl` | Extra Large | ≥ 1920px |
| `xx` | Extra Extra Large | ≥ 2560px |

#### Breakpoint Usage Patterns

- **Layouts:** `RadzenColumn` responsive sizes — e.g. `SizeMD="6"`, `SizeLG="4"`, `SizeXS="12"`
- **Display:** `.rz-display-{breakpoint}-{value}` — e.g. `rz-display-md-flex`, `rz-display-lg-none`
- **Overflow:** `.rz-overflow-{breakpoint}-{value}` — e.g. `rz-overflow-md-scroll`
- **Sizing:** `.rz-w-{breakpoint}-{size}` — e.g. `rz-w-md-100`
- **Spacing:** `.rz-m-{breakpoint}-{size}`, `.rz-p-{breakpoint}-{size}` — e.g. `rz-m-md-1`, `rz-p-lg-4`

### Display

Use display utility CSS classes to control element visibility and layout. Reference: https://blazor.radzen.com/display?theme=material3-dark

#### Display CSS Classes

Format: `rz-display-{value}`

| Class | Effect |
|-------|--------|
| `rz-display-none` | Hide element |
| `rz-display-block` | Block-level element |
| `rz-display-inline` | Inline element |
| `rz-display-inline-block` | Inline-block element |
| `rz-display-flex` | Flexbox container |
| `rz-display-inline-flex` | Inline flexbox container |
| `rz-display-grid` | Grid container |
| `rz-display-inline-grid` | Inline grid container |

#### Responsive Display

Format: `rz-display-{breakpoint}-{value}` — e.g. `rz-display-md-flex`, `rz-display-lg-none`

### Overflow

Use overflow utility CSS classes to control content overflow. Reference: https://blazor.radzen.com/overflow?theme=material3-dark

#### Overflow CSS Classes

Format: `rz-overflow-{value}`

| Class | Effect |
|-------|--------|
| `rz-overflow-auto` | Scroll only when needed |
| `rz-overflow-scroll` | Always show scrollbars |
| `rz-overflow-hidden` | Clip overflowing content |
| `rz-overflow-visible` | Content overflows the container |

#### Responsive Overflow

Format: `rz-overflow-{breakpoint}-{value}` — e.g. `rz-overflow-md-scroll`, `rz-overflow-lg-hidden`

For text overflow, use `rz-text-wrap`, `rz-text-nowrap`, and `rz-text-truncate` (see Typography section).
