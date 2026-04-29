$src = "c:\Users\mccoy\AppData\Roaming\Code - Insiders\User\workspaceStorage\0d815da52215a483a4fd9c5a7912676b\GitHub.copilot-chat\chat-session-resources\79cb03ba-71b4-4b07-8f40-07d6a71c74b1\toolu_vrtx_01KEj7b4TjGqLtquqjr7ZX4e__vscode-1777497414512\content.json"
$j = Get-Content $src -Raw | ConvertFrom-Json -Depth 30
$cols = $j.columnInfo | Sort-Object columnOrdinal
$colNames = $cols | ForEach-Object { $_.columnName }
$colTypes = @{}; foreach ($c in $cols) { $colTypes[$c.columnName] = $c.dataTypeName }

function Format-Value($cell, $typeName) {
  if ($cell.isNull) { return 'NULL' }
  $v = $cell.displayValue
  switch ($typeName) {
    'bit'    { if ($v -eq 'True' -or $v -eq '1') { return '1' } else { return '0' } }
    'int'    { return $v }
    'bigint' { return $v }
    default  { return "'" + $v.Replace("'","''") + "'" }
  }
}

$colList = ($colNames | ForEach-Object { "[$_]" }) -join ','
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("SET XACT_ABORT ON;")
[void]$sb.AppendLine("SET QUOTED_IDENTIFIER ON;")
[void]$sb.AppendLine("BEGIN TRAN;")
[void]$sb.AppendLine("DELETE FROM AspNetUserRoles;")
[void]$sb.AppendLine("DELETE FROM AspNetUserClaims;")
[void]$sb.AppendLine("DELETE FROM AspNetUserLogins;")
[void]$sb.AppendLine("DELETE FROM AspNetUserTokens;")
[void]$sb.AppendLine("DELETE FROM AspNetRoleClaims;")
[void]$sb.AppendLine("DELETE FROM AspNetRoles;")
# Rename existing local users to avoid UserName/Email unique-index collisions when inserting remote users.
[void]$sb.AppendLine("UPDATE AspNetUsers SET UserName = '_old_' + Id, NormalizedUserName = '_OLD_' + UPPER(Id), Email = '_old_' + Id, NormalizedEmail = '_OLD_' + UPPER(Id);")
foreach ($row in $j.rows) {
  $cells = @($row); $vals = @()
  for ($i = 0; $i -lt $cells.Count; $i++) { $vals += (Format-Value $cells[$i] $colTypes[$colNames[$i]]) }
  [void]$sb.AppendLine("INSERT INTO AspNetUsers ($colList) VALUES (" + ($vals -join ',') + ");")
}
[void]$sb.AppendLine("UPDATE Bookmarks SET UserId = 'aa6be79c-ee31-45e1-9aed-48c8a68e9264' WHERE UserId = '0b3fdf71-c10b-40e3-a97c-aa348d881464';")
$keepIds = ($j.rows | ForEach-Object { "'" + $_[0].displayValue + "'" }) -join ','
[void]$sb.AppendLine("DELETE FROM AspNetUsers WHERE Id NOT IN ($keepIds);")
[void]$sb.AppendLine("COMMIT;")
[void]$sb.AppendLine("SELECT 'AspNetUsers' AS T, COUNT(*) AS N FROM AspNetUsers UNION ALL SELECT 'AspNetRoles', COUNT(*) FROM AspNetRoles UNION ALL SELECT 'AspNetUserRoles', COUNT(*) FROM AspNetUserRoles;")

Set-Content -Path "D:\source\repos\donellmccoy\FormValidationExperiments\sync-users.sql" -Value $sb.ToString() -Encoding UTF8
Write-Host "Wrote sync-users.sql"
