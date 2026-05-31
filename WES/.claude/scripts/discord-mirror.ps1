# discord-mirror.ps1 (v2 — inbox polling)
# Claude Code Agent Teams의 inbox 메시지를 Discord webhook으로 미러링한다.
#
# 동작 원리:
#   - SendMessage는 PostToolUse 훅 대상이 아니라 SDK 내부 라우팅이므로 훅으로 가로챌 수 없다.
#   - 대신 ~/.claude/teams/{team}/inboxes/{recipient}.json 에 저장되는 메시지를 폴링한다.
#   - cursor 파일(.discord-cursor.json)에 마지막으로 미러링한 메시지 timestamp를 보관한다.
#   - 새 메시지만 webhook으로 전송하고 cursor를 갱신한다.
#
# 호출 방법:
#   - settings.json의 Stop / SubagentStop / PostToolUse 훅에서 자동 호출 (stdin은 무시)
#   - 수동: powershell -ExecutionPolicy Bypass -File .claude\scripts\discord-mirror.ps1
#   - 강제 catch up: powershell ... discord-mirror.ps1 -Catchup
#
# Setup:
#   1. Discord 채널에서 webhook 생성 (Forum 채널 권장 — 팀별 스레드로 분리됨)
#   2. .claude/settings.local.json 의 env.WES_DISCORD_WEBHOOK_URL 에 webhook URL 입력
#   3. Claude Code 재시작

[CmdletBinding()]
param(
    [switch]$Catchup
)

$ErrorActionPreference = "Stop"

# ─── 0. 진단 로그 (훅 호출 흔적 추적용) ─────────────────────────
# Stop / SubagentStop 훅이 실제로 발사되는지 검증하려면 이 로그가 유일한 흔적.
# 한 줄/호출이라 noise가 적고, 검증 후에도 운영 모니터링 용도로 유지 가치 있음.
$logPath = Join-Path $PSScriptRoot "..\.discord-mirror.log"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$catchupTag = if ($Catchup) { "catchup" } else { "auto" }
Add-Content -Path $logPath -Value "[$timestamp] FIRE mode=$catchupTag pid=$PID"

# ─── 1. Webhook URL 검증 ─────────────────────────
$webhook = $env:WES_DISCORD_WEBHOOK_URL
if ([string]::IsNullOrEmpty($webhook) -or $webhook -like "*PLACEHOLDER*") {
    Add-Content -Path $logPath -Value "[$timestamp] EXIT no-webhook-url"
    exit 0
}

# stdin이 있어도 무시 (PostToolUse payload는 사용하지 않음)
if ([Console]::IsInputRedirected) {
    $null = [Console]::In.ReadToEnd()
}

# ─── 0.5. Mutex 획득 (동시 fire 직렬화 — race로 인한 forum thread 폭주 방지) ─────────────────────────
$mutex = New-Object System.Threading.Mutex($false, "WES_Discord_Mirror")
$acquired = $false
try {
    if (-not $mutex.WaitOne(30000)) {
        Add-Content -Path $logPath -Value "[$timestamp] EXIT mutex-timeout pid=$PID"
        $mutex.Dispose()
        exit 0
    }
    $acquired = $true
}
catch [System.Threading.AbandonedMutexException] {
    # 이전 process가 비정상 종료. 소유권 이전됐으니 진행.
    $acquired = $true
}
$lockTs = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
Add-Content -Path $logPath -Value "[$lockTs] LOCK pid=$PID"

