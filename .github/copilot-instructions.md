# Copilot Instructions

## Project Overview

This is a **Blazor WebAssembly** application targeting **.NET 10** that implements an **Air Force Line of Duty (LOD) Determination** workflow system. The app uses **Radzen Blazor** components for its UI and manages multi-step form workflows for processing LOD cases (injury/illness determinations for military members).

## Technology Stack

- **Framework:** .NET 10, Blazor WebAssembly (client-side)
- **UI Components:** Radzen Blazor (`Radzen.Blazor`)
- **Language:** C# (nullable disabled, implicit usings enabled)
- **CSS:** Component-scoped CSS (`.razor.css` files)

## Project Structure

- `Pages/` — Razor pages and their code-behind (`.razor.cs`) and scoped styles (`.razor.css`)
- `Models/` — Domain model classes under the `AirForceLODSystem` namespace (e.g., `LODCase`, `LODDocument`, enums)
- `Layout/` — App layout components (`MainLayout.razor`)
- `Shared/` — Reusable shared components (e.g., `WorkflowSidebar`)
- `wwwroot/` — Static assets (HTML entry point, CSS, sample data)

## Coding Conventions

- Use **partial classes** with code-behind files (`.razor.cs`) to separate logic from markup.
- Domain models live in the `AirForceLODSystem` namespace; page/form models live in `FormValidationExperiments.Web.Pages`.
- Format enum display names by inserting spaces before uppercase letters (e.g., `LODProcessType` → "LOD Process Type").
- Use component-scoped CSS (`.razor.css`) rather than global styles where possible.
- Follow the existing pattern of form models per workflow step (e.g., `MemberInfoFormModel`, `MedicalAssessmentFormModel`, `CommanderReviewFormModel`, `LegalSJAReviewFormModel`).
- Workflow steps are tracked via a `List<WorkflowStep>` with `WorkflowStepStatus` enum (`Completed`, `InProgress`, `Pending`).

## Component Patterns

- Use Radzen components (`RadzenTextBox`, `RadzenDropDown`, `RadzenButton`, `RadzenTabs`, etc.) for all form controls and UI elements.
- Radzen services are registered via `builder.Services.AddRadzenComponents()` in `Program.cs`.
- Global Radzen imports (`@using Radzen`, `@using Radzen.Blazor`) are in `_Imports.razor`.

## Domain Context

This application models the U.S. Air Force's Line of Duty determination process per AFI 36-2910. Key domain concepts:

- **LOD Case** — A case tracking an injury/illness determination through multiple review stages.
- **Workflow Steps** — Start → Member Reports → LOD Initiation → Medical Assessment → Commander Review → Legal/SJA Review → Wing CC Review → Board Review → Determination → Appeal → End.
- **Service Components** — RegAF (Regular Air Force), AFR (Air Force Reserve), ANG (Air National Guard).
- **Findings** — Line of Duty, Not in Line of Duty (NILOD), with proximate cause analysis.
- **MEDCON/INCAP** — Medical Continuation and Incapacitation Pay benefit tracking.

## Guidelines

- When adding new workflow steps or forms, follow the existing multi-step wizard pattern in `Home.razor` / `Home.razor.cs`.
- Keep form validation logic in code-behind files.
- Use `EditForm` with model binding for form handling.
- Prefer strongly-typed enums for status values and dropdown options.
- When generating test data, use realistic military/LOD terminology consistent with the existing codebase.
