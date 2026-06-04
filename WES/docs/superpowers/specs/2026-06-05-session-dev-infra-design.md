# 세션 개발 인프라 2종 — 설계서

작성일: 2026-06-05
대상: WES 개발 워크플로우 (Claude Code 세션이 직접 부리는 인프라)

세션이 자기 환경을 직접 제어하기 위한 두 기능을 정의한다.

1. **restart-unity** — 사유 불문, 세션이 "유니티 재실행 필요"라고 판단하면 끄고 다시 켠 뒤 준비 완료까지 기다렸다가 하던 작업을 자동으로 계속한다.
2. **session-mailbox** — 세션이 보이는 VSCode Claude 채팅 탭을 새로 띄워 일을 위임하고, 세션끼리 파일 메일박스로 양방향 소통한다.

---

## 1. restart-unity

### 목표
세션이 단일 블로킹 호출 한 번으로 유니티를 재기동하고, 에디터+MCP가 다시 준비되면 제어를 돌려받는다. 세션 무중단·완전 자동.

### 아키텍처

```
세션 ── Bash(restart-unity.ps1, timeout 200s) ──┐
                                                 │ 블로킹
   ┌─────────────────────────────────────────────┘
   ▼
restart-unity.ps1
  1. ready 파일 삭제           (Temp/unity_ready.txt)
  2. Unity.exe 강제 종료        (해당 프로젝트 프로세스만)
  3. 종료 대기                  (프로세스 사라질 때까지)
  4. Unity.exe -projectPath WES 실행
  5. ready 파일 재생성 폴링      (launch 시각보다 새 timestamp, timeout 180s)
  6. 성공/실패 코드 반환
   │
   ▼
세션 ── MCP ping 1회(u_console) 더블체크 ── 작업 계속
```

### 준비 완료 판정 (선택지 A 채택)
유니티 측에 ready 신호 파일을 쓰는 Editor 스크립트를 둔다.

- **`Assets/Scripts/Editor/UnityReadySignal.cs`** — `[InitializeOnLoadMethod]` 로 도메인 리로드(에디터 기동·컴파일 완료) 시마다 `<project>/Temp/unity_ready.txt` 에 현재 UTC timestamp(ISO 8601)를 기록.
- ps1 은 launch 직전에 파일을 지우고, launch 시각 이후 timestamp 로 파일이 재생성될 때까지 폴링(1초 간격).
- 컴파일 에러로 Safe Mode 진입 시 InitializeOnLoad 가 정상 실행되지 않아 ready 가 안 뜬다 → timeout → 비정상 종료 코드로 세션에 보고(정직). 세션은 사용자에게 "Safe Mode 의심" 1줄 보고 후 중단.

### 경로·파라미터
- Unity exe: `ProjectVersion.txt` 의 `m_EditorVersion` 으로 `C:\Program Files\Unity\Hub\Editor\<ver>\Editor\Unity.exe` 조립. 실행 중 프로세스 path 로도 보강 가능.
- Project path: 스크립트 호출 시 `-ProjectPath` 인자 (기본 `c:\GitFork\WES_Project\WES`).
- MCP.exe(브리지 서버)는 건드리지 않는다. 유니티만 재기동하면 플러그인이 MCP.exe 에 재연결된다.

### 스코프 경계
- MCP 플러그인 코드 변경 후 MCP.exe 재빌드는 본 기능 밖 — 기존 `stop_and_rebuild.bat` 담당. restart-unity 는 유니티 에디터만 다룬다.

### 산출물
| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Editor/UnityReadySignal.cs` | ready 파일 writer (`[InitializeOnLoadMethod]`) |
| `.claude/scripts/restart-unity.ps1` | kill→launch→ready 폴링 블로킹 스크립트 |

### 검증 기준
- 정상: 유니티 켜진 상태에서 호출 → 재기동 후 200s 내 0 코드 반환, 직후 MCP 툴 호출 성공.
- 비정상(컴파일 에러): ready 미생성 → timeout, 비-0 코드, 세션이 사용자에 보고.

---

## 2. session-mailbox

### 목표
부모 세션이 보이는 VSCode Claude 채팅 탭을 새로 띄워 작업을 위임하고, 두 세션이 파일 메일박스로 비동기 양방향 대화한다. 각 세션은 idle 상태에서도 상대 메시지에 깨어난다.

### spawn 메커니즘 (검증 완료)
익스텐션이 URI 핸들러를 등록함:

```
vscode://anthropic.claude-code/open?prompt=<urlencoded>
  → claude-vscode.primaryEditor.open(undefined, prompt)
  → createPanel: session 없음이면 새 webview 패널 생성 + prompt 를 웹뷰에 주입
