# xUnit v2 → v3 Migration Plan

## Current state (`ECTSystem.Tests`)

- `xunit` `2.*` (deprecated), `xunit.runner.visualstudio` `3.1.5` (already v3-compatible).
- 51 test files. Surface area touched by v3 breaking changes:

| Area | Files | Migration impact |
|---|---|---|
| `using Xunit;` | 33 | Mostly unchanged |
| `using Xunit.Abstractions;` | 7 | **Remove** — types moved into `Xunit` / `Xunit.v3` |
| `ITestOutputHelper` | 6 | Namespace change only (`Xunit` instead of `Xunit.Abstractions`) |
| `IAsyncLifetime` | 4 | **Breaking**: methods now return `ValueTask` instead of `Task` |
| `IClassFixture` / `ICollectionFixture` / `[Collection]` / `[CollectionDefinition]` | 2 / 1 / 1 / 1 | Mostly unchanged; verify fixture disposal |
| `Assert.Throws` / `ThrowsAsync` / `Assert.Equal` | 4 / 3 / 23 | Largely compatible; stricter overloads may surface new warnings |
| `[Theory]` / `[InlineData]` | 7 / 7 | Compatible |
| `Xunit.Sdk` (1 file: [ECTSystem.Tests/E2E/AlphabeticalOrderer.cs](../ECTSystem.Tests/E2E/AlphabeticalOrderer.cs)) | 1 | **Breaking**: `ITestCaseOrderer` signature changed — now generic over `ITestCase` from `Xunit.v3` with different members |

No `[assembly: CollectionBehavior]` / `[assembly: TestFramework]` / `[MemberData]` / `[ClassData]` / `Skip =` / `Record.Exception` usages — those migration categories are moot.

## Plan

### Phase 0 — Safety net

1. Confirm clean working tree; create branch `chore/xunit-v3`.
2. Baseline: `dotnet test ECTSystem.slnx -c Debug --filter "Category!=E2E&Category!=LoadTest"` and record pass/fail counts.

### Phase 1 — Package swap

1. In [ECTSystem.Tests/ECTSystem.Tests.csproj](../ECTSystem.Tests/ECTSystem.Tests.csproj):
   - Remove `<PackageReference Include="xunit" Version="2.*" />`.
   - Add `<PackageReference Include="xunit.v3" Version="3.*" />`.
   - Keep `xunit.runner.visualstudio` at `3.1.5` (already v3-aware).
   - Keep `Microsoft.NET.Test.Sdk` 18.4.0.
2. `dotnet restore`.

### Phase 2 — Namespace & usings cleanup

3. In the 7 files with `using Xunit.Abstractions;`: delete that line (v3 consolidates `ITestOutputHelper` etc. into `Xunit`). Keep `using Xunit;`.

### Phase 3 — Breaking API fixes

4. **`IAsyncLifetime` (4 files)**: change
   - `public async Task InitializeAsync()` → `public async ValueTask InitializeAsync()`
   - `public async Task DisposeAsync()` → `public async ValueTask DisposeAsync()`

   (If a class also implements `IAsyncDisposable`, reconcile the single `DisposeAsync` signature.)
5. **`AlphabeticalOrderer`** ([ECTSystem.Tests/E2E/AlphabeticalOrderer.cs](../ECTSystem.Tests/E2E/AlphabeticalOrderer.cs)): rewrite against v3's `ITestCaseOrderer`:
   - `using Xunit.v3;` (and drop `using Xunit.Abstractions;`).
   - New signature: `IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases) where TTestCase : ITestCase`.
   - `tc.TestMethod.Method.Name` → `tc.TestMethod!.MethodName`.

### Phase 4 — Build & remediate

6. `dotnet build ECTSystem.slnx -c Debug` — fix remaining compile errors iteratively (expect a handful from analyzer-driven `Assert.Equal` overload tightening, e.g. `Assert.Equal(int, long)` now flagged).
7. Resolve any new analyzer warnings promoted to errors (xunit.v3 ships analyzers by default).

### Phase 5 — Validate

8. Run the same filtered test command as the baseline; diff pass/fail against Phase 0.
9. Spot-run E2E and performance suites locally if tooling is available; otherwise note as out-of-scope.

### Phase 6 — Commit & push

10. Commit `chore(tests): migrate to xunit.v3` with summary of touched files.
11. Push branch, open PR (or push to `main` if that's the established flow).

## Risk / rollback

- Risk is contained to `ECTSystem.Tests` — production code doesn't reference xunit.
- Rollback = revert the single migration commit; `xunit.runner.visualstudio` 3.x still runs v2 if the package is swapped back.

## Fallback (if deferring)

Pin to the last v2 release to silence the deprecation without code changes:

- `<PackageReference Include="xunit" Version="2.9.3" />` — no other edits required.
