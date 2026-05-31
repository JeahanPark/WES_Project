---
title: WES Auto-Doc Log
category: log
updated: 2026-05-14
---

# WES Auto-Doc Log

vault 변경 이력. 시간 역순(최신이 위).
형식: `## [YYYY-MM-DD] 작업유형 | 제목`
작업유형: `ingest` / `query` / `lint` / `setup` / `update`

추출 팁: `grep "^## \[" log.md | head -10` 으로 최근 10건 확인.

---

## [2026-05-17] update | 낮밤 모닥불 컷아웃 셰이더 재작성 (라운드4) — PASS

## [2026-05-17] update | 낮밤 시나리오 4 시각 캡처 (라운드3) — 모닥불 컷아웃 FAIL 신규 이슈

## [2026-05-17] update | 낮밤 게임플레이 검증

## [2026-05-17] query | 주간 작업 우선순위 제안

## [2026-05-14] setup | Karpathy 패턴 도입 (index + log + Sources)

- `auto/index.md` 생성 — LLM 진입점 표준화
- `auto/log.md` 생성 — 이 파일
- `auto/Sources/` 폴더 신설
- 첫 외부 소스로 [[Sources/karpathy-llm-wiki|Karpathy LLM Wiki gist]] 보관 (.original.md = 원본 전문 archive)
- `_templates/Source.md` 신규 — 외부 소스 페이지 양식

## [2026-05-14] ingest | WES 메인 클래스 카탈로그 초기 시드 (26)

- `Assets/Scripts/{Manager,Controller,Worker,Component}/` 스캔
- 클래스 26개 → `catalog/Class/*.md` 생성 (Manager 10, Controller 4, Worker 8, Component 4)
- 이벤트 1개 → `catalog/Signal/DayNightWorker.OnPhaseChanged.md`
- 카테고리별 클래스 다이어그램 4개 (Mermaid) — 이후 Canvas 도입으로 사실상 deprecated
- Canvas 시각화 [[diagrams/class/WES-Class-Overview]] 생성

## [2026-05-14] setup | Obsidian + Dataview + Discord 환경 검증

- Obsidian v1.12.7 사일런트 설치
- vault 등록 (`%APPDATA%\obsidian\obsidian.json`)
- Dataview v0.5.68 사전 설치, community-plugins.json에서 활성화
- Discord webhook 테스트 메시지 전송 성공
- 샘플 카탈로그 2개 + 샘플 시그널 1개 + 샘플 리포트 1개 + Dataview 뷰 3개 생성

## [2026-05-14] setup | vault 초기화

- `document/` 를 vault root로 결정
- `auto/` 폴더 신설 (에이전트 자동 생성 영역)
- 폴더 트리: `catalog/{Class,Signal}/`, `diagrams/{class,sequence}/`, `reports/`, `views/`, `_templates/`
- `auto/README.md` 작성
- 상위 [[../obsidian_agent_plan|스펙 문서]] v3.1로 정착 (Notion → Obsidian, lat.md 제거)
