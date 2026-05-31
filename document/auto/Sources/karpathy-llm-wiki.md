---
title: "LLM Wiki — A pattern for building personal knowledge bases using LLMs"
category: source
kind: gist
url: "https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f"
author: "Andrej Karpathy"
ingested_at: 2026-05-14
fetched_at: 2026-05-14
status: Active
related:
  - "[[obsidian_agent_plan]]"
  - "[[index]]"
---

# LLM Wiki (Karpathy)

> **한 줄 요약**: RAG처럼 매번 raw 문서에서 답을 새로 합성하지 말고, **LLM이 점진적으로 유지하는 persistent wiki**를 만들자. 위키는 시간이 지나며 풍부해지는 누적 자산이다.
>
> **원본**: https://gist.github.com/karpathy/442a6bf555914893e9891c11519de94f
>
> **archived copy**: [[karpathy-llm-wiki.original|원본 전문]]

## 우리 시스템과의 관계

이 gist는 우리가 만들고 있는 **WES 자동 문서화 시스템의 이론적 토대**이자 **상위 패턴**.

- 우리 vault(`document/auto/`)는 Karpathy의 "wiki" 층
- `obsidian_agent_plan.md`는 "schema" 층 (CLAUDE.md 역할)
- `catalog/Class/`, `catalog/Signal/`은 **entity pages** — Karpathy 패턴의 핵심 구성요소
- 우리의 "자동 생성 흐름"은 Karpathy의 **Ingest** 작업을 코드 스캔용으로 특화한 것

## 핵심 takeaways

### 3-layer architecture
1. **Raw sources** (immutable) — 원본 문서들. 변경하지 않음. → 우리 적용: `Sources/` 폴더 + 코드 자체
2. **The wiki** (LLM-owned) — 요약·엔티티·개념 페이지. LLM이 전적으로 작성 → 우리 적용: `catalog/`, `diagrams/`, `reports/`
3. **The schema** (CLAUDE.md) — LLM을 disciplined maintainer로 만드는 규약 → 우리 적용: `obsidian_agent_plan.md` + 프로젝트 `CLAUDE.md`

### 3가지 작업
- **Ingest** — 새 소스 → 위키 일괄 갱신 (한 소스가 10~15페이지 건드릴 수 있음)
- **Query** — 위키에서 답 찾기. 좋은 답은 다시 페이지로 환원
- **Lint** — 모순·고아 페이지·누락 참조 주기 점검

### 2가지 navigation 파일
- **index.md** (content-oriented) — 카테고리별 페이지 카탈로그, LLM이 매 쿼리 시 첫 진입점
- **log.md** (chronological) — `## [YYYY-MM-DD] 작업유형 | 제목` 형식 append-only 기록

### 핵심 인사이트

> "Obsidian은 IDE, LLM은 프로그래머, 위키는 코드베이스"
>
> "The bookkeeping burden grows faster than value" — 위키가 죽는 이유. LLM이 bookkeeping 비용을 0에 가깝게 만듦.
>
> "지식은 한 번 컴파일되고 계속 최신화된다 — 매 쿼리마다 다시 합성하지 않는다"

## 우리 시스템이 이 소스에서 직접 채택한 것

- [[index]] / [[log]] 루트 파일 (이 페이지 작성과 동시에 추가됨)
- `Sources/` 폴더 컨벤션 (이 페이지 자체가 그 첫 사례)
- Ingest / Query / Lint 어휘 — [[obsidian_agent_plan]] 갱신 예정
- Wiki-First / Trust-but-Verify 원칙 — 프로젝트 `CLAUDE.md` 갱신 예정

## 의도적으로 채택하지 않은 것

- **소스의 1:1 카피** — 우리 시스템은 코드 자체가 1차 source이고, 외부 문서는 보조. Karpathy의 "정기적 ingest로 위키 누적"은 우리 케이스에선 코드 시드(자동)가 메인이고 외부 문서 ingest는 부수
- **Lint를 월 1회 강제** — 우리는 commit hook으로 점진 검증, 별도 Lint 주기는 미정

## Related

- [[obsidian_agent_plan]]
- [[index]]
- [[log]]
- [[karpathy-llm-wiki.original]]
