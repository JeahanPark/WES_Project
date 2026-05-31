param([switch]$Catchup)
$ErrorActionPreference = 'Continue'
& "$PSScriptRoot\discord-mirror.ps1" -Catchup:$Catchup
$code = $LASTEXITCODE
Write-Host ('exit=' + $code)
Write-Host '--- after run ---'
Get-ChildItem 'c:\GitFork\WES_Project\WES\.claude\.discord-*' -Force -ErrorAction SilentlyContinue |
    ForEach-Object { Write-Host ('{0}  {1} bytes  {2:HH:mm:ss}' -f $_.Name, $_.Length, $_.LastWriteTime) }
