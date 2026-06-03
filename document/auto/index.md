---
title: WES Auto-Doc Index
category: index
updated: 2026-05-14
---

# WES Auto-Doc Index

이 vault는 WES 프로젝트의 **자동 문서화 시스템** (LLM이 유지하는 wiki).
Karpathy의 [[Sources/karpathy-llm-wiki|LLM Wiki 패턴]]을 코드 카탈로그 용도로 특화한 형태.

**LLM이 매 작업 시작 시 이 파일을 먼저 읽는다.**

---

## Catalog (entity pages — LLM이 자동 갱신)

### 클래스 카탈로그

`auto/catalog/Class/` — 클래스 1개 = .md 파일 1개. frontmatter에 카테고리/부모/파일경로/역할.

**뷰**: [[views/클래스카탈로그|Dataview 표]] · [[diagrams/class/WES-Class-Overview|Canvas 시각화]]

카테고리별:
- Manager (10개)
- Controller (4개)
- Worker (8개)
- Component (4개)

### 시그널·이벤트 카탈로그

`auto/catalog/Signal/` — C# event / NGO RPC / NetworkVariable 단위.

**뷰**: [[views/시그널카탈로그]]

현재 등록:
- [[DayNightWorker.OnPhaseChanged]]

---

## Design Catalog (기획 — LLM이 유지)

`auto/catalog/Design/` — 게임 기획/비전을 주제 단위로 추적. 코드 카탈로그(`catalog/Class/`)와 짝을 이룬다.

- [[catalog/Design/index|Design Catalog 인덱스]] — 시스템별 구현 상태 표 + 4분기 현황
- [[catalog/Design/코어비전_4분기]] — 자원투자 4분기 트레이드오프 (게임 메타 루프)

원본 설계: `design/` · 보완 로드맵: `design/CORE_갭보완_로드맵.md` · 일정: `WES_Schedule.md`

---

## Diagrams

`auto/diagrams/` — 카테고리별 클래스 다이어그램, 플로우별 시퀀스 다이어그램.

- [[diagrams/class/WES-Class-Overview|전체 클래스 Canvas (메인 시각화)]]
- (sequence 다이어그램: 추가 시 여기 인덱싱)

---

## Reports

`auto/reports/` — 에이전트 작업 종료 시 자동 생성되는 작업 리포트.

**뷰**: [[views/작업리포트]]

샘플:
- [[2026-05-14-DayNight시스템추가]]

---

## Sources (raw external references)

`auto/Sources/` — 외부 참고 자료. 변경하지 않는 immutable layer.

- [[Sources/karpathy-llm-wiki|Karpathy: LLM Wiki 패턴]] — 이 시스템의 이론적 토대

---

## Schema & Templates

- [[../obsidian_agent_plan|시스템 스펙 (CLAUDE.md 역할)]]
- [[OPERATIONS|일상 사용 가이드]]
- `_templates/Class.md`, `_templates/Signal.md`, `_templates/Report.md`, `_templates/Source.md`

---

## 작업

이 vault에서 LLM이 수행하는 3가지 작업 (Karpathy 어휘):

| 작업 | 트리거 | 내용 |
|---|---|---|
| **Ingest** | 코드 변경(에이전트 Stop hook / git post-commit) 또는 외부 소스 추가 | catalog 갱신 + 영향 클래스 wiki link + 작업 리포트 생성 |
| **Query** | 에이전트가 작업 전 시스템 파악 | 이 index.md를 먼저 읽고 관련 catalog 페이지 탐색 |
| **Lint** | 수동 (월 1회 정도) | 모순/고아 페이지/누락 참조 점검 |

---

## 갱신 규칙

- LLM이 이 vault에 페이지를 **추가/수정/삭제**할 때마다 [[log]]에 한 줄 append
- index.md는 카테고리 수가 늘거나 메타 구조가 바뀔 때만 수정
