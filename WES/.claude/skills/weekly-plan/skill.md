---
name: weekly-plan
description: Use when the user wants this week's WES work priorities. Dispatches director sub-agent to perform Wiki-First analysis of vault and produce a weekly plan report.
---

# Weekly Plan (vault 기반 주간 작업 우선순위)

이 스킬이 호출되면 **director 서브에이전트**에게 vault 기반 주간 기획을 의뢰하고, 결과 리포트를 사용자에게 요약 보고한다.

## 절차

### 1. director 서브에이전트 spawn

Agent 도구로 director를 호출한다. subagent_type은 `director`. prompt는 아래 텍스트 그대로 전달:

```
Wiki-First 흐름으로 진행:

1. document/auto/index.md 먼저 읽어서 vault 진입점 확인
2. catalog/Class/ 훑어 현재 시스템 윤곽 파악, 최근 reports/ 1~2개 확인
3. 종합해서 이번 주 후보 작업 2~3개를 우선순위로 제안
   - 각 후보: 한 줄 요약 / 예상 공수 / 영향 클래스 wiki link
4. 최우선 추천 1개에 대해선 client에게 넘길 시점·방식을 1~2줄로 정리
5. 결과를 document/auto/reports/<오늘날짜>-주간기획.md 로 저장
   frontmatter 형식 — 반드시 [[]] 위키 링크는 따옴표 안에:
     title: 주간 작업 우선순위 제안
     date: <오늘날짜>
     area: [Planning]
     status: Done
     affected:
       - "[[<영향클래스1>]]"
       - "[[<영향클래스2>]]"
     team: director
6. log.md 최상단에 "## [<오늘날짜>] query | 주간 작업 우선순위 제안" 한 줄 append

이번 사이클은 분석/기획 단독 — client/designer/qa 호출 안 함. 코드 변경 없음.
```

`<오늘날짜>`는 호출 시점 기준 YYYY-MM-DD 형식으로 치환한다.

### 2. director 결과 수신 후 사용자 보고

director가 반환한 응답을 받아서 사용자에게 다음 형식으로 요약 보고:

```
## 주간 기획 완료

### 후보 (우선순위 순)
1. <후보 A 제목> — <한 줄 요약>
2. <후보 B 제목> — <한 줄 요약>
3. <후보 C 제목> — <한 줄 요약>

### 최우선 추천: <후보 X>
<추천 근거 2~3줄>

### 산출물
- reports/<날짜>-주간기획.md
- log.md 갱신

### 다음 액션
client 인계 준비 완료. 다음 사이클에서 풀 4-Phase로 진행하려면 `/feature <후보 제목>` 또는 직접 의뢰.
```

### 3. 자동으로 일어나는 사이드 이펙트 (확인용)

스킬 사용자에게 따로 알릴 필요 없음. 자동 발생:
- Stop / SubagentStop hook → `update-vault.ps1` 발사 → 새 리포트 감지 시 Discord push (auto-doc 스레드 또는 director 팀 스레드)
- catalog 변경 없으니 catalog/Class/* 갱신은 없음
- `log.md`는 director가 직접 append
