# 세션 개발 인프라 2종 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 세션이 (A) 유니티를 끄고 켜 준비될 때까지 기다렸다 자동 재개하고, (B) 보이는 VSCode Claude 채팅 탭을 새로 띄워 파일 메일박스로 양방향 소통하게 한다.

**Architecture:** Part A는 Unity 측 `[InitializeOnLoad]` ready 파일 writer + PowerShell 블로킹 재기동 스크립트. Part B는 VSCode 익스텐션의 `vscode://anthropic.claude-code/open?prompt=` URI 핸들러로 새 탭을 spawn하고, `.claude/mailbox/` 파일함 + `run_in_background watcher 종료 = wake` 패턴으로 비동기 양방향 통신.

**Tech Stack:** PowerShell 5.1+, Unity 6000.0.60f1 Editor C#, VSCode claude-code 익스텐션 URI 핸들러.

**검증 철학:** 인프라 스크립트는 자동 단위테스트가 부적합 → 각 태스크는 "스크립트를 실제 실행하고 관측 가능한 부수효과(생성 파일·출력 문자열·exit code)를 확인"하는 방식으로 검증한다. 가짜 테스트를 만들지 않는다.

**스펙:** [2026-06-05-session-dev-infra-design.md](../specs/2026-06-05-session-dev-infra-design.md)

---

## File Structure

| 파일 | Part | 책임 |
|---|---|---|
| `Assets/Scripts/Editor/UnityReadySignal.cs` | A | 도메인 리로드 시 `Temp/unity_ready.txt` 에 UTC timestamp 기록 |
| `.claude/scripts/restart-unity.ps1` | A | Unity kill→launch→ready 폴링 (블로킹) |
| `.claude/scripts/mailbox-send.ps1` | B | 상대 inbox.jsonl 에 메시지 1줄 append |
| `.claude/scripts/mailbox-read.ps1` | B | 미읽음 메시지 출력 + cursor 전진 |
| `.claude/scripts/mailbox-watch.ps1` | B | inbox 새 줄 폴링, 도착/타임아웃 시 종료(=wake) |
| `.claude/scripts/spawn-session.ps1` | B | registry 등록 + URI로 새 VSCode 채팅 탭 띄움 |
| `.claude/mailbox/PROTOCOL.md` | B | 세션 간 규약 (부트스트랩이 참조) |
| `.gitignore` | B | `.claude/mailbox/` 런타임 데이터 제외 |

각 파일은 단일 책임. Part A와 Part B는 의존 없음 — 순서 무관.

---

# Part A — restart-unity

### Task A1: Unity ready 신호 writer

**Files:**
- Create: `Assets/Scripts/Editor/UnityReadySignal.cs`

- [ ] **Step 1: Editor 스크립트 작성**

```csharp
#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 도메인 리로드(에디터 기동·스크립트 컴파일 완료)마다 프로젝트 루트
/// Temp/unity_ready.txt 에 현재 UTC 시각(ISO 8601 round-trip)을 기록한다.
/// restart-unity.ps1 이 이 파일의 timestamp 갱신을 "에디터 준비 완료" 신호로 사용한다.
/// </summary>
[InitializeOnLoad]
public static class UnityReadySignal
{
    static UnityReadySignal()
    {
        try
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var path = Path.Combine(projectRoot, "Temp", "unity_ready.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, DateTime.UtcNow.ToString("o"));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UnityReadySignal] ready 파일 기록 실패: {e.Message}");
        }
    }
}
#endif
```

- [ ] **Step 2: 에셋 refresh 후 동작 확인**

MCP로 `u_editor_asset(action: refresh)` 호출 → 컴파일·도메인 리로드 유도.
Run (Bash): `cat "c:/GitFork/WES_Project/WES/Temp/unity_ready.txt"`
Expected: ISO 8601 timestamp 1줄 출력 (예: `2026-06-05T06:12:34.5678901Z`). 파일 없으면 실패.

- [ ] **Step 3: 커밋**

```bash
git add WES/Assets/Scripts/Editor/UnityReadySignal.cs
git commit -m "restart-unity: 에디터 ready 신호 파일 writer 추가"
```