try {

# ─── 1. 경로 정의 ─────────────────────────
$teamsDir       = Join-Path $env:USERPROFILE ".claude\teams"
$cursorPath     = Join-Path $PSScriptRoot "..\.discord-cursor.json"
$threadMapPath  = Join-Path $PSScriptRoot "..\.discord-threads.json"

if (-not (Test-Path $teamsDir)) {
    Add-Content -Path $logPath -Value "[$timestamp] DONE pid=$PID reason=no-teams"
    exit 0
}

# ─── 2. cursor / threadMap 로드 ─────────────────────────
function ConvertTo-HashtableRecursive($_obj) {
    if ($null -eq $_obj) { return @{} }
    if ($_obj -is [hashtable]) {
        $h = @{}
        foreach ($k in $_obj.Keys) { $h[$k] = ConvertTo-HashtableRecursive $_obj[$k] }
        return $h
    }
    if ($_obj -is [PSCustomObject]) {
        $h = @{}
        foreach ($p in $_obj.PSObject.Properties) { $h[$p.Name] = ConvertTo-HashtableRecursive $p.Value }
        return $h
    }
    return $_obj
}

function Read-Json($_path) {
    # PowerShell 5.1 호환: ConvertFrom-Json -AsHashtable 옵션이 없으므로
    # PSCustomObject로 받고 재귀로 hashtable 변환한다.
    if (-not (Test-Path $_path)) { return @{} }
    try {
        $raw = Get-Content $_path -Raw -Encoding UTF8
        if ([string]::IsNullOrWhiteSpace($raw)) { return @{} }
        $obj = $raw | ConvertFrom-Json
        if ($null -eq $obj) { return @{} }
        return (ConvertTo-HashtableRecursive $obj)
    } catch {
        return @{}
    }
}

$cursors   = Read-Json $cursorPath
$threadMap = Read-Json $threadMapPath
# firstRun skip 정책 제거: hook fire 직전 도착한 메시지가 영원히 누락되는 문제 발생.
# 중복 방지는 cursor(timestamp 기반) + mutex(직렬화)로 충분히 안전.
$firstRun  = $false

# ─── 3. 색상 매핑 ─────────────────────────
# title은 ASCII 안전 형식("from -> to")으로 통일.
# 이모지 prefix는 디스코드 클라이언트 폰트 환경에 따라 깨질 수 있어 사용하지 않는다.
# 대신 발신자 색상으로 시각적 구분.
function Get-SenderColor($_sender) {
    switch -Wildcard ($_sender) {
        "director"   { return 3447003 }   # 파랑
        "client"     { return 2278750 }   # 초록
        "team-lead"  { return 16494146 }  # 금색
        "user"       { return 16494146 }
        default      { return 9807270 }   # 회색
    }
}

# ─── 4. 시스템 메시지 필터 ─────────────────────────
# idle_notification, shutdown_response 등 JSON 페이로드는 미러링 제외
function Test-IsSystemMessage($_text) {
    if ([string]::IsNullOrWhiteSpace($_text)) { return $false }
    $trimmed = $_text.Trim()
    if (-not ($trimmed.StartsWith("{") -and $trimmed.EndsWith("}"))) { return $false }
    try {
        $parsed = $trimmed | ConvertFrom-Json
        if ($parsed.PSObject.Properties.Name -contains "type") { return $true }
    } catch { }
    return $false
}

# ─── 5. 새 메시지 수집 ─────────────────────────
$newMessages = New-Object System.Collections.Generic.List[object]

foreach ($teamDir in Get-ChildItem $teamsDir -Directory -ErrorAction SilentlyContinue) {
    $teamName   = $teamDir.Name
    $inboxesDir = Join-Path $teamDir.FullName "inboxes"
    if (-not (Test-Path $inboxesDir)) { continue }

    if (-not $cursors.ContainsKey($teamName)) { $cursors[$teamName] = @{} }
    $teamCursor = $cursors[$teamName]
    if ($teamCursor -isnot [hashtable]) {
        $teamCursor = @{}
        $cursors[$teamName] = $teamCursor
    }

    foreach ($inboxFile in Get-ChildItem $inboxesDir -Filter "*.json" -ErrorAction SilentlyContinue) {
        $recipient = $inboxFile.BaseName
        $lastTs = if ($teamCursor.ContainsKey($recipient)) { [string]$teamCursor[$recipient] } else { "" }

        try {
            $raw = Get-Content $inboxFile.FullName -Raw -Encoding UTF8
            if ([string]::IsNullOrWhiteSpace($raw)) { continue }
            $messages = $raw | ConvertFrom-Json
        } catch {
            continue
        }
        if ($null -eq $messages) { continue }
        if ($messages -isnot [System.Collections.IEnumerable] -or $messages -is [string]) {
            $messages = @($messages)
        }

        $maxTs = $lastTs
        foreach ($msg in $messages) {
            $ts = [string]$msg.timestamp
            if ([string]::IsNullOrEmpty($ts)) { continue }
            if (-not [string]::IsNullOrEmpty($lastTs) -and [string]::Compare($ts, $lastTs) -le 0) { continue }

            if ([string]::Compare($ts, $maxTs) -gt 0) { $maxTs = $ts }

            # 시스템 메시지 필터
            if (Test-IsSystemMessage $msg.text) { continue }

            $newMessages.Add([pscustomobject]@{
                team      = $teamName
                from      = [string]$msg.from
                to        = $recipient
                text      = [string]$msg.text
                summary   = [string]$msg.summary
                timestamp = $ts
            }) | Out-Null
        }

        $teamCursor[$recipient] = $maxTs
    }
}

# ─── 6. cursor 우선 저장 (전송 실패해도 중복 방지) ─────────────────────────
$cursors | ConvertTo-Json -Depth 10 | Set-Content $cursorPath -Encoding UTF8

if ($newMessages.Count -eq 0) {
    $tmDump = ($threadMap | ConvertTo-Json -Compress -Depth 5)
    Add-Content -Path $logPath -Value "[$timestamp] DONE pid=$PID newMsgs=0 firstRun=$firstRun threadMap=$tmDump"
    exit 0
}

# ─── 7. timestamp 순 정렬 후 webhook 전송 ─────────────────────────
$sorted = $newMessages | Sort-Object timestamp

foreach ($msg in $sorted) {
    $color = Get-SenderColor $msg.from

    $description = $msg.text
    if ($description.Length -gt 3800) {
        $description = $description.Substring(0, 3800) + "`n`n...[잘림]"
    }

    $embed = @{
        color       = $color
        title       = "$($msg.from) -> $($msg.to)"
        description = $description
        timestamp   = $msg.timestamp
    }
    if (-not [string]::IsNullOrWhiteSpace($msg.summary)) {
        $embed.footer = @{ text = "summary: $($msg.summary)" }
    }

    $threadId = if ($threadMap.ContainsKey($msg.team)) { [string]$threadMap[$msg.team] } else { "" }

    $bodyObj = @{ embeds = @($embed) }
    $url     = "$webhook" + "?wait=true"

    if ([string]::IsNullOrEmpty($threadId)) {
        $bodyObj.thread_name = $msg.team
    } else {
        $url = "$webhook" + "?wait=true&thread_id=$threadId"
    }

    $body = $bodyObj | ConvertTo-Json -Depth 10 -Compress
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($body)

    try {
        $resp = Invoke-RestMethod -Uri $url -Method Post -Body $bodyBytes -ContentType "application/json; charset=utf-8"

        if ([string]::IsNullOrEmpty($threadId) -and $resp.channel_id) {
            $threadMap[$msg.team] = [string]$resp.channel_id
        }
    } catch {
        [Console]::Error.WriteLine("Discord mirror failed: $_")
    }
}

# ─── 8. threadMap 저장 ─────────────────────────
$threadMap | ConvertTo-Json -Depth 10 | Set-Content $threadMapPath -Encoding UTF8

$tmDump = ($threadMap | ConvertTo-Json -Compress -Depth 5)
Add-Content -Path $logPath -Value "[$timestamp] DONE pid=$PID newMsgs=$($newMessages.Count) firstRun=$firstRun threadMap=$tmDump"

}
finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

exit 0
