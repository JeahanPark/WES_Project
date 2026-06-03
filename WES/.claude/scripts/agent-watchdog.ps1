# agent-watchdog.ps1
# 서브에이전트 트랜스크립트(agent-*.jsonl)를 검사해 멈춤/포맷붕괴를 탐지한다 (TEAM_PROCESS §0-3).
#
# 탐지 신호:
#   - STALL_BREAKDOWN : assistant의 'text' 블록에 도구호출 문법('invoke name=' 등)이 누출 = 포맷 붕괴.
#                       더 깨워도 무한 반복 → 종료+재spawn 필요. 가장 신뢰도 높은 신호.
#   - STALL_HUNG      : 마지막 블록이 tool_use인데 -StaleSec 초과 무응답 = 도구/턴 행.
#   - IDLE            : 마지막이 text인데 -StaleSec 초과 = 완료했거나 유휴. (in-flight면 점검 대상)
#   - OK              : 최근 활동 있음.
#
# 사용:
#   powershell -ExecutionPolicy Bypass -File agent-watchdog.ps1                 # 최신 세션 전체
#   powershell ... agent-watchdog.ps1 -Ids a727...,adfb...   -StaleSec 180      # 특정 in-flight 에이전트만
#   powershell ... agent-watchdog.ps1 -SessionDir <path>
#
# 종료코드: STALL_BREAKDOWN/STALL_HUNG가 하나라도 있으면 2 (asyncRewake 훅 호환), 아니면 0.

[CmdletBinding()]
param(
    [string]$SessionDir = "",
    [string]$Ids = "",
    [int]$StaleSec = 180
)

$ErrorActionPreference = "Stop"
$projRoot = Join-Path $env:USERPROFILE ".claude\projects\c--GitFork-WES-Project-WES"

# ── 1. 세션 디렉터리 결정 (subagents 폴더를 가진 최신) ──
if ([string]::IsNullOrWhiteSpace($SessionDir)) {
    $cand = Get-ChildItem $projRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName "subagents") } |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($null -eq $cand) { Write-Output "[]"; exit 0 }
    $SessionDir = $cand.FullName
}
$subDir = Join-Path $SessionDir "subagents"
if (-not (Test-Path $subDir)) { Write-Output "[]"; exit 0 }

$idFilter = @()
if (-not [string]::IsNullOrWhiteSpace($Ids)) {
    $idFilter = $Ids.Split(",") | ForEach-Object { $_.Trim() } | Where-Object { $_ }
}

$now = (Get-Date).ToUniversalTime()
$breakdownRegex = 'invoke name=|<invoke\b|antml:invoke|<function_calls>'
$results = New-Object System.Collections.Generic.List[object]
$anyStall = $false

foreach ($f in Get-ChildItem $subDir -Filter "agent-*.jsonl" -ErrorAction SilentlyContinue) {
    $id = $f.BaseName -replace '^agent-', ''
    if ($idFilter.Count -gt 0 -and ($idFilter -notcontains $id)) { continue }

    $lines = Get-Content $f.FullName -ErrorAction SilentlyContinue
    if (-not $lines -or $lines.Count -eq 0) { continue }

    $lastTs = $null
    $lastBlock = ""
    $breakdown = $false
    $toolUsesTail = 0

    # 꼬리 12줄만 검사 (붕괴/정체는 최근에 나타남)
    $tail = if ($lines.Count -gt 12) { $lines[($lines.Count-12)..($lines.Count-1)] } else { $lines }
    foreach ($ln in $lines) {
        if ([string]::IsNullOrWhiteSpace($ln)) { continue }
        try { $o = $ln | ConvertFrom-Json } catch { continue }
        if ($o.timestamp) { $lastTs = [string]$o.timestamp }
    }
    foreach ($ln in $tail) {
        if ([string]::IsNullOrWhiteSpace($ln)) { continue }
        try { $o = $ln | ConvertFrom-Json } catch { continue }
        $msg = $o.message
        if ($null -eq $msg) { continue }
        $role = $msg.role
        $content = $msg.content
        if ($content -is [System.Collections.IEnumerable] -and $content -isnot [string]) {
            foreach ($b in $content) {
                $bt = $b.type
                if ($role -eq "assistant") {
                    $lastBlock = $bt
                    if ($bt -eq "tool_use") { $toolUsesTail++ }
                    if ($bt -eq "text" -and $b.text -and ($b.text -match $breakdownRegex)) {
                        $breakdown = $true
                    }
                }
            }
        }
    }

    $secs = -1
    if ($lastTs) {
        try { $secs = [int]($now - ([datetime]::Parse($lastTs)).ToUniversalTime()).TotalSeconds } catch { $secs = -1 }
    }

    $status = "OK"
    if ($breakdown) { $status = "STALL_BREAKDOWN" }
    elseif ($secs -ge $StaleSec -and $lastBlock -eq "tool_use") { $status = "STALL_HUNG" }
    elseif ($secs -ge $StaleSec) { $status = "IDLE" }

    if ($status -eq "STALL_BREAKDOWN" -or $status -eq "STALL_HUNG") { $anyStall = $true }

    $results.Add([pscustomobject]@{
        id           = $id
        status       = $status
        secsSinceLast= $secs
        lastBlock    = $lastBlock
        toolUsesTail = $toolUsesTail
        breakdown    = $breakdown
        lastTs       = $lastTs
    }) | Out-Null
}

$results | ConvertTo-Json -Depth 5 -Compress
if ($anyStall) { exit 2 } else { exit 0 }