---

### Task A2: restart-unity.ps1 (kill→launch→ready 폴링)

**Files:**
- Create: `.claude/scripts/restart-unity.ps1`

- [ ] **Step 1: 스크립트 작성**

```powershell
<#
.SYNOPSIS
  WES 유니티 에디터를 재기동하고 준비될 때까지 블로킹 대기한다.
.DESCRIPTION
  ready 파일 삭제 → 프로젝트 Unity.exe 종료 → 재실행 → unity_ready.txt 가
  launch 시각 이후로 갱신될 때까지 폴링. 준비되면 exit 0, timeout 시 exit 1.
#>
param(
    [string]$ProjectPath = "c:\GitFork\WES_Project\WES",
    [int]$TimeoutSec = 180
)
$ErrorActionPreference = "Stop"

function Get-UnityExe([string]$proj) {
    $verLine = Get-Content (Join-Path $proj "ProjectSettings\ProjectVersion.txt") |
        Where-Object { $_ -match "^m_EditorVersion:" }
    $ver = ($verLine -replace "m_EditorVersion:\s*", "").Trim()
    $exe = "C:\Program Files\Unity\Hub\Editor\$ver\Editor\Unity.exe"
    if (-not (Test-Path $exe)) { throw "Unity exe 없음: $exe" }
    return $exe
}

$ProjectPath = (Resolve-Path $ProjectPath).Path.TrimEnd('\')
$readyFile = Join-Path $ProjectPath "Temp\unity_ready.txt"
$exe = Get-UnityExe $ProjectPath

# 1. ready 파일 삭제
Remove-Item $readyFile -ErrorAction SilentlyContinue

# 2. 이 프로젝트의 Unity.exe 종료 (CommandLine에 projectPath 포함된 것만)
$matched = @()
Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | ForEach-Object {
    if ($_.CommandLine -and $_.CommandLine.ToLower().Contains($ProjectPath.ToLower())) {
        $matched += $_.ProcessId
    }
}
if ($matched.Count -eq 0) {
    # CommandLine을 못 읽었을 수 있음(권한). 단일-개발 환경 가정하에 모든 Unity.exe 종료.
    Get-Process Unity -ErrorAction SilentlyContinue | ForEach-Object { $matched += $_.Id }
    if ($matched.Count -gt 0) { Write-Warning "프로젝트 매칭 실패 → 모든 Unity.exe 종료" }
}
$matched | Select-Object -Unique | ForEach-Object {
    Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue
}

# 3. 종료 대기 (최대 30s)
$deadline = (Get-Date).AddSeconds(30)
while ((Get-Process Unity -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
    Start-Sleep -Milliseconds 500
}

$launchTime = (Get-Date).ToUniversalTime()

# 4. 재실행
Start-Process -FilePath $exe -ArgumentList @("-projectPath", "`"$ProjectPath`"")

# 5. ready 파일 폴링
$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    if (Test-Path $readyFile) {
        $content = (Get-Content $readyFile -Raw).Trim()
        $ts = $null
        if ([datetime]::TryParse($content, [ref]$ts)) {
            if ($ts.ToUniversalTime() -gt $launchTime) {
                Write-Output "READY $($ts.ToUniversalTime().ToString('o'))"
                exit 0
            }
        }
    }
    Start-Sleep -Seconds 1
}
Write-Error "TIMEOUT: ${TimeoutSec}s 내 Unity 준비 안 됨 (컴파일 에러/Safe Mode 의심)"
exit 1
```

- [ ] **Step 2: 정상 경로 검증**

유니티가 켜진 상태에서 실행:
Run (Bash): `powershell -NoProfile -ExecutionPolicy Bypass -File "c:/GitFork/WES_Project/WES/.claude/scripts/restart-unity.ps1"`
Expected: 유니티가 닫혔다 다시 뜨고, 약 30~120s 후 `READY <timestamp>` 출력 + exit code 0.
이어서 검증: `cat "c:/GitFork/WES_Project/WES/Temp/unity_ready.txt"` 의 timestamp 가 호출 시각 이후인지 확인.

- [ ] **Step 3: 종료코드 확인**

Run (Bash): `powershell -NoProfile -ExecutionPolicy Bypass -File "c:/GitFork/WES_Project/WES/.claude/scripts/restart-unity.ps1"; echo "EXIT=$LASTEXITCODE"`
Expected: `EXIT=0` (정상). MCP 툴(`u_console`)이 재기동 후 정상 응답하는지 1회 확인.

- [ ] **Step 4: 커밋**

```bash
git add WES/.claude/scripts/restart-unity.ps1
git commit -m "restart-unity: 유니티 kill→launch→ready 폴링 블로킹 스크립트"
```

---

### Task A3: 사용 규약 문서화 (CLAUDE.md MCP Rules)

**Files:**
- Modify: `WES/CLAUDE.md` (MCP Rules 섹션 끝)

- [ ] **Step 1: 규약 1줄 추가**

`## MCP Rules` 섹션의 "MCP 서버 재빌드/재시작" 항목 바로 아래에 추가:

