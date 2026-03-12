$fixes = @(
    "docs/deploy-option-1-app-service.md:526",
    "docs/deploy-option-1a-app-service-dev.md:241",
    "docs/deploy-option-2-swa-app-service.md:266",
    "docs/deploy-option-3-container-apps.md:317",
    "docs/deploy-option-4-aks.md:432",
    "docs/deploy-option-5-azure-functions.md:364"
)
foreach ($fix in $fixes) {
    $p = $fix.Split(':')
    $f = $p[0]
    $n = [int]$p[1]
    $lines = Get-Content $f
    $line = $lines[$n - 1]
    $newLine = $line -replace '\| {2,}\|', '| |'
    if ($newLine -ne $line) {
        $lines[$n - 1] = $newLine
        Set-Content -Path $f -Value $lines
        Write-Host "Fixed: $fix"
    } else {
        Write-Host "NoChange: $fix"
    }
}
Write-Host "Done"
