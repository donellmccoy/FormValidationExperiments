$localPath = "c:\Users\mccoy\AppData\Roaming\Code - Insiders\User\workspaceStorage\0d815da52215a483a4fd9c5a7912676b\GitHub.copilot-chat\chat-session-resources\79cb03ba-71b4-4b07-8f40-07d6a71c74b1\toolu_vrtx_01Sn5V2demqUF2EmVom1yxBL__vscode-1777497414488\content.json"
$remotePath = "c:\Users\mccoy\AppData\Roaming\Code - Insiders\User\workspaceStorage\0d815da52215a483a4fd9c5a7912676b\GitHub.copilot-chat\chat-session-resources\79cb03ba-71b4-4b07-8f40-07d6a71c74b1\toolu_vrtx_01FXyYG2LuCmtspXM6LMdFdn__vscode-1777497414489\content.json"

function Load-Counts($path) {
  $j = Get-Content $path -Raw | ConvertFrom-Json -Depth 20
  $h = @{}
  foreach ($row in $j.rows) {
    $cells = @($row)
    $key = "$($cells[0].displayValue).$($cells[1].displayValue)"
    $h[$key] = [int64]$cells[2].displayValue
  }
  return $h
}

$L = Load-Counts $localPath
$R = Load-Counts $remotePath
$keys = @($L.Keys + $R.Keys) | Sort-Object -Unique

"{0,-50} {1,12} {2,12} {3,10}" -f "Table","Local","Remote","Diff"
"-" * 88
foreach ($k in $keys) {
  $hasL = $L.ContainsKey($k); $hasR = $R.ContainsKey($k)
  $lv = if ($hasL) { $L[$k] } else { "MISSING" }
  $rv = if ($hasR) { $R[$k] } else { "MISSING" }
  $dv = if ($hasL -and $hasR) { ($L[$k] - $R[$k]).ToString() } else { "-" }
  "{0,-50} {1,12} {2,12} {3,10}" -f $k, $lv, $rv, $dv
}