```markdown
- **유니티 에디터 재기동**: 어떤 이유로든 유니티를 껏다 켜야 하면 `.claude/scripts/restart-unity.ps1` 을 블로킹 호출한다(timeout 200s). `READY` 출력·exit 0 이면 작업을 계속하고, `TIMEOUT`(exit 1)이면 컴파일 에러/Safe Mode 의심을 사용자에게 1줄 보고하고 중단한다. MCP.exe 재빌드와는 별개(유니티만 재기동).
```

- [ ] **Step 2: 커밋**

```bash
git add WES/CLAUDE.md
git commit -m "restart-unity: CLAUDE.md에 유니티 재기동 사용 규약 추가"
```

---

# Part B — session-mailbox

### Task B1: spawn URI 스파이크 (자동전송 여부 실측)

목적: `vscode://anthropic.claude-code/open?prompt=` 가 (1) 새 탭을 여는지, (2) prompt 를 자동 전송하는지 프리필만 하는지 **실측**해 이후 부트스트랩 문구를 확정한다.

**Files:** 없음 (수동 검증)

- [ ] **Step 1: 트리비얼 prompt로 URI 실행**

Run (Bash):
```bash
powershell -NoProfile -Command "Start-Process 'vscode://anthropic.claude-code/open?prompt=SPAWN-TEST%20say%20pong'"
```
Expected: VSCode에 새 Claude Code 채팅 탭이 뜬다.

- [ ] **Step 2: 자동전송 여부 관측·기록**

새 탭에서 입력창에 `SPAWN-TEST say pong` 이 **자동 전송**됐는지(자식이 바로 응답) vs **프리필만**(전송 버튼 대기) 됐는지 사용자에게 확인.
결과를 다음 태스크의 부트스트랩 분기에 반영:
- 자동전송 O → spawn-session.ps1 부트스트랩 그대로.
- 프리필만 → 부트스트랩 첫 줄에 변화 없음(사용자가 1회 전송 눌러야 자식 시작 — PROTOCOL.md에 명시).

(코드 변경 없음, 커밋 없음)

---

### Task B2: mailbox-send.ps1

**Files:**
- Create: `.claude/scripts/mailbox-send.ps1`

- [ ] **Step 1: 작성**

```powershell
<#
.SYNOPSIS  상대 세션 inbox.jsonl 에 메시지 1줄(JSON)을 append 한다.
#>
param(
    [Parameter(Mandatory)][string]$To,
    [Parameter(Mandatory)][string]$From,
    [ValidateSet("task","msg","result","done")][string]$Type = "msg",
    [Parameter(Mandatory)][string]$Body
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\mailbox") -ErrorAction SilentlyContinue
if (-not $root) {
    $root = New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot "..\mailbox")
}
$inboxDir = Join-Path $root $To
New-Item -ItemType Directory -Force -Path $inboxDir | Out-Null
$inbox = Join-Path $inboxDir "inbox.jsonl"

$msg = [ordered]@{
    ts   = (Get-Date).ToUniversalTime().ToString("o")
    from = $From
    to   = $To
    type = $Type
    body = $Body
}
$line = ($msg | ConvertTo-Json -Compress -Depth 10)
Add-Content -Path $inbox -Value $line -Encoding utf8
Write-Output "SENT $From -> $To [$Type]"
```

