$ErrorActionPreference = "Stop"

# Login
$loginResult = Invoke-RestMethod -Uri "https://localhost:7173/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body (@{email="admin@ect.mil"; password="Pass123"} | ConvertTo-Json) `
    -SkipCertificateCheck
$token = $loginResult.accessToken
Write-Host "Login OK, got token"

# POST WorkflowStateHistory
$headers = @{ Authorization = "Bearer $token" }
$body = @{
    lineOfDutyCaseId = 1
    workflowState    = "MemberInformationEntry"
    action           = "Completed"
    status           = "Completed"
    occurredAt       = (Get-Date).ToUniversalTime().ToString("o")
    performedBy      = ""
} | ConvertTo-Json

Write-Host "Sending POST..."
Write-Host "Body: $body"

try {
    $response = Invoke-WebRequest -Uri "https://localhost:7173/odata/WorkflowStateHistories" `
        -Method POST `
        -ContentType "application/json" `
        -Headers $headers `
        -Body $body `
        -SkipCertificateCheck
    Write-Host "Status: $($response.StatusCode)"
    Write-Host "Response: $($response.Content)"
} catch {
    Write-Host "ERROR: $($_.Exception.Message)"
    $errResp = $_.Exception.Response
    if ($errResp) {
        Write-Host "StatusCode: $($errResp.StatusCode)"
        $stream = $errResp.GetResponseStream()
        $reader = [System.IO.StreamReader]::new($stream)
        Write-Host "Body: $($reader.ReadToEnd())"
        $reader.Close()
    }
}
