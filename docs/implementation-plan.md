# Implementation Plan — Project Analysis Recommendations

This plan organizes the remaining recommendations from the project analysis into phased, actionable work items. Each phase groups related changes that can be developed and tested together.

---

## Phase 4: Architecture & Maintainability (Priority: Medium)

### 4.1 Extract EditCase Child Components

**Problem:** `EditCase.razor.cs` is a 1000+ line God Component spanning multiple partial files with 30+ injected services. It handles case CRUD, documents, workflow transitions, member search, bookmarks, previous cases, history tracking, validation, and PDF generation.

**Files:**
- `ECTSystem.Web/Pages/EditCase.razor` / `.razor.cs` and all partial files

**Steps:**
1. **Identify boundaries:** Map which fields/methods belong to which concern
2. **Extract `DocumentManager` component:**
   - Move `_documentsData`, `_documentsGrid`, `LoadDocumentsData()`, upload/download logic
   - Parameters: `CaseId`, `Documents` (initial data)
   - Events: `OnDocumentUploaded`, `OnDocumentDeleted`
3. **Extract `WorkflowTransitionPanel` component:**
   - Move workflow action buttons, `TransitionAsync()`, guard display logic
   - Parameters: `Case`, `StateMachine`
   - Events: `OnTransitioned`
4. **Extract `MemberSearchModal` component:**
   - Move member search dialog, `_memberSearchResults`, search/select handlers
   - Events: `OnMemberSelected`
5. **Extract `PreviousCasesGrid` component:**
   - Move `_previousCases`, `LoadPreviousCasesAsync()`
   - Parameters: `MemberId`
6. **Wire up:** Replace inline markup/logic in EditCase with new child components, passing parameters and handling events
7. **Verify:** Each component should be independently testable

**Validation:** All existing functionality works as before; EditCase.razor.cs drops below 300 lines

---

## ~~Phase 5: Testing (Priority: Medium)~~ ✅ Completed

### ~~5.1 Add Integration Test Harness~~ ✅

**Implemented:** `EctSystemWebApplicationFactory` with in-memory SQLite, `SqlServerToSqliteInterceptor` for DDL/DML rewrites, `IntegrationTestBase` with `IClassFixture` pattern, and `CasesIntegrationTests`.

**Files:**
- `ECTSystem.Tests/Integration/EctSystemWebApplicationFactory.cs`
- `ECTSystem.Tests/Integration/IntegrationTestBase.cs`
- `ECTSystem.Tests/Integration/CasesIntegrationTests.cs`

---

### ~~5.2 Add Regression Tests for Known Bugs~~ ✅

**Implemented:** All four known bugs have been fixed and tested:
1. ✅ **Authority data loss:** `SaveTabFormDataAsync` captures `var authoritiesToSave = _lineOfDutyCase.Authorities` before `SaveCaseAsync` replaces the object
2. ✅ **IsLegallySufficient mapping:** Uses `.Equals("Legally sufficient", StringComparison.OrdinalIgnoreCase)` — tested in `LineOfDutyCaseMapperTests`
3. ✅ **WorkflowStateHistory ordering:** `.ThenByDescending(h => h.Id)` tiebreaker added; composite index `(LineOfDutyCaseId, CreatedDate DESC, Id DESC)` created
4. ✅ **OData Key() vs Filter():** `GetCaseAsync` uses `.Filter().Top(1)` pattern instead of `.Key()`

---

### ~~5.3 Add E2E Workflow Test~~ ✅

**Implemented:** `LineOfDutyStateMachineTests.cs` (~1700 lines, 40+ test methods) covers:
- Full `Draft → Completed` path: `FullWorkflow_Draft_ToCompleted_TraversesAllStates()`
- Guard condition tests verifying illegal transitions are blocked
- Moq-based `ICaseService.TransitionCaseAsync` success/failure scenarios

**Files:**
- `ECTSystem.Tests/StateMachines/LineOfDutyStateMachineTests.cs`

---

## Phase 6: Security Hardening (Priority: Lower — Pre-Production)

### 6.1 OData $expand Authorization Filtering

**Problem:** Any authenticated user can `$expand` navigation properties to access data they shouldn't see (e.g., medical assessments, SJA recommendations).

**Files:**
- `ECTSystem.Api/Controllers/CasesController.cs`
- Potentially a new `ODataAuthorizationFilter`

**Steps:**
1. Define which roles can access which navigation properties
2. Implement `IAsyncActionFilter` that inspects `$expand` query option and rejects unauthorized expansions
3. Apply to controllers via attribute or convention

---

### 6.2 Row-Level Security

**Problem:** All authenticated users can query all cases. No case-ownership or role-based filtering.

**Steps:**
1. Add claims-based role system (e.g., MedTech, Commander, SJA, WingCC, Admin)
2. Add query filter in DbContext: `modelBuilder.Entity<LineOfDutyCase>().HasQueryFilter(...)` scoped by user's unit/role
3. Add `[Authorize(Policy = "...")]` attributes on sensitive endpoints

---

### 6.3 Strengthen Password Policy for Production

**Files:**
- `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` — `AddIdentity()`

**Steps:**
1. Move current relaxed policy behind `IsDevelopment()` check
2. Add production-strength policy: min 12 chars, require digit + uppercase + special char
3. Consider CAC/PIV integration for military deployment (future)

---

## Phase Summary

| Phase | Items | Scope | Status |
|-------|-------|-------|--------|
| **Phase 4** | 4.1 | Architecture improvements | Not started |
| ~~**Phase 5**~~ | ~~5.1–5.3~~ | ~~Test coverage~~ | ✅ Completed |
| **Phase 6** | 6.1–6.3 | Pre-production security | Not started |

## Completed Phases

- ✅ **Phase 1: Critical Security & Stability** (1.1–1.4) — Structured logging, global exception handler, security headers, file upload MIME validation
- ✅ **Phase 2: Data Integrity & Correctness** (2.1–2.4) — WorkflowStateHistory ordering, optimistic concurrency, CaseId race condition, OData URL encoding
- ✅ **Phase 3: Performance** (3.1–3.3) — Duplicate HTTP elimination, database indexes, HTTP cache headers
- ✅ **Phase 4 partial: Rate Limiting** (4.2) — Sliding window rate limiter with 429 responses
- ✅ **Phase 5: Testing** (5.1–5.3) — Integration test harness with SQLite, regression tests for all known bugs, full E2E workflow state machine tests (40+ test methods)

## Existing Plans in `docs/` to Incorporate

The following existing documents in `docs/` align with recommendations and should be consulted:
- `implement-validation-summary.md` — Form validation UI improvements
- `case-detail-performance-plan.md` — Performance optimization strategy
- `split-data-service-plan.md` — Service layer decomposition
- `role-based-security-in-state-machine-guards.md` — Security in workflow transitions
- `future-optimizations.md` — Additional optimization ideas