- [ ] **Step 2: 검증 — 메시지 1줄이 inbox에 쌓이는지**

Run (Bash):
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "c:/GitFork/WES_Project/WES/.claude/scripts/mailbox-send.ps1" -To alice -From main -Type task -Body "hello"
cat "c:/GitFork/WES_Project/WES/.claude/mailbox/alice/inbox.jsonl"
```
Expected: `SENT main -> alice [task]` 출력. inbox.jsonl 에 `{"ts":...,"from":"main","to":"alice","type":"task","body":"hello"}` 1줄.

- [ ] **Step 3: 정리·커밋**

```bash
rm -rf "c:/GitFork/WES_Project/WES/.claude/mailbox/alice"
git add WES/.claude/scripts/mailbox-send.ps1
git commit -m "session-mailbox: inbox에 메시지 append하는 mailbox-send 추가"
```

---

### Task B3: mailbox-read.ps1 (미읽음 출력 + cursor 전진)

**Files:**
- Create: `.claude/scripts/mailbox-read.ps1`

- [ ] **Step 1: 작성**

```powershell
<#
.SYNOPSIS  내 inbox에서 cursor 이후 미읽음 메시지를 출력하고 cursor를 끝으로 전진한다.
.OUTPUTS   미읽음 JSON 줄들 (없으면 출력 없음)
#>
param([Parameter(Mandatory)][string]$Me)
$ErrorActionPreference = "Stop"
$root   = Join-Path $PSScriptRoot "..\mailbox"
$inbox  = Join-Path $root "$Me\inbox.jsonl"
$cursorF = Join-Path $root "$Me\cursor.txt"

if (-not (Test-Path $inbox)) { exit 0 }
$lines = @(Get-Content $inbox)
$cursor = 0
if (Test-Path $cursorF) { $cursor = [int]((Get-Content $cursorF -Raw).Trim()) }

if ($cursor -lt $lines.Count) {
    $lines[$cursor..($lines.Count - 1)] | ForEach-Object { $_ }
}
New-Item -ItemType Directory -Force -Path (Split-Path $cursorF) | Out-Null
Set-Content -Path $cursorF -Value $lines.Count -Encoding utf8
```

- [ ] **Step 2: 검증 — 첫 호출은 미읽음, 둘째 호출은 빈 출력**

Run (Bash):
```bash
S=c:/GitFork/WES_Project/WES/.claude/scripts
powershell -NoProfile -ExecutionPolicy Bypass -File "$S/mailbox-send.ps1" -To bob -From main -Type msg -Body "m1"
powershell -NoProfile -ExecutionPolicy Bypass -File "$S/mailbox-send.ps1" -To bob -From main -Type msg -Body "m2"
echo "--- read 1 ---"; powershell -NoProfile -ExecutionPolicy Bypass -File "$S/mailbox-read.ps1" -Me bob
echo "--- read 2 ---"; powershell -NoProfile -ExecutionPolicy Bypass -File "$S/mailbox-read.ps1" -Me bob
```
Expected: read 1 = m1·m2 두 줄. read 2 = 출력 없음 (cursor 전진됨).

- [ ] **Step 3: 정리·커밋**

```bash
rm -rf "c:/GitFork/WES_Project/WES/.claude/mailbox/bob"
git add WES/.claude/scripts/mailbox-read.ps1
git commit -m "session-mailbox: 미읽음 출력+cursor 전진 mailbox-read 추가"
```

---

### Task B4: mailbox-watch.ps1 (wake 신호)

**Files:**
- Create: `.claude/scripts/mailbox-watch.ps1`

- [ ] **Step 1: 작성**

```powershell
<#
.SYNOPSIS  내 inbox에 cursor 이후 새 메시지가 생기면 종료(=세션 wake 신호). 없으면 폴링.
.NOTES     run_in_background 로 가동. 종료 시 하네스가 세션을 재호출한다.
           timeout 도달 시에도 exit 0(하트비트) — 세션이 watcher를 재가동한다.
