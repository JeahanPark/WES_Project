Write-Host '=== inboxes ==='
Get-ChildItem "$env:USERPROFILE\.claude\teams\mirror-check\inboxes" -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host ("{0}  {1} bytes" -f $_.Name, $_.Length) }

Write-Host ''
Write-Host '=== cursor / threads / log files ==='
Get-ChildItem 'c:\GitFork\WES_Project\WES\.claude\.discord-*' -Force -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host ("{0}  {1} bytes  {2:yyyy-MM-dd HH:mm:ss}" -f $_.Name, $_.Length, $_.LastWriteTime) }

Write-Host ''
Write-Host '=== team config ==='
$cfg = "$env:USERPROFILE\.claude\teams\mirror-check\config.json"
if (Test-Path $cfg) { Get-Content $cfg -Raw } else { Write-Host 'config.json missing' }

Write-Host ''
Write-Host '=== inbox content (team-lead.json) ==='
$inbox = "$env:USERPROFILE\.claude\teams\mirror-check\inboxes\team-lead.json"
if (Test-Path $inbox) { Get-Content $inbox -Raw } else { Write-Host 'team-lead inbox empty' }
