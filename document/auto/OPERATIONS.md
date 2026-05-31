---
title: WES Auto-Doc Operations Guide
category: ops
updated: 2026-05-16
---

# 일상 사용 가이드

WES 자동 문서화 시스템을 **어떻게 일상에서 쓰는지** 정리.

---

## 자동으로 일어나는 것 (사용자 개입 불필요)

| 트리거 | 일어나는 일 |
|---|---|
| **Claude Code 에이전트 작업 종료 (Stop / SubagentStop hook)** | `update-vault.ps1` → 카탈로그 자동 갱신 → 변경 있으면 `log.md` append → `discord-mirror.ps1` 이 inbox 메시지를 팀별 Discord 스레드로 미러링 |
| **`git commit` (post-commit hook)** | `update-vault.ps1` 백그라운드 실행. 사람이 직접 코딩한 변경도 catalog에 반영 |

## 사용자가 직접 할 것

### 1. 새 세션 시작할 때 — 자동
- 에이전트가 `document/auto/index.md`를 먼저 읽음 (Wiki-First 원칙)
- 별도로 명령할 필요 없음

### 2. 시스템·기능 질문할 때
- 그냥 평소처럼 질문하면 됨
- 에이전트가 vault부터 보고, 부족하면 코드 탐색하고, 결과를 vault에 반영

### 3. 새 클래스/시그널의 의미 정보(`role`) 채우고 싶을 때
- `document/auto/catalog/Class/<ClassName>.md` 직접 열어서 frontmatter의 `role` 한 줄 작성
- 본문도 자유롭게 추가
- update-vault.ps1은 기존 파일을 덮어쓰지 않으니 (overwrite=false) 안전하게 보존됨

### 4. 외부 자료를 vault에 추가하고 싶을 때 (게임 디자인 PDF, 참고 게임 분석 등)
- 에이전트에게 "이 문서를 ingest해줘" + 경로 제공
- 에이전트가 `document/auto/Sources/` 에 보관 + 요약 페이지 생성

### 5. 작업 리포트 만들고 싶을 때 (현재 수동)
- 에이전트에게 "이 작업 리포트로 남겨줘" 요청
- 에이전트가 `document/auto/_templates/Report.md` 참고해서 `document/auto/reports/YYYY-MM-DD-제목.md` 생성

---

## 진입점 cheatsheet

| 보고 싶은 것 | 어디로 가나 |
|---|---|
| **vault 전체 구조** | [[index]] |
| **무슨 일이 있었나 (시간순)** | [[log]] |
| **클래스 표** | [[views/클래스카탈로그]] |
| **시그널 표** | [[views/시그널카탈로그]] |
| **작업 리포트 표** | [[views/작업리포트]] |
| **클래스 시각화 (drag/zoom)** | [[diagrams/class/WES-Class-Overview]] (Canvas) |
| **시스템 스펙** | [[../obsidian_agent_plan]] |
| **이론적 토대 (Karpathy)** | [[Sources/karpathy-llm-wiki]] |

---

## Obsidian 단축키 (실제 자주 쓰는 것만)

| 키 | 효과 |
|---|---|
| **Ctrl+O** | 빠른 파일 검색 — 일상에서 가장 많이 씀 |
| **Ctrl+E** | Reading ↔ Edit 모드 토글 |
| **Ctrl+Click** | wiki link 따라가기 |
| **Alt+←** | 이전 페이지로 뒤로가기 |
| **Ctrl+G** | 그래프 뷰 (가끔만) |

---

## 트러블슈팅

| 증상 | 확인 |
|---|---|
| Stop hook이 안 도는 것 같음 | `WES/.claude/.auto-doc.log` 확인. 마지막 timestamp 확인 |
| Discord 메시지 안 옴 | `WES/.claude/.discord-mirror.log` 확인. webhook URL 환경변수 확인 |
| 클래스가 카탈로그에 안 보임 | Manager/Controller/Worker/Component 4 폴더 안에 있는지 확인. 다른 폴더는 현재 scope 밖 |
| 카탈로그에 잘못된 정보가 있음 | 해당 .md 파일 직접 수정. update-vault.ps1은 기존 파일 안 덮어씀 |
| Canvas가 안 갱신됨 | `node WES/.claude/scripts/auto-doc/update-diagrams.js` 수동 실행 |

---

## 수동 명령 (필요 시)

```powershell
# 카탈로그 + Canvas 강제 갱신
powershell -ExecutionPolicy Bypass -File "WES\.claude\scripts\auto-doc\update-vault.ps1"

# Discord 웹훅 테스트
powershell -ExecutionPolicy Bypass -File "WES\.claude\scripts\test-discord.ps1"

# Discord 강제 catch-up (놓친 inbox 메시지 일괄 전송)
powershell -ExecutionPolicy Bypass -File "WES\.claude\scripts\discord-mirror.ps1" -Catchup
```