#>
param(
    [Parameter(Mandatory)][string]$Me,
    [int]$TimeoutSec = 1800,
    [int]$IntervalSec = 1
)
$ErrorActionPreference = "Stop"
$root    = Join-Path $PSScriptRoot "..\mailbox"
$inbox   = Join-Path $root "$Me\inbox.jsonl"
$cursorF = Join-Path $root "$Me\cursor.txt"

function Get-LineCount($path) {
    if (-not (Test-Path $path)) { return 0 }
    return @(Get-Content $path).Count
}
$cursor = 0
if (Test-Path $cursorF) { $cursor = [int]((Get-Content $cursorF -Raw).Trim()) }

$deadline = (Get-Date).AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
    $count = Get-LineCount $inbox
    if ($count -gt $cursor) {
        Write-Output "WAKE $Me +$($count - $cursor)"
        exit 0
    }
    Start-Sleep -Seconds $IntervalSec
}
Write-Output "WATCH-TIMEOUT $Me"
exit 0
```

- [ ] **Step 2: 검증 — 메시지 도착 시 즉시 종료**

Run (Bash):
```bash
S=c:/GitFork/WES_Project/WES/.claude/scripts
# 백그라운드로 watcher 띄우고(짧은 timeout), 1초 뒤 메시지 보냄
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Job { & '$S/mailbox-watch.ps1' -Me carol -TimeoutSec 15 } | Out-Null; Start-Sleep 1; & '$S/mailbox-send.ps1' -To carol -From main -Type msg -Body 'wake'; Start-Sleep 2; Get-Job | Receive-Job; Get-Job | Remove-Job -Force"
```
Expected: `SENT main -> carol [msg]` 와 `WAKE carol +1` 둘 다 출력 (watcher가 timeout 전에 종료됨).

- [ ] **Step 3: 정리·커밋**

```bash
rm -rf "c:/GitFork/WES_Project/WES/.claude/mailbox/carol"
git add WES/.claude/scripts/mailbox-watch.ps1
git commit -m "session-mailbox: inbox 새 메시지 wake 신호 mailbox-watch 추가"
```

---

### Task B5: spawn-session.ps1

**Files:**
- Create: `.claude/scripts/spawn-session.ps1`

- [ ] **Step 1: 작성**

```powershell
<#
.SYNOPSIS  새 VSCode Claude 채팅 탭을 띄워 자식 세션을 spawn하고 registry에 등록한다.
.NOTES     prompt가 길면 URI 길이 한계에 걸릴 수 있으므로 작업 본문은 간결히.
           큰 작업은 파일로 써두고 그 경로를 Prompt에 넣어 자식이 읽게 한다.
#>
param(
    [Parameter(Mandatory)][string]$Name,
    [string]$Role = "worker",
    [Parameter(Mandatory)][string]$Prompt,
    [string]$Parent = "main"
)
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Web

$root = New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot "..\mailbox")
$root = $root.FullName
New-Item -ItemType Directory -Force -Path (Join-Path $root $Name) | Out-Null

# registry 등록 (같은 이름 있으면 교체)
$regF = Join-Path $root "registry.json"
$existing = @()
if (Test-Path $regF) {
    $obj = Get-Content $regF -Raw | ConvertFrom-Json
    if ($obj.sessions) { $existing = @($obj.sessions | Where-Object { $_.name -ne $Name }) }
}
$entry = [ordered]@{
    name = $Name; role = $Role; parent = $Parent
    startedAt = (Get-Date).ToUniversalTime().ToString("o"); status = "active"
}
@{ sessions = (@($existing) + $entry) } | ConvertTo-Json -Depth 10 |
    Set-Content -Path $regF -Encoding utf8

