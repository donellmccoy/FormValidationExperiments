# ECT System — Line of Duty Determination Workflow

A multi-project .NET 10 solution implementing the U.S. Air Force **Line of Duty (LOD) Determination** workflow system per DAFI 36-2910. The application models the **AF Form 348** (Line of Duty Determination) through a multi-step wizard interface built with Blazor WebAssembly and Radzen Blazor components.

## Solution Structure

| Project | Description |
|---------|-------------|
| **ECTSystem.Web** | Blazor WebAssembly client — multi-step wizard UI, state machine workflow, PDF generation |
| **ECTSystem.Api** | ASP.NET Core Web API with OData endpoints for cases, members, documents, and workflow history |
| **ECTSystem.Persistence** | Entity Framework Core data access layer with SQL Server, migrations, and Identity |
| **ECTSystem.Shared** | Shared models, enums, view models, and mapping between API and Web projects |
| **ECTSystem.Tests** | xUnit integration tests for API controllers using EF Core InMemory provider |

## Technology Stack

- **.NET 10** (C#, nullable disabled, implicit usings)
- **Blazor WebAssembly** (standalone client-side)
- **Radzen Blazor** v9 — Material 3 Dark theme
- **ASP.NET Core OData** v9 — RESTful data API
- **Entity Framework Core** with SQL Server
- **ASP.NET Core Identity** — authentication and authorization
- **Stateless** — state machine for workflow transitions
- **PDFsharp** — AF Form 348 PDF generation
- **Riok.Mapperly** — compile-time object mapping
- **xUnit / Moq** — testing

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server (LocalDB, Express, or full instance)

## Getting Started

### 1. Clone the repository

```bash
git clone https://github.com/donellmccoy/FormValidationExperiments.git
cd FormValidationExperiments
```

### 2. Configure the database

Update the connection string in `ECTSystem.Api/appsettings.Development.json`, then apply migrations:

```bash
dotnet ef database update --project ECTSystem.Persistence --startup-project ECTSystem.Api
```

### 3. Build the solution

```bash
dotnet build ECTSystem.slnx
```

### 4. Run the application

Start both the API and Web projects:

```bash
# Terminal 1 — API
dotnet run --project ECTSystem.Api --launch-profile https

# Terminal 2 — Web
dotnet run --project ECTSystem.Web --launch-profile https
```

- **Web UI:** https://localhost:7240
- **API:** https://localhost:7173

### 5. Run tests

```bash
dotnet test ECTSystem.slnx
```

## Domain Overview

The application models the LOD determination process through these workflow stages:

1. **Start** — Case creation
2. **Member Reports** — Member identification (AF Form 348 Items 1–8)
3. **LOD Initiation** — Case initiation and assignment
4. **Medical Assessment** — Clinical data and medical review (Items 9–15)
5. **Commander Review** — Unit commander endorsement (Items 16–23)
6. **Legal/SJA Review** — Wing Judge Advocate legal review
7. **Wing CC Review** — Wing commander determination (Items 24–25)
8. **Board Review** — Line of Duty board review

### Key Domain Concepts

- **Service Components:** RegAF, USSF, AFR, ANG — ARC-specific fields appear conditionally for Reserve/Guard members
- **Findings:** In Line of Duty (ILOD), Not in Line of Duty (NILOD), Existed Prior to Service (EPTS)
- **Benefits Tracking:** MEDCON (Medical Continuation) and INCAP (Incapacitation Pay)
- **Documents:** Uploaded supporting documents attached to cases
- **Workflow History:** Full audit trail of state transitions with timestamps and actors
