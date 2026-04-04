# OpenAPI Implementation Plan

## Current State

- **`Microsoft.AspNetCore.OpenApi` v10.0.3** is already installed
- **`builder.Services.AddOpenApi()`** is already registered
- **`app.MapOpenApi()`** is already called in Development — the raw JSON doc is served at `/openapi/v1.json`
- **No UI** (Swagger UI, Scalar, or ReDoc) is configured — you can't browse/test the API interactively
- **No endpoint metadata** — controllers lack `[ProducesResponseType]`, `[EndpointSummary]`, summary tags, or schema annotations, so the generated doc is sparse
- **OData + OpenAPI gap** — `Microsoft.AspNetCore.OData` doesn't automatically emit rich OpenAPI schemas for `$filter`, `$expand`, `$select`, `$orderby` query options

---

## Phase 1 — Add an Interactive API Documentation UI

| Step | Action | Details |
|------|--------|---------|
| 1.1 | **Add Scalar NuGet package** | `Scalar.AspNetCore` is the modern, lightweight choice for .NET 10. Alternative: `Swashbuckle.AspNetCore.SwaggerUI`. |
| 1.2 | **Map the UI endpoint** in `Program.cs` | `app.MapScalarApiReference()` under the `IsDevelopment()` block, right after `MapOpenApi()`. |
| 1.3 | **Verify** | Browse `https://localhost:7173/scalar/v1` to see the interactive docs. |

---

## Phase 2 — Enrich the OpenAPI Document Metadata

| Step | Action | Details |
|------|--------|---------|
| 2.1 | **Configure document info** | In `AddOpenApi()`, set `Title`, `Version`, `Description`, `Contact` via the options callback. |
| 2.2 | **Add server URLs** | Declare the dev (`https://localhost:7173`) and any future staging/prod base URLs. |
| 2.3 | **Add security scheme** | Define the Bearer JWT scheme (matching your Identity API auth) so the UI can send tokens. |

---

## Phase 3 — Annotate Controllers with Response Metadata

| Step | Action | Files |
|------|--------|-------|
| 3.1 | **Add `[ProducesResponseType]` attributes** to every action | All 6 controllers (`Cases`, `Members`, `Authorities`, `Documents`, `WorkflowStateHistories`, `Bookmarks`) |
| 3.2 | **Add `[Tags]`** per controller | Group endpoints logically in the UI (e.g., `[Tags("Cases")]`). |
| 3.3 | **Add XML doc `<summary>` + `<response>` tags** | Already partially present — ensure all actions have them. Enable XML doc generation in the `.csproj`. |
| 3.4 | **Add `[Consumes]` / `[Produces]` attributes** | Declare `application/json` (and `multipart/form-data` for the Documents upload endpoint). |

Typical annotation pattern per action:

```csharp
/// <summary>Returns a paged, filterable list of LOD cases.</summary>
/// <response code="200">OData collection of cases.</response>
/// <response code="401">Unauthorized — missing or invalid token.</response>
[ProducesResponseType(typeof(IQueryable<LineOfDutyCase>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
```

---

## Phase 4 — OData Query Parameter Documentation

| Step | Action | Details |
|------|--------|---------|
| 4.1 | **Add an OpenAPI document transformer** | Register a custom `IOpenApiDocumentTransformer` that injects `$filter`, `$select`, `$expand`, `$orderby`, `$top`, `$skip`, `$count` parameter descriptions on every `[EnableQuery]` endpoint. |
| 4.2 | **Document bound actions/functions** | Ensure `Checkout`, `Checkin`, `ByCurrentState`, `Bookmarked` appear with correct parameter schemas. OData convention routing may not auto-generate these — add manual operation descriptions if needed. |

---

## Phase 5 — Enable XML Documentation

| Step | Action | Details |
|------|--------|---------|
| 5.1 | **Enable XML doc output** in `ECTSystem.Api.csproj` | Add `<GenerateDocumentationFile>true</GenerateDocumentationFile>`. |
| 5.2 | **Suppress warning 1591** | `<NoWarn>$(NoWarn);1591</NoWarn>` to avoid build warnings for undocumented members. |
| 5.3 | **Wire XML docs into OpenAPI** | .NET 10's built-in OpenAPI reads XML docs automatically when the file is present — no extra config needed. |

---

## Phase 6 — Schema Improvements

| Step | Action | Details |
|------|--------|---------|
| 6.1 | **Add `[Description]` attributes** to enum values | So `WorkflowState`, `IncidentType`, `MilitaryRank`, etc. display meaningful descriptions. |
| 6.2 | **Add `[Required]`/`[StringLength]` annotations** on model properties | Drives `required` and `maxLength` in the JSON Schema output. |
| 6.3 | **Use schema transformers** | Register `IOpenApiSchemaTransformer` to customize how `Delta<T>` (OData PATCH) and `SingleResult<T>` render in schemas. |

---

## Phase 7 — Non-Development Environment Considerations

| Step | Action | Details |
|------|--------|---------|
| 7.1 | **Decide production exposure** | Keep `MapOpenApi()` dev-only, or gate behind an auth policy for staging. |
| 7.2 | **Generate static OpenAPI file at build time** | Use `dotnet openapi generate` or MSBuild target to produce `openapi.json` as a build artifact for CI/CD, client generation, or API gateway import. |

---

## Recommended Implementation Order

1. **Phase 5** (XML docs) — low effort, immediate improvement to generated descriptions
2. **Phase 1** (Scalar UI) — instant developer productivity gain
3. **Phase 2** (document metadata) — polishes the doc header and auth
4. **Phase 3** (response annotations) — biggest effort, biggest quality improvement
5. **Phase 4** (OData params) — fills the OData-specific gap
6. **Phase 6** (schema) — refinement pass
7. **Phase 7** (production/CI) — when approaching deployment