```

- `start "" "vscode://anthropic.claude-code/open?prompt=..."` (또는 `code --open-url`) 한 줄로 **보이는 새 Claude 채팅 탭**이 뜬다.
- 주의: `session=<id>` 가 있으면 기존 세션 reveal 만 하고 prompt 는 무시("수동 입력하세요" 안내) → spawn 은 항상 session 없이 호출, 새 세션만 만든다.
- prompt 자동 전송 여부는 구현 1단계 스파이크에서 실측 확정(프리필만이면 부트스트랩에서 사용자에게 "전송 눌러" 안내 1줄 추가).

### wake 메커니즘
하네스의 `run_in_background → 종료 시 세션 재호출` 동작을 신호로 사용.

```
세션 boot ─ mailbox-watch.ps1 -Me <name> 를 run_in_background 로 가동
              │ (inbox.jsonl 새 줄 폴링, 1초 간격)
              ▼ 새 메시지 도착
            watcher 종료 ── 하네스가 세션 wake
              ▼
          세션: inbox 신규 메시지 읽고 처리 → mailbox-send 로 답장 → watcher 재가동
```

별도 cron/ScheduleWakeup 불필요. agent-watchdog.ps1 과 동일 패턴.

### 메일박스 레이아웃
위치: `.claude/mailbox/` (gitignore).

```
.claude/mailbox/
  registry.json              # { sessions: [{ name, role, startedAt, status }] }
  <name>/
    inbox.jsonl              # 받은편지함, 한 줄 = 한 메시지
    cursor.txt              # 마지막으로 읽은 줄 번호 (세션이 갱신)
```

메시지 1줄(JSON): `{ ts, from, to, type, body }`
- `type`: `task` | `msg` | `result` | `done`

### 산출물
| 파일 | 역할 |
|---|---|
| `.claude/scripts/spawn-session.ps1` | `-Name -Role -Prompt` → registry 등록 + URI 로 새 탭 띄움. 부트스트랩 프롬프트에 이름·역할·프로토콜·초기 작업 주입 |
| `.claude/scripts/mailbox-send.ps1` | `-To -From -Type -Body` → 상대 inbox.jsonl append |
| `.claude/scripts/mailbox-watch.ps1` | `-Me [-TimeoutSec]` → cursor 이후 새 줄 생기면 종료(wake), 없으면 폴링. timeout 시 정상 종료(세션이 재가동) |
| `.claude/mailbox/PROTOCOL.md` | 세션 간 규약 문서 — 부트스트랩이 참조 |
| `.gitignore` | `.claude/mailbox/` 추가 |

### 세션 라이프사이클 규약 (PROTOCOL.md 핵심)
1. **부모**: `spawn-session.ps1` 호출 전 자신을 registry 에 등록(이름 예: `main`), 자기 watcher 가동.
2. **자식**: 부트스트랩 프롬프트로 깨어나면 → 자기 이름/역할 인지 → registry 등록 → cursor 0 → watcher 가동 → 초기 작업 수행 → 결과를 부모에게 `mailbox-send -Type result`.
3. **양방향**: wake 시 cursor 이후 메시지 전부 처리, cursor 갱신, 필요 답장, watcher 재가동.
4. **종료**: 작업 끝나면 `-Type done` 전송 + registry status=closed + watcher 미재가동. 부모가 done 받으면 정리.
5. **비용 가드**: 핑퐁 무한루프 방지 — 메시지는 목적이 있을 때만. done 으로 명확히 닫는다.

### 검증 기준
- spawn: 호출 → 새 VSCode Claude 탭이 부트스트랩 프롬프트로 뜸.
- 양방향: 부모→자식 task 전송 후 자식 wake→처리→result 회신→부모 wake→수신 확인의 1왕복 성공.
- watcher: idle 세션이 inbox 새 메시지로 자동 재호출됨.

---

## 공통 / 비목표
- 두 기능은 독립. 순서 무관하게 각각 구현·검증 가능.
- 비목표: MCP.exe 재빌드 자동화(기존 배치), 다대다 팀 채널(C안 보류), 세션 컨텍스트 승계(/resume 양도).
- 플랫폼: Windows / PowerShell 전제.