$protoPath = Join-Path $root "PROTOCOL.md"
$boot = @"
[SESSION-BOOTSTRAP] 너의 세션 이름='$Name', 역할='$Role', 부모='$Parent'.
먼저 $protoPath 를 읽고 세션 메일박스 프로토콜을 그대로 따른다.
즉시:
1) .claude/scripts/mailbox-watch.ps1 -Me $Name 를 run_in_background 로 가동(wake용).
2) 아래 [작업] 수행.
3) 끝나면 mailbox-send.ps1 -To $Parent -From $Name -Type result -Body "<요약>" 로 보고하고 -Type done 으로 닫는다.

[작업]
$Prompt
"@

$enc = [System.Web.HttpUtility]::UrlEncode($boot)
$uri = "vscode://anthropic.claude-code/open?prompt=$enc"
Start-Process $uri
Write-Output "SPAWNED $Name ($Role) -> VSCode 채팅 탭 오픈"
```

- [ ] **Step 2: 검증 — 탭 오픈 + registry 등록**

Run (Bash):
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File "c:/GitFork/WES_Project/WES/.claude/scripts/spawn-session.ps1" -Name tester -Role worker -Prompt "이건 spawn 검증용. mailbox-send로 main에게 'pong' 보내고 done."
cat "c:/GitFork/WES_Project/WES/.claude/mailbox/registry.json"
```
Expected: `SPAWNED tester (worker) ...` 출력 + 새 VSCode Claude 탭이 부트스트랩 프롬프트로 뜸. registry.json 에 `tester` 항목 존재.

- [ ] **Step 3: 정리·커밋**

새로 뜬 tester 탭은 수동으로 닫는다.
```bash
rm -rf "c:/GitFork/WES_Project/WES/.claude/mailbox/tester"
rm -f "c:/GitFork/WES_Project/WES/.claude/mailbox/registry.json"
git add WES/.claude/scripts/spawn-session.ps1
git commit -m "session-mailbox: URI로 새 VSCode 세션 spawn하는 spawn-session 추가"
```

---

### Task B6: PROTOCOL.md + .gitignore

**Files:**
- Create: `.claude/mailbox/PROTOCOL.md`
- Modify: `WES/.gitignore`

- [ ] **Step 1: PROTOCOL.md 작성**

```markdown
# 세션 메일박스 프로토콜

VSCode Claude 세션끼리 파일 메일박스로 비동기 양방향 소통하는 규약.

## 정체성
- 각 세션은 고유 이름을 가진다(예: `main`, `tester`). 부트스트랩 프롬프트가 이름·역할·부모를 알려준다.
- registry.json 에 활성 세션 목록이 있다.

## 받은편지함
- 내 받은편지함: `.claude/mailbox/<내이름>/inbox.jsonl` (1줄 = 1메시지 JSON)
- 메시지 필드: `ts, from, to, type, body` / `type ∈ {task, msg, result, done}`
- 읽은 위치: `.claude/mailbox/<내이름>/cursor.txt`

## 사이클 (모든 세션 공통)
1. **부트스트랩**: 깨어나면 이 문서를 읽고 내 이름 인지 → registry 등록(spawn된 경우 이미 등록됨) →
   `mailbox-watch.ps1 -Me <내이름>` 를 run_in_background 로 가동.
2. **작업/대기**: 초기 작업이 있으면 수행. watcher가 살아있는 한 idle이어도 메시지 도착 시 자동 wake.
3. **wake 시**: `mailbox-read.ps1 -Me <내이름>` 로 미읽음 전부 읽고 처리 →
   필요하면 `mailbox-send.ps1 -To <상대> -From <나> -Type <t> -Body "<내용>"` 로 답장 →
   **watcher 재가동**(이전 watcher는 종료됐으므로 반드시 다시 띄운다).
4. **종료**: 할 일이 끝나면 `-Type done` 전송 + watcher 미재가동. 부모는 done 받으면 정리.

## 메시지 타입
| type | 의미 |
|---|---|
| task | 작업 위임 |
| msg | 일반 대화 |
| result | 작업 결과 보고 |
| done | 세션 종료 통지 |

## 규칙
- **watcher는 wake마다 한 번 죽고, 처리 후 반드시 재가동**한다(안 하면 다음 메시지에 못 깨어남).
- 무한 핑퐁 금지 — 메시지는 목적 있을 때만. done 으로 명확히 닫는다.
- prompt URI 길이 한계 때문에 **큰 작업 본문은 파일로 써두고 경로만 전달**한다.
- prompt 자동전송이 안 되는 환경이면(B1 스파이크 결과), 새 탭에서 사용자가 1회 전송을 눌러야 자식이 시작한다.
```

