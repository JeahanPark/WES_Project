# update-vault.ps1
# WES 자동 문서화 vault 갱신.
#
# 동작:
#   1. update-catalog.js — Assets/Scripts/{Manager,Controller,Worker,Component}/ 스캔 → catalog/Class, catalog/Signal upsert (idempotent)
#   2. update-diagrams.js — catalog 결과로 WES-Class-Overview.canvas 재생성
#   3. 변경된 게 있을 때만 auto/log.md 에 한 줄 append
#   4. document/auto/reports/ 신규 .md 감지 → Discord에 요약 push
#       - frontmatter team 값이 .discord-threads.json 에 있으면 그 스레드로
#       - 없으면 "auto-doc" 스레드 (없으면 webhook이 자동 생성)
#
# 호출:
#   - Stop / SubagentStop hook (settings.json) — auto
#   - post-commit hook — auto (background)
#   - 수동: powershell -ExecutionPolicy Bypass -File .claude\scripts\auto-doc\update-vault.ps1
#
# 정책:
#   - 빠르게 종료 (hook chain 막지 않음)
#   - 에러는 log에 흔적만 남기고 exit 0
#   - stdout 최소화

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Script path: <projectRoot>/WES/.claude/scripts/auto-doc/update-vault.ps1
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..\..")).Path
$claudeDir = Join-Path $projectRoot "WES\.claude"
$logPath = Join-Path $claudeDir ".auto-doc.log"
$reportCursorPath = Join-Path $claudeDir ".auto-doc-report-cursor.json"
$threadMapPath = Join-Path $claudeDir ".discord-threads.json"
$vaultRoot = Join-Path $projectRoot "document\auto"
$logMdPath = Join-Path $vaultRoot "log.md"
$reportsDir = Join-Path $vaultRoot "reports"
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"

function Write-AutoDocLog($msg) {
    Add-Content -Path $logPath -Value "[$timestamp] $msg"
}

# Read text file as UTF-8 safely
function Read-Text($path) {
    if (-not (Test-Path $path)) { return "" }
    return Get-Content $path -Raw -Encoding UTF8
}

# Parse YAML-ish frontmatter (very simple — handles key: value and key: [array])
function ConvertFrom-Frontmatter($mdContent) {
    $fm = @{}
    if ($mdContent -match "^---\r?\n([\s\S]*?)\r?\n---") {
        $body = $Matches[1]
        foreach ($line in $body -split "`r?`n") {
            if ($line -match "^(\w+):\s*(.*)$") {
                $k = $Matches[1]
                $v = $Matches[2].Trim()
                if ($v.StartsWith('"') -and $v.EndsWith('"')) { $v = $v.Substring(1, $v.Length - 2) }
                $fm[$k] = $v
            }
        }
    }
    return $fm
}

# Get body text after frontmatter (used as Discord description preview)
function Get-Body($mdContent) {
    if ($mdContent -match "^---\r?\n[\s\S]*?\r?\n---\r?\n([\s\S]*)$") {
        return $Matches[1].Trim()
    }
    return $mdContent
}

# Read JSON safely (PowerShell 5.1 compatible)
function Read-Json($path) {
    if (-not (Test-Path $path)) { return @{} }
    $raw = Read-Text $path
    if ([string]::IsNullOrWhiteSpace($raw)) { return @{} }
    try {
        $obj = $raw | ConvertFrom-Json
        if ($null -eq $obj) { return @{} }
        # Convert PSCustomObject to hashtable
        $ht = @{}
        foreach ($p in $obj.PSObject.Properties) { $ht[$p.Name] = $p.Value }
        return $ht
    } catch {
        return @{}
    }
}

try {
    $null = Get-Command node -ErrorAction Stop
} catch {
    Write-AutoDocLog "EXIT no-node"
    exit 0
}

$scriptsDir = $PSScriptRoot
$catalogScript = Join-Path $scriptsDir "update-catalog.js"
$diagramsScript = Join-Path $scriptsDir "update-diagrams.js"

if (-not (Test-Path $catalogScript) -or -not (Test-Path $diagramsScript)) {
    Write-AutoDocLog "EXIT scripts-missing"
    exit 0
}

if ([Console]::IsInputRedirected) {
    $null = [Console]::In.ReadToEnd()
}

$mutex = New-Object System.Threading.Mutex($false, "WES_AutoDoc_Update")
$acquired = $false
try {
    if (-not $mutex.WaitOne(15000)) {
        Write-AutoDocLog "EXIT mutex-timeout"
        $mutex.Dispose()
        exit 0
    }
    $acquired = $true
} catch [System.Threading.AbandonedMutexException] {
    $acquired = $true
}

