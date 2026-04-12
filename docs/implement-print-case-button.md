# Plan: Make the "Print Case" Button Work

## Current State

- The Print Case button on `EditCase.razor` (line 102) has no `Click` handler
- The app already has Form 348 PDF generation: `DocumentService.GetForm348PdfAsync()` fetches a PDF, and `pdfViewerInterop` JS module creates blob URLs for iframe display
- `IJSRuntime` is already injected as `JSRuntime`

## Implementation Steps

### 1. Add a `printBlobUrl` JS interop function — `wwwroot/js/pdf-viewer.js`

- Add a `printBlobUrl(base64)` function that creates a blob URL, opens it in a hidden iframe, and calls `contentWindow.print()` to trigger the browser print dialog
- This keeps print logic in the existing `pdfViewerInterop` namespace

### 2. Create `OnPrintCaseClick` handler — `EditCase.Form348.razor.cs`

- Fetch the PDF via `DocumentService.GetForm348PdfAsync(_lineOfDutyCase.Id, _cts.Token)`
- Convert to base64 and call the new JS interop `pdfViewerInterop.printBlobUrl`
- Handle loading state (disable button while generating) and errors (notify via `NotificationService`)
- Guard against null/zero case ID

### 3. Wire up the button — `EditCase.razor` (line 102)

- Add `Click="@OnPrintCaseClick"` to the RadzenButton
- Add `Disabled="@_isPrintingCase"` to prevent double-clicks during generation

## Files Changed

| File | Change |
|------|--------|
| `ECTSystem.Web/wwwroot/js/pdf-viewer.js` | Add `printBlobUrl` function |
| `ECTSystem.Web/Pages/EditCase.Form348.razor.cs` | Add `OnPrintCaseClick` method + `_isPrintingCase` field |
| `ECTSystem.Web/Pages/EditCase.razor` | Add `Click` and `Disabled` attributes to Print Case button |

## Pattern

Follows the same pattern as `OnHistoryClick` (button → handler in code-behind) and `LoadForm348Async` (fetch PDF → JS interop).