- [ ] **Step 2: .gitignore 에 mailbox 런타임 제외 추가**

`WES/.gitignore` 끝에 추가 (PROTOCOL.md는 추적, 런타임 데이터만 제외):

```gitignore
# session-mailbox 런타임 데이터 (프로토콜 문서는 추적)
.claude/mailbox/*
!.claude/mailbox/PROTOCOL.md
```

- [ ] **Step 3: 검증 — PROTOCOL은 추적, inbox는 무시**

Run (Bash):
```bash
cd c:/GitFork/WES_Project/WES
powershell -NoProfile -ExecutionPolicy Bypass -File ".claude/scripts/mailbox-send.ps1" -To zzz -From main -Type msg -Body x
git status --porcelain .claude/mailbox/
```
Expected: `.claude/mailbox/PROTOCOL.md` 만 추적 대상(`A`/`??`)으로 보이고 `zzz/inbox.jsonl` 은 안 나타남.
정리: `rm -rf .claude/mailbox/zzz`

- [ ] **Step 4: 커밋**

```bash
git add WES/.claude/mailbox/PROTOCOL.md WES/.gitignore
git commit -m "session-mailbox: 프로토콜 문서 + mailbox 런타임 gitignore"
```

---

### Task B7: 양방향 1왕복 통합 검증

목적: 실제 부모↔자식 세션이 task→result 1왕복을 완주하는지 확인 (코드 변경 없음, 수동/대화 검증).

**Files:** 없음

- [ ] **Step 1: main(현재 세션) 등록 + watcher 가동**

현재 세션이 자신을 `main` 으로 등록하고 `mailbox-watch.ps1 -Me main` 을 run_in_background 로 가동.

- [ ] **Step 2: 자식 spawn**

`spawn-session.ps1 -Name worker1 -Prompt "Util.cs 의 public 메서드 개수를 세서 result로 보고"` 호출. 새 탭이 뜨고 자식이 작업.

- [ ] **Step 3: 왕복 관측**

자식이 `mailbox-send -To main -Type result -Body "<개수>"` → main watcher 종료 → main 세션 wake → `mailbox-read -Me main` 으로 수신 확인 → 자식이 `done` 전송.
Expected: main 세션이 자식의 result 를 자동 수신·표시. registry 에 worker1 status가 closed(또는 done 통지).

- [ ] **Step 4: 결과를 vault에 기록**

`document/auto/log.md` 에 1줄 append, `document/auto/reports/2026-06-05-session-dev-infra.md` 작업 리포트 생성(두 기능 요약·검증 결과).

```bash
git add document/auto/log.md document/auto/reports/2026-06-05-session-dev-infra.md
git commit -m "session-dev-infra: 양방향 통합 검증 + 작업 리포트"
```

---

## Self-Review 결과
- **스펙 커버리지**: 기능1(A1·A2·A3), 기능2 spawn(B5)·양방향(B2~B4·B7)·준비판정 A안(A1)·gitignore(B6)·프로토콜(B6) 모두 태스크 존재. 갭 없음.
- **placeholder 스캔**: 모든 코드 step에 실제 ps1/C# 전문 포함. TBD 없음.
- **타입·이름 일관성**: `unity_ready.txt`, `inbox.jsonl`, `cursor.txt`, `registry.json`, `-Me/-To/-From/-Type/-Body` 파라미터명, `mailbox-send/read/watch`·`spawn-session`·`restart-unity` 스크립트명 전 태스크 일치.
- **검증 현실성**: 자동 단위테스트 대신 스크립트 실행+부수효과 확인으로 대체(인프라 특성상 정직한 검증).
