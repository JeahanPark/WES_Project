$log = Get-Content '.claude/.discord-mirror.log'
Write-Host "total lines: $($log.Count)"
$matches = $log | Select-String 'newMsgs=(\d+)' -AllMatches
$total = 0
foreach ($m in $matches) { $total += [int]$m.Matches[0].Groups[1].Value }
Write-Host "total newMsgs delivered: $total"
$fires = ($log | Select-String 'FIRE').Count
Write-Host "total fires: $fires"
