$files = Get-ChildItem "docs/*.md"
foreach ($file in $files) {
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $changed = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\|.*\|$') {
            # Check if separator row (only pipes, dashes, colons, spaces)
            if ($line -match '^\|[\s:-]+(\|[\s:-]+)+\|$') {
                # Replace long dashes with exactly 3
                $newLine = $line -replace '-{3,}', '---'
                $newLine = $newLine -replace '\s+', ' '
                $newLine = $newLine.TrimEnd()
            }
            else {
                # Content row: trim each cell
                $parts = $line -split '\|'
                $trimmedParts = @()
                for ($j = 0; $j -lt $parts.Count; $j++) {
                    $trimmedParts += $parts[$j].Trim()
                }
                $newLine = $trimmedParts -join ' | '
                $newLine = $newLine.Trim()
                if ($newLine.StartsWith('| |')) { $newLine = $newLine.Substring(2) }
                if ($newLine.EndsWith('| |')) { $newLine = $newLine.Substring(0, $newLine.Length - 2) }
                $newLine = $newLine.TrimEnd()
            }
            if ($newLine -ne $line) {
                $lines[$i] = $newLine
                $changed = $true
            }
        }
    }
    if ($changed) {
        [System.IO.File]::WriteAllLines($file.FullName, $lines)
        Write-Host "Fixed tables: $($file.Name)"
    }
}
Write-Host "Done"
