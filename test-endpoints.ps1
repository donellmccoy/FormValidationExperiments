param([string]$Token)

$h = @{ Authorization = "Bearer $Token" }
$b = "https://localhost:7173"
$pass = 0; $fail = 0

function Test($name, $block) {
    Write-Host "`n=== $name ===" -ForegroundColor Cyan
    try {
        & $block
        $script:pass++
    } catch {
        Write-Host "FAIL: $($_.Exception.Message)" -ForegroundColor Red
        $script:fail++
    }
}

# T1: GET /odata/Cases (collection with query options)
Test "T1: GET /odata/Cases (collection)" {
    $r = Invoke-RestMethod "$b/odata/Cases?`$top=3&`$count=true&`$orderby=CreatedDate desc" -Headers $h -SkipCertificateCheck
    Write-Host "OK - count=$($r.'@odata.count') items=$($r.value.Count)"
}

# T2: GET /odata/Cases?$filter=CaseId eq '...' (single case with expand)
Test "T2: GET /odata/Cases (filter by CaseId)" {
    $r = Invoke-RestMethod "$b/odata/Cases?`$filter=CaseId eq '20250101-001'" -Headers $h -SkipCertificateCheck
    Write-Host "OK - items=$($r.value.Count)"
}

# T3: GET /odata/Cases(key) (single entity by key)
Test "T3: GET /odata/Cases(1)" {
    $r = Invoke-RestMethod "$b/odata/Cases(1)" -Headers $h -SkipCertificateCheck
    Write-Host "OK - CaseId=$($r.CaseId) HasMember=$($null -ne $r.Member)"
}

# T4: GET /odata/Members with filter and top
Test "T4: GET /odata/Members" {
    $r = Invoke-RestMethod "$b/odata/Members?`$top=25&`$orderby=LastName asc" -Headers $h -SkipCertificateCheck
    Write-Host "OK - items=$($r.value.Count)"
}

# T5: GET /odata/Cases/Bookmarked()
Test "T5: GET /odata/Cases/Bookmarked()" {
    $r = Invoke-RestMethod "$b/odata/Cases/Bookmarked()" -Headers $h -SkipCertificateCheck
    Write-Host "OK - items=$($r.value.Count)"
}

# T6: GET /odata/Documents (collection)
Test "T6: GET /odata/Documents" {
    $r = Invoke-RestMethod "$b/odata/Documents?`$top=10" -Headers $h -SkipCertificateCheck
    Write-Host "OK - items=$($r.value.Count)"
}

# T7: GET /odata/WorkflowStateHistories (NEW endpoint)
Test "T7: GET /odata/WorkflowStateHistories" {
    $r = Invoke-RestMethod "$b/odata/WorkflowStateHistories?`$top=10" -Headers $h -SkipCertificateCheck
    Write-Host "OK - items=$($r.value.Count)"
}

# T8: GET /odata/Cases(1)/Documents (navigation property)
Test "T8: GET /odata/Cases(1)/Documents" {
    $r = Invoke-RestMethod "$b/odata/Cases(1)/Documents" -Headers $h -SkipCertificateCheck
    Write-Host "OK - items=$($r.value.Count)"
}

# T9: GET /odata/Cases(1)/WorkflowStateHistories (navigation)
Test "T9: GET /odata/Cases(1)/WorkflowStateHistories" {
    $r = Invoke-RestMethod "$b/odata/Cases(1)/WorkflowStateHistories" -Headers $h -SkipCertificateCheck
    Write-Host "OK - items=$($r.value.Count)"
}

# T10: GET /odata/CaseBookmarks/IsBookmarked(caseId=1)
Test "T10: GET /odata/CaseBookmarks/IsBookmarked(caseId=1)" {
    $r = Invoke-RestMethod "$b/odata/CaseBookmarks/IsBookmarked(caseId=1)" -Headers $h -SkipCertificateCheck
    Write-Host "OK - value=$($r.value)"
}