try {
    # 1. catalog 갱신
    $catalogOut = & node $catalogScript 2>&1 | Out-String
    $catalogChanged = $false
    $classCount = "0"; $sigCount = "0"
    if ($catalogOut -match "Class \.md: created=(\d+)") { $classCount = $Matches[1]; if ([int]$classCount -gt 0) { $catalogChanged = $true } }
    if ($catalogOut -match "Signal \.md: created=(\d+)") { $sigCount = $Matches[1]; if ([int]$sigCount -gt 0) { $catalogChanged = $true } }

    # 2. diagrams 재생성 (idempotent — 출력은 무시)
    & node $diagramsScript 2>&1 | Out-Null

    # 3. catalog 변경 시 log.md append
    if ($catalogChanged -and (Test-Path $logMdPath)) {
        $today = Get-Date -Format "yyyy-MM-dd"
        $line = "`n## [$today] ingest | Stop hook auto-update - new class=$classCount, new signal=$sigCount`n"
        $content = Read-Text $logMdPath
        $idx = $content.IndexOf("`n## [")
        if ($idx -ge 0) {
            $new = $content.Substring(0, $idx) + $line + $content.Substring($idx)
            [System.IO.File]::WriteAllText($logMdPath, $new, [System.Text.UTF8Encoding]::new($false))
        } else {
            Add-Content -Path $logMdPath -Value $line -Encoding UTF8
        }
    }

    # 4. 신규 리포트 감지 + Discord push
    $newReports = @()
    if (Test-Path $reportsDir) {
        $currentReports = Get-ChildItem -Path $reportsDir -Filter *.md -File | ForEach-Object { $_.Name }
        $cursorExists = Test-Path $reportCursorPath

        if (-not $cursorExists) {
            # 최초 실행: 현재 reports 전부를 baseline으로 등록, 푸시 안 함
            $json = @{ seen = @($currentReports) } | ConvertTo-Json -Depth 5
            [System.IO.File]::WriteAllText($reportCursorPath, $json, [System.Text.UTF8Encoding]::new($false))
            Write-AutoDocLog "INIT report-cursor baseline=$($currentReports.Count)"
        } else {
            $cursor = Read-Json $reportCursorPath
            $seen = @()
            if ($cursor.ContainsKey('seen')) { $seen = @($cursor['seen']) }
            $seenSet = @{}
            foreach ($n in $seen) { $seenSet[$n] = $true }

            foreach ($name in $currentReports) {
                if (-not $seenSet.ContainsKey($name)) { $newReports += $name }
            }

            # cursor 즉시 갱신 (push 실패해도 다음 실행에서 중복 방지)
            $json = @{ seen = @($currentReports) } | ConvertTo-Json -Depth 5
            [System.IO.File]::WriteAllText($reportCursorPath, $json, [System.Text.UTF8Encoding]::new($false))
        }
    }

    $webhook = $env:WES_DISCORD_WEBHOOK_URL
    $pushedCount = 0
    if ($newReports.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($webhook)) {
        $threadMap = Read-Json $threadMapPath

        foreach ($name in $newReports) {
            $filePath = Join-Path $reportsDir $name
            $content = Read-Text $filePath
            $fm = ConvertFrom-Frontmatter $content
            $body = Get-Body $content

            $title = if ($fm.ContainsKey('title')) { $fm['title'] } else { $name }
            $team = if ($fm.ContainsKey('team')) { $fm['team'] } else { "" }
            $status = if ($fm.ContainsKey('status')) { $fm['status'] } else { "" }

            $desc = ($body -replace "`r", "" -split "`n" | Select-Object -First 8) -join "`n"
            if ($desc.Length -gt 800) { $desc = $desc.Substring(0, 800) + "`n...[truncated]" }

            $footerText = $name
            if (-not [string]::IsNullOrWhiteSpace($status)) { $footerText = "$name - status: $status" }
            $embed = @{
                color       = 5025616
                title       = "[Report] $title"
                description = $desc
                timestamp   = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                footer      = @{ text = $footerText }
            }
            $bodyObj = @{ embeds = @($embed) }

            # 스레드 라우팅
            $threadId = ""
            if (-not [string]::IsNullOrWhiteSpace($team) -and $threadMap.ContainsKey($team)) {
                $threadId = [string]$threadMap[$team]
            }

            $url = "$webhook" + "?wait=true"
            if ([string]::IsNullOrEmpty($threadId)) {
                $bodyObj.thread_name = "auto-doc"
            } else {
                $url = "$url&thread_id=$threadId"
            }

            $json = $bodyObj | ConvertTo-Json -Depth 10 -Compress
            $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
            try {
                $resp = Invoke-RestMethod -Uri $url -Method Post -Body $bytes -ContentType "application/json; charset=utf-8"
                $pushedCount++

                # auto-doc 스레드 처음 생성됐으면 threadMap에 기록
                if ([string]::IsNullOrEmpty($threadId) -and $resp.channel_id) {
                    $threadMap['auto-doc'] = [string]$resp.channel_id
                }
            } catch {
                Write-AutoDocLog "WARN push-failed report=$name err=$_"
            }
        }

        # threadMap 갱신
        $tmJson = $threadMap | ConvertTo-Json -Depth 5
        [System.IO.File]::WriteAllText($threadMapPath, $tmJson, [System.Text.UTF8Encoding]::new($false))
    }

    # 5. 종합 로그
    $summary = "DONE class=$classCount sig=$sigCount newReports=$($newReports.Count) pushed=$pushedCount"
    if (-not $catalogChanged -and $newReports.Count -eq 0) { $summary = "DONE no-change" }
    Write-AutoDocLog $summary

} catch {
    Write-AutoDocLog "ERR $_"
}
finally {
    if ($acquired) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}

exit 0
