---
title: <작업 제목>
date: <YYYY-MM-DD>
area: [<Worker>, <Component>, <Config>]  # multi
status: Done                          # Done | In Progress | Blocked
affected:                              # 각 entry는 반드시 [[]] 위키 링크 포함 + 따옴표
  - "[[InGameController]]"            # 예시 — 실제 영향받은 클래스명으로
  - "[[GameSceneManager]]"
team: <팀명 또는 빈 문자열>            # discord-threads.json 키와 일치
thread_id: ""                         # 자동 채워짐
discord_notified: false               # 자동 갱신
---

# <작업 제목>

<한 줄 요약>

## 시퀀스

```mermaid
sequenceDiagram
    participant A
    participant B
    A->>B: <action>
```

## 변경 파일

- `Assets/Scripts/<...>.cs` (신규/수정/삭제)

## 검증

- <테스트 방법>
- <확인 결과>

## 발생/관련 시그널

- (Signal 카탈로그 wiki link, 예: `[[DayNightWorker.OnPhaseChanged]]`)