# T11: POST /odata/CaseBookmarks (create or return existing)
Test "T11: POST /odata/CaseBookmarks" {
    $body = '{"LineOfDutyCaseId":1,"UserId":"test@test.com"}'
    $r = Invoke-RestMethod "$b/odata/CaseBookmarks" -Method Post -Body $body -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK - Id=$($r.Id)"
}

# T12: POST /odata/CaseBookmarks/DeleteByCaseId (deletes the bookmark we just created in T11)
Test "T12: POST /odata/CaseBookmarks/DeleteByCaseId" {
    $body = '{"caseId":1}'
    $r = Invoke-RestMethod "$b/odata/CaseBookmarks/DeleteByCaseId" -Method Post -Body $body -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK"
}

# T13: POST /odata/Cases(1)/CheckOut (bound action, convention routing)
Test "T13: POST /odata/Cases(1)/CheckOut" {
    $r = Invoke-RestMethod "$b/odata/Cases(1)/CheckOut" -Method Post -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK - CaseId=$($r.CaseId)"
}

# T14: POST /odata/Cases(1)/CheckIn (bound action, convention routing)
Test "T14: POST /odata/Cases(1)/CheckIn" {
    $r = Invoke-RestMethod "$b/odata/Cases(1)/CheckIn" -Method Post -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK - CaseId=$($r.CaseId)"
}

# T15: POST /odata/Cases(1)/Transition (bound action, convention routing)
Test "T15: POST /odata/Cases(1)/Transition" {
    $body = '{"NewWorkflowState":"MemberReported","HistoryEntries":[]}'
    $r = Invoke-RestMethod "$b/odata/Cases(1)/Transition" -Method Post -Body $body -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK - CaseId=$($r.CaseId)"
}

# T16: PATCH /odata/Cases(1) (partial update)
Test "T16: PATCH /odata/Cases(1)" {
    $body = '{"IncidentDescription":"Test update via PATCH"}'
    $r = Invoke-RestMethod "$b/odata/Cases(1)" -Method Patch -Body $body -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK - CaseId=$($r.CaseId)"
}

# T17: POST /odata/WorkflowStateHistories (single entry)
Test "T17: POST /odata/WorkflowStateHistories" {
    $body = '{"LineOfDutyCaseId":1,"WorkflowState":"MemberReported","Action":"Enter","Status":"Completed","PerformedBy":"test@test.com"}'
    $r = Invoke-RestMethod "$b/odata/WorkflowStateHistories" -Method Post -Body $body -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK - Id=$($r.Id)"
}

# T18: POST /odata/WorkflowStateHistories/Batch (collection action)
Test "T18: POST /odata/WorkflowStateHistories/Batch" {
    $body = '[{"LineOfDutyCaseId":1,"WorkflowState":"LODInitiated","Action":"Enter","Status":"Completed","PerformedBy":"test@test.com"}]'
    $r = Invoke-RestMethod "$b/odata/WorkflowStateHistories/Batch" -Method Post -Body $body -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK - Count=$($r.Count)"
}

# T19: OData bound action: POST /odata/Cases(1)/SaveAuthorities
Test "T19: POST /odata/Cases(1)/SaveAuthorities" {
    $body = '{"Authorities":[]}'
    $r = Invoke-RestMethod "$b/odata/Cases(1)/SaveAuthorities" -Method Post -Body $body -ContentType "application/json" -Headers $h -SkipCertificateCheck
    Write-Host "OK"
}

# T20: Non-OData: GET /api/cases/1/form348
Test "T20: GET /api/cases/1/form348" {
    $r = Invoke-RestMethod "$b/api/cases/1/form348" -Headers $h -SkipCertificateCheck
    Write-Host "OK - bytes=$($r.Length)"
}

Write-Host "`n========================================" -ForegroundColor Yellow
Write-Host "RESULTS: $pass PASSED, $fail FAILED" -ForegroundColor $(if ($fail -eq 0) { "Green" } else { "Red" })
Write-Host "========================================" -ForegroundColor Yellow
