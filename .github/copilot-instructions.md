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

```
FormValidationExperiments.Web/
├── Pages/                  # Razor pages with code-behind (.razor.cs) and scoped styles (.razor.css)
│   ├── Home.razor/.cs/.css       # Multi-step wizard workflow (primary page)
│   └── NotFound.razor            # 404 fallback page
├── ViewModels/             # Form models per workflow step (namespace: FormValidationExperiments.Web.ViewModels)
│   ├── CaseInfoModel.cs            # Read-only case header/summary data
│   ├── MemberInfoFormModel.cs      # Items 1–8: member identification
│   ├── MedicalAssessmentFormModel.cs # Items 9–15: medical/clinical data
│   ├── CommanderReviewFormModel.cs  # Items 16–23: commander endorsement
│   ├── LegalSJAReviewFormModel.cs  # Items 24–25: legal sufficiency review
│   └── DocumentItem.cs            # Supporting document metadata
├── Models/                 # Domain model classes (namespace: FormValidationExperiments.Web.Models)
│   ├── LODCase.cs                # Root aggregate — full LOD case record
│   ├── LODDocument.cs            # Uploaded/attached documents
│   ├── LODAppeal.cs              # Appeal records
│   ├── LODAuthority.cs           # Reviewing authority entries
│   ├── MEDCONDetails.cs          # Medical Continuation benefit tracking
│   ├── INCAPDetails.cs           # Incapacitation Pay benefit tracking
│   └── TimelineStep.cs           # Workflow timeline entries
├── Enums/                  # Domain enums (namespace: FormValidationExperiments.Web.Enums)
│   ├── CommanderRecommendation.cs  # Item 21: commander's LOD recommendation
│   ├── DutyStatus.cs              # Duty status at time of incident
│   ├── IncidentType.cs            # Injury, Illness, Disease, Death, etc.
│   ├── LineOfDutyFinding.cs       # ILOD, NILOD, EPTS findings
│   ├── LineOfDutyProcessType.cs   # Informal vs. Formal process
│   ├── MemberStatus.cs            # Item 8: AFR, ANG member status
│   ├── MilitaryRank.cs            # Enlisted, Officer, Cadet ranks
│   ├── ServiceComponent.cs        # RegAF, USSF, AFR, ANG
│   └── SubstanceType.cs           # Item 13a: Alcohol, Drugs, Both
├── Shared/                 # Reusable components
│   └── WorkflowSidebar.razor/.cs/.css  # Vertical step-progress sidebar
├── Layout/                 # App layout
│   └── MainLayout.razor
├── Program.cs              # WebAssembly host builder and service registration
├── App.razor               # Root component with Router
├── _Imports.razor           # Global usings for Radzen, domain, and view-model namespaces
├── AF348_06012015_Template.md/.pdf  # Reference AF Form 348 template
└── wwwroot/                # Static assets (index.html, CSS, sample data)
```

## Namespaces

| Namespace | Contents |
|-----------|----------|
| `FormValidationExperiments.Web.Models` | Domain model classes (`Models/` folder) |
| `FormValidationExperiments.Web.Enums` | Domain enums (`Enums/` folder) |
| `FormValidationExperiments.Web.Pages` | Page components (`Home`) |
| `FormValidationExperiments.Web.ViewModels` | Form/view models per workflow step |
| `FormValidationExperiments.Web.Shared` | Shared components (`WorkflowSidebar`, `WorkflowStep`, `WorkflowStepStatus`) |
| `FormValidationExperiments.Web.Layout` | Layout components |

## Coding Conventions

- Use **partial classes** with code-behind files (`.razor.cs`) to separate logic from markup.
- View models live in `ViewModels/` under `FormValidationExperiments.Web.ViewModels`; domain models live under `FormValidationExperiments.Web.Models`; enums live under `FormValidationExperiments.Web.Enums`.
- Namespaces follow the folder structure (e.g., `FormValidationExperiments.Web.Models`, `FormValidationExperiments.Web.Enums`).
- Format enum display names by inserting spaces before uppercase letters using `Regex.Replace(value.ToString(), "(\\B[A-Z])", " $1")`.
- Use component-scoped CSS (`.razor.css`) rather than global styles where possible.
- Follow the existing pattern of one form model per workflow step (e.g., `MemberInfoFormModel`, `MedicalAssessmentFormModel`, `CommanderReviewFormModel`, `LegalSJAReviewFormModel`).
- Workflow steps are tracked via a `List<WorkflowStep>` with the `WorkflowStepStatus` enum (`Completed`, `InProgress`, `Pending`), defined in `Shared/WorkflowSidebar.razor.cs`.
- Use conditional visibility properties (e.g., `ShowSubstanceType`, `ShowToxicologyResults`) to toggle dependent form sections.
- Dropdown data sources use helper classes like `DropdownItem<T>` populated from `Enum.GetValues<T>()`.

## Component Patterns

- Use Radzen components (`RadzenTextBox`, `RadzenDropDown`, `RadzenButton`, `RadzenTabs`, etc.) for all form controls and UI elements.
- Radzen services are registered via `builder.Services.AddRadzenComponents()` in `Program.cs`.
- Global Radzen imports (`@using Radzen`, `@using Radzen.Blazor`) are in `_Imports.razor`.
- The `Home` page implements a **multi-step wizard** pattern — the `WorkflowSidebar` drives step navigation, and `selectedTabIndex` / `currentStepIndex` control which form sections are visible.
- The `Home` page uses a `FormatEnum<T>()` helper for display-friendly enum names.

## Domain Context

This application models the U.S. Air Force's Line of Duty determination process per AFI 36-2910 (now DAFI 36-2910). Key domain concepts:

- **AF Form 348** — The official form being modeled; view models map to numbered form items (Items 1–25+).
- **LOD Case** (`LODCase.cs`) — Root aggregate tracking an injury/illness determination through multiple review stages, including documents, appeals, MEDCON/INCAP details, timeline, and audit comments.
- **Workflow Steps** — Start → Member Reports → LOD Initiation → Medical Assessment → Commander Review → Legal/SJA Review → Wing CC Review → Board Review (8 steps in the current implementation).
- **Service Components** — RegAF (Regular Air Force), AFR (Air Force Reserve), ANG (Air National Guard). ARC-specific fields appear conditionally for Reserve/Guard members.
- **Findings** — Line of Duty, Not in Line of Duty (NILOD), with proximate cause analysis.
- **MEDCON/INCAP** — Medical Continuation and Incapacitation Pay benefit tracking via dedicated detail classes.
- **EPTS/NSA** — Existed Prior to Service / Not Service Aggravated; tracked in the medical assessment with service-aggravation sub-fields.

## Guidelines

- When adding new workflow steps or forms, follow the existing multi-step wizard pattern in `Home.razor` / `Home.razor.cs`.
- When adding new tabbed form sections, follow the existing tabbed pattern in `Workflow.razor` / `Workflow.razor.cs`.
- Keep form validation logic and conditional visibility in code-behind files.
- Use `EditForm` with model binding for form handling; each workflow step should have its own `EditForm` with a dedicated submit handler.
- Prefer strongly-typed enums for status values and dropdown options; populate dropdowns from `Enum.GetValues<T>()`.
- When generating test data, use realistic military/LOD terminology consistent with AF Form 348 item numbering.
- The `WorkflowStep` and `WorkflowStepStatus` types are defined in `Shared/WorkflowSidebar.razor.cs` — import via `@using FormValidationExperiments.Web.Shared` (already in `_Imports.razor`).
- Use the `CaseInfoModel` for read-only case summary/header displays; use the specific form models for editable form sections.
